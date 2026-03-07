using UnityEngine;

namespace Morphis.Motion
{
    /// <summary>
    /// Auto-creates runtime MDM generation UI.
    /// </summary>
    public static class MDMMotionGenerateUIBootstrap
    {
        private static bool created;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureUI()
        {
            if (created) return;

            if (Object.FindFirstObjectByType<MDMMotionGenerateUI>() != null)
            {
                created = true;
                return;
            }

            var go = new GameObject("MDMMotionGenerateUIRoot");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<MDMMotionGenerateUI>();
            created = true;
        }
    }
}
