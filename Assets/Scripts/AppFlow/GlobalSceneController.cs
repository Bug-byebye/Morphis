using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Mirror;

namespace Morphis.AppFlow
{
    /// <summary>
    /// Global controller to handle universal inputs like returning to MainScene.
    /// Should be attached to a persistent object (like BootFlowManager or its own DontDestroyOnLoad object).
    /// </summary>
    public class GlobalSceneController : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Name of the main scene to return to.")]
        public string mainSceneName = "MainScene";

        private void Update()
        {
            // Check for ESC key
            // Supporting both New Input System and Legacy for robustness
            bool escPressed = false;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                escPressed = true;
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                escPressed = true;
            }

            if (escPressed)
            {
                ReturnToMainScene();
            }
        }

        public void ReturnToMainScene()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            string target = "MainScene";

            // Don't exit from BootScene (Login)
            if (currentScene.name == "BootScene")
            {
                return;
            }

            // If already in MainScene, do nothing
            if (currentScene.name == target)
            {
                Debug.Log("[GlobalSceneController] Already in MainScene.");
                return;
            }

            Debug.Log($"[GlobalSceneController] Returning to {target}...");
            
            // If in a networked game, unload current scene additively and keep main scene
            if (NetworkClient.isConnected)
            {
                // Just unload current additive scene, player stays in main scene
                StartCoroutine(UnloadCurrentSceneAdditive(currentScene.name));
            }
            else
            {
                // Not networked, just load scene normally
                SceneManager.LoadScene(target);
            }
        }

        private System.Collections.IEnumerator UnloadCurrentSceneAdditive(string sceneName)
        {
            if (sceneName != "MainScene")
            {
                // First move player back to MainScene
                Scene mainScene = SceneManager.GetSceneByName("MainScene");
                if (mainScene.isLoaded && NetworkClient.localPlayer != null)
                {
                    SceneManager.MoveGameObjectToScene(NetworkClient.localPlayer.gameObject, mainScene);
                    Debug.Log("[GlobalSceneController] Moved player back to MainScene");
                }
                
                // Set MainScene as active
                if (mainScene.isLoaded)
                {
                    SceneManager.SetActiveScene(mainScene);
                }
                
                // Then unload the additive scene
                Debug.Log($"[GlobalSceneController] Unloading additive scene: {sceneName}");
                yield return SceneManager.UnloadSceneAsync(sceneName);
            }
        }
    }
}
