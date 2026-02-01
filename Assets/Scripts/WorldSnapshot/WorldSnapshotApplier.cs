using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Morphis.ModelPlacement;

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

            // 1. 清空前先通知 ObjectInteractionManager 清空引用，避免访问已销毁对象
            ObjectInteractionManager.ClearTargetsIfExists();

            // 2. 清空现有物体（如果需要）
            if (clearExisting)
            {
                ClearAllWorldObjects();
            }

            // 3. 实例化所有物体
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

                GameObject instance = null;

                // primitive:XXX 由本处创建（与 ModelLibrary/HotBar 放置时 prefab_id 一致）
                if (objData.prefab_id.StartsWith("primitive:"))
                {
                    var typeStr = objData.prefab_id.Substring("primitive:".Length);
                    if (Enum.TryParse<PrimitiveType>(typeStr, true, out var primitiveType))
                    {
                        instance = GameObject.CreatePrimitive(primitiveType);
                        instance.name = typeStr;
                        if (parent != null) instance.transform.SetParent(parent);
                        instance.transform.position = objData.position;
                        instance.transform.rotation = objData.rotation;
                        instance.transform.localScale = objData.scale;
                        if (instance.GetComponent<PlaceableObjectMover>() == null) instance.AddComponent<PlaceableObjectMover>();
                        if (instance.GetComponent<InteractableObject>() == null) instance.AddComponent<InteractableObject>();
                        var wo = instance.GetComponent<WorldObject>();
                        if (wo == null) wo = instance.AddComponent<WorldObject>();
                        wo.PrefabId = objData.prefab_id;
                        wo.ApplyData(objData);
                        successCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"[WorldSnapshotApplier] Unknown primitive type: {typeStr}");
                        failCount++;
                    }
                    continue;
                }

                var prefab = PrefabRegistryManager.GetPrefab(objData.prefab_id);
                if (prefab == null)
                {
                    // glb: 等暂无法从本地恢复，仅记录
                    Debug.LogWarning($"[WorldSnapshotApplier] Prefab not found for id: {objData.prefab_id}, object_id: {objData.object_id}");
                    failCount++;
                    continue;
                }

                // 实例化 Prefab
                instance = UnityEngine.Object.Instantiate(prefab, parent);
                instance.name = prefab.name; // 保持 Prefab 名称

                // 应用 Transform 数据
                instance.transform.position = objData.position;
                instance.transform.rotation = objData.rotation;
                instance.transform.localScale = objData.scale;

                // 恢复后可移动、可留言（与放置时 EnsurePlaceableComponents 一致）
                if (instance.GetComponent<PlaceableObjectMover>() == null) instance.AddComponent<PlaceableObjectMover>();
                if (instance.GetComponent<InteractableObject>() == null) instance.AddComponent<InteractableObject>();

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
                UnityEngine.Object.Destroy(worldObj.gameObject);
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
