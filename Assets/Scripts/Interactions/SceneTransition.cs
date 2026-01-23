using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

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
                // Try to load anyway, it might fail if not in build settings but worth a shot in Editor sometimes works if open
            }
#endif
            
            SceneManager.LoadScene(targetSceneName);
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
