using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq; // for finding starter assets inputs
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace Morphis.AppFlow
{
    /// <summary>
    /// 启动流程：登录 -> 空间选择 -> 加载主场景（Playground）
    /// - UI 全部运行时创建（不依赖 prefab/场景搭建）
    /// - 后端接口：POST /auth/login, POST /auth/register, GET /workspaces
    /// </summary>
    public class BootFlowManager : MonoBehaviour
    {
        [Header("Backend")]
        [SerializeField] private string baseUrl = "http://localhost:8000";

        [Header("Scene")]
        [SerializeField] private string mainSceneName = "MainScene";
        [SerializeField] private Material backgroundSkybox;

        private Camera _skyboxCamera;

        private Canvas _canvas;
        private TMP_Text _title;
        private TMP_Text _status;

        // 登录 UI
        private GameObject _loginPanel;
        private TMP_InputField _usernameInput;
        private TMP_InputField _passwordInput;
        private Button _loginBtn;
        private Button _registerBtn;

        // 空间选择 UI
        private GameObject _workspacePanel;
        private Transform _workspaceListRoot;
        private Button _enterBtn;
        private string _selectedWorkspaceId;
        private string _selectedWorkspaceName;

        private bool _busy;

        private string LoginUrl => $"{AppSession.BaseUrl}/auth/login";
        private string RegisterUrl => $"{AppSession.BaseUrl}/auth/register";
        private string WorkspacesUrl => $"{AppSession.BaseUrl}/workspaces";

        private static BootFlowManager _instance;
        private bool _initialized;
        private bool _createdEventSystem;
        private GameObject _createdEventSystemGO;

        private void Awake()
        {
            // 如果已经完成登录 + 选空间（例如已经进入 MainScene），则不再重复执行引导流程
            if (AppSession.IsLoggedIn && !string.IsNullOrEmpty(AppSession.WorkspaceId))
            {
                Debug.Log("[BootFlow] Already logged in + workspace selected. Skipping Boot UI.");
                Destroy(gameObject);
                return;
            }

            if (_instance != null && _instance != this)
            {
                // 冲突解决策略：如果已存在的实例没有 Skybox，而我有，说明我是用户新配置的“更好的”实例。
                // 此时应该销毁旧的，保留我。
                if (_instance.backgroundSkybox == null && this.backgroundSkybox != null)
                {
                    Debug.Log($"[BootFlow] Replacing existing unconfigured instance ({_instance.name}) with configured instance ({name}).");
                    Destroy(_instance.gameObject);
                    _instance = this;
                }
                else
                {
                    Debug.LogWarning($"[BootFlow] Duplicate BootFlowManager detected on {gameObject.name}. Destroying myself (Instance already exists and is valid).");
                    Destroy(gameObject);
                    return;
                }
            }
            else
            {
                _instance = this;
            }

            AppSession.BaseUrl = baseUrl;
            DontDestroyOnLoad(gameObject);
            
            // Auto-add Global Scene Controller for ESC key handling
            if (GetComponent<GlobalSceneController>() == null)
            {
                gameObject.AddComponent<GlobalSceneController>();
            }
        }
        
        private void Start()
        {
            // 在 Start() 中初始化，确保场景已经完全加载
            Debug.Log($"[BootFlow] Start() called, scene: {SceneManager.GetActiveScene().name}, isLoaded: {SceneManager.GetActiveScene().isLoaded}");
            if (_initialized) return;

            // 二次兜底：如果在 Awake 之后状态变为已登录+已选空间，则不再初始化 UI
            if (AppSession.IsLoggedIn && !string.IsNullOrEmpty(AppSession.WorkspaceId))
            {
                Debug.Log("[BootFlow] Start(): already logged in + workspace selected. Skipping Boot UI.");
                Destroy(gameObject);
                return;
            }

            _initialized = true;
            StartCoroutine(InitializeBootUI());
        }

        private System.Collections.IEnumerator InitializeBootUI()
        {
            Debug.Log("[BootFlow] InitializeBootUI coroutine started");
            
            // 等待一帧，确保所有系统都已初始化
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null; // 再等待一帧，确保渲染系统准备好
            
            Debug.Log("[BootFlow] Starting UI initialization...");
            
            // 先确保 EventSystem 存在
            EnsureEventSystem();
            yield return null;
            
            BuildUI();
            yield return null; // 等待 UI 构建完成
            
            ShowLogin();
            
            // 初始状态：禁用玩家输入，把控制权给 Login UI，避免光标被抢占
            SetPlayerInputEnabled(false);
            
            // 再等待一帧，确保 UI 完全构建完成
            yield return null;
            yield return new WaitForEndOfFrame();
            
            // 强制刷新 Canvas
            if (_canvas != null)
            {
                Canvas.ForceUpdateCanvases();
                _canvas.enabled = false;
                yield return null;
                _canvas.enabled = true;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Debug.Log($"[BootFlow] Canvas refreshed: enabled={_canvas.enabled}, renderMode={_canvas.renderMode}");
            }
            else
            {
                Debug.LogError("[BootFlow] Canvas is null after BuildUI!");
            }
            
            // 确保 EventSystem 激活并正确配置
            var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (es != null)
            {
                es.gameObject.SetActive(true);
                yield return null;
                
                // 强制更新 EventSystem
                es.UpdateModules();
                Debug.Log($"[BootFlow] EventSystem ready: {es.name}, active={es.gameObject.activeInHierarchy}");
                
#if ENABLE_INPUT_SYSTEM
                var inputModule = es.GetComponent<InputSystemUIInputModule>();
                if (inputModule != null)
                {
                    Debug.Log($"[BootFlow] InputSystemUIInputModule: enabled={inputModule.enabled}, actionsAsset={inputModule.actionsAsset?.name}");
                    
                    // 确保 InputModule 启用
                    inputModule.enabled = true;
                    
                    // 确保 actionsAsset 存在并启用
                    if (inputModule.actionsAsset == null)
                    {
                        Debug.LogWarning("[BootFlow] InputSystemUIInputModule has no actionsAsset! Creating new one...");
                        inputModule.actionsAsset = CreateMinimalUIActions();
                    }
                    
                    if (inputModule.actionsAsset != null)
                    {
                        if (!inputModule.actionsAsset.enabled)
                        {
                            inputModule.actionsAsset.Enable();
                        }
                        Debug.Log($"[BootFlow] InputSystem actionsAsset enabled: {inputModule.actionsAsset.enabled}, name: {inputModule.actionsAsset.name}");
                    }
                    
                    // 强制更新模块
                    inputModule.UpdateModule();
                }
                else
                {
                    Debug.LogError("[BootFlow] InputSystemUIInputModule not found! Recreating EventSystem...");
                    EnsureEventSystem();
                    yield return null;
                }
#endif
            }
            else
            {
                Debug.LogError("[BootFlow] EventSystem not found after BuildUI! Recreating...");
                EnsureEventSystem();
                yield return null;
            }
            
            // 最后再次确保输入框可以接收输入
            yield return null;
            if (_usernameInput != null)
            {
                _usernameInput.enabled = false;
                yield return null;
                _usernameInput.enabled = true;
                Debug.Log($"[BootFlow] Username input field ready: enabled={_usernameInput.enabled}, interactable={_usernameInput.interactable}");
            }
            if (_passwordInput != null)
            {
                _passwordInput.enabled = false;
                yield return null;
                _passwordInput.enabled = true;
                Debug.Log($"[BootFlow] Password input field ready: enabled={_passwordInput.enabled}, interactable={_passwordInput.interactable}");
            }
            
            // 确保光标可见且解锁
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("[BootFlow] Cursor unlocked and visible");
            
            // 先用英文，避免 TMP 默认字体缺中文导致的警告刷屏；后续我们再接入中文字体资源。
            SetStatus("Enter username/password (default: 111111 / 111111)");
            
            Debug.Log("[BootFlow] UI initialization complete! Canvas and EventSystem should be ready.");
        }

        private void BuildUI()
        {
            // 先确保 EventSystem 存在并正确配置
            EnsureEventSystem();

            // Canvas
            Debug.Log("[BootFlow] Creating Canvas...");
            var canvasGO = new GameObject("BootCanvas");
            canvasGO.SetActive(true); // 先激活 GameObject
            
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            var raycaster = canvasGO.AddComponent<GraphicRaycaster>();
            
            // 确保所有组件都激活
            _canvas.enabled = true;
            raycaster.enabled = true;
            
            // 强制设置 Canvas 为激活状态
            canvasGO.SetActive(true);
            
            Debug.Log($"[BootFlow] Canvas created: renderMode={_canvas.renderMode}, enabled={_canvas.enabled}, active={canvasGO.activeSelf}");

            DontDestroyOnLoad(canvasGO);

            // Background
            Debug.Log("[BootFlow] Creating background...");
            var bg = new GameObject("Background");
            bg.transform.SetParent(_canvas.transform, false);
            bg.transform.SetAsFirstSibling(); // 确保背景在最底层
            var bgRect = bg.AddComponent<RectTransform>();
            Stretch(bgRect);
            var bgImg = bg.AddComponent<Image>();
            
            // 确保 Image 组件正确初始化
            bgImg.raycastTarget = false; // 背景不需要接收射线检测
            bgImg.maskable = false;
            
            if (backgroundSkybox != null)
            {
                Debug.Log("[BootFlow] Skybox material FOUND. Setting up transparent background.");
                // 透明背景，透出后方的 Skybox Camera
                bgImg.color = Color.clear;
                SetupSkyboxCamera();
            }
            else
            {
                Debug.LogWarning("[BootFlow] backgroundSkybox is NULL! Using default dark background.");
                // 设为完全不透明，确保启动时看不到后面的场景
                bgImg.color = new Color(0.06f, 0.06f, 0.08f, 1.0f);
            }
            
            // 确保背景激活并强制刷新
            bg.SetActive(true);
            bgRect.ForceUpdateRectTransforms();
            Debug.Log($"[BootFlow] Background created: color={bgImg.color}, active={bg.activeSelf}");

            // Root container
            var root = new GameObject("Root");
            root.transform.SetParent(bg.transform, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            // TARGET: ~1/3 Screen Area. 1920x1080 -> ~640x360 is 1/9 area. 
            // 1/3 area is huge. Let's go for 1/3 Width (640) and 2/3 Height (720)?
            // User asked for "1/3 OF THE SCREEN AREA", that implies Sqrt(1/3) linear scale ~= 0.58 width/height.
            // 1920 * 0.58 = 1100. 1080 * 0.58 = 620.
            // Let's use 1000 x 800 for a solid block.
            rootRect.sizeDelta = new Vector2(1000, 800); 
            rootRect.anchoredPosition = Vector2.zero;

            // Title
            // Title
            _title = CreateText(root.transform, "Morphis", 100, FontStyles.Bold); // Even Bigger Title
            var titleRect = _title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 0); // Pivot Bottom
            // Move UPWARDS outside the root box. 
            // Root is 1000x800. Anchor (0.5, 1) is Top Center.
            // Pos Y=0 is top edge. We want it ABOVE.
            titleRect.anchoredPosition = new Vector2(0, 20); 
            titleRect.sizeDelta = new Vector2(0, 120);
            _title.alignment = TextAlignmentOptions.Center;
            // Add a subtle shadow or outline to make it pop against skybox
            _title.outlineWidth = 0.2f;
            _title.outlineColor = new Color(0,0,0,0.5f);



            // Panels
            _loginPanel = BuildLoginPanel(root.transform);
            _workspacePanel = BuildWorkspacePanel(root.transform);

            // 进入引导时，必须确保鼠标可用（能点 UI）
            SetCursorForUI(true);
        }

        private void SetupSkyboxCamera()
        {
            if (_skyboxCamera != null) return;

            var camGO = new GameObject("BootSkyboxCamera");
            // 挂在 BootFlowManager 下面，随之一起 DontDestroyOnLoad
            camGO.transform.SetParent(this.transform);
            
            _skyboxCamera = camGO.AddComponent<Camera>();
            _skyboxCamera.clearFlags = CameraClearFlags.Skybox;
            _skyboxCamera.depth = -2; // 确保在 UI Canvas 和 BootScene 摄像机下方
            _skyboxCamera.cullingMask = 0; // 不渲染任何 Layer，只显示 Skybox
            
            var skybox = camGO.AddComponent<Skybox>();
            if (backgroundSkybox != null)
            {
                skybox.material = backgroundSkybox;
            }
            
            // 确保摄像机激活
            camGO.SetActive(true);
            _skyboxCamera.enabled = true;
            
            camGO.AddComponent<SkyboxRotator>(); 
        }

        public class SkyboxRotator : MonoBehaviour
        {
            void Update()
            {
                transform.Rotate(Vector3.up * 1.5f * Time.deltaTime);
            }
        }

        private GameObject BuildLoginPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "LoginPanel");
            panel.SetActive(false);

            // Layout Constants for BIG UI
            float contentWidth = 800;
            float inputHeight = 80;
            float labelHeight = 50;
            float fontSizeLabel = 32;
            float buttonHeight = 90;
            
            float startY = 160; 
            float gap = 40;

            // ============ Username ============
            // Label
            var userLabel = CreateText(panel.transform, "Username", fontSizeLabel, FontStyles.Bold);
            var userLabelRect = userLabel.rectTransform;
            userLabelRect.sizeDelta = new Vector2(contentWidth, labelHeight);
            userLabelRect.anchorMin = new Vector2(0.5f, 1);
            userLabelRect.anchorMax = new Vector2(0.5f, 1);
            userLabelRect.pivot = new Vector2(0, 1);
            // Left align relative to center: -HalfWidth
            userLabelRect.anchoredPosition = new Vector2(-contentWidth/2, -startY); 
            userLabel.alignment = TextAlignmentOptions.BottomLeft;

            // Input
            _usernameInput = CreateInput(panel.transform, "");
            var userInRt = _usernameInput.GetComponent<RectTransform>();
            userInRt.sizeDelta = new Vector2(contentWidth, inputHeight);
            PositionRow(userInRt, y: startY + labelHeight + 10); // 10px padding

            // ============ Password ============
            float passY = startY + labelHeight + inputHeight + gap;
            
            // Label
            var pwdLabel = CreateText(panel.transform, "Password", fontSizeLabel, FontStyles.Bold);
            var pwdLabelRect = pwdLabel.rectTransform;
            pwdLabelRect.sizeDelta = new Vector2(contentWidth, labelHeight);
            pwdLabelRect.anchorMin = new Vector2(0.5f, 1);
            pwdLabelRect.anchorMax = new Vector2(0.5f, 1);
            pwdLabelRect.pivot = new Vector2(0, 1);
            pwdLabelRect.anchoredPosition = new Vector2(-contentWidth/2, -passY); // Gap from username input
            pwdLabel.alignment = TextAlignmentOptions.BottomLeft;

            // Input
            _passwordInput = CreateInput(panel.transform, "", isPassword: true);
            var pwdInRt = _passwordInput.GetComponent<RectTransform>();
            pwdInRt.sizeDelta = new Vector2(contentWidth, inputHeight);
            PositionRow(pwdInRt, y: passY + labelHeight + 10);

            // ============ Buttons ============
            float btnY = passY + labelHeight + inputHeight + gap * 2;
            
            _loginBtn = CreateButton(panel.transform, "Login", new Color(0.30f, 0.70f, 0.45f));
            var loginRt = _loginBtn.GetComponent<RectTransform>();
            loginRt.sizeDelta = new Vector2(contentWidth * 0.48f, buttonHeight); 
            PositionHalf(loginRt, left: true, y: btnY); 
            _loginBtn.onClick.AddListener(() => { if (!_busy) StartCoroutine(Login()); });
            _loginBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 36; 

            _registerBtn = CreateButton(panel.transform, "Register", new Color(0.55f, 0.45f, 0.85f));
            var regRt = _registerBtn.GetComponent<RectTransform>();
            regRt.sizeDelta = new Vector2(contentWidth * 0.48f, buttonHeight);
            PositionHalf(regRt, left: false, y: btnY);
            _registerBtn.onClick.AddListener(() => { if (!_busy) StartCoroutine(Register()); });
            _registerBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 36;

            return panel;
        }

        private GameObject BuildWorkspacePanel(Transform parent)
        {
            var panel = CreatePanel(parent, "WorkspacePanel");
            panel.SetActive(false);

            var header = CreateText(panel.transform, "Select Workspace", 32, FontStyles.Bold);
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 60);
            headerRect.anchoredPosition = new Vector2(0, -20);
            header.alignment = TextAlignmentOptions.Center;

            var listBox = new GameObject("WorkspaceList");
            listBox.transform.SetParent(panel.transform, false);
            var listRect = listBox.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.5f, 0.5f);
            listRect.anchorMax = new Vector2(0.5f, 0.5f);
            listRect.pivot = new Vector2(0.5f, 0.5f);
            listRect.sizeDelta = new Vector2(480, 400);
            listRect.anchoredPosition = new Vector2(0, 40);
            
            // Removed inner background image to avoid "double frame" look
            // var listBg = listBox.AddComponent<Image>();
            // listBg.color = new Color(0.12f, 0.12f, 0.16f, 1f);

            var layout = listBox.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 10;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            _workspaceListRoot = listBox.transform;

            _enterBtn = CreateButton(panel.transform, "Enter", new Color(0.30f, 0.55f, 0.90f));
            // Larger Enter button
            var enterRect = _enterBtn.GetComponent<RectTransform>();
            enterRect.anchorMin = new Vector2(0.5f, 0);
            enterRect.anchorMax = new Vector2(0.5f, 0);
            enterRect.pivot = new Vector2(0.5f, 0);
            enterRect.sizeDelta = new Vector2(240, 60);
            enterRect.anchoredPosition = new Vector2(0, 70); 
            _enterBtn.onClick.AddListener(() => { if (!_busy) StartCoroutine(EnterMainScene()); });
            _enterBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;

            return panel;
        }

        private void ShowLogin()
        {
            _loginPanel.SetActive(true);
            _workspacePanel.SetActive(false);
            _selectedWorkspaceId = null;
            _selectedWorkspaceName = null;
            SetCursorForUI(true);
        }

        private void ShowWorkspaces()
        {
            _loginPanel.SetActive(false);
            _workspacePanel.SetActive(true);
            SetCursorForUI(true);
        }

        private IEnumerator Login()
        {
            _busy = true;
            SetStatus("Logging in...");
            SetButtons(false);

            var username = _usernameInput.text?.Trim();
            var password = _passwordInput.text ?? "";

            yield return AuthRequest(LoginUrl, username, password, onOk: () =>
            {
                SetStatus("Login ok. Loading workspaces...");
            });

            if (AppSession.IsLoggedIn)
            {
                yield return LoadWorkspaces();
            }

            SetButtons(true);
            _busy = false;
        }

        private IEnumerator Register()
        {
            _busy = true;
            SetStatus("Registering...");
            SetButtons(false);

            var username = _usernameInput.text?.Trim();
            var password = _passwordInput.text ?? "";

            yield return AuthRequest(RegisterUrl, username, password, onOk: () =>
            {
                SetStatus("Register ok. Loading workspaces...");
            });

            if (AppSession.IsLoggedIn)
            {
                yield return LoadWorkspaces();
            }

            SetButtons(true);
            _busy = false;
        }

        private IEnumerator AuthRequest(string url, string username, string password, Action onOk)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                SetStatus("Username/password required");
                yield break;
            }

            var body = $"{{\"username\":\"{EscapeJson(username)}\",\"password\":\"{EscapeJson(password)}\"}}";
            var bodyRaw = Encoding.UTF8.GetBytes(body);

            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    SetStatus($"Request failed: {req.error}");
                    yield break;
                }

                if (req.responseCode >= 400)
                {
                    SetStatus($"Auth failed ({req.responseCode}): {req.downloadHandler.text}");
                    yield break;
                }

                // 解析：{"token":"...","username":"..."}
                var json = req.downloadHandler.text;
                if (!TryParseAuth(json, out var token, out var user))
                {
                    SetStatus("Failed to parse auth response");
                    yield break;
                }

                AppSession.SetAuth(user, token);
                onOk?.Invoke();
            }
        }

        private IEnumerator LoadWorkspaces()
        {
            ShowWorkspaces();
            ClearWorkspaceList();
            SetStatus("Loading workspaces...");

            using (var req = UnityWebRequest.Get(WorkspacesUrl))
            {
                req.SetRequestHeader("Authorization", $"Bearer {AppSession.Token}");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    SetStatus($"Workspaces failed: {req.error} (showing fake UI)");
                    BuildFakeWorkspaceList();
                    yield break;
                }

                if (req.responseCode >= 400)
                {
                    SetStatus($"Workspaces failed ({req.responseCode}): {req.downloadHandler.text} (showing fake UI)");
                    BuildFakeWorkspaceList();
                    yield break;
                }

                var json = req.downloadHandler.text;
                BuildWorkspaceListFromJson(json);
            }
        }

        private IEnumerator EnterMainScene()
        {
            if (string.IsNullOrEmpty(_selectedWorkspaceId))
            {
                SetStatus("Please select a workspace");
                yield break;
            }

            _busy = true;
            SetStatus($"Entering: {_selectedWorkspaceName} ...");

            AppSession.SetWorkspace(_selectedWorkspaceId, _selectedWorkspaceName);

            // 立即隐藏/销毁 BootScene 的 UI，确保不会在加载过程中显示
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(false);
                Debug.Log("[BootFlow] BootCanvas hidden before scene load");
            }
            if (_skyboxCamera != null)
            {
                _skyboxCamera.gameObject.SetActive(false);
                Debug.Log("[BootFlow] BootSkyboxCamera hidden before scene load");
            }
            if (_createdEventSystem && _createdEventSystemGO != null)
            {
                _createdEventSystemGO.SetActive(false);
                Debug.Log("[BootFlow] Boot-created EventSystem hidden before scene load");
            }
            
            // 禁用 BootScene 的摄像机（如果存在）
            var bootSceneCamera = Camera.main;
            if (bootSceneCamera != null && bootSceneCamera.name == "BootUICamera")
            {
                bootSceneCamera.gameObject.SetActive(false);
                Debug.Log("[BootFlow] BootScene camera disabled");
            }
            
            // 等待一帧，确保 UI 完全隐藏
            yield return null;

            // 无论选择哪个 workspace，统一进入 MainScene
            var sceneToLoad = "MainScene";
            Debug.Log($"[BootFlow] Loading scene: {sceneToLoad}");
            
            var op = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);
            
            if (op == null)
            {
                SetStatus($"Error: Scene '{sceneToLoad}' not found! Add it to Build Settings.");
                _busy = false;
                yield break;
            }
            
            // 设置加载优先级，确保快速加载
            op.priority = 1;
            
            // 等待场景完全加载
            while (!op.isDone)
            {
                yield return null;
            }
            
            // 等待场景激活完成
            yield return null;
            yield return new WaitForEndOfFrame();

            // 确保 UI 完全销毁（不再需要，因为已经进入主场景）
            if (_canvas != null)
            {
                UnityEngine.Object.Destroy(_canvas.gameObject);
                _canvas = null;
                Debug.Log("[BootFlow] BootCanvas destroyed");
            }
            if (_skyboxCamera != null)
            {
                UnityEngine.Object.Destroy(_skyboxCamera.gameObject);
                _skyboxCamera = null;
                Debug.Log("[BootFlow] BootSkyboxCamera destroyed");
            }
            if (_createdEventSystem && _createdEventSystemGO != null)
            {
                UnityEngine.Object.Destroy(_createdEventSystemGO);
                _createdEventSystemGO = null;
                Debug.Log("[BootFlow] Boot-created EventSystem destroyed");
            }

            // 主场景默认是第三人称：把鼠标锁回去（玩家可用右键/Tab 等切换 UI 时再解锁）
            SetCursorForUI(false);
            SetPlayerInputEnabled(true);
            
            Debug.Log("[BootFlow] MainScene loaded, Boot UI cleaned up");
            _busy = false;

            // 引导流程完成后销毁自身，避免进入 MainScene 后再重复执行登录/选空间逻辑
            Destroy(gameObject);
        }

        private void SetPlayerInputEnabled(bool enabled)
        {
#if ENABLE_INPUT_SYSTEM
            var playerInputs = UnityEngine.Object.FindObjectsByType<UnityEngine.InputSystem.PlayerInput>(FindObjectsSortMode.None);
            foreach (var pi in playerInputs)
            {
                pi.enabled = enabled;
            }
#endif
            // 尝试禁用 StarterAssetsInputs（它是 MonoBehaviour，负责光标锁定逻辑）
            var starterInputs = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(m => m.GetType().Name == "StarterAssetsInputs").ToArray();
            foreach (var si in starterInputs)
            {
                si.enabled = enabled;
                if (!enabled)
                {
                    // 强制解锁光标
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }

        // ===== workspace list UI =====

        private void ClearWorkspaceList()
        {
            for (int i = _workspaceListRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_workspaceListRoot.GetChild(i).gameObject);
            }
        }

        private void BuildFakeWorkspaceList()
        {
            AddWorkspaceItem("ws-fake-001", "Couple Space (fake)");
            AddWorkspaceItem("ws-fake-002", "Home (fake)");
            SetStatus("Workspace list is currently fake UI. Hook real service later.");
        }

        private void BuildWorkspaceListFromJson(string json)
        {
            // 极简解析（避免引入 JSON 库）：只要能把 id/name 抠出来即可。
            // 期望格式：{"items":[{"id":"...","name":"...","members":[...]}, ...]}
            // 若解析失败，则回退到伪数据 UI。
            try
            {
                var itemsIdx = json.IndexOf("\"items\"", StringComparison.OrdinalIgnoreCase);
                if (itemsIdx < 0) throw new Exception("items not found");

                // 粗暴拆分：按 "id":" 作为锚点
                var parts = json.Split(new[] { "\"id\"" }, StringSplitOptions.RemoveEmptyEntries);
                int added = 0;
                foreach (var p in parts)
                {
                    var id = ExtractJsonStringValue(p, ":");
                    if (string.IsNullOrEmpty(id)) continue;

                    var nameIdx = p.IndexOf("\"name\"", StringComparison.OrdinalIgnoreCase);
                    string name = null;
                    if (nameIdx >= 0)
                    {
                        var sub = p.Substring(nameIdx);
                        name = ExtractJsonStringValue(sub, ":");
                    }

                    AddWorkspaceItem(id, string.IsNullOrEmpty(name) ? id : name);
                    added++;
                }

                if (added == 0) throw new Exception("no items parsed");

                SetStatus("Please select a workspace");
            }
            catch
            {
                BuildFakeWorkspaceList();
            }
        }

        private void AddWorkspaceItem(string id, string name)
        {
            var btn = CreateButton(_workspaceListRoot, name, new Color(0.25f, 0.25f, 0.32f));
            var rt = btn.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 70);
            
            // Override font size
            btn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 28;
            
            // Add LayoutElement for VerticalLayoutGroup to work properly
            var layoutElement = btn.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 70;
            layoutElement.preferredHeight = 70;
            layoutElement.flexibleWidth = 1;
            
            btn.onClick.AddListener(() =>
            {
                _selectedWorkspaceId = id;
                _selectedWorkspaceName = name;
                SetStatus($"Selected: {name}");
            });
        }

        // ===== helpers =====

        private void SetButtons(bool enabled)
        {
            if (_loginBtn != null) _loginBtn.interactable = enabled;
            if (_registerBtn != null) _registerBtn.interactable = enabled;
            if (_enterBtn != null) _enterBtn.interactable = enabled;
        }

        private void SetStatus(string msg)
        {
            if (_status != null) _status.text = msg ?? "";
            Debug.Log($"[BootFlow] {msg}");
        }

        private static void EnsureEventSystem()
        {
            var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                Debug.Log("[BootFlow] Creating EventSystem...");
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
                UnityEngine.Object.DontDestroyOnLoad(go);

                // 标记为“由 BootFlow 创建”，便于进入 MainScene 后清理，避免与主场景 EventSystem 冲突
                var mgr = UnityEngine.Object.FindFirstObjectByType<BootFlowManager>();
                if (mgr != null)
                {
                    mgr._createdEventSystem = true;
                    mgr._createdEventSystemGO = go;
                }
            }

            // 确保 EventSystem 激活
            if (!es.gameObject.activeInHierarchy)
            {
                es.gameObject.SetActive(true);
            }

            // check input module
#if ENABLE_INPUT_SYSTEM
            var existingModule = es.GetComponent<InputSystemUIInputModule>();
            if (existingModule == null)
            {
                // Remove old module if exists
                var old = es.GetComponent<StandaloneInputModule>();
                if (old != null) 
                {
                    Debug.Log("[BootFlow] Removing old StandaloneInputModule...");
                    UnityEngine.Object.DestroyImmediate(old);
                }

                Debug.Log("[BootFlow] Adding InputSystemUIInputModule...");
                var uiModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
                var actionsAsset = CreateMinimalUIActions();
                uiModule.actionsAsset = actionsAsset;
                
                // 确保 InputSystem 已启用
                if (actionsAsset != null)
                {
                    if (!actionsAsset.enabled)
                    {
                        actionsAsset.Enable();
                    }
                    Debug.Log($"[BootFlow] InputSystem actionsAsset enabled: {actionsAsset.enabled}");
                }
            }
            else
            {
                // 确保现有的 InputSystemUIInputModule 正常工作
                Debug.Log("[BootFlow] InputSystemUIInputModule already exists, ensuring it's enabled...");
                if (existingModule.actionsAsset != null)
                {
                    if (!existingModule.actionsAsset.enabled)
                    {
                        existingModule.actionsAsset.Enable();
                    }
                    Debug.Log($"[BootFlow] Existing actionsAsset enabled: {existingModule.actionsAsset.enabled}");
                }
                else
                {
                    Debug.LogWarning("[BootFlow] InputSystemUIInputModule exists but has no actionsAsset! Creating new one...");
                    existingModule.actionsAsset = CreateMinimalUIActions();
                    existingModule.actionsAsset.Enable();
                }
            }
#else
            if (es.GetComponent<StandaloneInputModule>() == null)
            {
                Debug.Log("[BootFlow] Adding StandaloneInputModule...");
                es.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
            
            Debug.Log($"[BootFlow] EventSystem ready: {es.name}, active: {es.gameObject.activeInHierarchy}");
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// 运行时创建一套最小 UI InputActions（仅保证鼠标点击/指针移动/滚轮/Submit/Cancel 可用）。
        /// 避免依赖工程里已有的 .inputactions 资源，从而让 Boot UI 在任何场景都可用。
        /// </summary>
        private static InputActionAsset CreateMinimalUIActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "BootUI_Actions";

            var map = new InputActionMap("UI");

            // 鼠标/指针输入
            // 注意：InputSystemUIInputModule 期望的 action 名称是 "Click"，不是 "LeftClick"！
            var point = map.AddAction("Point", InputActionType.PassThrough, "<Pointer>/position");
            var click = map.AddAction("Click", InputActionType.PassThrough, "<Pointer>/press"); // 必须是 "Click"！
            var rightClick = map.AddAction("RightClick", InputActionType.PassThrough, "<Mouse>/rightButton");
            var middleClick = map.AddAction("MiddleClick", InputActionType.PassThrough, "<Mouse>/middleButton");
            var scroll = map.AddAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll");

            // 键盘输入 - 这是关键！TMP_InputField 需要这些来接收键盘输入
            var navigate = map.AddAction("Navigate", InputActionType.PassThrough);
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a").With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d").With("Right", "<Keyboard>/rightArrow");

            // 文本输入 - 关键！必须添加这个才能让输入框接收键盘输入
            var textInput = map.AddAction("TextInput", InputActionType.PassThrough);
            // 绑定所有键盘按键
            textInput.AddBinding("<Keyboard>/anyKey");

            var submit = map.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/numpadEnter");
            submit.AddBinding("<Gamepad>/buttonSouth");

            var cancel = map.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");

            asset.AddActionMap(map);
            
            // 必须在返回前启用
            asset.Enable();
            
            Debug.Log("[BootFlow] Created InputActionAsset with keyboard support");

            return asset;
        }
#endif

        private static void SetCursorForUI(bool uiMode)
        {
            // Always unlock!
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(10, 80);
            rt.offsetMax = new Vector2(-10, -80);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.10f, 0.10f, 0.14f, 0.9f);

            return go;
        }

        private TMP_Text CreateText(Transform parent, string text, float size, FontStyles style)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            return tmp;
        }

        private TMP_InputField CreateInput(Transform parent, string placeholder, bool isPassword = false)
        {
            var go = new GameObject("InputField");
            go.transform.SetParent(parent, false);
            go.SetActive(true); // 确保激活
            
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(420, 44);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.14f, 0.14f, 0.18f, 1f);
            img.raycastTarget = true; // 确保可以接收射线

            var input = go.AddComponent<TMP_InputField>();
            input.interactable = true; // 确保可交互
            input.enabled = true;

            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(go.transform, false);
            var textAreaRT = textArea.AddComponent<RectTransform>();
            textAreaRT.anchorMin = new Vector2(0, 0);
            textAreaRT.anchorMax = new Vector2(1, 1);
            textAreaRT.offsetMin = new Vector2(12, 6);
            textAreaRT.offsetMax = new Vector2(-12, -6);
            textArea.AddComponent<RectMask2D>();

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textArea.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            Stretch(textRT);
            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.fontSize = 36; // Big!
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false; // 文本不需要接收射线
            
            input.textComponent = text;
            input.textViewport = textAreaRT;

            // Placeholder
            if (!string.IsNullOrEmpty(placeholder))
            {
                var phGO = new GameObject("Placeholder");
                phGO.transform.SetParent(textArea.transform, false);
                var phRT = phGO.AddComponent<RectTransform>();
                Stretch(phRT);
                var phText = phGO.AddComponent<TextMeshProUGUI>();
                phText.text = placeholder;
                phText.fontSize = 36; // Big!
                phText.color = new Color(1f, 1f, 1f, 0.5f);
                phText.fontStyle = FontStyles.Italic;
                phText.alignment = TextAlignmentOptions.MidlineLeft;
                phText.raycastTarget = false;
                input.placeholder = phText;
            }

            input.textViewport = textAreaRT;
            input.textComponent = text;
            input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.characterLimit = 0; // 无限制
            input.readOnly = false; // 确保可编辑

            Debug.Log($"[BootFlow] Created InputField: interactable={input.interactable}, enabled={input.enabled}, readOnly={input.readOnly}");

            return input;
        }

        private Button CreateButton(Transform parent, string text, Color color)
        {
            var go = new GameObject($"Button_{text}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 44);

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var labelRT = label.AddComponent<RectTransform>();
            Stretch(labelRT);

            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
        }

        private static void PositionRow(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -y);
        }

        private static void PositionHalf(RectTransform rt, bool left, float y)
        {
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(200, 44);
            rt.anchoredPosition = new Vector2(left ? -110 : 110, -y);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static bool TryParseAuth(string json, out string token, out string username)
        {
            token = null;
            username = null;
            token = ExtractJsonField(json, "token");
            username = ExtractJsonField(json, "username");
            return !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(username);
        }

        private static string ExtractJsonField(string json, string field)
        {
            var idx = json.IndexOf($"\"{field}\"", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var sub = json.Substring(idx);
            return ExtractJsonStringValue(sub, ":");
        }

        private static string ExtractJsonStringValue(string text, string afterToken)
        {
            var idx = text.IndexOf(afterToken, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var rest = text.Substring(idx + afterToken.Length);
            var q1 = rest.IndexOf('"');
            if (q1 < 0) return null;
            var q2 = rest.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            return rest.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        /// <summary>
        /// 根据所选 workspace 决定要进入的场景名。
        /// 当前版本：所有空间都进入 <see cref="mainSceneName"/>（默认 Playground）。
        /// 后续如果你有不同类型的空间，可以在这里根据 id/name 做映射。
        /// </summary>
        private string GetSceneNameForWorkspace(string workspaceId, string workspaceName)
        {
            // 需求：不管用户选择什么 workspace，都进入 MainScene
            return "MainScene";
        }
    }
}

