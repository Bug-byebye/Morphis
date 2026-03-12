using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Morphis;
using Morphis.WorldSnapshot;
using Morphis.ModelPlacement;
using GLTFast;

namespace StarterAssets
{
    /// <summary>
    /// 负责 Mirror 下本地/远程玩家的输入与相机配置：
    /// - 本地玩家：启用 ThirdPersonController / PlayerInput / StarterAssetsInputs，绑定 Cinemachine 相机
    /// - 远程玩家：禁用所有输入组件 + Camera/AudioListener，只保留网络同步的可见角色
    /// </summary>
    public class NetworkPlayerSetup : NetworkBehaviour
    {
        // =========================
        // World Authority (Server)
        // =========================
        private static bool _serverWorldLoaded;
        private static string _serverWorldId;
        private static int _serverWorldVersion = 1;
        private static readonly Dictionary<string, WorldObjectData> _serverObjects = new Dictionary<string, WorldObjectData>();

        // =========================
        // Client object cache
        // =========================
        private static readonly Dictionary<string, GameObject> _clientObjects = new Dictionary<string, GameObject>();

        public static NetworkPlayerSetup Local { get; private set; }

        [Header("Components to disable for remote players")]
        [Tooltip("这些组件会在远程玩家上被禁用（可选扩展）")]
        public MonoBehaviour[] componentsToDisableForRemote;

        [Header("Cinemachine")]
        [Tooltip("Cinemachine 跟随目标（通常是 PlayerCameraRoot）")]
        public Transform cinemachineFollowTarget;

        public override void OnStartServer()
        {
            base.OnStartServer();

            // 每个玩家对象都会跑到这里：用于给新加入的客户端下发当前世界快照
            if (_serverWorldLoaded)
            {
                var snapshot = BuildSnapshotFromAuthority();
                var json = JsonUtility.ToJson(snapshot, prettyPrint: false);
                TargetApplySnapshotJson(connectionToClient, json);
            }
            else
            {
                // 首次启动：由服务器拉取一次数据库快照，并广播给所有客户端
                StartCoroutine(LoadWorldOnceOnServer());
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            Local = this;

            // 本地玩家：启用所有输入与运动组件
            EnableComponents(true);

            // 绑定所有 Cinemachine 虚拟相机的 Follow / LookAt
            SetupCameraForLocalPlayer();

            Debug.Log($"[NetworkPlayerSetup] Local player setup complete: {gameObject.name}");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isLocalPlayer)
            {
                // 远程玩家：只作为被同步的可见角色
                DisableRemotePlayerComponents();
                Debug.Log($"[NetworkPlayerSetup] Remote player input/camera disabled: {gameObject.name}");
            }
            else
            {
                // Safety: ensure local player always has components enabled
                EnableComponents(true);
                Debug.Log($"[NetworkPlayerSetup] Local player components ensured enabled: {gameObject.name}");
            }
        }

        /// <summary>
        /// 远程玩家：完全禁用输入 & 本地专用的视觉/听觉组件。
        /// </summary>
        private void DisableRemotePlayerComponents()
        {
            // 1) 输入相关组件
#if ENABLE_INPUT_SYSTEM
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = false;
            }
#endif

            var starterInputs = GetComponent<StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.enabled = false;
            }

            var thirdPersonController = GetComponent<ThirdPersonController>();
            if (thirdPersonController != null)
            {
                thirdPersonController.enabled = false;
            }

            // 可选扩展组件
            if (componentsToDisableForRemote != null)
            {
                foreach (var component in componentsToDisableForRemote)
                {
                    if (component != null)
                    {
                        component.enabled = false;
                    }
                }
            }

            // 2) 碰撞 / 移动控制（通过 NetworkTransform 同步 Transform）
            var characterController = GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            // 3) 禁用挂在该玩家层级下的 Camera / AudioListener，
            //    确保远程玩家不会产生额外视角与声音。
            var cameras = GetComponentsInChildren<Camera>(true);
            foreach (var cam in cameras)
            {
                cam.enabled = false;
            }

            var listeners = GetComponentsInChildren<AudioListener>(true);
            foreach (var listener in listeners)
            {
                listener.enabled = false;
            }
        }

        /// <summary>
        /// 本地玩家：启用输入 & 控制组件。
        /// </summary>
        private void EnableComponents(bool enable)
        {
#if ENABLE_INPUT_SYSTEM
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = enable;
            }
#endif

            var starterInputs = GetComponent<StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.enabled = enable;
            }

            var thirdPersonController = GetComponent<ThirdPersonController>();
            if (thirdPersonController != null)
            {
                thirdPersonController.enabled = enable;
            }

            var characterController = GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = enable;
            }
        }

        /// <summary>
        /// 将所有 Cinemachine 虚拟相机的 Follow / LookAt 都指向本地玩家。
        /// </summary>
        private void SetupCameraForLocalPlayer()
        {
            if (cinemachineFollowTarget == null)
            {
                // 默认尝试寻找 PlayerCameraRoot 子物体
                var cameraRoot = transform.Find("PlayerCameraRoot");
                if (cameraRoot != null)
                {
                    cinemachineFollowTarget = cameraRoot;
                }
                else
                {
                    Debug.LogWarning("[NetworkPlayerSetup] cinemachineFollowTarget is null and couldn't find PlayerCameraRoot");
                    return;
                }
            }

            // 查找场景中所有 Cinemachine 虚拟相机
            var virtualCameras = FindObjectsByType<Cinemachine.CinemachineVirtualCamera>(FindObjectsSortMode.None);
            foreach (var vc in virtualCameras)
            {
                if (vc == null) continue;

                vc.Follow = cinemachineFollowTarget;
                vc.LookAt = cinemachineFollowTarget;

                Debug.Log($"[NetworkPlayerSetup] Cinemachine camera '{vc.name}' now Follow/LookAt: {cinemachineFollowTarget.name}");
            }
        }

        // ==========================================================
        // Client API (called by UI / mover) -> Command -> Server -> RPC
        // ==========================================================
        public bool RequestPlace(string prefabId, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!isLocalPlayer) return false;
            if (!NetworkClient.active) return false;
            if (string.IsNullOrEmpty(prefabId)) return false;
            CmdRequestPlace(prefabId, position, rotation, scale);
            return true;
        }

        public bool RequestMove(string objectId, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!isLocalPlayer) return false;
            if (!NetworkClient.active) return false;
            if (string.IsNullOrEmpty(objectId)) return false;
            CmdRequestMove(objectId, position, rotation, scale);
            return true;
        }

        public bool RequestDelete(string objectId)
        {
            if (!isLocalPlayer) return false;
            if (!NetworkClient.active) return false;
            if (string.IsNullOrEmpty(objectId)) return false;
            CmdRequestDelete(objectId);
            return true;
        }

        public bool RequestSaveWorld()
        {
            if (!isLocalPlayer) return false;
            if (!NetworkClient.active) return false;
            CmdRequestSaveWorld();
            return true;
        }

        // =========================
        // Commands (Client -> Server)
        // =========================
        [Command]
        private void CmdRequestPlace(string prefabId, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            EnsureServerWorldId();

            // 服务器生成权威 object_id
            var data = new WorldObjectData(prefabId, position, rotation, scale)
            {
                object_id = Guid.NewGuid().ToString(),
                prefab_id = prefabId,
                position = position,
                rotation = rotation,
                scale = scale
            };

            // 权威存储
            _serverObjects[data.object_id] = data;
            _serverWorldVersion++;

            // 广播生成
            RpcSpawnWorldObject(data.object_id, data.prefab_id, data.position, data.rotation, data.scale, data.comment ?? "");
        }

        [Command]
        private void CmdRequestMove(string objectId, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (string.IsNullOrEmpty(objectId)) return;
            if (!_serverObjects.TryGetValue(objectId, out var data)) return;

            data.position = position;
            data.rotation = rotation;
            data.scale = scale;
            _serverWorldVersion++;

            RpcUpdateWorldObject(objectId, position, rotation, scale);
        }

        [Command]
        private void CmdRequestDelete(string objectId)
        {
            if (string.IsNullOrEmpty(objectId)) return;
            if (!_serverObjects.Remove(objectId)) return;
            _serverWorldVersion++;
            RpcDestroyWorldObject(objectId);
        }

        [Command]
        private void CmdRequestSaveWorld()
        {
            EnsureServerWorldId();
            var snapshot = BuildSnapshotFromAuthority();

            var http = HttpWorldService.GetOrCreate();
            http.SaveToServer(snapshot,
                onSuccess: () => Debug.Log($"[WorldAuthority] Saved world '{snapshot.world_id}' (v{snapshot.version})"),
                onError: err => Debug.LogWarning($"[WorldAuthority] Save failed: {err}"));
        }

        // =========================
        // RPCs (Server -> Clients)
        // =========================
        [ClientRpc]
        private void RpcSpawnWorldObject(string objectId, string prefabId, Vector3 position, Quaternion rotation, Vector3 scale, string comment)
        {
            if (string.IsNullOrEmpty(objectId) || string.IsNullOrEmpty(prefabId)) return;
            if (_clientObjects.ContainsKey(objectId)) return; // 已存在则忽略

            SpawnOrUpdateClientObject(objectId, prefabId, position, rotation, scale, comment);
        }

        [ClientRpc]
        private void RpcUpdateWorldObject(string objectId, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (string.IsNullOrEmpty(objectId)) return;
            if (!_clientObjects.TryGetValue(objectId, out var go) || go == null) return;

            go.transform.position = position;
            go.transform.rotation = rotation;
            go.transform.localScale = scale;
        }

        [ClientRpc]
        private void RpcDestroyWorldObject(string objectId)
        {
            if (string.IsNullOrEmpty(objectId)) return;
            if (_clientObjects.TryGetValue(objectId, out var go) && go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
            _clientObjects.Remove(objectId);
        }

        [TargetRpc]
        private void TargetApplySnapshotJson(NetworkConnectionToClient target, string snapshotJson)
        {
            if (string.IsNullOrEmpty(snapshotJson)) return;
            ApplySnapshotJson(snapshotJson);
        }

        [ClientRpc]
        private void RpcApplySnapshotJson(string snapshotJson)
        {
            if (string.IsNullOrEmpty(snapshotJson)) return;
            ApplySnapshotJson(snapshotJson);
        }

        private void ApplySnapshotJson(string snapshotJson)
        {
            try
            {
                var snapshot = JsonUtility.FromJson<WorldSnapshot>(snapshotJson);
                if (snapshot == null)
                {
                    Debug.LogWarning("[WorldAuthority] Snapshot JSON parse returned null");
                    return;
                }

                // 清空当前客户端缓存与场景对象（只清空 WorldObject 标记的对象）
                _clientObjects.Clear();
                WorldSnapshotApplier.ApplySnapshot(snapshot, clearExisting: true);

                // 重新建立 object_id -> GameObject 的缓存（通过场景扫描 WorldObject）
                var worldObjects = UnityEngine.Object.FindObjectsByType<WorldObject>(FindObjectsSortMode.None);
                foreach (var wo in worldObjects)
                {
                    if (wo == null) continue;
                    var id = wo.ObjectId;
                    if (!string.IsNullOrEmpty(id) && !_clientObjects.ContainsKey(id))
                    {
                        _clientObjects[id] = wo.gameObject;
                    }
                }

                Debug.Log($"[WorldAuthority] Applied snapshot: world='{snapshot.world_id}', objects={snapshot.objects?.Count ?? 0}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WorldAuthority] Failed to apply snapshot JSON: {e.GetType().Name}: {e.Message}");
            }
        }

        private void SpawnOrUpdateClientObject(string objectId, string prefabId, Vector3 position, Quaternion rotation, Vector3 scale, string comment)
        {
            // primitive
            if (prefabId.StartsWith("primitive:", StringComparison.OrdinalIgnoreCase))
            {
                var typeStr = prefabId.Substring("primitive:".Length);
                if (Enum.TryParse<PrimitiveType>(typeStr, true, out var primitiveType))
                {
                    var go = GameObject.CreatePrimitive(primitiveType);
                    go.name = typeStr;
                    go.transform.position = position;
                    go.transform.rotation = rotation;
                    go.transform.localScale = scale;
                    EnsurePlaceableComponents(go);
                    EnsureWorldObject(go, objectId, prefabId, comment);
                    _clientObjects[objectId] = go;
                }
                return;
            }

            // glb:XXX (客户端从 Resources/Placeables/ 加载 TextAsset 并实例化)
            if (prefabId.StartsWith("glb:", StringComparison.OrdinalIgnoreCase))
            {
                var name = prefabId.Substring("glb:".Length);
                StartCoroutine(LoadGlbAndSpawn(name, objectId, prefabId, position, rotation, scale, comment));
                return;
            }

            // prefab from registry/resources
            var prefab = PrefabRegistryManager.GetPrefab(prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[WorldAuthority] Prefab not found for id='{prefabId}'");
                return;
            }

            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = prefab.name;
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.transform.localScale = scale;
            EnsurePlaceableComponents(instance);
            EnsureWorldObject(instance, objectId, prefabId, comment);
            _clientObjects[objectId] = instance;
        }

        private System.Collections.IEnumerator LoadGlbAndSpawn(string resourceName, string objectId, string prefabId, Vector3 position, Quaternion rotation, Vector3 scale, string comment)
        {
            if (string.IsNullOrEmpty(resourceName)) yield break;

            // 约定：Resources/Placeables/<name>.glb 以 TextAsset 形式存在
            var ta = Resources.Load<TextAsset>($"Placeables/{resourceName}");
            if (ta == null)
            {
                Debug.LogWarning($"[WorldAuthority] GLB resource not found: Placeables/{resourceName}");
                yield break;
            }

            var root = new GameObject(resourceName);
            root.transform.position = position;
            root.transform.rotation = rotation;
            root.transform.localScale = scale;

            var gltf = new GltfImport();
            var loadTask = gltf.LoadGltfBinary(ta.bytes);
            while (!loadTask.IsCompleted) yield return null;
            if (!loadTask.Result)
            {
                Debug.LogWarning($"[WorldAuthority] Failed to load GLB: {resourceName}");
                UnityEngine.Object.Destroy(root);
                yield break;
            }

            var instTask = gltf.InstantiateMainSceneAsync(root.transform);
            while (!instTask.IsCompleted) yield return null;
            if (!instTask.Result)
            {
                Debug.LogWarning($"[WorldAuthority] Failed to instantiate GLB: {resourceName}");
                UnityEngine.Object.Destroy(root);
                yield break;
            }

            EnsurePlaceableComponents(root);
            EnsureWorldObject(root, objectId, prefabId, comment);
            _clientObjects[objectId] = root;
        }

        private static void EnsurePlaceableComponents(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponent<PlaceableObjectMover>() == null) go.AddComponent<PlaceableObjectMover>();
            if (go.GetComponent<InteractableObject>() == null) go.AddComponent<InteractableObject>();
        }

        private static void EnsureWorldObject(GameObject go, string objectId, string prefabId, string comment)
        {
            if (go == null) return;
            var wo = go.GetComponent<WorldObject>();
            if (wo == null) wo = go.AddComponent<WorldObject>();
            wo.ObjectId = objectId;
            wo.PrefabId = prefabId;

            if (!string.IsNullOrEmpty(comment))
            {
                var interactable = go.GetComponent<InteractableObject>();
                if (interactable != null) interactable.SetComment(comment);
            }
        }

        // =========================
        // Server world load
        // =========================
        private System.Collections.IEnumerator LoadWorldOnceOnServer()
        {
            if (_serverWorldLoaded) yield break;

            EnsureServerWorldId();
            var worldId = _serverWorldId;
            if (string.IsNullOrEmpty(worldId))
            {
                Debug.LogWarning("[WorldAuthority] Server worldId is empty. Start empty world.");
                _serverWorldLoaded = true;
                yield break;
            }

            var http = HttpWorldService.GetOrCreate();
            bool done = false;
            WorldSnapshot loaded = null;
            string error = null;

            http.LoadFromServer(worldId,
                onSuccess: s =>
                {
                    loaded = s;
                    done = true;
                },
                onError: e =>
                {
                    error = e;
                    done = true;
                });

            while (!done) yield return null;

            if (loaded == null)
            {
                if (!string.IsNullOrEmpty(error) && error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    Debug.Log($"[WorldAuthority] World '{worldId}' not found on backend. Start empty.");
                else
                    Debug.LogWarning($"[WorldAuthority] Load world failed. Start empty. err={error}");

                _serverWorldLoaded = true;
                yield break;
            }

            // 写入权威数据
            _serverObjects.Clear();
            if (loaded.objects != null)
            {
                foreach (var obj in loaded.objects)
                {
                    if (obj == null) continue;
                    if (string.IsNullOrEmpty(obj.object_id)) obj.object_id = Guid.NewGuid().ToString();
                    _serverObjects[obj.object_id] = obj;
                }
            }

            _serverWorldVersion = Mathf.Max(loaded.version, 1);
            _serverWorldLoaded = true;

            // 广播给所有客户端（包括 host）
            var json = JsonUtility.ToJson(BuildSnapshotFromAuthority(), prettyPrint: false);
            RpcApplySnapshotJson(json);

            Debug.Log($"[WorldAuthority] Loaded world '{worldId}' from backend. objects={_serverObjects.Count}, v={_serverWorldVersion}");
        }

        private static void EnsureServerWorldId()
        {
            if (!string.IsNullOrEmpty(_serverWorldId)) return;

            // 优先命令行 worldId（用于 Server 构建）
            if (AppRuntime.IsInitialized && !string.IsNullOrEmpty(AppRuntime.WorldId))
            {
                _serverWorldId = AppRuntime.WorldId;
                return;
            }

            // 其次使用配置文件中的 DefaultWorldId
            var cfg = Morphis.Config.AppConfig.Instance;
            if (cfg != null && !string.IsNullOrEmpty(cfg.DefaultWorldId))
            {
                _serverWorldId = cfg.DefaultWorldId;
                return;
            }

            var msg = "[WorldAuthority] worldId is not provided via --worldId and AppConfig.DefaultWorldId is empty.";
            Debug.LogError(msg);
#if UNITY_EDITOR
            throw new Exception(msg);
#else
            Application.Quit();
#endif
        }

        private static WorldSnapshot BuildSnapshotFromAuthority()
        {
            var snapshot = new WorldSnapshot(_serverWorldId);
            snapshot.version = Mathf.Max(_serverWorldVersion, 1);
            snapshot.objects.Clear();
            foreach (var kv in _serverObjects)
            {
                snapshot.objects.Add(kv.Value);
            }
            return snapshot;
        }
    }
}
