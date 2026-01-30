using UnityEngine;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 世界快照系统使用示例
    /// 将此脚本挂载到场景中的任意 GameObject 上，即可通过按键测试功能
    /// </summary>
    public class WorldSnapshotExample : MonoBehaviour
    {
        [Header("测试配置")]
        [SerializeField] private string testWorldId = "test_world";

        [Header("按键绑定")]
        [SerializeField] private KeyCode saveLocalKey = KeyCode.F5;
        [SerializeField] private KeyCode loadLocalKey = KeyCode.F6;
        [SerializeField] private KeyCode saveServerKey = KeyCode.F7;
        [SerializeField] private KeyCode loadServerKey = KeyCode.F8;
        [SerializeField] private KeyCode clearWorldKey = KeyCode.F9;

        private WorldSnapshotManager _manager;

        private void Start()
        {
            _manager = WorldSnapshotManager.Instance;
            if (_manager == null)
            {
                Debug.LogWarning("[WorldSnapshotExample] WorldSnapshotManager not found. Creating one...");
                var go = new GameObject("WorldSnapshotManager");
                _manager = go.AddComponent<WorldSnapshotManager>();
            }

            Debug.Log($"[WorldSnapshotExample] 世界快照系统已就绪");
            Debug.Log($"[WorldSnapshotExample] 按键说明：");
            Debug.Log($"[WorldSnapshotExample]   {saveLocalKey} - 保存到本地");
            Debug.Log($"[WorldSnapshotExample]   {loadLocalKey} - 从本地加载");
            Debug.Log($"[WorldSnapshotExample]   {saveServerKey} - 保存到服务器");
            Debug.Log($"[WorldSnapshotExample]   {loadServerKey} - 从服务器加载");
            Debug.Log($"[WorldSnapshotExample]   {clearWorldKey} - 清空世界");
        }

        private void Update()
        {
            if (_manager == null) return;

            // 保存到本地
            if (Input.GetKeyDown(saveLocalKey))
            {
                bool success = _manager.SaveWorldLocal(testWorldId);
                Debug.Log($"[WorldSnapshotExample] 保存到本地: {(success ? "成功" : "失败")}");
            }

            // 从本地加载
            if (Input.GetKeyDown(loadLocalKey))
            {
                _manager.LoadWorldFromLocal(testWorldId,
                    onSuccess: () => Debug.Log($"[WorldSnapshotExample] 从本地加载成功"),
                    onError: (error) => Debug.LogError($"[WorldSnapshotExample] 从本地加载失败: {error}"));
            }

            // 保存到服务器
            if (Input.GetKeyDown(saveServerKey))
            {
                _manager.SaveWorldServer(testWorldId,
                    onSuccess: () => Debug.Log($"[WorldSnapshotExample] 保存到服务器成功"),
                    onError: (error) => Debug.LogError($"[WorldSnapshotExample] 保存到服务器失败: {error}"));
            }

            // 从服务器加载
            if (Input.GetKeyDown(loadServerKey))
            {
                _manager.LoadWorldFromServer(testWorldId,
                    onSuccess: () => Debug.Log($"[WorldSnapshotExample] 从服务器加载成功"),
                    onError: (error) => Debug.LogError($"[WorldSnapshotExample] 从服务器加载失败: {error}"));
            }

            // 清空世界
            if (Input.GetKeyDown(clearWorldKey))
            {
                _manager.ClearWorld();
                Debug.Log($"[WorldSnapshotExample] 世界已清空");
            }
        }
    }
}
