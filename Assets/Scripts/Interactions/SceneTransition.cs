using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Mirror;

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
            // Debug.Log("OnMouseDown");
            HandleClick();
        }

        // EventSystem Event (works if Camera has PhysicsRaycaster and using Input System)
        public void OnPointerClick(PointerEventData eventData)
        {
            // Debug.Log("OnPointerClick");
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
                SceneManager.SetActiveScene(targetScene);
                Debug.Log($"[SceneTransition] Active scene set to: {sceneName}");
                
                // Move local player to the new scene
                if (NetworkClient.localPlayer != null)
                {
                    SceneManager.MoveGameObjectToScene(NetworkClient.localPlayer.gameObject, targetScene);
                    Debug.Log($"[SceneTransition] Moved local player to scene: {sceneName}");
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
