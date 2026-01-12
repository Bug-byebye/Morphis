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
            
            Vector3 spawnPos = Camera.main.transform.position + Camera.main.transform.forward * 3f;
            GameObject modelObj = new GameObject("GeneratedModel");
            modelObj.transform.position = spawnPos;
            
            var instantiateTask = gltf.InstantiateMainSceneAsync(modelObj.transform);
            while (!instantiateTask.IsCompleted)
                yield return null;
            
            UpdateStatus("✨ Model generated successfully!");
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
