using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Morphis.ModelPlacement;
using GLTFast;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 世界快照应用器：根据 WorldSnapshot 重建场景中的物体
    /// </summary>
    public static class WorldSnapshotApplier
    {
        private sealed class SnapshotCoroutineHost : MonoBehaviour { }
        private static SnapshotCoroutineHost _coroutineHost;

        private static SnapshotCoroutineHost GetCoroutineHost()
        {
            if (_coroutineHost != null) return _coroutineHost;
            var go = new GameObject("WorldSnapshotCoroutineHost");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _coroutineHost = go.AddComponent<SnapshotCoroutineHost>();
            return _coroutineHost;
        }

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

                var normalizedPrefabId = NormalizePrefabId(objData.prefab_id);

                // primitive:XXX 由本处创建（与 ModelLibrary/HotBar 放置时 prefab_id 一致）
                if (normalizedPrefabId.StartsWith("primitive:"))
                {
                    var typeStr = normalizedPrefabId.Substring("primitive:".Length);
                    if (Enum.TryParse<PrimitiveType>(typeStr, true, out var primitiveType))
                    {
                        var instance = GameObject.CreatePrimitive(primitiveType);
                        instance.name = typeStr;
                        ApplyObjectData(instance, objData, normalizedPrefabId, parent);
                        successCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"[WorldSnapshotApplier] Unknown primitive type: {typeStr}");
                        failCount++;
                    }
                    continue;
                }

                var prefab = PrefabRegistryManager.GetPrefab(normalizedPrefabId);
                if (prefab != null)
                {
                    // 实例化 Prefab
                    var instance = UnityEngine.Object.Instantiate(prefab, parent);
                    instance.name = prefab.name; // 保持 Prefab 名称
                    ApplyObjectData(instance, objData, normalizedPrefabId, null);
                    successCount++;
                    continue;
                }

                var glbAsset = ResolveGlbAsset(normalizedPrefabId);
                if (glbAsset != null)
                {
                    GetCoroutineHost().StartCoroutine(InstantiateGlbAndApply(glbAsset, objData, normalizedPrefabId, parent));
                    successCount++;
                    continue;
                }

                Debug.LogWarning($"[WorldSnapshotApplier] Prefab/GLB not found for id: {objData.prefab_id}, object_id: {objData.object_id}");
                failCount++;
            }

            Debug.Log($"[WorldSnapshotApplier] Applied snapshot: {successCount} success, {failCount} failed");

            return successCount;
        }

        private static string NormalizePrefabId(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId)) return prefabId;

            if (prefabId.StartsWith("glb:"))
            {
                var name = prefabId.Substring("glb:".Length);
                if (!string.IsNullOrEmpty(name))
                    return $"Placeables/{name}";
            }

            return prefabId;
        }

        private static TextAsset ResolveGlbAsset(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId)) return null;
            // 支持直接用 Placeables/XXX 或 legacy glb:XXX（已在 NormalizePrefabId 转成 Placeables/XXX）
            var asset = Resources.Load<TextAsset>(prefabId);
            if (asset != null) return asset;

            // 兼容旧 fallback：prefab_id 只有名称时，尝试按 Placeables/名称 加载
            if (!prefabId.Contains("/"))
                return Resources.Load<TextAsset>($"Placeables/{prefabId}");

            return null;
        }

        private static IEnumerator InstantiateGlbAndApply(TextAsset glbAsset, WorldObjectData objData, string prefabId, Transform parent)
        {
            if (glbAsset == null) yield break;

            var root = new GameObject(glbAsset.name);
            if (parent != null) root.transform.SetParent(parent, false);

            var gltf = new GltfImport();
            var loadTask = gltf.LoadGltfBinary(glbAsset.bytes);
            while (!loadTask.IsCompleted) yield return null;
            if (!loadTask.Result)
            {
                Debug.LogWarning($"[WorldSnapshotApplier] Failed to load GLB bytes: {prefabId}");
                UnityEngine.Object.Destroy(root);
                yield break;
            }

            var instTask = gltf.InstantiateMainSceneAsync(root.transform);
            while (!instTask.IsCompleted) yield return null;
            if (!instTask.Result)
            {
                Debug.LogWarning($"[WorldSnapshotApplier] Failed to instantiate GLB scene: {prefabId}");
                UnityEngine.Object.Destroy(root);
                yield break;
            }

            EnsureColliderFromRenderers(root);
            ApplyObjectData(root, objData, prefabId, null);
        }

        private static void ApplyObjectData(GameObject instance, WorldObjectData objData, string prefabId, Transform parentIfNeeded)
        {
            if (instance == null) return;
            if (parentIfNeeded != null) instance.transform.SetParent(parentIfNeeded, false);

            instance.transform.position = objData.position;
            instance.transform.rotation = objData.rotation;
            instance.transform.localScale = objData.scale;

            // 一些资源本身无 Collider。放置流程会补，这里重载也要补，避免“看得到但点不到”。
            EnsureColliderFromRenderers(instance);

            // 恢复后可移动、可留言（与放置时 EnsurePlaceableComponents 一致）
            if (instance.GetComponent<PlaceableObjectMover>() == null) instance.AddComponent<PlaceableObjectMover>();
            if (instance.GetComponent<InteractableObject>() == null) instance.AddComponent<InteractableObject>();

            // 确保有 WorldObject 组件并应用数据
            var worldObj = instance.GetComponent<WorldObject>();
            if (worldObj == null)
            {
                worldObj = instance.AddComponent<WorldObject>();
            }

            worldObj.PrefabId = prefabId;
            worldObj.ApplyData(objData);
        }

        private static void EnsureColliderFromRenderers(GameObject root)
        {
            if (root == null) return;
            if (root.GetComponent<Collider>() != null) return;

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                root.AddComponent<BoxCollider>();
                return;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var box = root.AddComponent<BoxCollider>();
            var centerLocal = root.transform.InverseTransformPoint(bounds.center);
            box.center = centerLocal;
            var ls = root.transform.lossyScale;
            box.size = new Vector3(
                ls.x != 0 ? bounds.size.x / ls.x : bounds.size.x,
                ls.y != 0 ? bounds.size.y / ls.y : bounds.size.y,
                ls.z != 0 ? bounds.size.z / ls.z : bounds.size.z
            );
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
