using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Morphis.AppFlow;
using Mirror;
using StarterAssets;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 世界快照管理器。
    /// 登录用户进入空间：仅允许经 <see cref="WorldEntryGate"/> 校验的服务端快照 + Mirror 权威 RPC，禁止本地/HTTP 旁路加载。
    /// </summary>
    public class WorldSnapshotManager : MonoBehaviour
    {
        public static WorldSnapshotManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private PrefabRegistry prefabRegistry;
        [SerializeField] private string defaultWorldId = "MainScene";
        [SerializeField] private bool autoLoadOnStart = true;

        private HttpWorldService _httpService;
        private Coroutine _pendingAutosaveCoroutine;
        private string _currentWorldId;

        private HttpWorldService EnsureHttpService()
        {
            if (_httpService == null)
                _httpService = HttpWorldService.GetOrCreate();
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
                Instance = null;
        }

        private string GetCurrentWorldId()
        {
            return !string.IsNullOrEmpty(_currentWorldId) ? _currentWorldId : defaultWorldId;
        }

        private static bool IsLoggedInWorkspaceSession()
        {
            return AppSession.IsLoggedIn && !string.IsNullOrEmpty(AppSession.WorkspaceId);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MainScene" || !autoLoadOnStart)
                return;

            if (IsLoggedInWorkspaceSession())
            {
                if (!WorldEntryGate.IsEntryAllowed())
                {
                    Debug.LogError("[WorldSnapshotManager] MainScene loaded without validated server snapshot. Returning to Boot.");
                    WorldEntryGate.ForceReturnToBoot("未通过服务端场景快照校验，无法进入空间。");
                }
                else
                {
                    Debug.Log("[WorldSnapshotManager] Logged-in workspace: scene objects come from server RPC only (no local/HTTP fallback).");
                }
                return;
            }
        }

        private void Start()
        {
            if (!autoLoadOnStart || SceneManager.GetActiveScene().name != "MainScene")
                return;

            if (IsLoggedInWorkspaceSession())
                return;

            if (string.IsNullOrEmpty(_currentWorldId))
                _currentWorldId = defaultWorldId;
        }

        private void OnApplicationQuit()
        {
            var worldId = GetCurrentWorldId();
            if (string.IsNullOrEmpty(worldId)) return;

            if (NetworkServer.active)
            {
                NetworkPlayerSetup.FlushAuthoritySaveBlocking("OnApplicationQuit");
                return;
            }
            if (NetworkClient.active)
            {
                // 联机客户端：让权威服务端立即同步落库，避免最后一次编辑因 0.8s 防抖丢失；
                // 然后阻塞短暂时间，给 Mirror 一次出站机会把 Cmd 真的发出去。
                if (NetworkPlayerSetup.Local != null)
                {
                    bool sent = NetworkPlayerSetup.Local.RequestSaveWorldImmediate();
                    if (sent)
                    {
                        // Mirror 默认 tick ~33ms，留 200ms 留够空间让 Cmd 进入出站队列并发出
                        try { System.Threading.Thread.Sleep(200); } catch { }
                    }
                    else
                    {
                        Debug.LogWarning("[WorldSnapshotManager] RequestSaveWorldImmediate not sent (not local player / no client?).");
                    }
                }
                return;
            }

            if (AppSession.IsLoggedIn)
                SaveWorldServer(worldId, onError: e => Debug.LogWarning($"[WorldSnapshotManager] Save to server on quit failed: {e}"));
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause) return;
            if (NetworkServer.active) return;
            if (NetworkClient.active)
            {
                if (NetworkPlayerSetup.Local != null)
                    NetworkPlayerSetup.Local.RequestSaveWorldImmediate();
                return;
            }
            if (AppSession.IsLoggedIn)
                SaveWorldServer(GetCurrentWorldId(), onError: _ => { });
        }

        public bool SaveWorldLocal(string worldId = null)
        {
            if (IsLoggedInWorkspaceSession())
            {
                Debug.LogWarning("[WorldSnapshotManager] Logged-in workspace: local save is disabled.");
                return false;
            }
            worldId = worldId ?? GetCurrentWorldId();
            var snapshot = WorldSnapshotBuilder.BuildSnapshot(worldId);
            return LocalWorldStorage.SaveToLocal(snapshot);
        }

        public void SaveWorldServer(string worldId = null, Action onSuccess = null, Action<string> onError = null)
        {
            worldId = worldId ?? GetCurrentWorldId();
            if (NetworkClient.active)
            {
                if (NetworkPlayerSetup.Local != null && NetworkPlayerSetup.Local.RequestSaveWorld())
                    onSuccess?.Invoke();
                else
                    onError?.Invoke("No local player / not connected. Cannot request server save.");
                return;
            }
            var snapshot = WorldSnapshotBuilder.BuildSnapshot(worldId);
            EnsureHttpService().SaveToServer(snapshot, onSuccess, onError);
        }

        public void RequestAutosave(float delaySeconds = 0.75f)
        {
            if (!AppSession.IsLoggedIn) return;
            if (_pendingAutosaveCoroutine != null)
                StopCoroutine(_pendingAutosaveCoroutine);
            _pendingAutosaveCoroutine = StartCoroutine(AutosaveAfterDelay(Mathf.Max(0f, delaySeconds)));
        }

        public void FlushAutosave()
        {
            if (!AppSession.IsLoggedIn) return;
            if (_pendingAutosaveCoroutine != null)
            {
                StopCoroutine(_pendingAutosaveCoroutine);
                _pendingAutosaveCoroutine = null;
            }
            SaveWorldServer(onError: err => Debug.LogWarning($"[WorldSnapshotManager] Flush autosave failed: {err}"));
        }

        private System.Collections.IEnumerator AutosaveAfterDelay(float delaySeconds)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);
            _pendingAutosaveCoroutine = null;
            SaveWorldServer(onError: err => Debug.LogWarning($"[WorldSnapshotManager] Autosave failed: {err}"));
        }

        public void LoadWorld(string worldId = null, bool useServer = false, Action onSuccess = null, Action<string> onError = null)
        {
            if (IsLoggedInWorkspaceSession())
            {
                onError?.Invoke("Logged-in workspace: loading is only allowed via WorldEntryGate + server RPC.");
                return;
            }
            worldId = worldId ?? GetCurrentWorldId();
            if (useServer)
                LoadWorldFromServer(worldId, onSuccess, onError);
            else
                LoadWorldFromLocal(worldId, onSuccess, onError);
        }

        public void LoadWorldFromLocal(string worldId = null, Action onSuccess = null, Action<string> onError = null)
        {
            if (IsLoggedInWorkspaceSession())
            {
                onError?.Invoke("Logged-in workspace: local load is forbidden.");
                return;
            }
            worldId = worldId ?? GetCurrentWorldId();
            var snapshot = LocalWorldStorage.LoadFromLocal(worldId);
            if (snapshot == null)
            {
                onError?.Invoke($"Failed to load world '{worldId}' from local storage");
                return;
            }
            WorldSnapshotApplier.ApplySnapshot(snapshot, clearExisting: true);
            onSuccess?.Invoke();
        }

        public void LoadWorldFromServer(string worldId = null, Action onSuccess = null, Action<string> onError = null)
        {
            if (IsLoggedInWorkspaceSession())
            {
                onError?.Invoke("Logged-in workspace: use BootFlow WorldEntryGate before entering.");
                return;
            }
            worldId = worldId ?? GetCurrentWorldId();
            EnsureHttpService().LoadFromServer(
                worldId,
                snapshot =>
                {
                    WorldSnapshotApplier.ApplySnapshot(snapshot, clearExisting: true);
                    onSuccess?.Invoke();
                },
                onError);
        }

        public void ClearWorld()
        {
            if (IsLoggedInWorkspaceSession())
            {
                Debug.LogWarning("[WorldSnapshotManager] Logged-in workspace: ClearWorld is server-authoritative only.");
                return;
            }
            WorldSnapshotApplier.ClearAllWorldObjects();
        }

        public WorldSnapshot BuildSnapshot(string worldId = null)
        {
            return WorldSnapshotBuilder.BuildSnapshot(worldId ?? GetCurrentWorldId());
        }

        public void SetPrefabRegistry(PrefabRegistry registry)
        {
            prefabRegistry = registry;
            PrefabRegistryManager.SetRegistry(registry);
        }

        public void RegisterPrefab(string prefabId, GameObject prefab)
        {
            PrefabRegistryManager.RegisterPrefab(prefabId, prefab);
        }
    }
}
