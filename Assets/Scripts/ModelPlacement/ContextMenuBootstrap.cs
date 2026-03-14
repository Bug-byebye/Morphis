using UnityEngine;

namespace Morphis.ModelPlacement
{
    /// <summary>
    /// Ensures ObjectContextMenu exists in the scene
    /// </summary>
    public static class ContextMenuBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            // Create context menu if it doesn't exist
            if (ObjectContextMenu.Instance == null)
            {
                var menuObj = new GameObject("ObjectContextMenu(Auto)");
                menuObj.AddComponent<ObjectContextMenu>();
                Object.DontDestroyOnLoad(menuObj);
                Debug.Log("[ContextMenuBootstrap] Created ObjectContextMenu");
            }
        }
    }
}
