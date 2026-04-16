using UnityEngine;
using UnityEngine.SceneManagement;
using Morphis.AppFlow;

namespace Morphis.Chat
{
    /// <summary>
    /// Creates the human chat widget automatically at runtime.
    /// </summary>
    public static class HumanChatBootstrap
    {
        private static bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (initialized) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            initialized = true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!CanCreateForScene(scene))
            {
                return;
            }

            if (Object.FindFirstObjectByType<HumanChatUI>() != null)
            {
                return;
            }

            var root = new GameObject("HumanChatRoot");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<HumanChatUI>();
        }

        private static bool CanCreateForScene(Scene scene)
        {
            if (Application.isBatchMode)
            {
                return false;
            }

            if (string.Equals(scene.name, "BootScene", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return AppSession.IsLoggedIn && !string.IsNullOrEmpty(AppSession.WorkspaceId);
        }
    }
}
