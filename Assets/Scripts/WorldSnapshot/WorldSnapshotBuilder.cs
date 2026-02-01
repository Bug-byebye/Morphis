using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Morphis.ModelPlacement;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 世界快照构建器：扫描场景中所有 WorldObject（以及可放置物体），构建 WorldSnapshot。
    /// 与后端 WorldSnapshotPayload 结构一致，便于之后同步到数据库。
    /// </summary>
    public static class WorldSnapshotBuilder
    {
        /// <summary>
        /// 构建当前场景的世界快照
        /// </summary>
        /// <param name="worldId">世界 ID</param>
        /// <param name="version">版本号（如果为 -1，则从现有快照中读取并递增）</param>
        /// <returns>世界快照</returns>
        public static WorldSnapshot BuildSnapshot(string worldId, int version = -1)
        {
            var snapshot = new WorldSnapshot(worldId);
            
            if (version > 0)
            {
                snapshot.version = version;
            }

            // 确保所有可放置物体都有 WorldObject（例如从模型库/热栏拖入但尚未带 WorldObject 的）
            EnsureWorldObjectOnPlaceables();

            // 扫描所有场景中的 WorldObject
            var worldObjects = FindAllWorldObjects();
            
            Debug.Log($"[WorldSnapshotBuilder] Found {worldObjects.Count} world objects in scene");

            foreach (var worldObj in worldObjects)
            {
                var data = worldObj.ExportData();
                snapshot.objects.Add(data);
            }

            Debug.Log($"[WorldSnapshotBuilder] Built snapshot for world '{worldId}' with {snapshot.objects.Count} objects, version {snapshot.version}");
            
            return snapshot;
        }

        /// <summary>
        /// 为所有带 PlaceableObjectMover 且无 WorldObject 的物体添加 WorldObject（prefab_id 用 glb:名称），以便被保存
        /// </summary>
        private static void EnsureWorldObjectOnPlaceables()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var rootObjects = scene.GetRootGameObjects();
                foreach (var rootObj in rootObjects)
                {
                    EnsureWorldObjectRecursive(rootObj.transform);
                }
            }
        }

        private static void EnsureWorldObjectRecursive(Transform parent)
        {
            var mover = parent.GetComponent<PlaceableObjectMover>();
            if (mover != null)
            {
                var wo = parent.GetComponent<WorldObject>();
                if (wo == null)
                {
                    wo = parent.gameObject.AddComponent<WorldObject>();
                    wo.PrefabId = "glb:" + parent.gameObject.name;
                }
            }
            for (int i = 0; i < parent.childCount; i++)
                EnsureWorldObjectRecursive(parent.GetChild(i));
        }

        /// <summary>
        /// 为可放置物体添加 WorldObject 并设置 prefab_id，供 ModelLibraryUI / HotBarManager 等在放置时调用
        /// </summary>
        public static void EnsureWorldObjectForSnapshot(GameObject go, string prefabId)
        {
            if (go == null || string.IsNullOrEmpty(prefabId)) return;
            var wo = go.GetComponent<WorldObject>();
            if (wo == null) wo = go.AddComponent<WorldObject>();
            wo.PrefabId = prefabId;
        }

        /// <summary>
        /// 查找场景中所有 WorldObject
        /// </summary>
        private static List<WorldObject> FindAllWorldObjects()
        {
            var result = new List<WorldObject>();

            // 遍历所有已加载的场景
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var rootObjects = scene.GetRootGameObjects();
                foreach (var rootObj in rootObjects)
                {
                    // 递归查找所有 WorldObject（包括子物体）
                    FindWorldObjectsRecursive(rootObj.transform, result);
                }
            }

            return result;
        }

        private static void FindWorldObjectsRecursive(Transform parent, List<WorldObject> result)
        {
            // 检查当前物体
            var worldObj = parent.GetComponent<WorldObject>();
            if (worldObj != null)
            {
                result.Add(worldObj);
            }

            // 递归检查子物体
            for (int i = 0; i < parent.childCount; i++)
            {
                FindWorldObjectsRecursive(parent.GetChild(i), result);
            }
        }

        /// <summary>
        /// 从现有快照加载并递增版本号
        /// </summary>
        public static WorldSnapshot BuildSnapshotFromExisting(WorldSnapshot existing, string worldId = null)
        {
            var snapshot = new WorldSnapshot(worldId ?? existing.world_id);
            snapshot.version = existing.version;
            snapshot.IncrementVersion(); // 自动递增版本号
            
            // 重新扫描场景
            var worldObjects = FindAllWorldObjects();
            snapshot.objects.Clear();
            
            foreach (var worldObj in worldObjects)
            {
                var data = worldObj.ExportData();
                snapshot.objects.Add(data);
            }

            Debug.Log($"[WorldSnapshotBuilder] Built snapshot from existing, new version: {snapshot.version}");
            
            return snapshot;
        }
    }
}
