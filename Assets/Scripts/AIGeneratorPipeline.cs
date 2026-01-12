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
    [SerializeField] private string serverUrl = "http://localhost:8000/generate";
    
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
    /// 使用 glTFast 加载 GLB 数据并实例化
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
        
        // 计算生成位置（相机前方）
        Vector3 spawnPosition = Camera.main.transform.position + 
                                Camera.main.transform.forward * spawnDistance;
        
        // 创建父物体
        GameObject modelParent = new GameObject("GeneratedModel_" + DateTime.Now.Ticks);
        modelParent.transform.position = spawnPosition;
        
        // 实例化模型
        var instantiateTask = gltf.InstantiateMainSceneAsync(modelParent.transform);
        while (!instantiateTask.IsCompleted)
        {
            yield return null;
        }
        
        if (!instantiateTask.Result)
        {
            UpdateStatus("Failed to instantiate model.");
            Destroy(modelParent);
            yield break;
        }
        
        UpdateStatus("Model generated successfully!");
        Debug.Log($"[AIGenerator] Model instantiated at {spawnPosition}");
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
