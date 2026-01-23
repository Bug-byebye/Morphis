using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
        private const string TargetSceneName = "Playground";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // 注册一次，全局监听场景加载
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, TargetSceneName, System.StringComparison.OrdinalIgnoreCase))
                return;

#if MORPHIS_APPFLOW
            // 仅当已经完成登录并选择了空间后，才显示模型库按钮
            if (!AppSession.IsLoggedIn || string.IsNullOrEmpty(AppSession.WorkspaceId))
                return;
#endif

            if (Object.FindFirstObjectByType<ModelLibraryUI>() != null)
                return;

            EnsureEventSystem();

            var go = new GameObject("ModelLibraryUI(Auto)");
            go.AddComponent<ModelLibraryUI>();
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
            var ui = es.AddComponent<InputSystemUIInputModule>();
            ui.actionsAsset = CreateMinimalUIActions();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
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
    }
}

