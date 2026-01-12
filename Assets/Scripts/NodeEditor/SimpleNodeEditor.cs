using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

namespace AIPipeline.UI
{
    /// <summary>
    /// 简化版可视化节点编辑器（修复版）
    /// </summary>
    public class SimpleNodeEditor : MonoBehaviour
    {
        [Header("Settings")]
        public string serverUrl = "http://localhost:8000/generate";
        
        // UI 元素
        private GameObject editorRoot;
        private GameObject toolbarObject;
        private RectTransform nodeContainer;
        private GameObject connectionContainer;
        private GameObject contextMenu;
        private TMP_Text statusText;
        private List<NodeData> nodeList = new List<NodeData>();
        private List<ConnectionLine> connections = new List<ConnectionLine>();
        
        private bool isVisible = false;
        private CursorLockMode savedLockMode;
        private bool savedCursorVisible;
        private PlayerInput playerInput;
        private Canvas mainCanvas;
        private Vector2 lastClickPos;
        
        // 连接模式
        private bool isConnecting = false;
        private NodeData connectingFromNode;
        
        void Start()
        {
            playerInput = FindObjectOfType<PlayerInput>();
            CreateEditorUI();
            editorRoot.SetActive(false);
            Debug.Log("[SimpleNodeEditor] Ready! Press Tab to open.");
        }
        
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleEditor();
            }
            
            if (!isVisible) return;
            
            // 右键菜单
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (isConnecting)
                {
                    // 取消连接
                    isConnecting = false;
                    connectingFromNode = null;
                    UpdateStatus("Connection cancelled");
                }
                else
                {
                    ShowContextMenu(Mouse.current.position.ReadValue());
                }
            }
            
            // 左键关闭菜单
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (contextMenu.activeSelf)
                {
                    Vector2 mousePos = Mouse.current.position.ReadValue();
                    RectTransform menuRect = contextMenu.GetComponent<RectTransform>();
                    if (!RectTransformUtility.RectangleContainsScreenPoint(menuRect, mousePos, null))
                    {
                        contextMenu.SetActive(false);
                    }
                }
            }
            
            // 更新连接线
            UpdateConnectionLines();
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
                if (playerInput != null) playerInput.enabled = false;
            }
            else
            {
                Cursor.lockState = savedLockMode;
                Cursor.visible = savedCursorVisible;
                if (playerInput != null) playerInput.enabled = true;
                if (contextMenu != null) contextMenu.SetActive(false);
            }
            
            editorRoot.SetActive(isVisible);
        }
        
        private void CreateEditorUI()
        {
            // 主 Canvas
            GameObject canvasObj = new GameObject("SimpleNodeEditorCanvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100;
            
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // 编辑器根节点（包含画布和工具栏）
            editorRoot = new GameObject("EditorRoot");
            editorRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform rootRect = editorRoot.AddComponent<RectTransform>();
            StretchToFill(rootRect);
            
            // ===== 工具栏 (先创建，在最底部) =====
            toolbarObject = CreateToolbar();
            toolbarObject.transform.SetParent(editorRoot.transform, false);
            
            // ===== 画布背景 (工具栏上方) =====
            GameObject canvasBg = new GameObject("CanvasBackground");
            canvasBg.transform.SetParent(editorRoot.transform, false);
            RectTransform bgRect = canvasBg.AddComponent<RectTransform>();
            // 使用绝对定位：底部 70px，其他边 0
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(0, 70); // 底部留 70px
            bgRect.offsetMax = Vector2.zero;
            
            Image bg = canvasBg.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.12f, 0.98f);
            bg.raycastTarget = true;
            
            // 连接线容器
            connectionContainer = new GameObject("ConnectionContainer");
            connectionContainer.transform.SetParent(canvasBg.transform, false);
            RectTransform connRect = connectionContainer.AddComponent<RectTransform>();
            StretchToFill(connRect);
            
            // 节点容器  
            GameObject nodeContainerObj = new GameObject("NodeContainer");
            nodeContainerObj.transform.SetParent(canvasBg.transform, false);
            nodeContainer = nodeContainerObj.AddComponent<RectTransform>();
            StretchToFill(nodeContainer);
            
            // 把工具栏移到最后（确保在最上层渲染）
            toolbarObject.transform.SetAsLastSibling();
            
            // 右键菜单
            CreateContextMenu();
            
            Debug.Log("[SimpleNodeEditor] UI Created. Toolbar height: 70px");
        }
        
        private GameObject CreateToolbar()
        {
            GameObject toolbar = new GameObject("Toolbar");
            RectTransform toolbarRect = toolbar.AddComponent<RectTransform>();
            toolbarRect.anchorMin = new Vector2(0, 0);
            toolbarRect.anchorMax = new Vector2(1, 0);
            toolbarRect.pivot = new Vector2(0.5f, 0);
            // 使用屏幕高度的百分比作为工具栏高度（约 5%）
            toolbarRect.sizeDelta = new Vector2(0, 0);
            toolbarRect.anchoredPosition = Vector2.zero;
            // 设置高度为屏幕的 6%
            var toolbarFitter = toolbar.AddComponent<ContentSizeFitter>();
            toolbarFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            Image tbBg = toolbar.AddComponent<Image>();
            tbBg.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            
            var layout = toolbar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 8, 8);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            
            // Execute 按钮
            CreateButton(toolbar.transform, "Execute", new Color(0.3f, 0.7f, 0.4f), OnExecuteClicked);
            
            // Clear 按钮
            CreateButton(toolbar.transform, "Clear", new Color(0.7f, 0.3f, 0.3f), OnClearClicked);
            
            // Connect 按钮
            CreateButton(toolbar.transform, "Connect", new Color(0.5f, 0.5f, 0.7f), OnConnectClicked);
            
            // Status 文本
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(toolbar.transform, false);
            var le = statusObj.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.minWidth = 100;
            
            statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Right-click to add nodes";
            statusText.fontSize = 14;
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 10;
            statusText.fontSizeMax = 18;
            statusText.color = Color.white;
            statusText.alignment = TextAlignmentOptions.MidlineRight;
            
            return toolbar;
        }
        
        private void CreateButton(Transform parent, string text, Color color, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(text + "Button");
            btnObj.transform.SetParent(parent, false);
            
            var le = btnObj.AddComponent<LayoutElement>();
            le.minWidth = 60;
            le.preferredWidth = 90;
            le.minHeight = 35;
            le.preferredHeight = 45;
            
            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = color;
            
            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            
            ColorBlock colors = btn.colors;
            colors.normalColor = color;
            colors.highlightedColor = color * 1.2f;
            colors.pressedColor = color * 0.8f;
            btn.colors = colors;
            
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            StretchToFill(textRect);
            
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 10;
            tmp.fontSizeMax = 16;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }
        
        private void CreateContextMenu()
        {
            contextMenu = new GameObject("ContextMenu");
            contextMenu.transform.SetParent(editorRoot.transform, false);
            RectTransform menuRect = contextMenu.AddComponent<RectTransform>();
            menuRect.sizeDelta = new Vector2(180, 320); // 增加高度
            menuRect.pivot = new Vector2(0, 1);
            
            Image menuBg = contextMenu.AddComponent<Image>();
            menuBg.color = new Color(0.18f, 0.18f, 0.22f, 0.98f);
            
            contextMenu.AddComponent<Outline>().effectColor = new Color(1f, 0.5f, 0.7f, 0.8f);
            
            var layout = contextMenu.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 5;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            
            CreateMenuLabel(contextMenu.transform, "Add Node");
            CreateMenuItem(contextMenu.transform, "Text Input", "TextInput");
            CreateMenuItem(contextMenu.transform, "Image Input", "ImageInput");
            CreateMenuItem(contextMenu.transform, "Text to Image", "Text2Image");
            CreateMenuItem(contextMenu.transform, "Image to Image", "Image2Image");
            CreateMenuItem(contextMenu.transform, "Image to 3D", "Image23D");
            CreateMenuItem(contextMenu.transform, "Text to 3D", "Text23D");
            CreateMenuItem(contextMenu.transform, "Preview", "Preview");
            
            contextMenu.SetActive(false);
        }
        
        private void CreateMenuLabel(Transform parent, string text)
        {
            GameObject label = new GameObject("Label");
            label.transform.SetParent(parent, false);
            label.AddComponent<LayoutElement>().preferredHeight = 30;
            
            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(1f, 0.6f, 0.75f);
            tmp.alignment = TextAlignmentOptions.Center;
        }
        
        private void CreateMenuItem(Transform parent, string text, string nodeType)
        {
            GameObject item = new GameObject(text);
            item.transform.SetParent(parent, false);
            item.AddComponent<LayoutElement>().preferredHeight = 32;
            
            Image itemBg = item.AddComponent<Image>();
            itemBg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            
            Button btn = item.AddComponent<Button>();
            string capturedType = nodeType;
            btn.onClick.AddListener(() => AddNode(capturedType));
            
            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.5f, 0.7f, 0.6f);
            btn.colors = colors;
            
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(item.transform, false);
            StretchToFill(textObj.AddComponent<RectTransform>());
            
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }
        
        private void ShowContextMenu(Vector2 screenPos)
        {
            lastClickPos = screenPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                editorRoot.GetComponent<RectTransform>(), screenPos, null, out Vector2 localPos);
            contextMenu.GetComponent<RectTransform>().anchoredPosition = localPos;
            contextMenu.SetActive(true);
        }
        
        private void AddNode(string nodeType)
        {
            contextMenu.SetActive(false);
            
            NodeData nodeData = CreateNodeUI(nodeType);
            
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                nodeContainer, lastClickPos, null, out Vector2 localPos);
            nodeData.gameObject.GetComponent<RectTransform>().anchoredPosition = localPos;
            
            nodeList.Add(nodeData);
            UpdateStatus($"Added {nodeType} node");
        }
        
        private NodeData CreateNodeUI(string nodeType)
        {
            GameObject node = new GameObject(nodeType + "Node");
            node.transform.SetParent(nodeContainer, false);
            
            RectTransform rect = node.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180, 100);
            rect.pivot = new Vector2(0, 1);
            
            Image nodeBg = node.AddComponent<Image>();
            nodeBg.color = GetNodeColor(nodeType);
            
            node.AddComponent<NodeDragger>().onDrag = UpdateConnectionLines;
            
            // 标题
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(node.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.sizeDelta = new Vector2(0, 28);
            titleRect.anchoredPosition = Vector2.zero;
            
            Image titleBg = titleObj.AddComponent<Image>();
            titleBg.color = GetNodeColor(nodeType) * 1.3f;
            titleBg.raycastTarget = false;
            
            var titleText = new GameObject("TitleText").AddComponent<TextMeshProUGUI>();
            titleText.transform.SetParent(titleObj.transform, false);
            StretchToFill(titleText.GetComponent<RectTransform>());
            titleText.text = nodeType;
            titleText.fontSize = 13;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;
            
            // 输入/输出端口
            GameObject inputPort = CreatePort(node.transform, true);
            GameObject outputPort = CreatePort(node.transform, false);
            
            NodeData nodeData = new NodeData
            {
                gameObject = node,
                nodeType = nodeType,
                inputPort = inputPort.GetComponent<RectTransform>(),
                outputPort = outputPort.GetComponent<RectTransform>()
            };
            
            // 如果是 TextInput，添加输入框
            if (nodeType == "TextInput")
            {
                CreateInputField(node, nodeData);
            }
            
            // 端口点击事件
            inputPort.GetComponent<Button>().onClick.AddListener(() => OnPortClicked(nodeData, true));
            outputPort.GetComponent<Button>().onClick.AddListener(() => OnPortClicked(nodeData, false));
            
            return nodeData;
        }
        
        private GameObject CreatePort(Transform parent, bool isInput)
        {
            GameObject port = new GameObject(isInput ? "InputPort" : "OutputPort");
            port.transform.SetParent(parent, false);
            
            RectTransform portRect = port.AddComponent<RectTransform>();
            portRect.sizeDelta = new Vector2(16, 16);
            portRect.anchorMin = new Vector2(isInput ? 0 : 1, 0.5f);
            portRect.anchorMax = portRect.anchorMin;
            portRect.anchoredPosition = new Vector2(isInput ? -8 : 8, -14);
            
            Image portImg = port.AddComponent<Image>();
            portImg.color = isInput ? new Color(0.4f, 0.7f, 1f) : new Color(1f, 0.5f, 0.7f);
            
            port.AddComponent<Button>();
            
            return port;
        }
        
        private void CreateInputField(GameObject parent, NodeData nodeData)
        {
            GameObject inputArea = new GameObject("InputArea");
            inputArea.transform.SetParent(parent.transform, false);
            RectTransform inputRect = inputArea.AddComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.08f, 0.12f);
            inputRect.anchorMax = new Vector2(0.92f, 0.68f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;
            
            Image inputBg = inputArea.AddComponent<Image>();
            inputBg.color = new Color(0.1f, 0.1f, 0.12f);
            
            TMP_InputField input = inputArea.AddComponent<TMP_InputField>();
            nodeData.inputField = input;
            
            GameObject textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputArea.transform, false);
            StretchToFill(textArea.AddComponent<RectTransform>());
            textArea.AddComponent<RectMask2D>();
            
            var text = new GameObject("Text").AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(textArea.transform, false);
            StretchToFill(text.GetComponent<RectTransform>());
            text.fontSize = 11;
            text.color = Color.white;
            
            input.textComponent = text;
            input.textViewport = textArea.GetComponent<RectTransform>();
            
            var ph = new GameObject("Placeholder").AddComponent<TextMeshProUGUI>();
            ph.transform.SetParent(textArea.transform, false);
            StretchToFill(ph.GetComponent<RectTransform>());
            ph.text = "Enter prompt...";
            ph.fontSize = 11;
            ph.fontStyle = FontStyles.Italic;
            ph.color = new Color(0.5f, 0.5f, 0.5f);
            
            input.placeholder = ph;
        }
        
        private void OnPortClicked(NodeData nodeData, bool isInputPort)
        {
            if (!isConnecting)
            {
                // 开始连接（只能从输出端口开始）
                if (!isInputPort)
                {
                    isConnecting = true;
                    connectingFromNode = nodeData;
                    UpdateStatus($"Click on an input port to connect from {nodeData.nodeType}");
                }
            }
            else
            {
                // 完成连接（只能连到输入端口）
                if (isInputPort && connectingFromNode != nodeData)
                {
                    CreateConnection(connectingFromNode, nodeData);
                    UpdateStatus($"Connected {connectingFromNode.nodeType} -> {nodeData.nodeType}");
                }
                isConnecting = false;
                connectingFromNode = null;
            }
        }
        
        private void OnConnectClicked()
        {
            if (nodeList.Count < 2)
            {
                UpdateStatus("Add at least 2 nodes to connect!");
                return;
            }
            UpdateStatus("Click output port (right), then input port (left)");
        }
        
        private void CreateConnection(NodeData from, NodeData to)
        {
            GameObject lineObj = new GameObject("Connection");
            lineObj.transform.SetParent(connectionContainer.transform, false);
            
            var line = lineObj.AddComponent<ConnectionLine>();
            line.fromPort = from.outputPort;
            line.toPort = to.inputPort;
            line.lineColor = new Color(1f, 0.5f, 0.7f, 0.8f);
            
            from.connectedTo = to;
            connections.Add(line);
        }
        
        private void UpdateConnectionLines()
        {
            foreach (var conn in connections)
            {
                if (conn != null)
                    conn.SetVerticesDirty();
            }
        }
        
        private Color GetNodeColor(string nodeType)
        {
            switch (nodeType)
            {
                case "TextInput": return new Color(0.25f, 0.45f, 0.65f);   // 蓝色
                case "ImageInput": return new Color(0.45f, 0.55f, 0.65f);  // 浅蓝色
                case "Text2Image": return new Color(0.6f, 0.4f, 0.65f);    // 紫色
                case "Image2Image": return new Color(0.55f, 0.45f, 0.6f);  // 淡紫色
                case "Image23D": return new Color(0.65f, 0.45f, 0.55f);    // 粉紫色
                case "Text23D": return new Color(0.65f, 0.35f, 0.5f);      // 玫瑰色
                case "Preview": return new Color(0.35f, 0.55f, 0.35f);     // 绿色
                default: return new Color(0.4f, 0.4f, 0.4f);
            }
        }
        
        private void StretchToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        
        private void OnExecuteClicked()
        {
            var textInputNode = nodeList.Find(n => n.nodeType == "TextInput");
            if (textInputNode == null)
            {
                UpdateStatus("Add a TextInput node first!");
                return;
            }
            
            string prompt = textInputNode.inputField != null ? textInputNode.inputField.text : "";
            if (string.IsNullOrWhiteSpace(prompt))
            {
                UpdateStatus("Enter a prompt in TextInput node!");
                return;
            }
            
            StartCoroutine(ExecutePipeline(prompt));
        }
        
        private System.Collections.IEnumerator ExecutePipeline(string prompt)
        {
            UpdateStatus($"Generating: {prompt}...");
            
            string jsonBody = $"{{\"prompt\": \"{prompt}\"}}";
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            
            using (var request = new UnityEngine.Networking.UnityWebRequest(serverUrl, "POST"))
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
                
                yield return LoadModel(request.downloadHandler.data);
            }
        }
        
        private System.Collections.IEnumerator LoadModel(byte[] glbData)
        {
            var gltf = new GLTFast.GltfImport();
            var loadTask = gltf.LoadGltfBinary(glbData);
            while (!loadTask.IsCompleted) yield return null;
            
            if (!loadTask.Result) { UpdateStatus("Failed to load model"); yield break; }
            
            Vector3 pos = Camera.main.transform.position + Camera.main.transform.forward * 3f;
            GameObject model = new GameObject("GeneratedModel");
            model.transform.position = pos;
            
            var instTask = gltf.InstantiateMainSceneAsync(model.transform);
            while (!instTask.IsCompleted) yield return null;
            
            UpdateStatus("Model generated!");
        }
        
        private void OnClearClicked()
        {
            foreach (var node in nodeList)
                if (node.gameObject != null) Destroy(node.gameObject);
            nodeList.Clear();
            
            foreach (var conn in connections)
                if (conn != null) Destroy(conn.gameObject);
            connections.Clear();
            
            UpdateStatus("Canvas cleared");
        }
        
        private void UpdateStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            Debug.Log($"[NodeEditor] {msg}");
        }
    }
    
    // ===== 辅助类 =====
    
    public class NodeData
    {
        public GameObject gameObject;
        public string nodeType;
        public RectTransform inputPort;
        public RectTransform outputPort;
        public TMP_InputField inputField;
        public NodeData connectedTo;
    }
    
    public class NodeDragger : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public System.Action onDrag;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        
        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        public void OnBeginDrag(PointerEventData e) { canvasGroup.blocksRaycasts = false; transform.SetAsLastSibling(); }
        public void OnDrag(PointerEventData e) { rectTransform.anchoredPosition += e.delta; onDrag?.Invoke(); }
        public void OnEndDrag(PointerEventData e) { canvasGroup.blocksRaycasts = true; }
    }
    
    [RequireComponent(typeof(CanvasRenderer))]
    public class ConnectionLine : Graphic
    {
        public RectTransform fromPort;
        public RectTransform toPort;
        public Color lineColor = new Color(1f, 0.5f, 0.7f, 0.8f);
        public float lineWidth = 3f;
        
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (fromPort == null || toPort == null) return;
            
            Vector2 start = transform.InverseTransformPoint(fromPort.position);
            Vector2 end = transform.InverseTransformPoint(toPort.position);
            
            float dist = Mathf.Abs(end.x - start.x) * 0.5f;
            Vector2 c1 = start + Vector2.right * dist;
            Vector2 c2 = end + Vector2.left * dist;
            
            int segments = 20;
            Vector2 prev = start;
            
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector2 curr = Bezier(t, start, c1, c2, end);
                DrawLine(vh, prev, curr, lineWidth, lineColor);
                prev = curr;
            }
        }
        
        private Vector2 Bezier(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            return u*u*u*p0 + 3*u*u*t*p1 + 3*u*t*t*p2 + t*t*t*p3;
        }
        
        private void DrawLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * width * 0.5f;
            
            int idx = vh.currentVertCount;
            vh.AddVert(a + perp, color, Vector2.zero);
            vh.AddVert(a - perp, color, Vector2.zero);
            vh.AddVert(b - perp, color, Vector2.zero);
            vh.AddVert(b + perp, color, Vector2.zero);
            vh.AddTriangle(idx, idx+1, idx+2);
            vh.AddTriangle(idx, idx+2, idx+3);
        }
    }
}
