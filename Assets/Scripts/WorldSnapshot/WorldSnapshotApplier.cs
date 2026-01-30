using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 世界快照应用器：根据 WorldSnapshot 重建场景中的物体
    /// </summary>
    public static class WorldSnapshotApplier
    {
        /// <summary>
        /// 应用世界快照到当前场景
        /// </summary>
        /// <param name="snapshot">要应用的世界快照</param>
        /// <param name="clearExisting">是否先清空现有的 WorldObject</param>
        /// <param name="parent">新实例化的物体的父节点（可选）</param>
        /// <returns>成功实例化的物体数量</returns>
        public static int ApplySnapshot(WorldSnapshot snapshot, bool clearExisting = true, Transform parent = null)
        {
            if (snapshot == null)
            {
                Debug.LogError("[WorldSnapshotApplier] Snapshot is null");
                return 0;
            }

            if (snapshot.objects == null || snapshot.objects.Count == 0)
            {
                Debug.LogWarning("[WorldSnapshotApplier] Snapshot has no objects");
                if (clearExisting)
                {
                    ClearAllWorldObjects();
                }
                return 0;
            }

            Debug.Log($"[WorldSnapshotApplier] Applying snapshot for world '{snapshot.world_id}', version {snapshot.version}, objects: {snapshot.objects.Count}");

            // 1. 清空现有物体（如果需要）
            if (clearExisting)
            {
                ClearAllWorldObjects();
            }

            // 2. 实例化所有物体
            int successCount = 0;
            int failCount = 0;

            foreach (var objData in snapshot.objects)
            {
                if (string.IsNullOrEmpty(objData.prefab_id))
                {
                    Debug.LogWarning($"[WorldSnapshotApplier] Object {objData.object_id} has no prefab_id, skipping");
                    failCount++;
                    continue;
                }

                var prefab = PrefabRegistryManager.GetPrefab(objData.prefab_id);
                if (prefab == null)
                {
                    Debug.LogWarning($"[WorldSnapshotApplier] Prefab not found for id: {objData.prefab_id}, object_id: {objData.object_id}");
                    failCount++;
                    continue;
                }

                // 实例化 Prefab
                var instance = Object.Instantiate(prefab, parent);
                instance.name = prefab.name; // 保持 Prefab 名称

                // 应用 Transform 数据
                instance.transform.position = objData.position;
                instance.transform.rotation = objData.rotation;
                instance.transform.localScale = objData.scale;

                // 确保有 WorldObject 组件并应用数据
                var worldObj = instance.GetComponent<WorldObject>();
                if (worldObj == null)
                {
                    Debug.LogWarning($"[WorldSnapshotApplier] Prefab '{objData.prefab_id}' has no WorldObject component. Adding it...");
                    worldObj = instance.AddComponent<WorldObject>();
                }

                worldObj.PrefabId = objData.prefab_id;
                worldObj.ApplyData(objData);

                successCount++;
            }

            Debug.Log($"[WorldSnapshotApplier] Applied snapshot: {successCount} success, {failCount} failed");

            return successCount;
        }

        /// <summary>
        /// 清空场景中所有 WorldObject
        /// </summary>
        public static void ClearAllWorldObjects()
        {
            var worldObjects = FindAllWorldObjects();
            int count = worldObjects.Count;

            foreach (var worldObj in worldObjects)
            {
                Object.Destroy(worldObj.gameObject);
            }

            Debug.Log($"[WorldSnapshotApplier] Cleared {count} world objects");
        }

        /// <summary>
        /// 查找场景中所有 WorldObject（与 WorldSnapshotBuilder 相同的逻辑）
        /// </summary>
        private static List<WorldObject> FindAllWorldObjects()
        {
            var result = new List<WorldObject>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var rootObjects = scene.GetRootGameObjects();
                foreach (var rootObj in rootObjects)
                {
                    FindWorldObjectsRecursive(rootObj.transform, result);
                }
            }

            return result;
        }

        private static void FindWorldObjectsRecursive(Transform parent, List<WorldObject> result)
        {
            var worldObj = parent.GetComponent<WorldObject>();
            if (worldObj != null)
            {
                result.Add(worldObj);
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                FindWorldObjectsRecursive(parent.GetChild(i), result);
            }
        }
    }
}
