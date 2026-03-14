using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Mirror;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Interactions
{
    /// <summary>
    /// Attach this script to any 3D object with a Collider to enable 
    /// double-click scene transitions.
    /// </summary>
    // [RequireComponent(typeof(Collider))] - Removed to avoid "Add component failed" error if collider is missing
    public class SceneTransition : MonoBehaviour, IPointerClickHandler
    {
        [Header("Scene Settings")]
        [Tooltip("The exact name of the scene file (without .unity extension) to load.")]
        public string targetSceneName = "demo";
        
        [Header("Interaction Settings")]
        [Tooltip("Max seconds between clicks to count as a double click.")]
        public float doubleClickThreshold = 0.3f;

        private float lastClickTime = -100f;
        private static bool _isLoadingScene = false;

        private void Start()
        {
            if (GetComponent<Collider>() == null)
            {
                Debug.LogError($"[SceneTransition] Object '{gameObject.name}' is missing a Comparator (BoxCollider, MeshCollider, etc.)! Click interaction will NOT work.");
            }
            
            // Ensure Camera has PhysicsRaycaster to handle pointer clicks
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<PhysicsRaycaster>() == null)
            {
                Debug.Log("[SceneTransition] Main Camera missing PhysicsRaycaster. Adding it automatically.");
                cam.gameObject.AddComponent<PhysicsRaycaster>();
            }
            
            // Auto-resize BoxCollider if it seems too small (default 1x1x1)
            var boxCol = GetComponent<BoxCollider>();
            if (boxCol != null && boxCol.size == Vector3.one && boxCol.center == Vector3.zero)
            {
                FitColliderToChildren(boxCol);
            }
        }
        
        private void FitColliderToChildren(BoxCollider boxVal)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            // Convert world bounds to local space
            // NOTE: Encapsulating bounds works in world space, but collider needs local center/size
            // This simple approximation assumes no rotation on parent during init, or handles it via inverse transform
            boxVal.center = transform.InverseTransformPoint(bounds.center);
            
            // For size, we need to handle scale
            Vector3 worldSize = bounds.size;
            Vector3 localScale = transform.lossyScale;
            boxVal.size = new Vector3(
                worldSize.x / Mathf.Abs(localScale.x), 
                worldSize.y / Mathf.Abs(localScale.y), 
                worldSize.z / Mathf.Abs(localScale.z)
            );
            
            Debug.Log($"[SceneTransition] Auto-resized BoxCollider for {name}");
        }

        // Standard Unity Mouse Event (works if using Legacy Input or "Both")
        private void OnMouseDown()
        {
            // Block clicks when the workflow station editor is open
            if (AIPipeline.UI.SimpleNodeEditor.IsEditorOpen) return;
            HandleClick();
        }

        // EventSystem Event (works if Camera has PhysicsRaycaster and using Input System)
        public void OnPointerClick(PointerEventData eventData)
        {
            if (AIPipeline.UI.SimpleNodeEditor.IsEditorOpen) return;
            HandleClick();
        }

        private void HandleClick()
        {
            float timeSinceLast = Time.time - lastClickTime;

            // Check double click
            if (timeSinceLast <= doubleClickThreshold)
            {
                // Prevent triple-click weirdness
                lastClickTime = -100f; 
                LoadTargetScene();
            }
            else
            {
                lastClickTime = Time.time;
            }
        }

        private void LoadTargetScene()
        {
            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning($"[SceneTransition] Target scene name is empty on {gameObject.name}!");
                return;
            }

            Debug.Log($"[SceneTransition] Double click detected on {gameObject.name}. Loading scene: {targetSceneName}");
            
            // Helpful check in Editor
#if UNITY_EDITOR
            if (!IsSceneInBuildSettings(targetSceneName))
            {
                Debug.LogError($"[SceneTransition] Error: Scene '{targetSceneName}' is NOT added to Build Settings.\n" +
                               "1. Go to File > Build Settings...\n" +
                               "2. Drag your scene asset into the 'Scenes In Build' list.");
            }
#endif
            
            // If in a networked game, load scene additively to keep network connection
            if (NetworkClient.isConnected)
            {
                StartCoroutine(LoadSceneAdditive(targetSceneName));
            }
            else
            {
                // Not networked, just load scene normally
                SceneManager.LoadScene(targetSceneName);
            }
        }

        private System.Collections.IEnumerator LoadSceneAdditive(string sceneName)
        {
            // Prevent concurrent scene loads (e.g. rapid double-clicks)
            if (_isLoadingScene)
            {
                Debug.LogWarning($"[SceneTransition] Scene load already in progress, ignoring request for: {sceneName}");
                yield break;
            }
            _isLoadingScene = true;

            // Check if scene is already loaded
            Scene targetScene = SceneManager.GetSceneByName(sceneName);
            if (!targetScene.isLoaded)
            {
                Debug.Log($"[SceneTransition] Loading scene additively: {sceneName}");
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                targetScene = SceneManager.GetSceneByName(sceneName);
            }
            
            // Set the new scene as active
            if (targetScene.isLoaded)
            {
                // Clean up conflicting objects from the NEW scene (duplicate cameras,
                // players, event systems, etc.)  The MainScene's camera must stay
                // active — it hosts the CinemachineBrain that follows the player.
                CleanUpAdditiveScene(targetScene);

                SceneManager.SetActiveScene(targetScene);
                Debug.Log($"[SceneTransition] Active scene set to: {sceneName}");
                
                // Move local player to the new scene
                if (NetworkClient.localPlayer != null)
                {
                    GameObject player = NetworkClient.localPlayer.gameObject;
                    SceneManager.MoveGameObjectToScene(player, targetScene);
                    Debug.Log($"[SceneTransition] Moved local player to scene: {sceneName}");

                    // Teleport player to spawn point in the new scene
                    TeleportToSpawnPoint(player, targetScene);

                    // Wait a frame so the scene move and teleport are fully processed
                    yield return null;

                    // Force re-activate PlayerInput — moving between scenes can
                    // silently deactivate the input action maps.
                    ReactivatePlayerInput(player);
                }
            }

            _isLoadingScene = false;
        }

        /// <summary>
        /// After a scene move, the PlayerInput component may silently lose its
        /// action activation.  Cycling enabled off→on forces a full re-init
        /// (OnDisable → OnEnable) which re-pairs devices and re-enables action maps.
        /// </summary>
        private void ReactivatePlayerInput(GameObject player)
        {
#if ENABLE_INPUT_SYSTEM
            var pi = player.GetComponent<PlayerInput>();
            if (pi != null)
            {
                pi.enabled = false;
                pi.enabled = true;
                Debug.Log($"[SceneTransition] Cycled PlayerInput on {player.name} — " +
                          $"enabled={pi.enabled}, actions={pi.actions?.name}, " +
                          $"currentMap={pi.currentActionMap?.name}, " +
                          $"scheme={pi.currentControlScheme}");
            }
            else
            {
                Debug.LogWarning("[SceneTransition] No PlayerInput found on local player after scene move!");
            }
#endif

            // Diagnostics: log the state of all movement components
            var inputs = player.GetComponent<StarterAssets.StarterAssetsInputs>();
            var tpc = player.GetComponent<StarterAssets.ThirdPersonController>();
            var cc = player.GetComponent<CharacterController>();
            Debug.Log($"[SceneTransition] Post-move component states: " +
                      $"StarterAssetsInputs={inputs?.enabled}, " +
                      $"ThirdPersonController={tpc?.enabled}, " +
                      $"CharacterController={cc?.enabled}");
        }



        /// <summary>
        /// Teleports the player to a spawn point in the given scene.
        /// Looks for a GameObject named "SpawnPoint" (root or child).
        /// Falls back to Vector3.zero if none is found.
        /// </summary>
        private void TeleportToSpawnPoint(GameObject player, Scene scene)
        {
            Vector3 targetPos = Vector3.zero;
            Quaternion targetRot = Quaternion.identity;
            bool found = false;

            // Search for a GameObject named "SpawnPoint" in the target scene
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "SpawnPoint")
                {
                    targetPos = root.transform.position;
                    targetRot = root.transform.rotation;
                    found = true;
                    break;
                }
                Transform sp = root.transform.Find("SpawnPoint");
                if (sp != null)
                {
                    targetPos = sp.position;
                    targetRot = sp.rotation;
                    found = true;
                    break;
                }
            }

            // Disable CharacterController before teleport (it blocks transform.position changes)
            var cc = player.GetComponent<CharacterController>();
            bool ccWasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            player.transform.position = targetPos;
            player.transform.rotation = targetRot;

            if (cc != null) cc.enabled = ccWasEnabled;

            Debug.Log($"[SceneTransition] Teleported player to {(found ? "SpawnPoint" : "default position")} at {targetPos} in scene '{scene.name}'");
        }

        /// <summary>
        /// Destroy design-time objects from an additively loaded scene that would
        /// conflict with the real networked player (duplicate cameras, players,
        /// audio listeners, event systems, etc.).
        /// </summary>
        private void CleanUpAdditiveScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();

            foreach (GameObject root in roots)
            {
                // Destroy any standalone PlayerInput objects (duplicate player prefabs).
                // Skip objects owned by Mirror (they have a NetworkIdentity).
#if ENABLE_INPUT_SYSTEM
                var playerInput = root.GetComponentInChildren<PlayerInput>(true);
                if (playerInput != null && root.GetComponent<NetworkIdentity>() == null)
                {
                    Debug.Log($"[SceneTransition] Destroying duplicate player object from additive scene: {root.name}");
                    Destroy(root);
                    continue; // already destroyed, skip further checks
                }
#endif

                // Destroy any standalone cameras tagged MainCamera (they bring
                // AudioListeners, Cinemachine Brains, etc. that clash).
                var cam = root.GetComponentInChildren<Camera>(true);
                if (cam != null && cam.CompareTag("MainCamera") && root.GetComponent<NetworkIdentity>() == null)
                {
                    Debug.Log($"[SceneTransition] Destroying duplicate MainCamera from additive scene: {root.name}");
                    Destroy(root);
                    continue;
                }

                // Destroy extra EventSystems (only one should exist).
                var eventSystem = root.GetComponentInChildren<EventSystem>(true);
                if (eventSystem != null)
                {
                    Debug.Log($"[SceneTransition] Destroying duplicate EventSystem from additive scene: {root.name}");
                    Destroy(root);
                    continue;
                }

                // Destroy duplicate Cinemachine virtual cameras from the additive scene.
                // The MainScene's virtual camera is the authoritative one.
                var virtualCam = root.GetComponentInChildren<Cinemachine.CinemachineVirtualCamera>(true);
                if (virtualCam != null && root.GetComponent<NetworkIdentity>() == null)
                {
                    Debug.Log($"[SceneTransition] Destroying duplicate Cinemachine camera from additive scene: {root.name}");
                    Destroy(root);
                    continue;
                }

                // Destroy duplicate Directional Lights from the additive scene.
                // MainScene's light is the authoritative one — having two doubles the brightness.
                var light = root.GetComponentInChildren<Light>(true);
                if (light != null && light.type == LightType.Directional && root.GetComponent<NetworkIdentity>() == null)
                {
                    Debug.Log($"[SceneTransition] Destroying duplicate Directional Light from additive scene: {root.name}");
                    Destroy(root);
                    continue;
                }

                // Destroy stray AudioListeners that aren't on a camera we kept.
                var listener = root.GetComponentInChildren<AudioListener>(true);
                if (listener != null && root.GetComponent<NetworkIdentity>() == null)
                {
                    Debug.Log($"[SceneTransition] Removing duplicate AudioListener from: {root.name}");
                    Destroy(listener);
                }
            }
        }

#if UNITY_EDITOR
        private bool IsSceneInBuildSettings(string sceneName)
        {
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < sceneCount; i++)
            {
                string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == sceneName) return true;
            }
            return false;
        }
#endif
    }
}
