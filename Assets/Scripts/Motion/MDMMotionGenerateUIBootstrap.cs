using UnityEngine;

namespace Morphis.Motion
{
    /// <summary>
    /// Auto-creates runtime MDM generation UI.
    /// Disabled by default because this panel is for local testing.
    /// </summary>
    public static class MDMMotionGenerateUIBootstrap
    {
        private static bool created;
        private const bool EnableRuntimeUI = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureUI()
        {
            if (!EnableRuntimeUI) return;
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
