using UnityEngine;
using System.IO;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using System.Collections;

namespace AIPipeline.UI
{
    /// <summary>
    /// 简化版可视化节点编辑器（修复版）
    /// </summary>
    public class SimpleNodeEditor : MonoBehaviour
    {
        [Header("Settings")]
        public string baseUrl = "http://localhost:8000";
        
        // API 端点
        private string Text2ImageUrl => $"{baseUrl}/text2image/urls";
        private string Image2ImageUrl => $"{baseUrl}/image2image";
        private string Image23DUrl => $"{baseUrl}/image23d";
        private string Text23DUrl => $"{baseUrl}/text23d";
        
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
        /// <summary>Whether the workflow station editor is currently open (blocks world clicks).</summary>
        public static bool IsEditorOpen => Instance != null && Instance.isVisible;
        private CursorLockMode savedLockMode;
        private bool savedCursorVisible;
        private Canvas mainCanvas;
        private Vector2 lastClickPos;

        /// <summary>Always find the current PlayerInput fresh — never cache across scenes.</summary>
        private PlayerInput FindPlayerInput()
        {
            return FindObjectOfType<PlayerInput>();
        }

        [Header("Main UI")]
        [SerializeField] private GameObject mainUICanvas;

        // private Button openEditorButton; // Removed serialized field to prevent dupes
        // private TextMeshProUGUI openEditorButtonText; // Removed serialized field
        private Button workflowStationButton; 
        private TextMeshProUGUI workflowStationButtonText;
        
        // 连接模式
        private bool isConnecting = false;
        private NodeData connectingFromNode;
        private string connectingToPortID; // Track which input port we are targetting? No, we start FROM output.
        // We only need to know target port when we click Input input.
        
        // Pipeline 错误标记
        private bool pipelineHasError = false;
        
        // Singleton Implementation
        public static SimpleNodeEditor Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        IEnumerator Start()
        {
            // Wait for BootCanvas to disappear (login flow clear)
            while (GameObject.Find("BootCanvas") != null)
            {
                yield return null;
                if (this == null) yield break; // Safety check if object destroyed during wait
            }

            if (this == null) yield break;

            // Re-check canvases if we are persisting
            if (mainCanvas == null) CreateEditorUI();
            if (mainUICanvas == null) CreateMainUI();
            
            if (editorRoot != null)
                editorRoot.SetActive(false);
            
            // Ensure player input is enabled at start
            SetAllPlayerInputEnabled(true);
            
            Debug.Log("[SimpleNodeEditor] Ready! Press Tab or Button to open.");
        }

        private void CreateMainUI()
        {
            // Detect and cleanup legacy duplicate buttons
            var legacyBtn = GameObject.Find("OpenButton");
            if (legacyBtn != null) 
            {
                Destroy(legacyBtn);
            }

            // Hide if in Boot Flow
            if (GameObject.Find("BootCanvas") != null) return;

            // If we already have a UI but it was destroyed (scene load), recreate it
            // Or if we are persistent, we keep it.
            
            if (mainUICanvas != null) return; // Already exists

            // Check if Main UI already exists in scene (duplicate check)
            GameObject canvasObj = GameObject.Find("NodeEditor_MainUI");
            if (canvasObj != null)
            {
                mainUICanvas = canvasObj;
                // Re-bind button...
            }
            else
            {
                // Create Canvas
                // Create Canvas
                canvasObj = new GameObject("NodeEditor_MainUI");
                var canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200; // Ensure it's above the Editor Window (order 100)
                
                canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObj.AddComponent<GraphicRaycaster>();
                mainUICanvas = canvasObj;
                
                DontDestroyOnLoad(canvasObj); // Persistent UI

                CreateWorkflowStationButton(canvasObj);
            }
        }

        private void CreateWorkflowStationButton(GameObject canvasObj)
        {
            // Create Button
            GameObject btnObj = new GameObject("WorkflowStationButton"); // New Unique Name
            btnObj.transform.SetParent(canvasObj.transform, false);
            
            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            
            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(ToggleEditor);
            workflowStationButton = btn;

            // Position Top-Right
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-20, -20);
            rt.sizeDelta = new Vector2(160, 40);

            // Button Text
            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            
            TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
            txt.text = "工作台";
            txt.fontSize = 18;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            workflowStationButtonText = txt;
            
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
        }
        
        void Update()
        {
            // Safety check: if UI is destroyed (e.g. scene change), stop updating references
            if (this == null || editorRoot == null) return;

            try
            {
                if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    ToggleEditor();
                }
                
                // M 键切换鼠标显示（用于与场景物体交互）
                if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame && !isVisible)
                {
                    ToggleMouseCursor();
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
                    if (contextMenu != null && contextMenu.activeSelf)
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
            catch (MissingReferenceException)
            {
                // Ignore errors during scene unload
            }
        }
        
        public void ToggleEditor()
        {
            // If UI not created (e.g. in Login Screen), do nothing
            if (editorRoot == null) return;

            isVisible = !isVisible;
            
            if (isVisible)
            {
                savedLockMode = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SetAllPlayerInputEnabled(false);
                
                if (workflowStationButtonText != null) workflowStationButtonText.text = "关闭工作台";
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SetAllPlayerInputEnabled(true);
                if (contextMenu != null) contextMenu.SetActive(false);
                
                if (workflowStationButtonText != null) workflowStationButtonText.text = "工作台";
            }
            
            editorRoot.SetActive(isVisible);
        }
        
        // 鼠标交互模式
        private bool isMouseCursorMode = false;
        
        private void ToggleMouseCursor()
        {
            isMouseCursorMode = !isMouseCursorMode;
            
            if (isMouseCursorMode)
            {
                savedLockMode = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                // 不禁用 playerInput，让玩家可以继续移动
                Debug.Log("[NodeEditor] Mouse cursor enabled (M key). Press M again to disable.");
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Debug.Log("[NodeEditor] Mouse cursor mode toggled off (cursor stays visible).");
            }
        }
        
        /// <summary>
        /// Enable or disable ALL PlayerInput components in the scene.
        /// Uses a fresh search every time — never stale references.
        /// </summary>
        private void SetAllPlayerInputEnabled(bool enabled)
        {
            var allInputs = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
            foreach (var pi in allInputs)
            {
                if (pi != null) pi.enabled = enabled;
            }
        }

        /// <summary>
        /// Safety net: if the editor is NOT visible, ensure all player movement components are enabled.
        /// Prevents getting stuck with disabled input due to stale references or errors.
        /// </summary>
        private void LateUpdate()
        {
            if (!isVisible)
            {
                // Find the local player's PlayerInput and ensure ALL movement components are enabled
                var pi = FindPlayerInput();
                if (pi == null) return;

                bool anyFixed = false;

                if (!pi.enabled) { pi.enabled = true; anyFixed = true; }

                var inputs = pi.GetComponent<StarterAssets.StarterAssetsInputs>();
                if (inputs != null && !inputs.enabled) { inputs.enabled = true; anyFixed = true; }

                var tpc = pi.GetComponent<StarterAssets.ThirdPersonController>();
                if (tpc != null && !tpc.enabled) { tpc.enabled = true; anyFixed = true; }

                var cc = pi.GetComponent<CharacterController>();
                if (cc != null && !cc.enabled) { cc.enabled = true; anyFixed = true; }

                if (anyFixed)
                {
                    Debug.LogWarning("[SimpleNodeEditor] Safety: Re-enabled movement components that were stuck disabled.");
                }
            }
        }

        private void CreateEditorUI()
        {
            // 主 Canvas
            // 检查是否在登录流程中（BootCanvas 存在且激活），如果是则暂不显示
            if (GameObject.Find("BootCanvas") != null)
            {
                Debug.Log("[SimpleNodeEditor] BootCanvas detected. Hiding Open Button.");
                return;
            }

            GameObject canvasObj = new GameObject("SimpleNodeEditorCanvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100;
            DontDestroyOnLoad(canvasObj); // Persistent Editor UI
            
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
            toolbarRect.sizeDelta = new Vector2(0, 70);
            toolbarRect.anchoredPosition = Vector2.zero;
            
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
            CreateButton(toolbar.transform, "执行", new Color(0.3f, 0.7f, 0.4f), OnExecuteClicked);
            
            // Clear 按钮
            CreateButton(toolbar.transform, "清空", new Color(0.7f, 0.3f, 0.3f), OnClearClicked);
            
            // Connect 按钮 - Removed in favor of drag-connect
            // CreateButton(toolbar.transform, "Connect", new Color(0.5f, 0.5f, 0.7f), OnConnectClicked);
            
            // Status 文本
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(toolbar.transform, false);
            var le = statusObj.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.minWidth = 100;
            
            statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "右键添加节点";
            statusText.fontSize = 18;
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 14;
            statusText.fontSizeMax = 24;
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
            tmp.fontSizeMin = 14;
            tmp.fontSizeMax = 20;
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
            menuRect.sizeDelta = new Vector2(240, 450); // 增加高度和宽度
            menuRect.pivot = new Vector2(0, 1);
            
            Image menuBg = contextMenu.AddComponent<Image>();
            menuBg.color = new Color(0.18f, 0.18f, 0.22f, 0.98f);
            
            contextMenu.AddComponent<Outline>().effectColor = new Color(1f, 0.5f, 0.7f, 0.8f);
            
            var layout = contextMenu.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 5;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            
            CreateMenuLabel(contextMenu.transform, "添加节点");
            CreateMenuItem(contextMenu.transform, "文字输入", "TextInput");
            CreateMenuItem(contextMenu.transform, "图片输入", "ImageInput");
            CreateMenuItem(contextMenu.transform, "文生图", "Text2Image");
            CreateMenuItem(contextMenu.transform, "图生图", "Image2Image");
            CreateMenuItem(contextMenu.transform, "图生3D", "Image23D");
            CreateMenuItem(contextMenu.transform, "文生3D", "Text23D");
            CreateMenuItem(contextMenu.transform, "预览", "Preview");
            
            contextMenu.SetActive(false);
        }
        
        private void CreateMenuLabel(Transform parent, string text)
        {
            GameObject label = new GameObject("Label");
            label.transform.SetParent(parent, false);
            label.AddComponent<LayoutElement>().preferredHeight = 40;
            
            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(1f, 0.6f, 0.75f);
            tmp.alignment = TextAlignmentOptions.Center;
        }
        
        private void CreateMenuItem(Transform parent, string text, string nodeType)
        {
            GameObject item = new GameObject(text);
            item.transform.SetParent(parent, false);
            item.AddComponent<LayoutElement>().preferredHeight = 40;
            
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
            tmp.fontSize = 18;
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
            rect.sizeDelta = new Vector2(260, 160); // 更大的节点
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
            titleRect.sizeDelta = new Vector2(0, 45); // 更高的标题栏
            titleRect.anchoredPosition = Vector2.zero;
            
            Image titleBg = titleObj.AddComponent<Image>();
            titleBg.color = GetNodeColor(nodeType) * 1.3f;
            titleBg.raycastTarget = false;
            
            var titleText = new GameObject("TitleText").AddComponent<TextMeshProUGUI>();
            titleText.transform.SetParent(titleObj.transform, false);
            StretchToFill(titleText.GetComponent<RectTransform>());
            titleText.text = nodeType;
            titleText.fontSize = 24; // 更大的字体
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
            
            // 如果是 Preview，添加图片预览区域
            if (nodeType == "Preview")
            {
                CreatePreviewArea(node, nodeData);
            }
            
            // 端口点击事件
            // Default ports
            if (nodeType != "Image2Image")
            {
                if (inputPort != null) 
                    inputPort.GetComponent<Button>().onClick.AddListener(() => OnPortClicked(nodeData, true, "default"));
            }

            if (outputPort != null)
                outputPort.GetComponent<Button>().onClick.AddListener(() => OnPortClicked(nodeData, false, "output"));
            
            // Special setup for Image2Image (Multiple Inputs)
            if (nodeType == "Image2Image")
            {
                // Destroy default input port
                Destroy(inputPort);
                nodeData.inputPort = null; // Clear default ref

                // Create Image Input (Top)
                GameObject imgPort = CreatePort(node.transform, true, "Image");
                imgPort.GetComponent<RectTransform>().anchoredPosition = new Vector2(-8, 10);
                imgPort.GetComponent<Button>().onClick.AddListener(() => OnPortClicked(nodeData, true, "Image"));

                // Create Prompt Input (Bottom)
                GameObject txtPort = CreatePort(node.transform, true, "Text");
                txtPort.GetComponent<RectTransform>().anchoredPosition = new Vector2(-8, -40);
                txtPort.GetComponent<Button>().onClick.AddListener(() => OnPortClicked(nodeData, true, "Text"));

                // Store references if needed, or just rely on IDs in validation
                // We'll trust the ID string passed to OnPortClicked
            }
            
            // Delete Button (X)
            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(titleObj.transform, false);
            RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 0.5f);
            closeRect.anchorMax = new Vector2(1, 0.5f);
            closeRect.pivot = new Vector2(1, 0.5f);
            closeRect.sizeDelta = new Vector2(30, 30);
            closeRect.anchoredPosition = new Vector2(-5, 0);

            Image closeImg = closeBtnObj.AddComponent<Image>();
            closeImg.color = new Color(0.8f, 0.2f, 0.2f, 0.0f); // Transparent hit area initially or simple red
            closeImg.color = new Color(1f, 1f, 1f, 0.1f); // Subtle background

            Button closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => DeleteNode(nodeData));
            
            // Text for Close Button
            var closeTextObj = new GameObject("X");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            StretchToFill(closeTextObj.AddComponent<RectTransform>());
            var closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "X";
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = new Color(1f, 0.6f, 0.6f);
            closeText.fontSize = 18;
            closeText.fontStyle = FontStyles.Bold;

            return nodeData;
        }

        private void DeleteNode(NodeData node)
        {
             if (node == null) return;

             // Remove connections
             // Since 'connections' is a list of lines, we need to find lines connected to this node
             List<ConnectionLine> linesToRemove = new List<ConnectionLine>();
             foreach (var conn in connections)
             {
                 if (conn.fromPort == node.outputPort || conn.toPort == node.inputPort)
                 {
                     linesToRemove.Add(conn);
                 }
             }

             foreach (var line in linesToRemove)
             {
                 connections.Remove(line);
                 if (line != null) Destroy(line.gameObject);
             }

             // Update logical connections
             // 1. If this node is 'connectedTo' others, remove references from them?
             // Actually, NodeData.connectedFrom ref is on the destination node.
             // We need to clean that up.
             
             // Clean outgoing: For every node this node connects TO (Downstream)
             if (node.connectedToNodes != null)
             {
                 foreach (var target in node.connectedToNodes)
                 {
                     if (target != null)
                     {
                         // Remove references to 'node' from target's inputConnections
                         var keysToRemove = new List<string>();
                         foreach(var kvp in target.inputConnections)
                         {
                             if (kvp.Value == node) keysToRemove.Add(kvp.Key);
                         }
                         foreach(var key in keysToRemove) target.inputConnections.Remove(key);
                     }
                 }
                 node.connectedToNodes.Clear();
             }

             // Clean incoming: For the nodes that connect TO this node (Upstream)
             // We need to find nodes that have 'node' in their connectedToNodes list.
             // Since we don't store upstream nodes directly in a list on them easily for "outgoing to me",
             // we rely on 'inputConnections' which tells us who connects to us.
             
             if (node.inputConnections.Count > 0)
             {
                 foreach (var kvp in node.inputConnections)
                 {
                     NodeData sourceNode = kvp.Value;
                     if (sourceNode != null && sourceNode.connectedToNodes != null)
                     {
                         sourceNode.connectedToNodes.Remove(node);
                     }
                 }
                 node.inputConnections.Clear();
             }

             // Remove from list
             nodeList.Remove(node);

             // Destroy Objects
             if (node.previewModel != null) Destroy(node.previewModel);
             if (node.previewCamera != null && node.previewCamera.gameObject != null) Destroy(node.previewCamera.gameObject);
             if (node.gameObject != null) Destroy(node.gameObject);

             UpdateStatus($"Deleted {node.nodeType}");
        }
        
        private GameObject CreatePort(Transform parent, bool isInput, string portName = "")
        {
            GameObject port = new GameObject(isInput ? $"InputPort_{portName}" : "OutputPort");
            port.transform.SetParent(parent, false);
            
            RectTransform portRect = port.AddComponent<RectTransform>();
            portRect.sizeDelta = new Vector2(16, 16);
            
            // Layout adjusting for multiple inputs?
            // For now, simple layout: if portName is "Image", top; "Prompt", bottom?
            // Or just hardcoded positions in CreateNodeUI caller.
            // Let's rely on caller setting position if needed, or default here.
            
            portRect.anchorMin = new Vector2(isInput ? 0 : 1, 0.5f);
            portRect.anchorMax = portRect.anchorMin;
            portRect.anchoredPosition = new Vector2(isInput ? -8 : 8, -14);
            
            Image portImg = port.AddComponent<Image>();
            // Color based on type?
            if (portName == "Text") portImg.color = new Color(0.25f, 0.45f, 0.65f);
            else if (portName == "Image") portImg.color = new Color(0.45f, 0.55f, 0.65f);
            else portImg.color = isInput ? new Color(0.4f, 0.7f, 1f) : new Color(1f, 0.5f, 0.7f);
            
            var btn = port.AddComponent<Button>();
            
            // Add label if named
            if (!string.IsNullOrEmpty(portName))
            {
                 GameObject lbl = new GameObject("Label");
                 lbl.transform.SetParent(port.transform, false);
                 var tmp = lbl.AddComponent<TextMeshProUGUI>();
                 tmp.text = portName;
                 tmp.fontSize = 10;
                 tmp.color = Color.white;
                 var rt = lbl.GetComponent<RectTransform>();
                 rt.sizeDelta = new Vector2(50, 20);
                 rt.anchoredPosition = new Vector2(isInput ? 30 : -30, 0); 
            }

            return port;
        }
        
        private void CreatePreviewArea(GameObject parent, NodeData nodeData)
        {
            // 扩大节点尺寸以容纳预览图
            RectTransform nodeRect = parent.GetComponent<RectTransform>();
            nodeRect.sizeDelta = new Vector2(320, 320); // 更大的 Preview 节点
            
            GameObject previewArea = new GameObject("PreviewArea");
            previewArea.transform.SetParent(parent.transform, false);
            RectTransform previewRect = previewArea.AddComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.08f, 0.08f);
            previewRect.anchorMax = new Vector2(0.92f, 0.78f);
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;
            
            // 背景
            Image previewBg = previewArea.AddComponent<Image>();
            previewBg.color = new Color(0.15f, 0.15f, 0.18f);
            
            // RawImage 用于显示生成的图片
            GameObject imgObj = new GameObject("PreviewImage");
            imgObj.transform.SetParent(previewArea.transform, false);
            RectTransform imgRect = imgObj.AddComponent<RectTransform>();
            imgRect.anchorMin = Vector2.zero;
            imgRect.anchorMax = Vector2.one;
            imgRect.offsetMin = new Vector2(4, 4);
            imgRect.offsetMax = new Vector2(-4, -4);
            
            RawImage rawImage = imgObj.AddComponent<RawImage>();
            rawImage.color = Color.white;
            nodeData.previewImage = rawImage;
            
            // 占位文字
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(previewArea.transform, false);
            StretchToFill(placeholderObj.AddComponent<RectTransform>());
            
            var placeholderText = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholderText.text = "预览\n(等待图片)";
            placeholderText.fontSize = 18; // 更大的字体
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f);
            placeholderText.alignment = TextAlignmentOptions.Center;
            placeholderText.raycastTarget = false;
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
            text.fontSize = 20;
            text.color = Color.white;
            
            input.textComponent = text;
            input.textViewport = textArea.GetComponent<RectTransform>();
            
            var ph = new GameObject("Placeholder").AddComponent<TextMeshProUGUI>();
            ph.transform.SetParent(textArea.transform, false);
            StretchToFill(ph.GetComponent<RectTransform>());
            ph.text = "输入提示词...";
            ph.fontSize = 20;
            ph.fontStyle = FontStyles.Italic;
            ph.color = new Color(0.5f, 0.5f, 0.5f);
            
            input.placeholder = ph;
        }
        
        private void OnPortClicked(NodeData nodeData, bool isInputPort, string portID)
        {
            if (!isConnecting)
            {
                // 开始连接（只能从输出端口开始）
                if (!isInputPort)
                {
                    isConnecting = true;
                    connectingFromNode = nodeData;
                    UpdateStatus($"Connect from {nodeData.nodeType}...");
                }
                else
                {
                    UpdateStatus("Click output port (right side) first to start connection");
                }
            }
            else
            {
                // 完成连接（只能连到输入端口）
                if (isInputPort && connectingFromNode != nodeData)
                {
                    // 验证连接类型
                    if (connectingFromNode.CanConnectTo(nodeData, portID, out string error))
                    {
                        CreateConnection(connectingFromNode, nodeData, portID);
                        UpdateStatus($"Connected: {connectingFromNode.nodeType} -> {nodeData.nodeType} ({portID})");
                    }
                    else
                    {
                        UpdateStatus($"[X] Failed: {error}");
                    }
                }
                isConnecting = false;
                connectingFromNode = null;
            }
        }

        private void CreateConnection(NodeData from, NodeData to, string toPortID)
        {
            GameObject lineObj = new GameObject("Connection");
            lineObj.transform.SetParent(connectionContainer.transform, false);
            
            var line = lineObj.AddComponent<ConnectionLine>();
            line.fromPort = from.outputPort;
            
            // Find target port rect based on ID?
            // Hacky: We didn't store port Rect in Dictionary.
            // But we know for Image2Image it's special.
            // Let's implement a helper in NodeData to find port rect by ID.
            line.toPort = to.GetInputPortRect(toPortID); 
            line.lineColor = new Color(1f, 0.5f, 0.7f, 0.8f);
            
            from.connectedToNodes.Add(to); 
            // Register input connection
            to.inputConnections[toPortID] = from;
            
            connections.Add(line);
        }

        private List<NodeData> GetOutgoingConnections(NodeData node)
        {
            if (node == null || node.connectedToNodes == null) return new List<NodeData>();
            return node.connectedToNodes;
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
        
        /// <summary>
        /// 递归标记下游节点需要重新执行（当上游节点变化时）
        /// </summary>
        private void InvalidateDownstreamCache(NodeData node, HashSet<NodeData> needsReExecution)
        {
            if (node == null || node.connectedToNodes == null) return;
            
            foreach (var downstream in node.connectedToNodes)
            {
                if (downstream != null && !needsReExecution.Contains(downstream))
                {
                    needsReExecution.Add(downstream);
                    downstream.cachedResult = null;  // Clear cached result
                    InvalidateDownstreamCache(downstream, needsReExecution);  // Recurse
                }
            }
        }
        
        private void OnExecuteClicked()
        {
            StartCoroutine(ExecutePipelineGraph());
        }
        
        /// <summary>
        /// 根据节点连接智能执行管线
        /// </summary>
        /// <summary>
        /// 根据节点连接执行管线 (Supporting branching)
        /// </summary>
        /// <summary>
        /// 根据节点连接执行管线 (Supporting branching)
        /// Starts from all TextInput/ImageInput nodes.
        /// </summary>
        private System.Collections.IEnumerator ExecutePipelineGraph()
        {
            UpdateStatus("Executing pipeline (incremental)...");
            pipelineHasError = false; // Reset error flag
            
            // Map to store results of each node to pass to inputs of others
            Dictionary<NodeData, object> nodeResults = new Dictionary<NodeData, object>();
            
            // === INCREMENTAL EXECUTION: Pre-populate with cached results ===
            // Also track which nodes need re-execution (no cache or upstream changed)
            HashSet<NodeData> needsReExecution = new HashSet<NodeData>();
            
            // Step 1: Check TextInput nodes for changes and pre-populate cached results
            foreach (var node in nodeList)
            {
                if (node.nodeType == "TextInput")
                {
                    string currentText = node.inputField != null ? node.inputField.text : "";
                    // Check if text changed since last execution
                    if (node.cachedInputPrompt != currentText)
                    {
                        // Text changed - mark for re-execution and invalidate downstream
                        needsReExecution.Add(node);
                        InvalidateDownstreamCache(node, needsReExecution);
                    }
                }
                
                // Pre-populate nodeResults with cached data (for nodes not needing re-execution)
                if (node.cachedResult != null && !needsReExecution.Contains(node))
                {
                    nodeResults[node] = node.cachedResult;
                }
            }
            
            // Step 2: Find nodes that have no cached result (new nodes)
            foreach (var node in nodeList)
            {
                if (node.cachedResult == null && node.nodeType != "TextInput")
                {
                    needsReExecution.Add(node);
                }
            }
            
            // List for Traversal (with Priority)
            List<NodeData> executionList = new List<NodeData>();
            
            // Find all Start Nodes (Roots)
            bool hasRoots = false;
            foreach (var node in nodeList)
            {
                if (node.nodeType == "TextInput")
                {
                    string p = node.inputField != null ? node.inputField.text : "";
                    if (!string.IsNullOrEmpty(p))
                    {
                        nodeResults[node] = p;
                        node.cachedResult = p;           // Cache the result
                        node.cachedInputPrompt = p;      // Remember for change detection
                        executionList.Add(node);
                        hasRoots = true;
                    }
                }
            }

            if (!hasRoots)
            {
                UpdateStatus("No TextInput nodes found or empty prompts!");
                yield break;
            }

            // Keep track of processed nodes to prevent infinite loops (though DAG should be enforced)
            HashSet<NodeData> processedNodes = new HashSet<NodeData>();
            // Roots are not "processed" in the loop sense (they are the data sources), 
            // but we need to enqueue their children.
            // Actually, we should enqueue the children of roots? 
            // The loop processes a node, DOES action, then enqueues children.
            // TextInput "action" is just providing data (already done in setup).
            // So we should mark them as processed?
            
            // Better approach: Treat TextInput as needing processing? 
            // The loop below processes dequeued nodes.
            // If we enqueue TextInput, it will be dequeued.
            // We need to handle TextInput in switch case to just pass data through or do nothing.

            // Queue/List for traversal (List allows priority extraction)
            // Rename to executionList to avoid confusion, but we are replacing the variable
            // in the scope we define it.
            // Wait, previous tool call defined it as 'executionQueue'. We should change the definition.
            
            // Note: I will replace the definition line below.

            while (executionList.Count > 0)
            {
                if (pipelineHasError)
                {
                    UpdateStatus("Pipeline stopped due to error!");
                    yield break;
                }

                // Priority Dequeue: Find 'Preview' nodes first
                NodeData currentNode = null;
                
                // Find index of high priority node
                int bestIndex = -1;
                for(int i=0; i<executionList.Count; i++)
                {
                    if (executionList[i].nodeType == "Preview")
                    {
                        bestIndex = i;
                        break; // Found one!
                    }
                }
                
                if (bestIndex == -1) bestIndex = 0; // Default FIFO (take first)
                
                currentNode = executionList[bestIndex];
                executionList.RemoveAt(bestIndex);
                
                // --- Input Resolution Logic ---
                // For most nodes, we just need ANY input (or specific single input).
                // For Image2Image, we need "Image" AND "Text" (optional or mandatory?).
                
                object inputData = null; // Logic below will determine this usually for single-input
                
                // SKIP Check: If this node has inputs, but they are not ready yet.
                if (currentNode.nodeType == "Image2Image")
                {
                    // Check if inputs connected
                    if (currentNode.inputConnections.ContainsKey("Image") && 
                        !nodeResults.ContainsKey(currentNode.inputConnections["Image"]))
                    {
                        UpdateStatus($"Waiting for Image input for {currentNode.nodeType}...");
                        continue;
                    }
                    if (currentNode.inputConnections.ContainsKey("Text") && 
                        !nodeResults.ContainsKey(currentNode.inputConnections["Text"]))
                    {
                         UpdateStatus($"Waiting for Text input for {currentNode.nodeType}...");
                         continue;
                    }
                }
                else
                {
                    // Standard nodes: ensure at least one input is ready if connected
                     if (currentNode.connectedFrom != null && !nodeResults.ContainsKey(currentNode.connectedFrom))
                    {
                        // Ensure we strictly wait for the specific single parent? 
                        // Yes, connectedFrom is the primary parent.
                        UpdateStatus($"Skipping {currentNode.nodeType}: Waiting for input data...");
                        continue; 
                    }
                }

                if (processedNodes.Contains(currentNode)) continue;

                UpdateStatus($"Processing: {currentNode.nodeType}...");

                object outputData = null;
                
                // === INCREMENTAL: Check if we can use cached result ===
                bool useCache = !needsReExecution.Contains(currentNode) && currentNode.cachedResult != null;
                
                if (useCache)
                {
                    outputData = currentNode.cachedResult;
                    UpdateStatus($"Using cached result for {currentNode.nodeType}");
                }
                else
                {
                    // Execute based on node type
                    switch (currentNode.nodeType)
                    {
                        case "TextInput":
                             // Already put in nodeResults. Just pass.
                             outputData = nodeResults[currentNode];
                             break;

                        case "Text2Image":
                            // Get input from connected
                            if (currentNode.connectedFrom != null && nodeResults.ContainsKey(currentNode.connectedFrom))
                                inputData = nodeResults[currentNode.connectedFrom];
                            
                            if (inputData is string txtPrompt && !string.IsNullOrEmpty(txtPrompt))
                            {
                                UpdateStatus($"Calling Text2Image API...");
                                yield return CallText2Image(txtPrompt, (result) => outputData = result);
                            }
                            else 
                                { UpdateStatus("Text2Image needs text input!"); pipelineHasError = true; }
                            break;
                            
                        case "Image2Image":
                            // Multi-input gathering
                            byte[] imgInput = null;
                            string txtInput = ""; 
                            
                            if (currentNode.inputConnections.ContainsKey("Image"))
                            {
                                var src = currentNode.inputConnections["Image"];
                                if (nodeResults.ContainsKey(src)) imgInput = nodeResults[src] as byte[];
                            }
                            
                            if (currentNode.inputConnections.ContainsKey("Text"))
                            {
                                 var src = currentNode.inputConnections["Text"];
                                 if (nodeResults.ContainsKey(src)) txtInput = nodeResults[src] as string;
                            }

                            if (imgInput != null)
                            {
                                UpdateStatus($"Calling Image2Image API...");
                                yield return CallImage2Image(imgInput, txtInput, (result) => outputData = result);
                            }
                            else
                            {
                                UpdateStatus("Image2Image needs image input!");
                                pipelineHasError = true;
                            }
                            break;
                            
                        case "Image23D":
                             if (currentNode.connectedFrom != null && nodeResults.ContainsKey(currentNode.connectedFrom))
                                inputData = nodeResults[currentNode.connectedFrom];

                            if (inputData is byte[] imgData2)
                            {
                                UpdateStatus($"Calling Image23D API...");
                                yield return CallImage23D(imgData2, (result) => outputData = result);
                            }
                            else
                            {
                                UpdateStatus("Image23D needs image input!");
                                pipelineHasError = true;
                            }
                            break;
                            
                        case "Text23D":
                             if (currentNode.connectedFrom != null && nodeResults.ContainsKey(currentNode.connectedFrom))
                                inputData = nodeResults[currentNode.connectedFrom];
                                
                            if (inputData is string txtPrompt2)
                            {
                                UpdateStatus($"Calling Text23D API...");
                                yield return CallText23D(txtPrompt2, (result) => outputData = result);
                            }
                            else
                                 { UpdateStatus("Text23D needs text input!"); pipelineHasError = true; }
                            break;
                            
                        case "Preview":
                            // No output for preview, just display
                            if (currentNode.connectedFrom != null && nodeResults.ContainsKey(currentNode.connectedFrom))
                                inputData = nodeResults[currentNode.connectedFrom];

                            if (inputData is byte[] imageOrModelData && imageOrModelData.Length > 100)
                            {
                                if (IsGLB(imageOrModelData))
                                {
                                    yield return LoadModelInNode(imageOrModelData, currentNode);
                                }
                                else
                                {
                                    DisplayImageInNode(imageOrModelData, currentNode);
                                }
                            }
                            else
                            {
                                UpdateStatus("Preview node: No valid data to display");
                            }
                            // Preview passes data through
                            outputData = inputData; 
                            break;
                    }
                    
                    // === Cache the result for future incremental runs ===
                    if (outputData != null)
                    {
                        currentNode.cachedResult = outputData;
                    }
                }

                if (pipelineHasError) yield break;

                // Cache result
                if (outputData != null)
                {
                    nodeResults[currentNode] = outputData;
                    processedNodes.Add(currentNode);

                    // Add downstream nodes to queue
                    var children = GetOutgoingConnections(currentNode);
                    UpdateStatus($"Node {currentNode.nodeType} has {children.Count} children.");
                    
                    foreach (var nextNode in children)
                    {
                        // Add if not already in list to avoid duplicates? 
                        // Actually standard BFS/Graph execution might visit nodes multiple times if multiple inputs?
                        // But we check 'processedNodes'.
                        // Though for Image2Image we might want to revisit?
                        // No, processedNodes check handles it.
                        
                        // Just Add.
                        UpdateStatus($"Enqueuing child: {nextNode.nodeType}");
                        executionList.Add(nextNode);
                    }
                }
                
                // FORCE UI UPDATE if we just processed a Preview node
                if (currentNode.nodeType == "Preview")
                {
                    yield return null; 
                    // Optional: yield return new WaitForEndOfFrame();
                }
            }
            
            if (!pipelineHasError)
            {
                UpdateStatus("Pipeline finished!");
            }
        }

        // Helper to find all nodes that have 'connectedFrom' == node
        // Warning: The current data structure 'connectedFrom' is single-link on the receiving end.
        // We need to find which nodes point TO 'node' as their 'connectedFrom'.
        // Wait, the existing structure is: NodeData.connectedTo (single). 
        // Logic check: "from.connectedTo = to;" 
        // This implies each node can only output to ONE node.
        // **This is the root cause.**
        // The user screenshot shows output connected to multiple nodes? 
        // If the UI allows dragging lines to multiple nodes, the data structure must support it.
        // Let's check CreateConnection/NodeData.
        
        // RE-READING NodeData:
        // public NodeData connectedTo;      // Output connection (Single?)
        // public NodeData connectedFrom;    // Input connection (Single?)
        
        // If the user visually connected one output to two inputs (Text2Image -> Preview AND Text2Image -> Image23D),
        // but 'connectedTo' only stores ONE reference, then one connection overwrote the other in data, 
        // even if lines might be drawn (if existing connections list stores them).
        
        // Let's look at CreateConnection:
        // from.connectedTo = to;
        // connections.Add(line);
        
        // Yes, 'connectedTo' is overwritten. So logic only follows the LAST made connection.
        // I need to change 'connectedTo' to a List<NodeData>.
        
        private bool IsGLB(byte[] data)
        {
            // GLB magic number: 0x46546C67 ("glTF" in little-endian)
            if (data.Length >= 4)
            {
                return data[0] == 0x67 && data[1] == 0x6C && data[2] == 0x54 && data[3] == 0x46;
            }
            return false;
        }
        
        /// <summary>
        /// 在 Preview 节点内显示图片
        /// </summary>
        private void DisplayImageInNode(byte[] pngData, NodeData previewNode)
        {
            UpdateStatus("Displaying image in Preview node...");
            
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(pngData))
            {
                // 显示到节点的 RawImage
                if (previewNode.previewImage != null)
                {
                    previewNode.previewImage.texture = texture;
                    
                    // 隐藏占位符文字 - 使用准确的路径查找
                    var placeholderTransform = previewNode.gameObject.transform.Find("PreviewArea/Placeholder");
                    if (placeholderTransform != null)
                    {
                        placeholderTransform.gameObject.SetActive(false);
                    }
                    
                    UpdateStatus($"Image displayed in node: {texture.width}x{texture.height}");
                }
                else
                {
                    UpdateStatus("Preview node has no image display!");
                }
            }
            else
            {
                UpdateStatus("Failed to load image data");
            }
        }
        
        /// <summary>
        /// 在世界空间显示图片（可选，用于"弹出"预览）
        /// </summary>
        private System.Collections.IEnumerator DisplayImageInWorld(byte[] pngData)
        {
            UpdateStatus("Displaying generated image in world...");
            
            // Create texture from PNG data
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(pngData))
            {
                // Create a quad in world space to display the image
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "GeneratedImage_" + System.DateTime.Now.Ticks;
                
                // Position in front of camera
                if (Camera.main != null)
                {
                    quad.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 3f;
                    quad.transform.LookAt(Camera.main.transform);
                    quad.transform.Rotate(0, 180, 0);
                }
                
                // Scale to match aspect ratio
                float aspect = (float)texture.width / texture.height;
                quad.transform.localScale = new Vector3(2f * aspect, 2f, 1f);
                
                // Apply texture
                Material mat = new Material(Shader.Find("Unlit/Texture"));
                mat.mainTexture = texture;
                quad.GetComponent<Renderer>().material = mat;
                
                // Remove collider
                var collider = quad.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                
                UpdateStatus($"Image displayed in world: {texture.width}x{texture.height}");
            }
            else
            {
                UpdateStatus("Failed to load image data");
            }
            
            yield return null;
        }
        
        // ========== API 调用方法 ==========
        
        private System.Collections.IEnumerator CallText2Image(string prompt, System.Action<byte[]> onComplete)
        {
            string jsonBody = $"{{\"prompt\": \"{EscapeJson(prompt)}\", \"width\": 1024, \"height\": 1024}}";
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            
            UpdateStatus("Calling Text2Image API...");
            
            using (var request = new UnityEngine.Networking.UnityWebRequest(Text2ImageUrl, "POST"))
            {
                request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 120; // 2 minutes timeout
                
                yield return request.SendWebRequest();
                
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    UpdateStatus($"Text2Image error: {request.error}. Is backend running?");
                    pipelineHasError = true;
                    yield break;
                }
                
                // Parse JSON response
                string responseText = request.downloadHandler.text;
                Debug.Log($"[Text2Image] Response: {responseText}");
                
                var response = JsonUtility.FromJson<Text2ImageResponse>(responseText);
                
                if (response.status == "error")
                {
                    UpdateStatus($"Text2Image API error: {response.error}");
                    pipelineHasError = true;
                    yield break;
                }
                
                if (response.imageUrls == null || response.imageUrls.Length == 0)
                {
                    UpdateStatus("No image URLs returned from API");
                    pipelineHasError = true;
                    yield break;
                }
                
                // Download the first image
                string imageUrl = response.imageUrls[0];
                UpdateStatus("Downloading generated image...");
                
                using (var imgRequest = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(imageUrl))
                {
                    yield return imgRequest.SendWebRequest();
                    
                    if (imgRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        UpdateStatus($"Failed to download image: {imgRequest.error}");
                        pipelineHasError = true;
                        yield break;
                    }
                    
                    // Convert texture to PNG bytes
                    var texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(imgRequest);
                    byte[] pngData = texture.EncodeToPNG();
                    UpdateStatus($"Image downloaded: {texture.width}x{texture.height}");
                    onComplete?.Invoke(pngData);
                }
            }
        }
        
        // JSON response class for Text2Image
        [System.Serializable]
        private class Text2ImageResponse
        {
            public string status;
            public string error;
            public string task_id;
            public string[] imageUrls;
        }
        
        private string EscapeJson(string str)
        {
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
        
        private System.Collections.IEnumerator CallImage2Image(byte[] imageData, string prompt, System.Action<byte[]> onComplete)
        {
            WWWForm form = new WWWForm();
            form.AddBinaryData("image", imageData, "image.png", "image/png");
            form.AddField("prompt", prompt);
            form.AddField("strength", "0.75");
            
            using (var request = UnityEngine.Networking.UnityWebRequest.Post(Image2ImageUrl, form))
            {
                request.timeout = 300; // 5 minutes to be safe
                yield return request.SendWebRequest();
                
                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    onComplete?.Invoke(request.downloadHandler.data);
                else
                    UpdateStatus($"Image2Image error: {request.error}");
            }
        }
        
        private System.Collections.IEnumerator CallImage23D(byte[] imageData, System.Action<byte[]> onComplete)
        {
            WWWForm form = new WWWForm();
            form.AddBinaryData("image", imageData, "image.png", "image/png");
            form.AddField("format", "glb");
            
            using (var request = UnityEngine.Networking.UnityWebRequest.Post(Image23DUrl, form))
            {
                request.timeout = 300;
                yield return request.SendWebRequest();
                
                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    onComplete?.Invoke(request.downloadHandler.data);
                else
                    UpdateStatus($"Image23D error: {request.error}");
            }
        }
        
        private System.Collections.IEnumerator CallText23D(string prompt, System.Action<byte[]> onComplete)
        {
            string jsonBody = $"{{\"prompt\": \"{prompt}\"}}";
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            
            using (var request = new UnityEngine.Networking.UnityWebRequest(Text23DUrl, "POST"))
            {
                request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    onComplete?.Invoke(request.downloadHandler.data);
                else
                    UpdateStatus($"Text23D error: {request.error}");
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
            
            UpdateStatus("Model loaded!");
        }
        
        /// <summary>
        /// 在 Preview 节点内显示 3D 模型
        /// </summary>
        private System.Collections.IEnumerator LoadModelInNode(byte[] glbData, NodeData previewNode)
        {
            UpdateStatus("Loading 3D model in Preview node...");
            
            // 保存模型数据用于之后放置到场景
            previewNode.cachedModelData = glbData;
            
            // 清理之前的预览模型和相机
            if (previewNode.previewModel != null)
            {
                Destroy(previewNode.previewModel);
            }
            if (previewNode.previewCamera != null)
            {
                Destroy(previewNode.previewCamera.gameObject);
            }
            
            // 加载 GLB
            var gltf = new GLTFast.GltfImport();
            var loadTask = gltf.LoadGltfBinary(glbData);
            while (!loadTask.IsCompleted) yield return null;
            
            if (!loadTask.Result)
            {
                UpdateStatus("Failed to load model");
                yield break;
            }
            
            // 如果已有模型，先销毁（防止叠加）
            if (previewNode.previewModel != null)
            {
                Destroy(previewNode.previewModel);
                previewNode.previewModel = null;
            }

            // 创建容器（远离主场景）
            Vector3 previewOrigin = new Vector3(1000, 1000, 1000);
            GameObject modelContainer = new GameObject("PreviewModel_" + previewNode.gameObject.GetInstanceID());
            modelContainer.transform.position = previewOrigin;
            previewNode.previewModel = modelContainer;
            
            // 实例化模型
            var instTask = gltf.InstantiateMainSceneAsync(modelContainer.transform);
            while (!instTask.IsCompleted) yield return null;
            
            if (!instTask.Result)
            {
                UpdateStatus("Failed to instantiate model");
                Destroy(modelContainer);
                yield break;
            }
            
            // 计算边界（世界坐标）
            Bounds worldBounds = CalculateBounds(modelContainer);
            float maxSize = Mathf.Max(worldBounds.size.x, worldBounds.size.y, worldBounds.size.z);
            float scale = (maxSize > 0) ? (2f / maxSize) : 1f;
            
            // 计算模型中心相对于 modelContainer 的本地偏移
            Vector3 localBoundsCenter = modelContainer.transform.InverseTransformPoint(worldBounds.center);
            
            // 先缩放
            modelContainer.transform.localScale = Vector3.one * scale;
            
            // 再居中：将本地中心偏移到预览原点
            modelContainer.transform.position = previewOrigin - localBoundsCenter * scale;
            
            // 销毁旧相机
            if (previewNode.previewCamera != null)
            {
                Destroy(previewNode.previewCamera.gameObject);
                previewNode.previewCamera = null;
            }

            // 创建预览相机
            GameObject camObj = new GameObject("PreviewCamera_" + previewNode.gameObject.GetInstanceID());
            camObj.transform.position = previewOrigin + new Vector3(0, 0.3f, -5f);
            camObj.transform.LookAt(previewOrigin);
            
            Camera previewCam = camObj.AddComponent<Camera>();
            previewCam.enabled = true;
            previewCam.clearFlags = CameraClearFlags.SolidColor;
            previewCam.backgroundColor = new Color(0.15f, 0.18f, 0.22f);
            previewCam.cullingMask = -1;
            previewCam.nearClipPlane = 0.01f;
            previewCam.farClipPlane = 200f;
            previewCam.fieldOfView = 35f;
            previewCam.depth = 50;
            previewNode.previewCamera = previewCam;
            
            // 添加灯光
            GameObject lightObj = new GameObject("PreviewLight");
            lightObj.transform.position = previewOrigin + new Vector3(3, 4, -3);
            lightObj.transform.LookAt(previewOrigin);
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            lightObj.transform.SetParent(modelContainer.transform);
            
            // 创建 RenderTexture
            if (previewNode.previewRT != null)
            {
                previewNode.previewRT.Release();
            }
            previewNode.previewRT = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            previewNode.previewRT.Create();
            previewCam.targetTexture = previewNode.previewRT;
            
            // 强制相机渲染一帧
            previewCam.Render();
            
            // 显示到节点的 RawImage
            if (previewNode.previewImage != null)
            {
                previewNode.previewImage.texture = previewNode.previewRT;
                
                // 隐藏占位符 - 使用准确的路径查找
                var placeholderTransform = previewNode.gameObject.transform.Find("PreviewArea/Placeholder");
                if (placeholderTransform != null)
                {
                    placeholderTransform.gameObject.SetActive(false);
                }
            }
            
            // 添加旋转动画
            modelContainer.AddComponent<ModelRotator>().rotationSpeed = 25f;
            
            // 添加 "Place in Scene" 按钮到节点
            AddPlaceInSceneButton(previewNode);
            
            UpdateStatus("3D Model loaded in Preview node! Click 'Place' to add to scene.");
        }
        
        /// <summary>
        /// 给 Preview 节点添加 "Place in Scene" / "Add to Bag" 按钮
        /// </summary>
        private void AddPlaceInSceneButton(NodeData previewNode)
        {
            // 检查是否已存在按钮
            if (previewNode.placeButton != null) return;
            
            // 找到节点的 RectTransform
            RectTransform nodeRect = previewNode.gameObject.GetComponent<RectTransform>();
            if (nodeRect == null) return;

            // 底部按钮容器
            GameObject btnRoot = new GameObject("PreviewButtons");
            btnRoot.transform.SetParent(previewNode.gameObject.transform, false);
            RectTransform rootRect = btnRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.sizeDelta = new Vector2(-8, 26);
            rootRect.anchoredPosition = new Vector2(0, 4);

            var hlg = btnRoot.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.padding = new RectOffset(4, 4, 0, 2);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = true;

            // Place in Scene 按钮
            GameObject placeObj = new GameObject("PlaceInSceneBtn");
            placeObj.transform.SetParent(btnRoot.transform, false);
            RectTransform placeRect = placeObj.AddComponent<RectTransform>();
            Image placeImg = placeObj.AddComponent<Image>();
            placeImg.color = new Color(0.3f, 0.65f, 0.35f);
            Button placeBtn = placeObj.AddComponent<Button>();
            placeBtn.targetGraphic = placeImg;
            placeBtn.onClick.AddListener(() => OnPlaceModelFromNode(previewNode));
            previewNode.placeButton = placeBtn;

            var lePlace = placeObj.AddComponent<LayoutElement>();
            lePlace.preferredHeight = 22;

            GameObject placeTextObj = new GameObject("Text");
            placeTextObj.transform.SetParent(placeObj.transform, false);
            var placeText = placeTextObj.AddComponent<TextMeshProUGUI>();
            placeText.text = "放置";
            placeText.fontSize = 11;
            placeText.alignment = TextAlignmentOptions.Center;
            placeText.color = Color.white;
            RectTransform placeTextRect = placeTextObj.GetComponent<RectTransform>();
            placeTextRect.anchorMin = Vector2.zero;
            placeTextRect.anchorMax = Vector2.one;
            placeTextRect.sizeDelta = Vector2.zero;

            // Add to Bag 按钮
            GameObject bagObj = new GameObject("AddToBagBtn");
            bagObj.transform.SetParent(btnRoot.transform, false);
            RectTransform bagRect = bagObj.AddComponent<RectTransform>();
            Image bagImg = bagObj.AddComponent<Image>();
            bagImg.color = new Color(0.35f, 0.55f, 0.9f);
            Button bagBtn = bagObj.AddComponent<Button>();
            bagBtn.targetGraphic = bagImg;
            bagBtn.onClick.AddListener(() => OnAddToBagFromNode(previewNode));

            var leBag = bagObj.AddComponent<LayoutElement>();
            leBag.preferredHeight = 22;

            GameObject bagTextObj = new GameObject("Text");
            bagTextObj.transform.SetParent(bagObj.transform, false);
            var bagText = bagTextObj.AddComponent<TextMeshProUGUI>();
            bagText.text = "加入背包";
            bagText.fontSize = 11;
            bagText.alignment = TextAlignmentOptions.Center;
            bagText.color = Color.white;
            RectTransform bagTextRect = bagTextObj.GetComponent<RectTransform>();
            bagTextRect.anchorMin = Vector2.zero;
            bagTextRect.anchorMax = Vector2.one;
            bagTextRect.sizeDelta = Vector2.zero;
        }
        
        private void OnPlaceModelFromNode(NodeData previewNode)
        {
            if (previewNode.cachedModelData == null) return;
            StartCoroutine(PlaceModelFromNodeCoroutine(previewNode));
        }

        /// <summary>
        /// 将 Preview 节点中的模型 GLB 写入 Assets/Resources/Placeables，供 ModelLibrary 使用。
        /// </summary>
        private void OnAddToBagFromNode(NodeData previewNode)
        {
            if (previewNode.cachedModelData == null || previewNode.cachedModelData.Length == 0)
            {
                UpdateStatus("No GLB data to add to bag.");
                return;
            }

#if UNITY_EDITOR
            try
            {
                const string relDir = "Assets/Resources/Placeables";
                if (!Directory.Exists(relDir))
                    Directory.CreateDirectory(relDir);

                var timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"NodePreview_{timestamp}.glb";
                var fullPath = Path.Combine(relDir, fileName);

                File.WriteAllBytes(fullPath, previewNode.cachedModelData);
                UnityEditor.AssetDatabase.Refresh();

                UpdateStatus($"Saved to bag: {fileName}");
            }
            catch (System.Exception ex)
            {
                UpdateStatus($"Add to bag failed: {ex.Message}");
            }
#else
            UpdateStatus("Add to Bag only works in Unity Editor (writes to Assets/Resources/Placeables).");
#endif
        }
        
        private System.Collections.IEnumerator PlaceModelFromNodeCoroutine(NodeData previewNode)
        {
            var gltf = new GLTFast.GltfImport();
            var loadTask = gltf.LoadGltfBinary(previewNode.cachedModelData);
            while (!loadTask.IsCompleted) yield return null;
            
            if (!loadTask.Result) yield break;
            
            Vector3 spawnPos = Camera.main.transform.position + Camera.main.transform.forward * 3f;
            spawnPos.y = 0; // 放在地面上
            
            GameObject modelObj = new GameObject("GeneratedModel_" + System.DateTime.Now.Ticks);
            modelObj.transform.position = spawnPos;
            
            var instTask = gltf.InstantiateMainSceneAsync(modelObj.transform);
            while (!instTask.IsCompleted) yield return null;
            
            // 计算边界并调整缩放
            Bounds bounds = CalculateBounds(modelObj);
            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize > 0)
            {
                float targetSize = 1.5f; // 目标大小（米）
                float scale = targetSize / maxSize;
                modelObj.transform.localScale = Vector3.one * scale;
            }
            
            // 重新计算边界并居中到地面
            bounds = CalculateBounds(modelObj);
            modelObj.transform.position = new Vector3(spawnPos.x, -bounds.min.y, spawnPos.z);
            
            // 使用 BoxCollider 代替慢的 MeshCollider
            BoxCollider boxCollider = modelObj.AddComponent<BoxCollider>();
            boxCollider.center = bounds.center - modelObj.transform.position;
            boxCollider.size = bounds.size;
            
            // 添加刚体（设为 Kinematic 避免物理计算）
            Rigidbody rb = modelObj.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = false;
            rb.isKinematic = true; // 不参与物理模拟，但可以接收点击
            
            // 添加交互组件 - 支持留言和光晕
            InteractableObject interactable = modelObj.AddComponent<InteractableObject>();
            Debug.Log($"[Interaction] Added InteractableObject to {modelObj.name}");
            
            // 确保场景中有 ObjectInteractionManager
            EnsureInteractionManager();
            
            // 保存模型文件到 Assets/Resources/Placeables
            string resourcesPath = Application.dataPath + "/Resources/Placeables";
            if (!System.IO.Directory.Exists(resourcesPath))
            {
                System.IO.Directory.CreateDirectory(resourcesPath);
            }
            string filename = $"generated_{System.DateTime.Now.Ticks}.glb";
            string fullPath = System.IO.Path.Combine(resourcesPath, filename);
            try 
            {
                System.IO.File.WriteAllBytes(fullPath, previewNode.cachedModelData);
                Debug.Log($"[NodeEditor] Saved generated model to: {fullPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NodeEditor] Failed to save model file: {e.Message}");
            }
            
            UpdateStatus($"Model placed at {modelObj.transform.position}");
        }
        
        /// <summary>
        /// 计算物体边界
        /// </summary>
        private Bounds CalculateBounds(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(obj.transform.position, Vector3.one);
            
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }
        
        /// <summary>
        /// 确保场景中有 ObjectInteractionManager
        /// </summary>
        private void EnsureInteractionManager()
        {
            if (ObjectInteractionManager.Instance == null)
            {
                GameObject managerObj = new GameObject("ObjectInteractionManager");
                managerObj.AddComponent<ObjectInteractionManager>();
            }
        }
        
        private void OnClearClicked()
        {
            foreach (var node in nodeList)
            {
                // Clear cached results
                node.cachedResult = null;
                node.cachedInputPrompt = null;
                
                if (node.previewModel != null) Destroy(node.previewModel);
                if (node.previewCamera != null) Destroy(node.previewCamera.gameObject);
                if (node.gameObject != null) Destroy(node.gameObject);
            }
            nodeList.Clear();
            
            foreach (var conn in connections)
                if (conn != null) Destroy(conn.gameObject);
            connections.Clear();
            
            UpdateStatus("Canvas cleared (all cache invalidated)");
        }
        
        private void UpdateStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            Debug.Log($"[NodeEditor] {msg}");
        }
    }
    
    // ===== 辅助类 =====
    
    /// <summary>
    /// 数据类型枚举
    /// </summary>
    public enum DataType
    {
        None,       // 无/起始
        Text,       // 文本 (prompt)
        Image,      // 图片
        Model3D     // 3D 模型
    }
    
    public class NodeData
    {
        public GameObject gameObject;
        public string nodeType;
        public RectTransform inputPort;
        public RectTransform outputPort;
        public TMP_InputField inputField;
        public RawImage previewImage;     // Preview 节点的图片显示
        public GameObject previewModel;   // Preview 节点的 3D 模型
        public Camera previewCamera;      // 预览相机
        public RenderTexture previewRT;   // 预览渲染纹理
        public byte[] cachedModelData;    // 缓存的模型数据用于放置到场景
        public Button placeButton;        // "Place in Scene" 按钮
        public List<NodeData> connectedToNodes = new List<NodeData>(); // Output connections
        
        // === Incremental Execution: Cached Results ===
        public object cachedResult;       // 缓存的执行结果 (string, byte[], etc.)
        public string cachedInputPrompt;  // TextInput 节点：缓存的输入文本（用于检测变化）
        
        // Multi-input support: PortID -> Connected Source Node
        public Dictionary<string, NodeData> inputConnections = new Dictionary<string, NodeData>();
        
        // Helper for backwards compatibility or single-input nodes
        public NodeData connectedFrom => inputConnections.Count > 0 ? (new List<NodeData>(inputConnections.Values))[0] : null;

        public bool HasInputConnection(string portID) => inputConnections.ContainsKey(portID) && inputConnections[portID] != null;
        
        public bool HasIncomingConnection => connectedFrom != null;
        
        /// <summary>
        /// 获取节点的输入类型要求
        /// </summary>
        public DataType InputType
        {
            get
            {
                switch (nodeType)
                {
                    case "TextInput":   return DataType.None;     // 无输入
                    case "ImageInput":  return DataType.None;     // 无输入
                    case "Text2Image":  return DataType.Text;     // 需要文本
                    case "Image2Image": return DataType.Image;    // 需要图片
                    case "Image23D":    return DataType.Image;    // 需要图片
                    case "Text23D":     return DataType.Text;     // 需要文本
                    case "Preview":     return DataType.Image | DataType.Model3D; // 图片或模型
                    default:            return DataType.None;
                }
            }
        }
        
        /// <summary>
        /// 获取节点的输出类型
        /// </summary>
        public DataType OutputType
        {
            get
            {
                switch (nodeType)
                {
                    case "TextInput":   return DataType.Text;     // 输出文本
                    case "ImageInput":  return DataType.Image;    // 输出图片
                    case "Text2Image":  return DataType.Image;    // 输出图片
                    case "Image2Image": return DataType.Image;    // 输出图片
                    case "Image23D":    return DataType.Model3D;  // 输出3D模型
                    case "Text23D":     return DataType.Model3D;  // 输出3D模型
                    case "Preview":     return DataType.None;     // 终端节点
                    default:            return DataType.None;
                }
            }
        }
        
        /// <summary>
        /// 检查是否可以连接到目标节点
        /// </summary>
        /// <summary>
        /// 检查是否可以连接到目标节点
        /// </summary>
        public bool CanConnectTo(NodeData target, string targetPortID, out string error)
        {
            error = "";
            if (target == null) { error = "Target is null"; return false; }
            if (target == this) { error = "Self-loop"; return false; }
            
            if (target.HasInputConnection(targetPortID))
            {
                error = "Port occupied";
                return false;
            }

            // Determine Target Port Type
            DataType targetInputType = DataType.None;
            if (target.nodeType == "Image2Image")
            {
                if (targetPortID == "Image") targetInputType = DataType.Image;
                else if (targetPortID == "Text") targetInputType = DataType.Text;
            }
            else
            {
                targetInputType = target.InputType; // Fallback
            }

            if ((this.OutputType & targetInputType) == 0)
            {
                error = $"Mismatch: {this.OutputType} -> {targetInputType}";
                return false;
            }
            
            return true;
        }

        public RectTransform GetInputPortRect(string portID)
        {
            // Simple lookup based on siblings?
            // Or better: store map when creating ports.
            // Re-finding children by name:
            if (portID == "default") return inputPort;
            var portObj = gameObject.transform.Find($"InputPort_{portID}");
            return portObj != null ? portObj.GetComponent<RectTransform>() : inputPort;
        }
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
    
    /// <summary>
    /// 模型旋转组件 - 用于 Preview 节点中的 3D 模型预览
    /// </summary>
    public class ModelRotator : MonoBehaviour
    {
        public float rotationSpeed = 30f;
        
        void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}
