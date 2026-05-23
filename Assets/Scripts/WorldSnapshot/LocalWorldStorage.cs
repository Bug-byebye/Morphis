using System.IO;
using UnityEngine;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 本地世界存储：使用 JSON 文件保存/加载世界快照
    /// </summary>
    public static class LocalWorldStorage
    {
        private static string GetStorageDirectory()
        {
            // 使用 Application.persistentDataPath（跨平台）
            var dir = Path.Combine(Application.persistentDataPath, "WorldSnapshots");
            
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Debug.Log($"[LocalWorldStorage] Created directory: {dir}");
            }

            return dir;
        }

        private static string GetFilePath(string worldId)
        {
            var fileName = $"world_{worldId}.json";
            return Path.Combine(GetStorageDirectory(), fileName);
        }

        /// <summary>
        /// 保存世界快照到本地
        /// </summary>
        /// <param name="snapshot">要保存的快照</param>
        /// <returns>是否成功</returns>
        public static bool SaveToLocal(WorldSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogError("[LocalWorldStorage] Cannot save: snapshot is null");
                return false;
            }

            if (string.IsNullOrEmpty(snapshot.world_id))
            {
                Debug.LogError("[LocalWorldStorage] Cannot save: world_id is null or empty");
                return false;
            }

            try
            {
                var json = WorldSnapshotJson.Serialize(snapshot);
                var filePath = GetFilePath(snapshot.world_id);

                File.WriteAllText(filePath, json);

                Debug.Log($"[LocalWorldStorage] Saved snapshot to: {filePath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LocalWorldStorage] Failed to save snapshot: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从本地加载世界快照
        /// </summary>
        /// <param name="worldId">世界 ID</param>
        /// <returns>世界快照，如果失败则返回 null</returns>
        public static WorldSnapshot LoadFromLocal(string worldId)
        {
            if (string.IsNullOrEmpty(worldId))
            {
                Debug.LogError("[LocalWorldStorage] Cannot load: world_id is null or empty");
                return null;
            }

            var filePath = GetFilePath(worldId);

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[LocalWorldStorage] File not found: {filePath}");
                return null;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var snapshot = WorldSnapshotJson.Deserialize(json);

                if (snapshot == null)
                {
                    Debug.LogError("[LocalWorldStorage] Failed to parse JSON");
                    return null;
                }

                Debug.Log($"[LocalWorldStorage] Loaded snapshot from: {filePath}, version: {snapshot.version}, objects: {snapshot.objects?.Count ?? 0}");
                return snapshot;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LocalWorldStorage] Failed to load snapshot: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查本地是否存在指定世界的快照
        /// </summary>
        public static bool Exists(string worldId)
        {
            if (string.IsNullOrEmpty(worldId)) return false;
            var filePath = GetFilePath(worldId);
            return File.Exists(filePath);
        }

        /// <summary>
        /// 删除本地快照
        /// </summary>
        public static bool Delete(string worldId)
        {
            if (string.IsNullOrEmpty(worldId)) return false;

            var filePath = GetFilePath(worldId);
            
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[LocalWorldStorage] File not found: {filePath}");
                return false;
            }

            try
            {
                File.Delete(filePath);
                Debug.Log($"[LocalWorldStorage] Deleted snapshot: {filePath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LocalWorldStorage] Failed to delete snapshot: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取所有已保存的世界 ID 列表
        /// </summary>
        public static string[] GetAllWorldIds()
        {
            var dir = GetStorageDirectory();
            if (!Directory.Exists(dir)) return new string[0];

            var files = Directory.GetFiles(dir, "world_*.json");
            var worldIds = new System.Collections.Generic.List<string>();

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                // 提取 world_xxx 中的 xxx
                if (fileName.StartsWith("world_"))
                {
                    var worldId = fileName.Substring(6); // "world_".Length = 6
                    worldIds.Add(worldId);
                }
            }

            return worldIds.ToArray();
        }
    }
}
