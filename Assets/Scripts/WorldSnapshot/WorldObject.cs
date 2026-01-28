using UnityEngine;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 标记一个 GameObject 为"可被世界快照系统序列化"的物体
    /// 所有需要保存到世界快照的物体都必须挂载此组件
    /// </summary>
    public class WorldObject : MonoBehaviour
    {
        [Header("World Snapshot Settings")]
        [Tooltip("Prefab ID，用于从 PrefabRegistry 中查找对应的 Prefab")]
        [SerializeField] private string _prefabId;

        [Tooltip("对象唯一 ID（自动生成，无需手动设置）")]
        [SerializeField] private string _objectId;

        /// <summary>
        /// Prefab ID，用于重建物体
        /// </summary>
        public string PrefabId
        {
            get
            {
                if (string.IsNullOrEmpty(_prefabId))
                {
                    // 如果没有设置 prefab_id，使用 GameObject 名称作为 fallback（不推荐，但允许）
                    Debug.LogWarning($"[WorldObject] {gameObject.name} has no prefab_id set. Using name as fallback.");
                    _prefabId = gameObject.name;
                }
                return _prefabId;
            }
            set => _prefabId = value;
        }

        /// <summary>
        /// 对象唯一 ID
        /// </summary>
        public string ObjectId
        {
            get
            {
                if (string.IsNullOrEmpty(_objectId))
                {
                    _objectId = System.Guid.NewGuid().ToString();
                }
                return _objectId;
            }
            set => _objectId = value;
        }

        private void Awake()
        {
            // 确保有唯一 ID
            if (string.IsNullOrEmpty(_objectId))
            {
                _objectId = System.Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// 从 WorldObjectData 恢复此物体的数据（用于加载快照时）
        /// </summary>
        public void ApplyData(WorldObjectData data)
        {
            if (data == null) return;

            _objectId = data.object_id;
            transform.position = data.position;
            transform.rotation = data.rotation;
            transform.localScale = data.scale;
        }

        /// <summary>
        /// 导出为 WorldObjectData
        /// </summary>
        public WorldObjectData ExportData()
        {
            return new WorldObjectData(
                PrefabId,
                transform.position,
                transform.rotation,
                transform.localScale
            )
            {
                object_id = ObjectId
            };
        }
    }
}
