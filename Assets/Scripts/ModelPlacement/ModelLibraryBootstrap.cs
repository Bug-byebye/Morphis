using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using LittleDog; // Fix CS0246: PlayerController namespace
#if MORPHIS_APPFLOW
using Morphis.AppFlow;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
#endif

namespace Morphis.ModelPlacement
{
    /// <summary>
    /// 仅在 Playground 场景中启用：自动创建模型库 UI（左侧按钮 + 弹窗）。
    /// 不需要你手动改场景。
    /// </summary>
    public static class ModelLibraryBootstrap
    {
        private const string TargetSceneName = "MainScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // 注册一次，全局监听场景加载
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Allow both "MainScene" and "demo"
            if (!string.Equals(scene.name, TargetSceneName, System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(scene.name, "demo", System.StringComparison.OrdinalIgnoreCase))
                return;

#if MORPHIS_APPFLOW
            // 仅当已经完成登录并选择了空间后，才显示模型库按钮
            if (!AppSession.IsLoggedIn || string.IsNullOrEmpty(AppSession.WorkspaceId))
                return;
#endif

            if (Object.FindFirstObjectByType<ModelLibraryUI>() != null)
                return;

            EnsureEventSystem();
            EnsureSceneColliders();

            var go = new GameObject("ModelLibraryUI(Auto)");
            go.AddComponent<ModelLibraryUI>();
        }

        private static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            // Ensure we have an Input Module
            if (es.GetComponent<BaseInputModule>() == null)
            {
                // Since PlayerController uses legacy Input.GetAxis, we should prefer StandaloneInputModule.
                // It works in "Old" and "Both" modes.
                // Only if StandaloneInputModule is missing (extremely rare) or fails should we consider alternatives.
                var standalone = es.gameObject.AddComponent<StandaloneInputModule>();
                
                // If the new Input System is exclusively enabled, StandaloneInputModule typically works but warns,
                // or we might need InputSystemUIInputModule. However, given the project context, Standalone is safer.
#if ENABLE_INPUT_SYSTEM
                // If we absolutely wanted to support New Input System ONLY mode, we'd check for that.
                // But for now, let's stick to Standalone as the default for this project.
#endif
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static InputActionAsset CreateMinimalUIActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "PlaygroundUI_Actions";

            var map = new InputActionMap("UI");
            map.AddAction("Point", InputActionType.PassThrough, "<Pointer>/position");
            map.AddAction("LeftClick", InputActionType.PassThrough, "<Pointer>/press");
            map.AddAction("RightClick", InputActionType.PassThrough, "<Mouse>/rightButton");
            map.AddAction("MiddleClick", InputActionType.PassThrough, "<Mouse>/middleButton");
            map.AddAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll");
            map.AddAction("Navigate", InputActionType.PassThrough);
            map.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
            map.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            asset.AddActionMap(map);
            asset.Enable();
            return asset;
        }
#endif

        private static void EnsureSceneColliders()
        {
            // Only auto-add colliders in the "demo" scene where environment is known to lack them.
            // Be careful not to mess up MainScene or others that might be set up correctly.
            // Only auto-add colliders in "demo" or "MainScene" if they lack them.
            // Only auto-add colliders in the "demo" scene where environment is known to lack them.
            var scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.name, "demo", System.StringComparison.OrdinalIgnoreCase))
                return;
            
            // Reverted main scene logic as per user request (scene already has colliders).

            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var r in renderers)
            {
                // Skip if it already has any collider
                if (r.GetComponent<Collider>() != null) continue;

                // Skip if it's part of the Player (has CharacterController in parent)
                if (r.GetComponentInParent<CharacterController>() != null) continue;
                if (r.GetComponentInParent<PlayerController>() != null) continue;

                // Skip if name contains "Player" (safety heuristic)
                if (r.name.Contains("Player") || r.transform.root.name.Contains("Player")) continue;
                
                // Skip if it is UI or small decoration
                // or if it's part of the player itself (though checking tag might be better)
                
                // Add MeshCollider
                var mc = r.gameObject.AddComponent<MeshCollider>();
                // In some cases, we might want convex if we need rigidbodies, 
                // but for static environment, non-convex is fine and more accurate.
                count++;
            }
            Debug.Log($"[ModelLibraryBootstrap] Auto-generated MeshColliders for {count} objects in 'demo' scene.");
        }
    }
}

