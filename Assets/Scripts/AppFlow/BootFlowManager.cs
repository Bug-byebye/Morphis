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
#if UNITY_EDITOR
using UnityEditor;
#endif
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
        [Serializable]
        private class JoinWorldResponseDto
        {
            public string status;
            public string world_id;
            public string server_address;
            public int server_port;
            public string message;
        }

        [Header("Scene")]
        [SerializeField] private string mainSceneName = "MainScene";
        [SerializeField] private Material backgroundSkybox;
        [Header("Login Art")]
        [SerializeField] private Sprite loginBackgroundSprite;
        [SerializeField] private Sprite leftLanternSprite;
        [SerializeField] private Sprite rightMascotSprite;
        [SerializeField] private Sprite rightStillLifeSprite;
        private Sprite[] _ambientEffectSprites;

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
        private Button _createSpaceBtn;
        private string _selectedWorkspaceId;
        private string _selectedWorkspaceName;

        // 创建空间 UI
        private GameObject _createSpacePanel;
        private TMP_InputField _spaceNameInput;
        private TMP_InputField _coOwnerUsernameInput;
        private Button _createSpaceSubmitBtn;
        private Button _createSpaceBackBtn;

        private bool _busy;

        private string LoginUrl => $"{AppSession.BaseUrl}/auth/login";
        private string RegisterUrl => $"{AppSession.BaseUrl}/auth/register";
        private string WorkspacesUrl => $"{AppSession.BaseUrl}/workspaces";
        private string CreateWorkspaceUrl => $"{AppSession.BaseUrl}/workspaces/create";
        private string JoinWorldUrl => $"{AppSession.BaseUrl}/workspaces/join";

        private static BootFlowManager _instance;
        private bool _initialized;
        private bool _createdEventSystem;
        private GameObject _createdEventSystemGO;
        private TMP_FontAsset _uiFont;
        private Button _selectedWorkspaceButton;

        private void Awake()
        {
            // Dedicated Server 模式下不需要任何登录/空间选择 UI，直接进入主场景由服务器权威同步
            if (Morphis.AppRuntime.IsServer)
            {
                Debug.Log("[BootFlow] Server mode detected. Skipping Boot UI and loading main scene directly.");
                var active = SceneManager.GetActiveScene();
                if (!string.IsNullOrEmpty(mainSceneName) && active.name != mainSceneName)
                {
                    SceneManager.LoadScene(mainSceneName);
                }
                Destroy(gameObject);
                return;
            }

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

            // 确保 AppSession.BaseUrl 从配置文件初始化（不再需要手动设置，getter 会自动处理）
            // 但为了确保配置已加载，我们在这里触发一次访问
            var baseUrl = AppSession.BaseUrl;
            Debug.Log($"[BootFlow] AppSession.BaseUrl initialized: {baseUrl}");
            
            DontDestroyOnLoad(gameObject);
            
            // GlobalSceneController now auto-creates itself at runtime.
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
            
            Debug.Log("[BootFlow] UI initialization complete! Canvas and EventSystem should be ready.");
        }

        private void BuildUI()
        {
            EnsureEventSystem();
            LoadUiResources();

            Debug.Log("[BootFlow] Creating Canvas...");
            var canvasGO = new GameObject("BootCanvas");
            canvasGO.SetActive(true);
            
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            var raycaster = canvasGO.AddComponent<GraphicRaycaster>();
            _canvas.enabled = true;
            raycaster.enabled = true;
            canvasGO.SetActive(true);
            
            Debug.Log($"[BootFlow] Canvas created: renderMode={_canvas.renderMode}, enabled={_canvas.enabled}, active={canvasGO.activeSelf}");
            DontDestroyOnLoad(canvasGO);

            var background = new GameObject("Background");
            background.transform.SetParent(_canvas.transform, false);
            var backgroundRect = background.AddComponent<RectTransform>();
            Stretch(backgroundRect);

            var backgroundImageGO = new GameObject("BackgroundImage");
            backgroundImageGO.transform.SetParent(background.transform, false);
            var backgroundImageRect = backgroundImageGO.AddComponent<RectTransform>();
            Stretch(backgroundImageRect);
            var backgroundImage = backgroundImageGO.AddComponent<Image>();
            if (loginBackgroundSprite != null)
            {
                backgroundImage.sprite = loginBackgroundSprite;
                backgroundImage.color = Color.white;
                backgroundImage.preserveAspect = true;
            }
            else
            {
                backgroundImage.color = new Color(0.04f, 0.05f, 0.12f, 1f);
            }
            backgroundImage.raycastTarget = false;

            var darkOverlayGO = new GameObject("DarkOverlay");
            darkOverlayGO.transform.SetParent(background.transform, false);
            var darkOverlayRect = darkOverlayGO.AddComponent<RectTransform>();
            Stretch(darkOverlayRect);
            var darkOverlay = darkOverlayGO.AddComponent<Image>();
            darkOverlay.color = new Color32(0x05, 0x06, 0x17, 0x59);
            darkOverlay.raycastTarget = false;

            if (loginBackgroundSprite == null && backgroundSkybox != null)
            {
                SetupSkyboxCamera();
            }

            var decorations = new GameObject("Decorations");
            decorations.transform.SetParent(_canvas.transform, false);
            var decorationsRect = decorations.AddComponent<RectTransform>();
            Stretch(decorationsRect);

            CreateAmbientEffects(decorations.transform);

            var titleGroup = new GameObject("TitleGroup");
            titleGroup.transform.SetParent(_canvas.transform, false);
            var titleGroupRect = titleGroup.AddComponent<RectTransform>();
            titleGroupRect.anchorMin = new Vector2(0.5f, 1f);
            titleGroupRect.anchorMax = new Vector2(0.5f, 1f);
            titleGroupRect.pivot = new Vector2(0.5f, 1f);
            titleGroupRect.sizeDelta = new Vector2(1280f, 220f);
            titleGroupRect.anchoredPosition = new Vector2(0f, -108f);

            _title = CreateText(titleGroup.transform, "Morphis", 104, FontStyles.Bold);
            _title.gameObject.name = "TitleText";
            var titleRect = _title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(1120f, 116f);
            titleRect.anchoredPosition = new Vector2(0f, 0f);
            _title.alignment = TextAlignmentOptions.Center;
            _title.color = new Color32(0xF5, 0xD8, 0xFF, 0xFF);
            _title.outlineWidth = 0.12f;
            _title.outlineColor = new Color32(0x74, 0x59, 0xC9, 0xAA);

            var subtitle = CreateText(titleGroup.transform, "你的专属情感陪伴", 28, FontStyles.Normal);
            subtitle.gameObject.name = "SubtitleText";
            var subtitleRect = subtitle.rectTransform;
            subtitleRect.anchorMin = new Vector2(0.5f, 1f);
            subtitleRect.anchorMax = new Vector2(0.5f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.sizeDelta = new Vector2(920f, 38f);
            subtitleRect.anchoredPosition = new Vector2(0f, -118f);
            subtitle.alignment = TextAlignmentOptions.Center;
            subtitle.color = new Color(0.847f, 0.78f, 1f, 0.9f);

            _loginPanel = BuildLoginPanel(_canvas.transform);
            _workspacePanel = BuildWorkspacePanel(_canvas.transform);
            _createSpacePanel = BuildCreateSpacePanel(_canvas.transform);

            _status = CreateText(_canvas.transform, "", 20, FontStyles.Normal);
            _status.gameObject.name = "StatusText";
            var statusRect = _status.rectTransform;
            statusRect.anchorMin = new Vector2(0.5f, 0f);
            statusRect.anchorMax = new Vector2(0.5f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(1100f, 30f);
            statusRect.anchoredPosition = new Vector2(0f, 86f);
            _status.alignment = TextAlignmentOptions.Center;
            _status.color = new Color(0.847f, 0.78f, 1f, 0.78f);

            var footerText = CreateText(_canvas.transform, "在这里，你的情绪被理解，你的心灵被陪伴", 22, FontStyles.Normal);
            footerText.gameObject.name = "FooterText";
            var footerRect = footerText.rectTransform;
            footerRect.anchorMin = new Vector2(0.5f, 0f);
            footerRect.anchorMax = new Vector2(0.5f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.sizeDelta = new Vector2(1200f, 34f);
            footerRect.anchoredPosition = new Vector2(0f, 55f);
            footerText.alignment = TextAlignmentOptions.Center;
            footerText.color = new Color(0.847f, 0.78f, 1f, 0.55f);

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
            var panel = CreatePanel(parent, "LoginCard", new Vector2(620f, 420f), new Color(0.607f, 0.509f, 1f, 0.35f));
            panel.SetActive(false);
            panel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -10f);
            var content = CreateInnerFill(panel.transform, "CardFill", new Vector2(616f, 416f), new Color(0.129f, 0.106f, 0.275f, 0.72f));

            _usernameInput = CreateInput(content.transform, "UsernameInput", "用户名");
            var userInRt = _usernameInput.GetComponent<RectTransform>();
            userInRt.sizeDelta = new Vector2(480f, 58f);
            PositionRow(userInRt, 55f);

            _passwordInput = CreateInput(content.transform, "PasswordInput", "密码", isPassword: true);
            var pwdInRt = _passwordInput.GetComponent<RectTransform>();
            pwdInRt.sizeDelta = new Vector2(480f, 58f);
            PositionRow(pwdInRt, 139f);

            var forgot = CreateText(content.transform, "忘记密码？", 20, FontStyles.Normal);
            forgot.gameObject.name = "ForgotPasswordText";
            var forgotRect = forgot.rectTransform;
            forgotRect.anchorMin = new Vector2(1f, 1f);
            forgotRect.anchorMax = new Vector2(1f, 1f);
            forgotRect.pivot = new Vector2(1f, 1f);
            forgotRect.sizeDelta = new Vector2(180f, 26f);
            forgotRect.anchoredPosition = new Vector2(-70f, -223f);
            forgot.alignment = TextAlignmentOptions.TopRight;
            forgot.color = new Color(0.788f, 0.721f, 1f, 0.75f);

            var buttonGroup = new GameObject("ButtonGroup");
            buttonGroup.transform.SetParent(content.transform, false);
            var buttonGroupRect = buttonGroup.AddComponent<RectTransform>();
            buttonGroupRect.anchorMin = new Vector2(0.5f, 0f);
            buttonGroupRect.anchorMax = new Vector2(0.5f, 0f);
            buttonGroupRect.pivot = new Vector2(0.5f, 0f);
            buttonGroupRect.sizeDelta = new Vector2(466f, 62f);
            buttonGroupRect.anchoredPosition = new Vector2(0f, 55f);

            _loginBtn = CreateFilledButton(buttonGroup.transform, "LoginButton", "登录", new Vector2(220f, 62f), new Color(0.914f, 0.545f, 0.847f, 1f), Color.white);
            var loginRt = _loginBtn.GetComponent<RectTransform>();
            loginRt.anchorMin = new Vector2(0f, 0.5f);
            loginRt.anchorMax = new Vector2(0f, 0.5f);
            loginRt.pivot = new Vector2(0f, 0.5f);
            loginRt.anchoredPosition = new Vector2(0f, 0f);
            _loginBtn.onClick.AddListener(() => { if (!_busy) StartCoroutine(Login()); });

            _registerBtn = CreateGhostButton(buttonGroup.transform, "RegisterButton", "注册", new Vector2(220f, 62f), new Color(0.607f, 0.509f, 1f, 0.35f), new Color(0.129f, 0.106f, 0.275f, 0.88f), new Color(0.847f, 0.78f, 1f, 1f));
            var regRt = _registerBtn.GetComponent<RectTransform>();
            regRt.anchorMin = new Vector2(1f, 0.5f);
            regRt.anchorMax = new Vector2(1f, 0.5f);
            regRt.pivot = new Vector2(1f, 0.5f);
            regRt.anchoredPosition = new Vector2(0f, 0f);
            _registerBtn.onClick.AddListener(() => { if (!_busy) StartCoroutine(Register()); });

            return panel;
        }

        private GameObject BuildWorkspacePanel(Transform parent)
        {
            var panel = CreatePanel(parent, "WorkspacePanel", new Vector2(704f, 504f), new Color(0.607f, 0.509f, 1f, 0.28f));
            panel.SetActive(false);
            panel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -6f);
            var content = CreateInnerFill(panel.transform, "PanelFill", new Vector2(700f, 500f), new Color(0.129f, 0.106f, 0.275f, 0.76f));
            const float contentWidth = 500f;
            const float buttonWidth = 237f;
            const float buttonGap = 26f;

            var header = CreateText(content.transform, "选择空间", 36, FontStyles.Bold);
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(contentWidth, 56f);
            headerRect.anchoredPosition = new Vector2(0, -46f);
            header.alignment = TextAlignmentOptions.Center;
            header.color = new Color32(0xF5, 0xD8, 0xFF, 0xFF);

            var listBox = new GameObject("WorkspaceList");
            listBox.transform.SetParent(content.transform, false);
            var listRect = listBox.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.5f, 1f);
            listRect.anchorMax = new Vector2(0.5f, 1f);
            listRect.pivot = new Vector2(0.5f, 1f);
            listRect.sizeDelta = new Vector2(contentWidth, 238f);
            listRect.anchoredPosition = new Vector2(0f, -116f);

            var listBg = listBox.AddComponent<Image>();
            listBg.color = new Color(0.09f, 0.086f, 0.20f, 0.90f);

            var layout = listBox.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 10;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            _workspaceListRoot = listBox.transform;

            _enterBtn = CreateFilledButton(content.transform, "EnterButton", "进入空间", new Vector2(buttonWidth, 62f), new Color(0.914f, 0.545f, 0.847f, 1f), Color.white);
            var enterRect = _enterBtn.GetComponent<RectTransform>();
            enterRect.anchorMin = new Vector2(0.5f, 0f);
            enterRect.anchorMax = new Vector2(0.5f, 0f);
            enterRect.pivot = new Vector2(0.5f, 0f);
            enterRect.anchoredPosition = new Vector2(-(buttonWidth + buttonGap) * 0.5f, 54f);
            _enterBtn.onClick.AddListener(() => { if (!_busy) StartCoroutine(EnterMainScene()); });

            _createSpaceBtn = CreateGhostButton(content.transform, "CreateSpaceButton", "创建空间", new Vector2(buttonWidth, 62f), new Color(0.607f, 0.509f, 1f, 0.35f), new Color(0.129f, 0.106f, 0.275f, 0.88f), new Color(0.847f, 0.78f, 1f, 1f));
            var createRect = _createSpaceBtn.GetComponent<RectTransform>();
            createRect.anchorMin = new Vector2(0.5f, 0f);
            createRect.anchorMax = new Vector2(0.5f, 0f);
            createRect.pivot = new Vector2(0.5f, 0f);
            createRect.anchoredPosition = new Vector2((buttonWidth + buttonGap) * 0.5f, 54f);
            _createSpaceBtn.onClick.AddListener(() => { if (!_busy) ShowCreateSpace(); });

            return panel;
        }

        private GameObject BuildCreateSpacePanel(Transform parent)
        {
            var panel = CreatePanel(parent, "CreateSpacePanel", new Vector2(664f, 434f), new Color(0.607f, 0.509f, 1f, 0.28f));
            panel.SetActive(false);
            panel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -10f);
            var content = CreateInnerFill(panel.transform, "PanelFill", new Vector2(660f, 430f), new Color(0.129f, 0.106f, 0.275f, 0.76f));

            float contentWidth = 500f;
            float inputHeight = 58f;

            var header = CreateText(content.transform, "创建新空间", 36, FontStyles.Bold);
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1);
            headerRect.anchorMax = new Vector2(0.5f, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(contentWidth, 52f);
            headerRect.anchoredPosition = new Vector2(0, -38f);
            header.alignment = TextAlignmentOptions.Center;
            header.color = new Color32(0xF5, 0xD8, 0xFF, 0xFF);

            _spaceNameInput = CreateInput(content.transform, "SpaceNameInput", "空间名称（可选）");
            var nameInRt = _spaceNameInput.GetComponent<RectTransform>();
            nameInRt.sizeDelta = new Vector2(contentWidth, inputHeight);
            PositionRow(nameInRt, 100f);

            _coOwnerUsernameInput = CreateInput(content.transform, "CoOwnerUsernameInput", "共同拥有者用户名（可选）");
            var coOwnerInRt = _coOwnerUsernameInput.GetComponent<RectTransform>();
            coOwnerInRt.sizeDelta = new Vector2(contentWidth, inputHeight);
            PositionRow(coOwnerInRt, 184f);

            _createSpaceSubmitBtn = CreateFilledButton(content.transform, "CreateSpaceSubmitButton", "创建", new Vector2(220f, 62f), new Color(0.914f, 0.545f, 0.847f, 1f), Color.white);
            var submitRt = _createSpaceSubmitBtn.GetComponent<RectTransform>();
            submitRt.anchorMin = new Vector2(0.5f, 0f);
            submitRt.anchorMax = new Vector2(0.5f, 0f);
            submitRt.pivot = new Vector2(0.5f, 0f);
            submitRt.anchoredPosition = new Vector2(-123f, 48f);
            _createSpaceSubmitBtn.onClick.AddListener(() => { if (!_busy) StartCoroutine(CreateSpace()); });

            _createSpaceBackBtn = CreateGhostButton(content.transform, "CreateSpaceBackButton", "返回", new Vector2(220f, 62f), new Color(0.607f, 0.509f, 1f, 0.35f), new Color(0.129f, 0.106f, 0.275f, 0.88f), new Color(0.847f, 0.78f, 1f, 1f));
            var backRt = _createSpaceBackBtn.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0.5f, 0f);
            backRt.anchorMax = new Vector2(0.5f, 0f);
            backRt.pivot = new Vector2(0.5f, 0f);
            backRt.anchoredPosition = new Vector2(123f, 48f);
            _createSpaceBackBtn.onClick.AddListener(() => { if (!_busy) HideCreateSpace(); });

            return panel;
        }

        private void ShowCreateSpace()
        {
            _workspacePanel.SetActive(false);
            _createSpacePanel.SetActive(true);
            if (_spaceNameInput != null) _spaceNameInput.text = "";
            if (_coOwnerUsernameInput != null) _coOwnerUsernameInput.text = "";
            SetStatus("Create a new space and optionally add a co-owner");
        }

        private void HideCreateSpace()
        {
            _createSpacePanel.SetActive(false);
            _workspacePanel.SetActive(true);
            SetStatus("Please select a workspace");
        }

        private IEnumerator CreateSpace()
        {
            _busy = true;
            SetButtons(false);
            if (_createSpaceSubmitBtn != null) _createSpaceSubmitBtn.interactable = false;
            if (_createSpaceBackBtn != null) _createSpaceBackBtn.interactable = false;

            var spaceName = _spaceNameInput?.text?.Trim() ?? "";
            var coOwnerUsername = _coOwnerUsernameInput?.text?.Trim() ?? "";

            if (string.IsNullOrEmpty(spaceName))
            {
                spaceName = "My Space";
            }

            var coOwnerList = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(coOwnerUsername))
            {
                coOwnerList.Add(coOwnerUsername);
            }

            var body = $"{{\"name\":\"{EscapeJson(spaceName)}\",\"co_owner_usernames\":[{string.Join(",", coOwnerList.ConvertAll(u => $"\"{EscapeJson(u)}\""))}]}}";

            SetStatus("Creating space...");

            LogRequest("POST", CreateWorkspaceUrl, body, AppSession.Token);

            using (var req = new UnityWebRequest(CreateWorkspaceUrl, "POST"))
            {
                var bodyRaw = Encoding.UTF8.GetBytes(body);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {AppSession.Token}");

                yield return req.SendWebRequest();

                LogResponse(req);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    SetStatus($"Create failed: {req.error}");
                    if (_createSpaceSubmitBtn != null) _createSpaceSubmitBtn.interactable = true;
                    if (_createSpaceBackBtn != null) _createSpaceBackBtn.interactable = true;
                    SetButtons(true);
                    _busy = false;
                    yield break;
                }

                if (req.responseCode >= 400)
                {
                    SetStatus($"Create failed ({req.responseCode}): {req.downloadHandler.text}");
                    if (_createSpaceSubmitBtn != null) _createSpaceSubmitBtn.interactable = true;
                    if (_createSpaceBackBtn != null) _createSpaceBackBtn.interactable = true;
                    SetButtons(true);
                    _busy = false;
                    yield break;
                }

                var json = req.downloadHandler.text;
                var id = ExtractJsonField(json, "id");
                var name = ExtractJsonField(json, "name");
                if (!string.IsNullOrEmpty(id))
                {
                    SetStatus($"Space created: {name ?? id}");
                    HideCreateSpace();
                    yield return LoadWorkspaces();
                    _selectedWorkspaceId = id;
                    _selectedWorkspaceName = name ?? id;
                    SetStatus($"Selected: {name ?? id}");
                }
            }

            if (_createSpaceSubmitBtn != null) _createSpaceSubmitBtn.interactable = true;
            if (_createSpaceBackBtn != null) _createSpaceBackBtn.interactable = true;
            SetButtons(true);
            _busy = false;
        }

        private void ShowLogin()
        {
            _loginPanel.SetActive(true);
            _workspacePanel.SetActive(false);
            if (_createSpacePanel != null) _createSpacePanel.SetActive(false);
            _selectedWorkspaceId = null;
            _selectedWorkspaceName = null;
            _selectedWorkspaceButton = null;
            SetCursorForUI(true);
        }

        private void ShowWorkspaces()
        {
            _loginPanel.SetActive(false);
            _workspacePanel.SetActive(true);
            if (_createSpacePanel != null) _createSpacePanel.SetActive(false);
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

            LogRequest("POST", url, body);

            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                LogResponse(req);

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

            LogRequest("GET", WorkspacesUrl, token: AppSession.Token);

            using (var req = UnityWebRequest.Get(WorkspacesUrl))
            {
                req.SetRequestHeader("Authorization", $"Bearer {AppSession.Token}");
                yield return req.SendWebRequest();

                LogResponse(req);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    SetStatus($"Workspaces failed: {req.error}. Check network and backend.");
                    yield break;
                }

                if (req.responseCode >= 400)
                {
                    SetStatus($"Workspaces failed ({req.responseCode}): {req.downloadHandler.text}");
                    yield break;
                }

                var json = req.downloadHandler.text;
                BuildWorkspaceListFromJson(json);
            }
            
            // 无论成功还是失败，都添加 mirror-test 选项
            AddWorkspaceItem("mirror-test", "Mirror Test");
        }

        private IEnumerator EnterMainScene()
        {
            if (string.IsNullOrEmpty(_selectedWorkspaceId))
            {
                SetStatus("Please select a workspace");
                yield break;
            }

            _busy = true;
            SetStatus($"Requesting world: {_selectedWorkspaceName} ...");

            // 调用 /workspaces/join API 获取 World 连接信息
            string serverAddress = null;
            int serverPort = 0;
            
            var body = $"{{\"world_id\":\"{EscapeJson(_selectedWorkspaceId)}\"}}";
            var bodyRaw = Encoding.UTF8.GetBytes(body);

            LogRequest("POST", JoinWorldUrl, body, AppSession.Token);

            using (var req = new UnityWebRequest(JoinWorldUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {AppSession.Token}");

                yield return req.SendWebRequest();

                LogResponse(req);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    SetStatus($"Join world failed: {req.error}");
                    _busy = false;
                    yield break;
                }

                if (req.responseCode >= 400)
                {
                    SetStatus($"Join world failed ({req.responseCode}): {req.downloadHandler.text}");
                    _busy = false;
                    yield break;
                }

                // 解析响应：{"status":"ok","world_id":"...","server_address":"...","server_port":7777}
                var json = req.downloadHandler.text;
                try
                {
                    var dto = JsonUtility.FromJson<JoinWorldResponseDto>(json);
                    serverAddress = dto?.server_address;
                    serverPort = dto?.server_port ?? 0;
                }
                catch
                {
                    serverAddress = null;
                    serverPort = 0;
                }

                if (!string.IsNullOrEmpty(serverAddress) && serverPort > 0)
                {
                    Debug.Log($"[BootFlow] World ready: {serverAddress}:{serverPort}");
                }
                else
                {
                    SetStatus("Failed to parse server connection info");
                    _busy = false;
                    yield break;
                }
            }

            // 保存连接信息到 AppSession
            AppSession.SetWorkspace(_selectedWorkspaceId, _selectedWorkspaceName);
            AppSession.SetServerConnection(serverAddress, serverPort);

            SetStatus($"Connecting to {serverAddress}:{serverPort} ...");

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

            // 根据选择的 workspace 决定加载哪个场景
            var sceneToLoad = GetSceneNameForWorkspace(_selectedWorkspaceId, _selectedWorkspaceName);
            Debug.Log($"[BootFlow] Loading scene: {sceneToLoad} (workspace: {_selectedWorkspaceId})");
            
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
            _selectedWorkspaceButton = null;
            for (int i = _workspaceListRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_workspaceListRoot.GetChild(i).gameObject);
            }
        }

        private void BuildFakeWorkspaceList()
        {
            AddWorkspaceItem("ws-fake-001", "Couple Space (fake)");
            AddWorkspaceItem("ws-fake-002", "Home (fake)");
            AddWorkspaceItem("mirror-test", "Mirror Test");
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
                
                // 注意：mirror-test 选项会在 LoadWorkspaces 方法中统一添加
            }
            catch
            {
                SetStatus("Failed to parse workspaces from server.");
            }
        }

        private void AddWorkspaceItem(string id, string name)
        {
            var btn = CreateFilledButton(_workspaceListRoot, $"WorkspaceItem_{id}", name, new Vector2(0f, 56f), new Color(0.09f, 0.086f, 0.20f, 1f), new Color(0.95f, 0.92f, 1f, 0.95f), false);
            var rt = btn.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 52f);
            
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = 24;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(18f, 0f, 18f, 0f);
            
            var layoutElement = btn.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 52;
            layoutElement.preferredHeight = 52;
            layoutElement.flexibleWidth = 1;
            ApplyWorkspaceItemStyle(btn, false);
            
            btn.onClick.AddListener(() =>
            {
                _selectedWorkspaceId = id;
                _selectedWorkspaceName = name;
                HighlightWorkspaceButton(btn);
                SetStatus($"Selected: {name}");
            });
        }

        // ===== helpers =====

        private void SetButtons(bool enabled)
        {
            if (_loginBtn != null) _loginBtn.interactable = enabled;
            if (_registerBtn != null) _registerBtn.interactable = enabled;
            if (_enterBtn != null) _enterBtn.interactable = enabled;
            if (_createSpaceBtn != null) _createSpaceBtn.interactable = enabled;
            if (_createSpaceSubmitBtn != null) _createSpaceSubmitBtn.interactable = enabled;
            if (_createSpaceBackBtn != null) _createSpaceBackBtn.interactable = enabled;
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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void LoadUiResources()
        {
            if (_uiFont == null)
            {
                _uiFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansSC-VariableFont_wght SDF");
                if (_uiFont == null)
                {
                    _uiFont = TMP_Settings.defaultFontAsset;
                }
            }

#if UNITY_EDITOR
            if (loginBackgroundSprite == null)
            {
                loginBackgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/login/romantic_lakeside_night_under_starry_skies.png");
            }

            var preferredLantern = LoadPreferredSprite(
                "Assets/Game/login/magical_lantern_and_glowing_heart_scene_transparent.png",
                "Assets/Game/login/magical_lantern_and_glowing_heart_scene.png");
            if (preferredLantern != null)
            {
                leftLanternSprite = preferredLantern;
            }

            var preferredMascot = LoadPreferredSprite(
                "Assets/Game/login/soft_pastel_cuddle_with_glowing_heart_transparent.png",
                "Assets/Game/login/soft_pastel_cuddle_with_glowing_heart.png");
            if (preferredMascot != null)
            {
                rightMascotSprite = preferredMascot;
            }

            var preferredStillLife = LoadPreferredSprite(
                "Assets/Game/login/magical_cozy_still_life_with_candlelight_transparent.png",
                "Assets/Game/login/magical_cozy_still_life_with_candlelight.png");
            if (preferredStillLife != null)
            {
                rightStillLifeSprite = preferredStillLife;
            }

            if (_ambientEffectSprites == null || _ambientEffectSprites.Length == 0)
            {
                _ambientEffectSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Game/login/effects_transparent.png")
                    .OfType<Sprite>()
                    .Where(sprite => sprite.rect.width <= 200f && sprite.rect.height <= 200f)
                    .OrderBy(sprite => sprite.name)
                    .ToArray();
            }
#endif
        }

        private static Sprite LoadPreferredSprite(string preferredPath, string fallbackPath)
        {
#if UNITY_EDITOR
            var preferred = AssetDatabase.LoadAssetAtPath<Sprite>(preferredPath);
            if (preferred != null)
            {
                return preferred;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(fallbackPath);
#else
            return null;
#endif
        }

        private void CreateDecorationImage(Transform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
        {
            if (sprite == null) return;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, 0.94f);
            image.raycastTarget = false;

            if (size.x > 0f)
            {
                float height = size.x * sprite.rect.height / sprite.rect.width;
                rt.sizeDelta = new Vector2(size.x, height);
            }
            else if (size.y > 0f)
            {
                float width = size.y * sprite.rect.width / sprite.rect.height;
                rt.sizeDelta = new Vector2(width, size.y);
            }
            else
            {
                rt.sizeDelta = sprite.rect.size;
            }
        }

        private void CreateAmbientEffects(Transform parent)
        {
            if (_ambientEffectSprites == null || _ambientEffectSprites.Length == 0)
            {
                return;
            }

            CreateAmbientEffect(parent, "EffectTitleLeft", _ambientEffectSprites[0], new Vector2(0.5f, 1f), new Vector2(-286f, -112f), 26f, new Color(1f, 0.88f, 0.98f, 0.26f), 0f);
            CreateAmbientEffect(parent, "EffectTitleRight", _ambientEffectSprites[Mathf.Min(1, _ambientEffectSprites.Length - 1)], new Vector2(0.5f, 1f), new Vector2(318f, -180f), 22f, new Color(0.92f, 0.85f, 1f, 0.22f), 0.8f);
            CreateAmbientEffect(parent, "EffectCardLeft", _ambientEffectSprites[Mathf.Min(2, _ambientEffectSprites.Length - 1)], new Vector2(0.5f, 0.5f), new Vector2(-344f, -4f), 18f, new Color(1f, 0.84f, 0.95f, 0.18f), 1.5f);
            CreateAmbientEffect(parent, "EffectCardRight", _ambientEffectSprites[Mathf.Min(3, _ambientEffectSprites.Length - 1)], new Vector2(0.5f, 0.5f), new Vector2(352f, 84f), 20f, new Color(0.9f, 0.84f, 1f, 0.18f), 2.1f);
        }

        private void CreateAmbientEffect(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, float width, Color color, float phaseOffset)
        {
            if (sprite == null) return;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(width, width * sprite.rect.height / sprite.rect.width);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;

            var pulse = go.AddComponent<AmbientPulse>();
            pulse.Initialize(image, rt, phaseOffset);
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private GameObject CreateInnerFill(Transform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        private TMP_Text CreateText(Transform parent, string text, float size, FontStyles style)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            if (_uiFont != null)
            {
                tmp.font = _uiFont;
            }
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.enableWordWrapping = false;
            tmp.richText = false;
            return tmp;
        }

        private TMP_InputField CreateInput(Transform parent, string name, string placeholder, bool isPassword = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.SetActive(true);
            
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(420, 72);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.09f, 0.086f, 0.20f, 0.85f);
            img.raycastTarget = true;

            var input = go.AddComponent<TMP_InputField>();
            input.interactable = true;
            input.enabled = true;
            input.customCaretColor = true;
            input.caretColor = new Color(1f, 0.82f, 0.94f, 0.95f);
            input.selectionColor = new Color(0.86f, 0.68f, 1f, 0.30f);

            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(go.transform, false);
            var textAreaRT = textArea.AddComponent<RectTransform>();
            textAreaRT.anchorMin = new Vector2(0, 0);
            textAreaRT.anchorMax = new Vector2(1, 1);
            textAreaRT.offsetMin = new Vector2(18, 8);
            textAreaRT.offsetMax = new Vector2(-18, -8);
            textArea.AddComponent<RectMask2D>();

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textArea.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            Stretch(textRT);
            var text = textGO.AddComponent<TextMeshProUGUI>();
            if (_uiFont != null)
            {
                text.font = _uiFont;
            }
            text.fontSize = 30;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            
            input.textComponent = text;
            input.textViewport = textAreaRT;

            if (!string.IsNullOrEmpty(placeholder))
            {
                var phGO = new GameObject("Placeholder");
                phGO.transform.SetParent(textArea.transform, false);
                var phRT = phGO.AddComponent<RectTransform>();
                Stretch(phRT);
                var phText = phGO.AddComponent<TextMeshProUGUI>();
                if (_uiFont != null)
                {
                    phText.font = _uiFont;
                }
                phText.text = placeholder;
                phText.fontSize = 28;
                phText.color = new Color(0.717f, 0.682f, 0.847f, 1f);
                phText.fontStyle = FontStyles.Normal;
                phText.alignment = TextAlignmentOptions.MidlineLeft;
                phText.raycastTarget = false;
                phText.enableWordWrapping = false;
                input.placeholder = phText;
            }

            input.textViewport = textAreaRT;
            input.textComponent = text;
            input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.characterLimit = 0;
            input.readOnly = false;
            return input;
        }

        private Button CreateFilledButton(Transform parent, string name, string text, Vector2 size, Color normalColor, Color textColor, bool addColorState = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = normalColor;

            var btn = go.AddComponent<Button>();
            if (addColorState)
            {
                ConfigureButtonColors(btn, normalColor);
            }
            else
            {
                btn.transition = Selectable.Transition.None;
            }

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var labelRT = label.AddComponent<RectTransform>();
            Stretch(labelRT);

            var tmp = label.AddComponent<TextMeshProUGUI>();
            if (_uiFont != null)
            {
                tmp.font = _uiFont;
            }
            tmp.text = text;
            tmp.fontSize = 30;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
        }

        private Button CreateGhostButton(Transform parent, string name, string text, Vector2 size, Color borderColor, Color fillColor, Color textColor)
        {
            var button = CreateFilledButton(parent, name, text, size, borderColor, textColor);
            var rt = button.GetComponent<RectTransform>();

            var fill = new GameObject("Fill");
            fill.transform.SetParent(button.transform, false);
            fill.transform.SetAsFirstSibling();
            var fillRect = fill.AddComponent<RectTransform>();
            Stretch(fillRect);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;

            return button;
        }

        private static void PositionRow(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -y);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void ConfigureButtonColors(Button btn, Color normalColor)
        {
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            btn.colors = colors;

            var state = btn.gameObject.AddComponent<ButtonColorState>();
            state.Initialize(normalColor);
        }

        private void ApplyWorkspaceItemStyle(Button btn, bool selected)
        {
            var img = btn.GetComponent<Image>();
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (img != null)
            {
                img.color = selected
                    ? new Color(0.33f, 0.23f, 0.56f, 1f)
                    : new Color(0.09f, 0.086f, 0.20f, 1f);
            }

            if (label != null)
            {
                label.color = selected
                    ? new Color(1f, 0.97f, 1f, 1f)
                    : new Color(0.95f, 0.92f, 1f, 0.95f);
            }
        }

        private void HighlightWorkspaceButton(Button btn)
        {
            if (_selectedWorkspaceButton != null && _selectedWorkspaceButton != btn)
            {
                ApplyWorkspaceItemStyle(_selectedWorkspaceButton, false);
            }

            _selectedWorkspaceButton = btn;
            ApplyWorkspaceItemStyle(btn, true);
        }

        private sealed class ButtonColorState : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
        {
            private Image _image;
            private Color _normalColor;
            private Color _hoverColor;
            private Color _pressedColor;
            private bool _isPointerInside;

            public void Initialize(Color normalColor)
            {
                _image = GetComponent<Image>();
                _normalColor = normalColor;
                _hoverColor = Color.Lerp(normalColor, Color.white, 0.08f);
                _pressedColor = Color.Lerp(normalColor, Color.black, 0.10f);
                if (_image != null)
                {
                    _image.color = _normalColor;
                }
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _isPointerInside = true;
                if (_image != null) _image.color = _hoverColor;
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _isPointerInside = false;
                if (_image != null) _image.color = _normalColor;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (_image != null) _image.color = _pressedColor;
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                if (_image != null) _image.color = _isPointerInside ? _hoverColor : _normalColor;
            }
        }

        private sealed class AmbientPulse : MonoBehaviour
        {
            private Image _image;
            private RectTransform _rectTransform;
            private Color _baseColor;
            private Vector2 _baseSize;
            private float _phaseOffset;

            public void Initialize(Image image, RectTransform rectTransform, float phaseOffset)
            {
                _image = image;
                _rectTransform = rectTransform;
                _baseColor = image.color;
                _baseSize = rectTransform.sizeDelta;
                _phaseOffset = phaseOffset;
            }

            private void Update()
            {
                if (_image == null || _rectTransform == null)
                {
                    return;
                }

                float t = (Mathf.Sin(Time.unscaledTime * 1.2f + _phaseOffset) + 1f) * 0.5f;
                var color = _baseColor;
                color.a = Mathf.Lerp(_baseColor.a * 0.68f, _baseColor.a, t);
                _image.color = color;
                _rectTransform.sizeDelta = _baseSize * Mathf.Lerp(0.94f, 1.04f, t);
            }
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
        /// 记录 HTTP 请求详情
        /// </summary>
        private static void LogRequest(string method, string url, string body = null, string token = null)
        {
            Debug.Log("=== HTTP REQUEST ===");
            Debug.Log($"Method: {method}");
            Debug.Log($"URL: {url}");
            if (!string.IsNullOrEmpty(token))
            {
                Debug.Log($"Authorization: Bearer {token.Substring(0, Math.Min(10, token.Length))}...");
            }
            if (!string.IsNullOrEmpty(body))
            {
                Debug.Log($"Body: {body}");
            }
            Debug.Log("====================");
        }

        /// <summary>
        /// 记录 HTTP 响应详情
        /// </summary>
        private static void LogResponse(UnityWebRequest req)
        {
            Debug.Log("=== HTTP RESPONSE ===");
            Debug.Log($"Status: {req.responseCode}");
            Debug.Log($"Result: {req.result}");
            if (!string.IsNullOrEmpty(req.error))
            {
                Debug.Log($"Error: {req.error}");
            }
            if (req.downloadHandler != null && !string.IsNullOrEmpty(req.downloadHandler.text))
            {
                var text = req.downloadHandler.text;
                if (text.Length > 500)
                {
                    Debug.Log($"Response: {text.Substring(0, 500)}... (truncated, total {text.Length} chars)");
                }
                else
                {
                    Debug.Log($"Response: {text}");
                }
            }
            Debug.Log("=====================");
        }

        /// <summary>
        /// 根据所选 workspace 决定要进入的场景名。
        /// 当前版本：所有空间都进入 <see cref="mainSceneName"/>（默认 Playground）。
        /// 后续如果你有不同类型的空间，可以在这里根据 id/name 做映射。
        /// </summary>
        private string GetSceneNameForWorkspace(string workspaceId, string workspaceName)
        {
            // 特殊处理：mirror-test 选项加载 MirrorTestScene
            if (workspaceId == "mirror-test")
            {
                return "MirrorTestScene";
            }
            
            // 默认：其他所有 workspace 都进入 MainScene
            return "MainScene";
        }
    }
}
// {"world_id": "ws-89964cb4533f", "version": 1, "objects": [{"object_id": "e6ab608c-5e1e-4102-844d-1639ec63a80a", "prefab_id": "Placeables/generated_639050625596004370", "comment": "", "pos_x": -8.003462791442871, "pos_y": -0.1170467734336853, "pos_z": 1.088456392288208, "rot_x": 0.0, "rot_y": 0.0, "rot_z": 0.0, "rot_w": 1.0, "scale_x": 1.0, "scale_y": 1.0, "scale_z": 1.0}]}
