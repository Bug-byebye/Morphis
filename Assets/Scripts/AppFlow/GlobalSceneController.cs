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
                    GameObject player = NetworkClient.localPlayer.gameObject;
                    SceneManager.MoveGameObjectToScene(player, mainScene);

                    // Teleport player to spawn point in MainScene
                    TeleportToSpawnPoint(player, mainScene);

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

        /// <summary>
        /// Teleports the player to a SpawnPoint in the given scene.
        /// Falls back to Vector3.zero if no spawn point is found.
        /// </summary>
        private void TeleportToSpawnPoint(GameObject player, Scene scene)
        {
            Vector3 targetPos = Vector3.zero;
            Quaternion targetRot = Quaternion.identity;
            bool found = false;

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

            var cc = player.GetComponent<CharacterController>();
            bool ccWasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            player.transform.position = targetPos;
            player.transform.rotation = targetRot;

            if (cc != null) cc.enabled = ccWasEnabled;

            Debug.Log($"[GlobalSceneController] Teleported player to {(found ? "SpawnPoint" : "default position")} at {targetPos}");
        }
    }
}
