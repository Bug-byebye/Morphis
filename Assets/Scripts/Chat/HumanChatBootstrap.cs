using UnityEngine;

namespace Morphis.Chat
{
    /// <summary>
    /// Creates the human chat widget automatically at runtime.
    /// </summary>
    public static class HumanChatBootstrap
    {
        private static bool created;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCreated()
        {
            if (created) return;

            if (Object.FindFirstObjectByType<HumanChatUI>() != null)
            {
                created = true;
                return;
            }

            GameObject root = new GameObject("HumanChatRoot");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<HumanChatUI>();
            created = true;
        }
    }
}
