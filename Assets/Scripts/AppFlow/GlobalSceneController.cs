using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

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
            
            // FORCE "MainScene" to be safe
            string target = "MainScene";

            // Don't reload if we are already there
            if (currentScene.name == target) 
            {
                Debug.Log("[GlobalSceneController] Already in MainScene.");
                return;
            }

            // Also don't exit from BootScene (Login)
            if (currentScene.name == "BootScene")
            {
                return;
            }

            Debug.Log($"[GlobalSceneController] Returning to {target}...");
            SceneManager.LoadScene(target);
        }
    }
}
