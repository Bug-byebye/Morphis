using System.Collections.Generic;
using UnityEngine;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// Prefab 注册表：管理 prefab_id 到 Prefab 的映射
    /// 支持通过 ScriptableObject 配置或运行时注册
    /// </summary>
    [CreateAssetMenu(fileName = "PrefabRegistry", menuName = "Morphis/WorldSnapshot/PrefabRegistry")]
    public class PrefabRegistry : ScriptableObject
    {
        [System.Serializable]
        public class PrefabEntry
        {
            public string prefabId;
            public GameObject prefab;
        }

        [Header("Prefab 映射表")]
        [SerializeField] private List<PrefabEntry> _prefabs = new List<PrefabEntry>();

        private Dictionary<string, GameObject> _prefabDict;

        /// <summary>
        /// 获取 Prefab（如果不存在则返回 null）
        /// </summary>
        public GameObject GetPrefab(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                Debug.LogWarning("[PrefabRegistry] prefab_id is null or empty");
                return null;
            }

            BuildDictionary();

            if (_prefabDict.TryGetValue(prefabId, out var prefab))
            {
                return prefab;
            }

            Debug.LogWarning($"[PrefabRegistry] Prefab not found for id: {prefabId}");
            return null;
        }

        /// <summary>
        /// 注册一个 Prefab（运行时）
        /// </summary>
        public void RegisterPrefab(string prefabId, GameObject prefab)
        {
            if (string.IsNullOrEmpty(prefabId) || prefab == null)
            {
                Debug.LogWarning("[PrefabRegistry] Cannot register: prefabId or prefab is null");
                return;
            }

            BuildDictionary();

            if (_prefabDict.ContainsKey(prefabId))
            {
                Debug.LogWarning($"[PrefabRegistry] Overwriting existing prefab for id: {prefabId}");
            }

            _prefabDict[prefabId] = prefab;

            // 同时更新序列化列表（如果不存在）
            var existing = _prefabs.Find(e => e.prefabId == prefabId);
            if (existing == null)
            {
                _prefabs.Add(new PrefabEntry { prefabId = prefabId, prefab = prefab });
            }
            else
            {
                existing.prefab = prefab;
            }
        }

        /// <summary>
        /// 检查 Prefab 是否存在
        /// </summary>
        public bool HasPrefab(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId)) return false;
            BuildDictionary();
            return _prefabDict.ContainsKey(prefabId);
        }

        private void BuildDictionary()
        {
            if (_prefabDict != null) return;

            _prefabDict = new Dictionary<string, GameObject>();

            foreach (var entry in _prefabs)
            {
                if (string.IsNullOrEmpty(entry.prefabId) || entry.prefab == null)
                {
                    continue;
                }

                if (_prefabDict.ContainsKey(entry.prefabId))
                {
                    Debug.LogWarning($"[PrefabRegistry] Duplicate prefab_id: {entry.prefabId}");
                    continue;
                }

                _prefabDict[entry.prefabId] = entry.prefab;
            }

            Debug.Log($"[PrefabRegistry] Built dictionary with {_prefabDict.Count} prefabs");
        }

        private void OnEnable()
        {
            // 重置字典，以便在编辑器中修改后重新构建
            _prefabDict = null;
        }
    }

    /// <summary>
    /// 全局 PrefabRegistry 访问器（单例模式）
    /// </summary>
    public static class PrefabRegistryManager
    {
        private static PrefabRegistry _instance;
        private static readonly Dictionary<string, GameObject> _runtimePrefabs = new Dictionary<string, GameObject>();

        /// <summary>
        /// 设置全局 PrefabRegistry 实例
        /// </summary>
        public static void SetRegistry(PrefabRegistry registry)
        {
            _instance = registry;
            Debug.Log("[PrefabRegistryManager] Registry set");
        }

        /// <summary>
        /// 获取 Prefab（支持运行时注册、ScriptableObject、Resources/Placeables/、primitive: 由 Applier 单独处理）
        /// </summary>
        public static GameObject GetPrefab(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
                return null;

            // 兼容旧格式：glb:XXX -> Placeables/XXX
            if (prefabId.StartsWith("glb:"))
            {
                var name = prefabId.Substring("glb:".Length);
                if (!string.IsNullOrEmpty(name))
                    prefabId = $"Placeables/{name}";
            }

            // 先检查运行时注册的 Prefab
            if (_runtimePrefabs.TryGetValue(prefabId, out var runtimePrefab))
            {
                return runtimePrefab;
            }

            // 再检查 ScriptableObject 中的 Prefab
            if (_instance != null)
            {
                var fromRegistry = _instance.GetPrefab(prefabId);
                if (fromRegistry != null) return fromRegistry;
            }

            // 与后端/场景保存一致：Placeables/XXX 从 Resources 加载（与 ModelLibrary 约定一致）
            if (prefabId != null && prefabId.StartsWith("Placeables/"))
            {
                var loaded = Resources.Load<GameObject>(prefabId);
                if (loaded != null)
                {
                    _runtimePrefabs[prefabId] = loaded;
                    return loaded;
                }
            }

            // 兼容旧 fallback：prefab_id 只有名称时，尝试 Placeables/名称
            if (!prefabId.Contains("/"))
            {
                var placeablesId = $"Placeables/{prefabId}";
                var loaded = Resources.Load<GameObject>(placeablesId);
                if (loaded != null)
                {
                    _runtimePrefabs[prefabId] = loaded;
                    _runtimePrefabs[placeablesId] = loaded;
                    return loaded;
                }
            }

            // primitive: 由 WorldSnapshotApplier 在应用时创建，此处返回 null
            if (prefabId != null && prefabId.StartsWith("primitive:"))
                return null;

            Debug.LogWarning($"[PrefabRegistryManager] Prefab not found for id: {prefabId}");
            return null;
        }

        /// <summary>
        /// 运行时注册 Prefab（优先级高于 ScriptableObject）
        /// </summary>
        public static void RegisterPrefab(string prefabId, GameObject prefab)
        {
            if (string.IsNullOrEmpty(prefabId) || prefab == null)
            {
                Debug.LogWarning("[PrefabRegistryManager] Cannot register: prefabId or prefab is null");
                return;
            }

            _runtimePrefabs[prefabId] = prefab;
            Debug.Log($"[PrefabRegistryManager] Registered runtime prefab: {prefabId}");
        }

        /// <summary>
        /// 检查 Prefab 是否存在
        /// </summary>
        public static bool HasPrefab(string prefabId)
        {
            if (_runtimePrefabs.ContainsKey(prefabId)) return true;
            if (_instance != null) return _instance.HasPrefab(prefabId);
            return false;
        }
    }
}
