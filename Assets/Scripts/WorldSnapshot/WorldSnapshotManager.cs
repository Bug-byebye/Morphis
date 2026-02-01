using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 世界快照管理器：提供统一的 API 来保存/加载世界快照
    /// 支持本地存储和服务器存储；与后端 POST/GET /world/{world_id} 结构一致，便于之后同步到数据库
    /// </summary>
    public class WorldSnapshotManager : MonoBehaviour
    {
        public static WorldSnapshotManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("Prefab Registry（可选，也可以通过代码设置）")]
        [SerializeField] private PrefabRegistry prefabRegistry;

        [Tooltip("默认世界 ID，与 MainScene 场景保存一致")]
        [SerializeField] private string defaultWorldId = "MainScene";

        [Tooltip("是否在进入 MainScene 时自动从本地加载世界")]
        [SerializeField] private bool autoLoadOnStart = true;

        private HttpWorldService _httpService;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 设置 PrefabRegistry
            if (prefabRegistry != null)
            {
                PrefabRegistryManager.SetRegistry(prefabRegistry);
            }

            // 获取或创建 HttpWorldService
            _httpService = HttpWorldService.GetOrCreate();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary> 进入 MainScene 时从本地加载世界；退出/暂停时保存 </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MainScene" || !autoLoadOnStart || string.IsNullOrEmpty(defaultWorldId))
                return;
            if (!LocalWorldStorage.Exists(defaultWorldId))
                return;
            Debug.Log($"[WorldSnapshotManager] MainScene loaded, loading world: {defaultWorldId}");
            LoadWorldFromLocal(defaultWorldId, onError: _ => { });
        }

        private void Start()
        {
            // 若当前已是 MainScene（直接 Play 主场景），也尝试加载
            if (autoLoadOnStart && !string.IsNullOrEmpty(defaultWorldId)
                && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainScene"
                && LocalWorldStorage.Exists(defaultWorldId))
            {
                Debug.Log($"[WorldSnapshotManager] Auto-loading world: {defaultWorldId}");
                LoadWorld(defaultWorldId, useServer: false);
            }
        }

        private void OnApplicationQuit()
        {
            if (!string.IsNullOrEmpty(defaultWorldId))
            {
                SaveWorldLocal(defaultWorldId);
                Debug.Log($"[WorldSnapshotManager] Saved world on quit: {defaultWorldId}");
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && !string.IsNullOrEmpty(defaultWorldId))
                SaveWorldLocal(defaultWorldId);
        }

        /// <summary>
        /// 保存当前世界（本地）
        /// </summary>
        public bool SaveWorldLocal(string worldId = null)
        {
            worldId = worldId ?? defaultWorldId;
            var snapshot = WorldSnapshotBuilder.BuildSnapshot(worldId);
            return LocalWorldStorage.SaveToLocal(snapshot);
        }

        /// <summary>
        /// 保存当前世界（服务器）
        /// </summary>
        public void SaveWorldServer(string worldId = null, Action onSuccess = null, Action<string> onError = null)
        {
            worldId = worldId ?? defaultWorldId;
            var snapshot = WorldSnapshotBuilder.BuildSnapshot(worldId);
            _httpService.SaveToServer(snapshot, onSuccess, onError);
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
            worldId = worldId ?? defaultWorldId;

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
            worldId = worldId ?? defaultWorldId;
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
            worldId = worldId ?? defaultWorldId;
            _httpService.LoadFromServer(worldId,
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
            int count = WorldSnapshotApplier.ApplySnapshot(snapshot, clearExisting: true);
            Debug.Log($"[WorldSnapshotManager] Applied snapshot: {count} objects instantiated");
        }

        /// <summary>
        /// 清空当前世界
        /// </summary>
        public void ClearWorld()
        {
            WorldSnapshotApplier.ClearAllWorldObjects();
            Debug.Log("[WorldSnapshotManager] World cleared");
        }

        /// <summary>
        /// 构建当前世界的快照（不保存）
        /// </summary>
        public WorldSnapshot BuildSnapshot(string worldId = null)
        {
            worldId = worldId ?? defaultWorldId;
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
