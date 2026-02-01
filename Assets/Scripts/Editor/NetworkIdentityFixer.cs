using UnityEngine;
using UnityEditor;
using Mirror;

namespace Scripts.Editor
{
    [InitializeOnLoad]
    public class NetworkIdentityFixer
    {
        static NetworkIdentityFixer()
        {
            EditorApplication.update += RunOnce;
        }

        static void RunOnce()
        {
            EditorApplication.update -= RunOnce;
            FixNetworkManager();
        }

        [MenuItem("Tools/Fix Network Manager")]
        public static void FixNetworkManager()
        {
            // 1. Try to find by Type (FindObjectsByType finds in all loaded scenes)
            var nms = Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            foreach (var nm in nms)
            {
                RemoveNetworkIdentity(nm.gameObject);
            }

            // 2. Fallback: Search all root objects in all loaded scenes by name
            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    if (root.name.Trim() == "NetworkManager" || root.GetComponent<NetworkManager>() != null)
                    {
                        RemoveNetworkIdentity(root);
                    }
                    // Optional: Search children if needed (unlikely for NetworkManager)
                    // var childNM = root.GetComponentInChildren<NetworkManager>(true);
                    // if(childNM) RemoveNetworkIdentity(childNM.gameObject);
                }
            }
        }

        private static void RemoveNetworkIdentity(GameObject go)
        {
            if (go == null) return;
            
            // Remove NetworkTransformReliable if present (it requires NetworkIdentity)
            // Using string name to avoid strict dependency if convenient, or type if available
            var netTransform = go.GetComponent("NetworkTransformReliable"); 
            if (netTransform != null)
            {
                 Object.DestroyImmediate(netTransform, true);
                 Debug.Log($"<color=green>Fixed NetworkManager:</color> Removed invalid NetworkTransformReliable component from '{go.name}'.");
                 UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            }
             // Also check base class or unreliable just in case, though error was specific
            var netTransformBase = go.GetComponent("NetworkTransformBase");
             if (netTransformBase != null)
            {
                 Object.DestroyImmediate(netTransformBase, true);
                 Debug.Log($"<color=green>Fixed NetworkManager:</color> Removed invalid NetworkTransformBase component from '{go.name}'.");
                 UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            }

            var ni = go.GetComponent<NetworkIdentity>();
            if (ni != null)
            {
                Object.DestroyImmediate(ni, true);
                Debug.Log($"<color=green>Fixed NetworkManager:</color> Removed invalid NetworkIdentity component from '{go.name}' in scene '{go.scene.name}'.");
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }
    }
}
