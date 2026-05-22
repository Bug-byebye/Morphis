using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

        private bool _exitDialogVisible;
        private GameObject _exitDialogCanvas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Application.isBatchMode) return;
            if (FindFirstObjectByType<GlobalSceneController>() != null) return;

            var go = new GameObject("GlobalSceneController(Auto)");
            DontDestroyOnLoad(go);
            go.AddComponent<GlobalSceneController>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            // 不在此处 EnsureEventSystem：BeforeSceneLoad 阶段场景尚未加载，
            // FindFirstObjectByType 会为 false，会创建一个 DontDestroyOnLoad 的 EventSystem，
            // 随后 MainScene 等场景自带的 UI_EventSystem 载入后就会出现「两个 EventSystem」警告。
            // 仅在真正需要自建退出对话框 UI 时再按需创建（见 CreateExitDialogCanvas）。
        }

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
                var current = SceneManager.GetActiveScene();
                if (current.name == mainSceneName)
                {
                    ToggleExitDialog();
                }
                else
                {
                    ReturnToMainScene();
                }
            }
        }

        private void ToggleExitDialog()
        {
            if (_exitDialogVisible)
            {
                if (_exitDialogCanvas != null)
                    _exitDialogCanvas.SetActive(false);
                _exitDialogVisible = false;
                return;
            }

            if (_exitDialogCanvas == null)
            {
                _exitDialogCanvas = CreateExitDialogCanvas();
            }

            _exitDialogCanvas.SetActive(true);
            _exitDialogVisible = true;
        }

        public void ToggleExitDialogFromUi()
        {
            if (SceneManager.GetActiveScene().name == mainSceneName)
            {
                ToggleExitDialog();
            }
        }

        private GameObject CreateExitDialogCanvas()
        {
            EnsureEventSystem();

            var canvasGO = new GameObject("ExitDialogCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGO);

            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(420f, 200f);
            panelRT.anchoredPosition = Vector2.zero;

            var panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panelGO.transform, false);
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 1f);
            titleRT.anchorMax = new Vector2(0.5f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0f, -20f);
            titleRT.sizeDelta = new Vector2(380f, 40f);
            var titleText = titleGO.AddComponent<TMPro.TextMeshProUGUI>();
            titleText.text = "退出游戏？";
            titleText.fontSize = 28;
            titleText.alignment = TMPro.TextAlignmentOptions.Center;

            // Buttons container
            var buttonsGO = new GameObject("Buttons");
            buttonsGO.transform.SetParent(panelGO.transform, false);
            var buttonsRT = buttonsGO.AddComponent<RectTransform>();
            buttonsRT.anchorMin = new Vector2(0.5f, 0f);
            buttonsRT.anchorMax = new Vector2(0.5f, 0f);
            buttonsRT.pivot = new Vector2(0.5f, 0f);
            buttonsRT.anchoredPosition = new Vector2(0f, 20f);
            buttonsRT.sizeDelta = new Vector2(380f, 60f);

            // 创建三个按钮：确认退出、保存游戏、继续游玩
            var quitBtn = CreateButton(buttonsGO.transform, "确认退出", new Vector2(-130f, 0f));
            quitBtn.onClick.AddListener(OnConfirmQuitClicked);

            var saveBtn = CreateButton(buttonsGO.transform, "保存游戏", new Vector2(0f, 0f));
            saveBtn.onClick.AddListener(OnSaveClicked);

            var resumeBtn = CreateButton(buttonsGO.transform, "继续游玩", new Vector2(130f, 0f));
            resumeBtn.onClick.AddListener(() => ToggleExitDialog());

            return canvasGO;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemGO.AddComponent<StandaloneInputModule>();
#endif
            DontDestroyOnLoad(eventSystemGO);
        }

        private Button CreateButton(Transform parent, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(110f, 40f);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.45f, 0.85f, 1f);

            var btn = go.AddComponent<Button>();

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = Color.white;

            return btn;
        }

        private void OnSaveClicked()
        {
            // 通过类型名查找 WorldSnapshotManager，避免直接依赖跨程序集引用
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Name != "WorldSnapshotManager") continue;

                // 反射调用 SaveWorldServer(string worldId = null, Action onSuccess = null, Action<string> onError = null)
                var method = type.GetMethod(
                    "SaveWorldServer",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                );
                if (method != null)
                {
                    try
                    {
                        var parameters = method.GetParameters();
                        object[] args = parameters.Length switch
                        {
                            0 => System.Array.Empty<object>(),
                            1 => new object[] { null },
                            2 => new object[] { null, null },
                            _ => new object[] { null, null, null }
                        };
                        method.Invoke(mb, args);
                        Debug.Log("[GlobalSceneController] Requested world save via exit dialog.");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[GlobalSceneController] Failed to invoke SaveWorldServer via reflection: {e.GetType().Name}: {e.Message}");
                    }
                }
                break;
            }
        }

        private void OnConfirmQuitClicked()
        {
            OnSaveClicked();
            // 不要立刻 Quit：让 OnApplicationQuit 里的同步 SaveToServerBlocking 跑完。
            // OnSaveClicked 触发的是异步协程，可能尚未发出 HTTP；直接 Quit 会丢这次保存。
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
