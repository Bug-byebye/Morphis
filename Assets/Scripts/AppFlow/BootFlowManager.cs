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
        [SerializeField] private string mainSceneName = "Playground";
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

        private void Awake()
        {
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
        }

        private void Start()
        {
            BuildUI();
            ShowLogin();
            
            // 初始状态：禁用玩家输入，把控制权给 Login UI，避免光标被抢占
            SetPlayerInputEnabled(false);
            
            // 先用英文，避免 TMP 默认字体缺中文导致的警告刷屏；后续我们再接入中文字体资源。
            SetStatus("Enter username/password (default: 111111 / 111111)");
        }

        private void BuildUI()
        {
            EnsureEventSystem();

            // Canvas
            var canvasGO = new GameObject("BootCanvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            DontDestroyOnLoad(canvasGO);

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(_canvas.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            Stretch(bgRect);
            var bgImg = bg.AddComponent<Image>();
            
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
            _skyboxCamera.depth = 0; 
            
            var skybox = camGO.AddComponent<Skybox>();
            skybox.material = backgroundSkybox;
            
            _skyboxCamera.cullingMask = 0; 
            
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

            var header = CreateText(panel.transform, "Select Workspace", 18, FontStyles.Bold);
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 50);
            headerRect.anchoredPosition = new Vector2(0, -20);
            header.alignment = TextAlignmentOptions.Center;

            var listBox = new GameObject("WorkspaceList");
            listBox.transform.SetParent(panel.transform, false);
            var listRect = listBox.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.5f, 0.5f);
            listRect.anchorMax = new Vector2(0.5f, 0.5f);
            listRect.pivot = new Vector2(0.5f, 0.5f);
            listRect.sizeDelta = new Vector2(480, 260);
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
            var enterRect = _enterBtn.GetComponent<RectTransform>();
            enterRect.anchorMin = new Vector2(0.5f, 0);
            enterRect.anchorMax = new Vector2(0.5f, 0);
            enterRect.pivot = new Vector2(0.5f, 0);
            enterRect.sizeDelta = new Vector2(240, 46);
            enterRect.anchoredPosition = new Vector2(0, 90);
            _enterBtn.onClick.AddListener(() => { if (!_busy) StartCoroutine(EnterMainScene()); });

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

            var sceneToLoad = GetSceneNameForWorkspace(_selectedWorkspaceId, _selectedWorkspaceName);
            var op = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);
            
            if (op == null)
            {
                SetStatus($"Error: Scene '{sceneToLoad}' not found! Add it to Build Settings.");
                _busy = false;
                yield break;
            }
            
            while (!op.isDone)
            {
                yield return null;
            }

            // 进入主场景后隐藏 UI（也可以销毁）
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_skyboxCamera != null) _skyboxCamera.gameObject.SetActive(false);

            // 主场景默认是第三人称：把鼠标锁回去（玩家可用右键/Tab 等切换 UI 时再解锁）
            SetCursorForUI(false);
            SetPlayerInputEnabled(true);
            _busy = false;
        }

        private void SetPlayerInputEnabled(bool enabled)
        {
#if ENABLE_INPUT_SYSTEM
            var playerInputs = FindObjectsOfType<UnityEngine.InputSystem.PlayerInput>();
            foreach (var pi in playerInputs)
            {
                pi.enabled = enabled;
            }
#endif
            // 尝试禁用 StarterAssetsInputs（它是 MonoBehaviour，负责光标锁定逻辑）
            var starterInputs = FindObjectsOfType<MonoBehaviour>().Where(m => m.GetType().Name == "StarterAssetsInputs").ToArray();
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
            rt.sizeDelta = new Vector2(0, 46);
            
            // Add LayoutElement for VerticalLayoutGroup to work properly
            var layoutElement = btn.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 46;
            layoutElement.preferredHeight = 46;
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
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
                UnityEngine.Object.DontDestroyOnLoad(go);
            }

            // check input module
#if ENABLE_INPUT_SYSTEM
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                // Remove old module if exists
                var old = es.GetComponent<StandaloneInputModule>();
                if (old != null) DestroyImmediate(old);

                var uiModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
                uiModule.actionsAsset = CreateMinimalUIActions();
            }
#else
            if (es.GetComponent<StandaloneInputModule>() == null)
            {
                 es.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
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

            var point = map.AddAction("Point", InputActionType.PassThrough, "<Pointer>/position");
            var leftClick = map.AddAction("LeftClick", InputActionType.PassThrough, "<Pointer>/press");
            var rightClick = map.AddAction("RightClick", InputActionType.PassThrough, "<Mouse>/rightButton");
            var middleClick = map.AddAction("MiddleClick", InputActionType.PassThrough, "<Mouse>/middleButton");
            var scroll = map.AddAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll");

            // 键盘/手柄导航（可选，但给 submit/cancel 留好）
            var move = map.AddAction("Navigate", InputActionType.PassThrough);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a").With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d").With("Right", "<Keyboard>/rightArrow");

            var submit = map.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/numpadEnter");
            submit.AddBinding("<Gamepad>/buttonSouth");

            var cancel = map.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");

            asset.AddActionMap(map);
            asset.Enable();

            // 将 map/actions 绑定到 InputSystemUIInputModule 需要的标准字段名
            //（字段名是序列化引用，运行时只要 actionsAsset 内有对应 action 名称即可）
            return asset;
        }
#endif

        private static void SetCursorForUI(bool uiMode)
        {
            if (uiMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
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
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(420, 44);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.14f, 0.14f, 0.18f, 1f);

            var input = go.AddComponent<TMP_InputField>();

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
                input.placeholder = phText;
            }

            input.textViewport = textAreaRT;
            input.textComponent = text;
            input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;

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
            // 示例：如果以后有不同空间类型，可以这样分支：
            // if (workspaceId.StartsWith("ws-love")) return "Playground";
            // if (workspaceId.StartsWith("ws-dev")) return "SampleScene";
            // 目前统一进入配置的主场景。
            return string.IsNullOrEmpty(mainSceneName) ? "Playground" : mainSceneName;
        }
    }
}

