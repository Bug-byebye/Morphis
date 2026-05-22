using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Morphis.AppFlow;
using Mirror;
using StarterAssets;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 世界快照管理器：提供统一的 API 来保存/加载世界快照
    /// 空间选项与空间数据均从数据库（后端 GET/POST /world/{world_id}）读写；当前空间 ID 来自 AppSession.WorkspaceId
    /// </summary>
    public class WorldSnapshotManager : MonoBehaviour
    {
        public static WorldSnapshotManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("Prefab Registry（可选，也可以通过代码设置）")]
        [SerializeField] private PrefabRegistry prefabRegistry;

        [Tooltip("默认世界 ID（未通过登录选空间时使用，如直接 Play MainScene）")]
        [SerializeField] private string defaultWorldId = "MainScene";

        [Tooltip("是否在进入 MainScene 时自动从服务器/本地加载世界")]
        [SerializeField] private bool autoLoadOnStart = true;

        private HttpWorldService _httpService;
        private Coroutine _pendingAutosaveCoroutine;
        /// <summary> 当前会话使用的世界 ID：来自 AppSession.WorkspaceId（选中的空间）或 defaultWorldId </summary>
        private string _currentWorldId;

        private HttpWorldService EnsureHttpService()
        {
            if (_httpService == null)
            {
                _httpService = HttpWorldService.GetOrCreate();
            }

            return _httpService;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (prefabRegistry != null)
                PrefabRegistryManager.SetRegistry(prefabRegistry);

            EnsureHttpService();
            SceneManager.sceneLoaded += OnSceneLoaded;

            // 若本对象是在 MainScene 的 sceneLoaded 回调里创建的，本次不会收到 sceneLoaded，
            // 必须在这里用 AppSession.WorkspaceId 设置 _currentWorldId，否则会一直用 defaultWorldId（MainScene）
            var active = SceneManager.GetActiveScene();
            if (active.name == "MainScene")
                _currentWorldId = !string.IsNullOrEmpty(AppSession.WorkspaceId) ? AppSession.WorkspaceId : defaultWorldId;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_pendingAutosaveCoroutine != null)
            {
                StopCoroutine(_pendingAutosaveCoroutine);
                _pendingAutosaveCoroutine = null;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private string GetCurrentWorldId()
        {
            return !string.IsNullOrEmpty(_currentWorldId) ? _currentWorldId : defaultWorldId;
        }

        /// <summary> 进入 MainScene 时：使用 AppSession.WorkspaceId 作为世界 ID，优先从服务器加载，404 则空场景 </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MainScene" || !autoLoadOnStart)
                return;

            // 联机模式：世界状态由服务器权威下发（Command/RPC），客户端不再走 HTTP/本地加载
            if (NetworkClient.active || NetworkServer.active)
            {
                Debug.Log("[WorldSnapshotManager] Network mode detected. Skip auto-load (world is server-authoritative).");
                return;
            }

            _currentWorldId = !string.IsNullOrEmpty(AppSession.WorkspaceId) ? AppSession.WorkspaceId : defaultWorldId;
            if (string.IsNullOrEmpty(_currentWorldId))
                return;

            // 已登录且为真实空间 ID 时，仅从数据库（服务器）加载，不使用本地
            if (AppSession.IsLoggedIn && !string.IsNullOrEmpty(AppSession.WorkspaceId))
            {
                Debug.Log($"[WorldSnapshotManager] MainScene loaded, loading world from server only: {_currentWorldId}");
                LoadWorldFromServer(_currentWorldId,
                    onSuccess: () => { },
                    onError: err =>
                    {
                        if (err != null && (err.Contains("404") || err.Contains("not found")))
                            Debug.Log($"[WorldSnapshotManager] World '{_currentWorldId}' not on server (new space), starting empty.");
                        else
                            Debug.LogWarning($"[WorldSnapshotManager] Failed to load from server: {err}. Scene remains empty.");
                    });
                return;
            }

            // 未登录（如直接 Play MainScene）时从本地加载
            if (LocalWorldStorage.Exists(_currentWorldId))
            {
                Debug.Log($"[WorldSnapshotManager] MainScene loaded (no login), loading from local: {_currentWorldId}");
                LoadWorldFromLocal(_currentWorldId, onError: _ => { });
            }
        }

        private void Start()
        {
            if (!autoLoadOnStart || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainScene")
                return;

            // 联机模式：世界由服务器下发，不从本地/HTTP 直接加载
            if (NetworkClient.active || NetworkServer.active)
            {
                Debug.Log("[WorldSnapshotManager] Network mode detected. Skip Start() auto-load.");
                return;
            }
            // 若 Awake 时未设置（非 MainScene 加载路径），这里用 AppSession.WorkspaceId 补上，避免误用 defaultWorldId
            if (string.IsNullOrEmpty(_currentWorldId))
                _currentWorldId = !string.IsNullOrEmpty(AppSession.WorkspaceId) ? AppSession.WorkspaceId : defaultWorldId;
            if (string.IsNullOrEmpty(GetCurrentWorldId()))
                return;
            if (AppSession.IsLoggedIn && !string.IsNullOrEmpty(AppSession.WorkspaceId))
            {
                LoadWorldFromServer(_currentWorldId, () => { }, _ => { });
                return;
            }
            if (LocalWorldStorage.Exists(_currentWorldId))
                LoadWorld(_currentWorldId, useServer: false);
        }

        private void OnApplicationQuit()
        {
            var worldId = GetCurrentWorldId();
            if (string.IsNullOrEmpty(worldId)) return;

            // 联机服务器：在 Unity Server 进程退出前同步刷盘权威快照（双保险，主入口见 NetworkPlayerSetup）
            if (NetworkServer.active)
            {
                StarterAssets.NetworkPlayerSetup.FlushAuthoritySaveBlocking("OnApplicationQuit");
                return;
            }
            // 联机客户端：保存由服务器权威执行，客户端退出无需写盘
            if (NetworkClient.active)
            {
                return;
            }
            // 单机/未联机：仅在已登录时同步写一次后端
            if (AppSession.IsLoggedIn)
            {
                SaveWorldServer(worldId, onError: e => Debug.LogWarning($"[WorldSnapshotManager] Save to server on quit failed: {e}"));
                Debug.Log($"[WorldSnapshotManager] Saved world to server on quit: {worldId}");
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause) return;
            var worldId = GetCurrentWorldId();
            if (string.IsNullOrEmpty(worldId)) return;

            // 联机模式：保存由服务器权威执行（客户端只可发起请求）
            if (NetworkClient.active || NetworkServer.active)
            {
                return;
            }
            if (AppSession.IsLoggedIn)
                SaveWorldServer(worldId, onError: _ => { });
        }

        /// <summary>
        /// 保存当前世界（本地）
        /// </summary>
        public bool SaveWorldLocal(string worldId = null)
        {
            worldId = worldId ?? GetCurrentWorldId();
            var snapshot = WorldSnapshotBuilder.BuildSnapshot(worldId);
            return LocalWorldStorage.SaveToLocal(snapshot);
        }

        /// <summary>
        /// 保存当前世界（服务器）
        /// </summary>
        public void SaveWorldServer(string worldId = null, Action onSuccess = null, Action<string> onError = null)
        {
            worldId = worldId ?? GetCurrentWorldId();
            Debug.Log($"[WorldSnapshotManager] SaveWorldServer: world_id={worldId}");

            // 联机模式：客户端不能直接构建并 POST；改为发 Command 给服务器
            if (NetworkClient.active)
            {
                if (NetworkPlayerSetup.Local != null && NetworkPlayerSetup.Local.RequestSaveWorld())
                {
                    onSuccess?.Invoke(); // 触发成功回调（实际保存结果在服务器日志里）
                }
                else
                {
                    onError?.Invoke("No local player / not connected. Cannot request server save.");
                }
                return;
            }

            // 单机/离线保持原逻辑
            var snapshot = WorldSnapshotBuilder.BuildSnapshot(worldId);
            EnsureHttpService().SaveToServer(snapshot, onSuccess, onError);
        }

        /// <summary>
        /// 请求一次自动保存；短时间内重复调用会被合并，避免连续编辑触发大量保存请求。
        /// </summary>
        public void RequestAutosave(float delaySeconds = 0.75f)
        {
            if (!AppSession.IsLoggedIn)
                return;

            if (_pendingAutosaveCoroutine != null)
            {
                StopCoroutine(_pendingAutosaveCoroutine);
            }

            _pendingAutosaveCoroutine = StartCoroutine(AutosaveAfterDelay(Mathf.Max(0f, delaySeconds)));
        }

        /// <summary>
        /// 立即执行一次保存，并清空尚未触发的自动保存。
        /// </summary>
        public void FlushAutosave()
        {
            if (!AppSession.IsLoggedIn)
                return;

            if (_pendingAutosaveCoroutine != null)
            {
                StopCoroutine(_pendingAutosaveCoroutine);
                _pendingAutosaveCoroutine = null;
            }

            SaveWorldServer(
                onError: err => Debug.LogWarning($"[WorldSnapshotManager] Flush autosave failed: {err}")
            );
        }

        private System.Collections.IEnumerator AutosaveAfterDelay(float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            _pendingAutosaveCoroutine = null;
            SaveWorldServer(
                onError: err => Debug.LogWarning($"[WorldSnapshotManager] Autosave failed: {err}")
            );
        }

        /// <summary>
        /// 加载世界（本地或服务器）
        /// </summary>
        /// <param name="worldId">世界 ID</param>
        /// <param name="useServer">是否从服务器加载（false 则从本地加载）</param>
        /// <param name="onSuccess">成功回调</param>
        /// <param name="onError">失败回调</param>
        public void LoadWorld(string worldId = null, bool useServer = false, Action onSuccess = null, Action<string> onError = null)
        {
            worldId = worldId ?? GetCurrentWorldId();

            if (useServer)
            {
                LoadWorldFromServer(worldId, onSuccess, onError);
            }
            else
            {
                LoadWorldFromLocal(worldId, onSuccess, onError);
            }
        }

        /// <summary>
        /// 从本地加载世界
        /// </summary>
        public void LoadWorldFromLocal(string worldId = null, Action onSuccess = null, Action<string> onError = null)
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                onError?.Invoke("Network mode: client is not allowed to load world directly.");
                return;
            }
            worldId = worldId ?? GetCurrentWorldId();
            var snapshot = LocalWorldStorage.LoadFromLocal(worldId);

            if (snapshot == null)
            {
                var error = $"Failed to load world '{worldId}' from local storage";
                Debug.LogError($"[WorldSnapshotManager] {error}");
                onError?.Invoke(error);
                return;
            }

            ApplySnapshot(snapshot);
            onSuccess?.Invoke();
        }

        /// <summary>
        /// 从服务器加载世界
        /// </summary>
        public void LoadWorldFromServer(string worldId = null, Action onSuccess = null, Action<string> onError = null)
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                onError?.Invoke("Network mode: client is not allowed to load world directly.");
                return;
            }
            worldId = worldId ?? GetCurrentWorldId();
            EnsureHttpService().LoadFromServer(worldId,
                snapshot =>
                {
                    ApplySnapshot(snapshot);
                    onSuccess?.Invoke();
                },
                onError);
        }

        /// <summary>
        /// 应用快照到场景
        /// </summary>
        private void ApplySnapshot(WorldSnapshot snapshot)
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                Debug.Log("[WorldSnapshotManager] Network mode: ApplySnapshot is handled by server RPC.");
                return;
            }
            int count = WorldSnapshotApplier.ApplySnapshot(snapshot, clearExisting: true);
            Debug.Log($"[WorldSnapshotManager] Applied snapshot: {count} objects instantiated");
        }

        /// <summary>
        /// 清空当前世界
        /// </summary>
        public void ClearWorld()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                Debug.Log("[WorldSnapshotManager] Network mode: ClearWorld is not allowed on client.");
                return;
            }
            WorldSnapshotApplier.ClearAllWorldObjects();
            Debug.Log("[WorldSnapshotManager] World cleared");
        }

        /// <summary>
        /// 构建当前世界的快照（不保存）
        /// </summary>
        public WorldSnapshot BuildSnapshot(string worldId = null)
        {
            worldId = worldId ?? GetCurrentWorldId();
            return WorldSnapshotBuilder.BuildSnapshot(worldId);
        }

        /// <summary>
        /// 设置 PrefabRegistry
        /// </summary>
        public void SetPrefabRegistry(PrefabRegistry registry)
        {
            prefabRegistry = registry;
            PrefabRegistryManager.SetRegistry(registry);
        }

        /// <summary>
        /// 运行时注册 Prefab
        /// </summary>
        public void RegisterPrefab(string prefabId, GameObject prefab)
        {
            PrefabRegistryManager.RegisterPrefab(prefabId, prefab);
        }
    }
}
