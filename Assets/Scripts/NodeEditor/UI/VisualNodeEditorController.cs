using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using AIPipeline.Nodes;

namespace AIPipeline.UI
{
    /// <summary>
    /// 可视化节点编辑器主控制器
    /// 处理 Tab 开关、管线执行、玩家输入
    /// </summary>
    public class VisualNodeEditorController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject editorRoot;
        [SerializeField] private VisualNodeCanvas nodeCanvas;
        [SerializeField] private Button executeButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private TMP_Text statusText;
        
        [Header("Settings")]
        [SerializeField] private string serverUrl = "http://localhost:8000";
        
        private bool isVisible = false;
        private CursorLockMode savedLockMode;
        private bool savedCursorVisible;
        private PlayerInput playerInput;
        
        void Start()
        {
            if (editorRoot != null)
                editorRoot.SetActive(false);
            
            if (executeButton != null)
                executeButton.onClick.AddListener(OnExecuteClicked);
            
            if (clearButton != null)
                clearButton.onClick.AddListener(OnClearClicked);
            
            playerInput = FindObjectOfType<PlayerInput>();
            
            UpdateStatus("Press Tab to open Node Editor");
        }
        
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleEditor();
            }
        }
        
        public void ToggleEditor()
        {
            isVisible = !isVisible;
            
            if (isVisible)
            {
                savedLockMode = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                
                if (playerInput != null)
                    playerInput.enabled = false;
            }
            else
            {
                Cursor.lockState = savedLockMode;
                Cursor.visible = savedCursorVisible;
                
                if (playerInput != null)
                    playerInput.enabled = true;
            }
            
            if (editorRoot != null)
                editorRoot.SetActive(isVisible);
        }
        
        private void OnExecuteClicked()
        {
            if (nodeCanvas == null || nodeCanvas.nodes.Count == 0)
            {
                UpdateStatus("No nodes! Right-click to add nodes.");
                return;
            }
            
            UpdateStatus("Executing pipeline...");
            
            // 创建并执行管线
            ExecutePipeline();
        }
        
        private void ExecutePipeline()
        {
            // 找到起始节点（TextInput）和终止节点（Preview）
            VisualNode startNode = null;
            VisualNode endNode = null;
            
            foreach (var node in nodeCanvas.nodes)
            {
                if (node.nodeTitle.Contains("Text Input"))
                    startNode = node;
                if (node.nodeTitle.Contains("Preview"))
                    endNode = node;
            }
            
            if (startNode == null)
            {
                UpdateStatus("Add a Text Input node first!");
                return;
            }
            
            // 获取输入文本
            var inputField = startNode.GetComponentInChildren<TMP_InputField>();
            string prompt = inputField != null ? inputField.text : "test";
            
            if (string.IsNullOrWhiteSpace(prompt))
            {
                UpdateStatus("Enter a prompt in the Text Input node");
                return;
            }
            
            // 执行 Text23D 请求
            StartCoroutine(ExecuteText23D(prompt));
        }
        
        private System.Collections.IEnumerator ExecuteText23D(string prompt)
        {
            UpdateStatus($"Generating: {prompt}...");
            
            string jsonBody = $"{{\"prompt\": \"{prompt}\"}}";
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            
            using (var request = new UnityEngine.Networking.UnityWebRequest(serverUrl + "/generate", "POST"))
            {
                request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                yield return request.SendWebRequest();
                
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    UpdateStatus($"Error: {request.error}");
                    yield break;
                }
                
                byte[] glbData = request.downloadHandler.data;
                UpdateStatus($"Received {glbData.Length} bytes. Loading...");
                
                // 加载模型
                yield return LoadModel(glbData);
            }
        }
        
        private System.Collections.IEnumerator LoadModel(byte[] glbData)
        {
            var gltf = new GLTFast.GltfImport();
            var loadTask = gltf.LoadGltfBinary(glbData);
            
            while (!loadTask.IsCompleted)
                yield return null;
            
            if (!loadTask.Result)
            {
                UpdateStatus("Failed to load model");
                yield break;
            }
            
            // 缓存 GLB 数据用于之后放置到场景
            cachedGlbData = glbData;
            
            // 创建预览环境（远离主场景）
            GameObject previewRoot = new GameObject("PreviewEnvironment");
            previewRoot.transform.position = new Vector3(1000, 1000, 1000);
            currentPreviewRoot = previewRoot;
            
            // 实例化模型到预览环境
            GameObject modelObj = new GameObject("PreviewModel");
            modelObj.transform.SetParent(previewRoot.transform);
            modelObj.transform.localPosition = Vector3.zero;
            
            var instantiateTask = gltf.InstantiateMainSceneAsync(modelObj.transform);
            while (!instantiateTask.IsCompleted)
                yield return null;
            
            // 计算边界并调整缩放
            Bounds bounds = CalculateBounds(modelObj);
            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize > 0)
            {
                float scale = 2f / maxSize;
                modelObj.transform.localScale = Vector3.one * scale;
                modelObj.transform.localPosition = -bounds.center * scale;
            }
            
            // 创建预览相机
            GameObject camObj = new GameObject("PreviewCamera");
            camObj.transform.SetParent(previewRoot.transform);
            camObj.transform.localPosition = new Vector3(0, 0.5f, -4f);
            camObj.transform.LookAt(previewRoot.transform.position);
            
            Camera previewCam = camObj.AddComponent<Camera>();
            previewCam.clearFlags = CameraClearFlags.SolidColor;
            previewCam.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
            previewCam.fieldOfView = 40f;
            previewCam.nearClipPlane = 0.1f;
            previewCam.farClipPlane = 100f;
            
            // 创建 RenderTexture
            if (previewRT == null)
                previewRT = new RenderTexture(400, 400, 24);
            previewCam.targetTexture = previewRT;
            currentPreviewCamera = previewCam;
            
            // 添加灯光
            GameObject lightObj = new GameObject("PreviewLight");
            lightObj.transform.SetParent(previewRoot.transform);
            lightObj.transform.localPosition = new Vector3(2, 3, -2);
            lightObj.transform.LookAt(previewRoot.transform.position);
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            
            // 显示预览窗口 UI
            ShowPreviewWindow();
            
            UpdateStatus("✨ Model ready! Preview in window.");
        }
        
        // 预览相关变量
        private byte[] cachedGlbData;
        private RenderTexture previewRT;
        private Camera currentPreviewCamera;
        private GameObject currentPreviewRoot;
        private GameObject previewWindowUI;
        
        private void ShowPreviewWindow()
        {
            if (previewWindowUI != null)
            {
                previewWindowUI.SetActive(true);
                return;
            }
            
            // 创建预览窗口 UI
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("PreviewCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            
            previewWindowUI = new GameObject("PreviewWindow");
            previewWindowUI.transform.SetParent(canvas.transform, false);
            
            RectTransform rect = previewWindowUI.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420, 500);
            rect.anchoredPosition = Vector2.zero;
            
            // 背景
            Image bg = previewWindowUI.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            
            // 标题栏
            GameObject titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(previewWindowUI.transform, false);
            RectTransform titleRect = titleBar.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.sizeDelta = new Vector2(0, 40);
            titleRect.anchoredPosition = Vector2.zero;
            
            Image titleBg = titleBar.AddComponent<Image>();
            titleBg.color = new Color(0.3f, 0.3f, 0.3f);
            
            GameObject titleTextObj = new GameObject("Title");
            titleTextObj.transform.SetParent(titleBar.transform, false);
            TMP_Text titleText = titleTextObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "3D Model Preview";
            titleText.fontSize = 18;
            titleText.fontStyle = TMPro.FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            RectTransform titleTextRect = titleTextObj.GetComponent<RectTransform>();
            titleTextRect.anchorMin = Vector2.zero;
            titleTextRect.anchorMax = Vector2.one;
            titleTextRect.sizeDelta = Vector2.zero;
            
            // 预览图像
            GameObject previewImageObj = new GameObject("PreviewImage");
            previewImageObj.transform.SetParent(previewWindowUI.transform, false);
            RectTransform imgRect = previewImageObj.AddComponent<RectTransform>();
            imgRect.anchorMin = new Vector2(0.5f, 0.5f);
            imgRect.anchorMax = new Vector2(0.5f, 0.5f);
            imgRect.sizeDelta = new Vector2(400, 400);
            imgRect.anchoredPosition = new Vector2(0, 10);
            
            RawImage rawImage = previewImageObj.AddComponent<RawImage>();
            rawImage.texture = previewRT;
            
            // 按钮区域
            GameObject buttonArea = new GameObject("Buttons");
            buttonArea.transform.SetParent(previewWindowUI.transform, false);
            RectTransform btnAreaRect = buttonArea.AddComponent<RectTransform>();
            btnAreaRect.anchorMin = new Vector2(0, 0);
            btnAreaRect.anchorMax = new Vector2(1, 0);
            btnAreaRect.pivot = new Vector2(0.5f, 0);
            btnAreaRect.sizeDelta = new Vector2(0, 50);
            btnAreaRect.anchoredPosition = Vector2.zero;
            
            HorizontalLayoutGroup hlg = buttonArea.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.padding = new RectOffset(10, 10, 5, 5);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = true;
            
            // Place in Scene 按钮
            CreatePreviewButton(buttonArea.transform, "Place in Scene", new Color(0.3f, 0.7f, 0.3f), OnPlaceInScene);
            
            // Close 按钮
            CreatePreviewButton(buttonArea.transform, "Close", new Color(0.5f, 0.5f, 0.5f), OnClosePreview);
        }
        
        private void CreatePreviewButton(Transform parent, string text, Color color, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(text + "Btn");
            btnObj.transform.SetParent(parent, false);
            
            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = color;
            
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(onClick);
            
            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 40;
            
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            TMP_Text btnText = textObj.AddComponent<TextMeshProUGUI>();
            btnText.text = text;
            btnText.fontSize = 16;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
        }
        
        private void OnPlaceInScene()
        {
            if (cachedGlbData == null) return;
            StartCoroutine(PlaceModelInScene());
        }
        
        private System.Collections.IEnumerator PlaceModelInScene()
        {
            var gltf = new GLTFast.GltfImport();
            var loadTask = gltf.LoadGltfBinary(cachedGlbData);
            while (!loadTask.IsCompleted) yield return null;
            
            if (!loadTask.Result) yield break;
            
            Vector3 spawnPos = Camera.main.transform.position + Camera.main.transform.forward * 3f;
            GameObject modelObj = new GameObject("GeneratedModel_" + System.DateTime.Now.Ticks);
            modelObj.transform.position = spawnPos;
            
            var instantiateTask = gltf.InstantiateMainSceneAsync(modelObj.transform);
            while (!instantiateTask.IsCompleted) yield return null;
            
            UpdateStatus("Model placed in scene!");
            OnClosePreview();
        }
        
        private void OnClosePreview()
        {
            if (previewWindowUI != null)
                previewWindowUI.SetActive(false);
            
            if (currentPreviewRoot != null)
            {
                Destroy(currentPreviewRoot);
                currentPreviewRoot = null;
            }
        }
        
        private Bounds CalculateBounds(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(obj.transform.position, Vector3.one);
            
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            return bounds;
        }
        
        private void OnClearClicked()
        {
            if (nodeCanvas != null)
            {
                foreach (var node in nodeCanvas.nodes)
                {
                    if (node != null)
                        Destroy(node.gameObject);
                }
                nodeCanvas.nodes.Clear();
                
                foreach (var conn in nodeCanvas.connections)
                {
                    if (conn != null)
                        Destroy(conn.gameObject);
                }
                nodeCanvas.connections.Clear();
            }
            UpdateStatus("Canvas cleared");
        }
        
        private void UpdateStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
            Debug.Log($"[NodeEditor] {message}");
        }
    }
}
