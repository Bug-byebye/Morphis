using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using GLTFast;
using TMPro;

/// <summary>
/// Runtime Text-to-3D Pipeline Manager
/// 处理 UI 显示/隐藏、发送请求到后端、加载并实例化 GLB 模型
/// </summary>
public class AIGeneratorPipeline : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "";
    
    [Header("UI References")]
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private TMP_InputField promptInput;
    [SerializeField] private Button generateButton;
    [SerializeField] private TMP_Text statusText;
    
    [Header("Generation Settings")]
    [SerializeField] private float spawnDistance = 2f;
    
    private bool isUIVisible = false;
    private bool isGenerating = false;
    
    // 记录原始的光标锁定状态
    private CursorLockMode originalLockMode;
    private bool originalCursorVisible;
    
    void Start()
    {
        // 初始化隐藏 UI
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
        }
        
        // 绑定按钮点击事件
        if (generateButton != null)
        {
            generateButton.onClick.AddListener(OnGenerateClicked);
        }
        
        UpdateStatus("Ready. Press Tab to open UI.");
    }
    
    void Update()
    {
        // Tab 键切换 UI 显示 (使用新 Input System)
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleUI();
        }
    }
    
    /// <summary>
    /// 切换 UI 显示/隐藏，并处理鼠标光标状态
    /// </summary>
    public void ToggleUI()
    {
        isUIVisible = !isUIVisible;
        
        if (isUIVisible)
        {
            // 显示 UI，解锁鼠标
            originalLockMode = Cursor.lockState;
            originalCursorVisible = Cursor.visible;
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 隐藏 UI，恢复鼠标状态
            Cursor.lockState = originalLockMode;
            Cursor.visible = originalCursorVisible;
        }
        
        if (uiPanel != null)
        {
            uiPanel.SetActive(isUIVisible);
        }
    }
    
    /// <summary>
    /// 点击生成按钮
    /// </summary>
    private void OnGenerateClicked()
    {
        if (isGenerating)
        {
            UpdateStatus("Already generating...");
            return;
        }
        
        string prompt = promptInput != null ? promptInput.text : "";
        if (string.IsNullOrWhiteSpace(prompt))
        {
            UpdateStatus("Please enter a prompt.");
            return;
        }
        
        StartCoroutine(SendGenerateRequest(prompt));
    }
    
    /// <summary>
    /// 发送生成请求到后端
    /// </summary>
    private IEnumerator SendGenerateRequest(string prompt)
    {
        isGenerating = true;
        UpdateStatus($"Generating: {prompt}...");
        
        // 构建 JSON 请求
        string jsonBody = $"{{\"prompt\": \"{EscapeJson(prompt)}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        
        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                UpdateStatus($"Error: {request.error}");
                Debug.LogError($"[AIGenerator] Request failed: {request.error}");
                isGenerating = false;
                yield break;
            }
            
            // 获取返回的 GLB 数据
            byte[] glbData = request.downloadHandler.data;
            UpdateStatus($"Received {glbData.Length} bytes. Loading model...");
            
            // 加载 GLB 模型
            yield return LoadGlbAndInstantiate(glbData);
        }
        
        isGenerating = false;
    }
    
    /// <summary>
    /// 使用 glTFast 加载 GLB 数据并在预览窗口显示
    /// </summary>
    private IEnumerator LoadGlbAndInstantiate(byte[] glbData)
    {
        var gltf = new GltfImport();
        
        // 异步加载 GLB
        var loadTask = gltf.LoadGltfBinary(glbData);
        while (!loadTask.IsCompleted)
        {
            yield return null;
        }
        
        if (!loadTask.Result)
        {
            UpdateStatus("Failed to load GLB model.");
            Debug.LogError("[AIGenerator] Failed to parse GLB data");
            yield break;
        }
        
        // 缓存 GLB 数据
        cachedGlbData = glbData;
        
        // 创建预览环境（远离主场景）
        CleanupPreview();
        previewRoot = new GameObject("PreviewEnvironment");
        previewRoot.transform.position = new Vector3(1000, 1000, 1000);
        
        // 实例化模型到预览环境
        GameObject modelObj = new GameObject("PreviewModel");
        modelObj.transform.SetParent(previewRoot.transform);
        modelObj.transform.localPosition = Vector3.zero;
        
        var instantiateTask = gltf.InstantiateMainSceneAsync(modelObj.transform);
        while (!instantiateTask.IsCompleted)
        {
            yield return null;
        }
        
        // 调整模型大小
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
        
        previewCamera = camObj.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
        previewCamera.fieldOfView = 40f;
        
        if (previewRT == null)
            previewRT = new RenderTexture(400, 400, 24);
        previewCamera.targetTexture = previewRT;
        
        // 添加灯光
        GameObject lightObj = new GameObject("PreviewLight");
        lightObj.transform.SetParent(previewRoot.transform);
        lightObj.transform.localPosition = new Vector3(2, 3, -2);
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        
        // 显示预览窗口
        ShowPreviewWindow();
        
        UpdateStatus("Model ready! Check preview window.");
        Debug.Log($"[AIGenerator] Model loaded in preview window");
    }
    
    // 预览相关变量
    private byte[] cachedGlbData;
    private RenderTexture previewRT;
    private Camera previewCamera;
    private GameObject previewRoot;
    private GameObject previewWindow;
    
    private void ShowPreviewWindow()
    {
        if (previewWindow != null)
        {
            previewWindow.SetActive(true);
            return;
        }
        
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        
        previewWindow = new GameObject("ModelPreviewWindow");
        previewWindow.transform.SetParent(canvas.transform, false);
        
        RectTransform rect = previewWindow.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(420, 500);
        rect.anchoredPosition = Vector2.zero;
        
        Image bg = previewWindow.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
        
        // 标题
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(previewWindow.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 40);
        
        Image titleBg = titleObj.AddComponent<Image>();
        titleBg.color = new Color(0.3f, 0.3f, 0.3f);
        
        GameObject titleTextObj = new GameObject("Text");
        titleTextObj.transform.SetParent(titleObj.transform, false);
        TMP_Text titleText = titleTextObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "3D Model Preview";
        titleText.fontSize = 18;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleTextObj.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        titleTextObj.GetComponent<RectTransform>().anchorMax = Vector2.one;
        titleTextObj.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
        
        // 预览图像
        GameObject imgObj = new GameObject("Preview");
        imgObj.transform.SetParent(previewWindow.transform, false);
        RectTransform imgRect = imgObj.AddComponent<RectTransform>();
        imgRect.anchorMin = new Vector2(0.5f, 0.5f);
        imgRect.anchorMax = new Vector2(0.5f, 0.5f);
        imgRect.sizeDelta = new Vector2(400, 400);
        imgRect.anchoredPosition = new Vector2(0, 10);
        
        RawImage rawImg = imgObj.AddComponent<RawImage>();
        rawImg.texture = previewRT;
        
        // 按钮区域
        GameObject btnsObj = new GameObject("Buttons");
        btnsObj.transform.SetParent(previewWindow.transform, false);
        RectTransform btnsRect = btnsObj.AddComponent<RectTransform>();
        btnsRect.anchorMin = new Vector2(0, 0);
        btnsRect.anchorMax = new Vector2(1, 0);
        btnsRect.pivot = new Vector2(0.5f, 0);
        btnsRect.sizeDelta = new Vector2(0, 50);
        
        HorizontalLayoutGroup hlg = btnsObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.padding = new RectOffset(10, 10, 5, 5);
        hlg.childControlWidth = true;
        hlg.childForceExpandWidth = true;
        
        CreateButton(btnsObj.transform, "Place in Scene", new Color(0.3f, 0.7f, 0.3f), OnPlaceInScene);
        CreateButton(btnsObj.transform, "Close", new Color(0.5f, 0.5f, 0.5f), OnClosePreview);
    }
    
    private void CreateButton(Transform parent, string text, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(text);
        btnObj.transform.SetParent(parent, false);
        
        Image img = btnObj.AddComponent<Image>();
        img.color = color;
        
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        
        btnObj.AddComponent<LayoutElement>().preferredHeight = 40;
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        TMP_Text txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = 16;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        textObj.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        textObj.GetComponent<RectTransform>().anchorMax = Vector2.one;
        textObj.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
    }
    
    private void OnPlaceInScene()
    {
        if (cachedGlbData == null) return;
        StartCoroutine(PlaceInScene());
    }
    
    private IEnumerator PlaceInScene()
    {
        var gltf = new GltfImport();
        var loadTask = gltf.LoadGltfBinary(cachedGlbData);
        while (!loadTask.IsCompleted) yield return null;
        if (!loadTask.Result) yield break;
        
        Vector3 pos = Camera.main.transform.position + Camera.main.transform.forward * spawnDistance;
        GameObject obj = new GameObject("GeneratedModel_" + DateTime.Now.Ticks);
        obj.transform.position = pos;
        
        var instTask = gltf.InstantiateMainSceneAsync(obj.transform);
        while (!instTask.IsCompleted) yield return null;
        
        UpdateStatus("Model placed in scene!");
        OnClosePreview();
    }
    
    private void OnClosePreview()
    {
        if (previewWindow != null) previewWindow.SetActive(false);
        CleanupPreview();
    }
    
    private void CleanupPreview()
    {
        if (previewRoot != null)
        {
            Destroy(previewRoot);
            previewRoot = null;
        }
    }
    
    private Bounds CalculateBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }
    
    /// <summary>
    /// 更新状态文本
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[AIGenerator] {message}");
    }
    
    /// <summary>
    /// 转义 JSON 字符串中的特殊字符
    /// </summary>
    private string EscapeJson(string str)
    {
        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }
}
