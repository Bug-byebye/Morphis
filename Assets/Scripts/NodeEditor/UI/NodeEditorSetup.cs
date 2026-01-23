using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace AIPipeline.UI
{
    /// <summary>
    /// 场景启动时自动创建节点编辑器 UI
    /// 自动初始化，无需手动添加到场景
    /// </summary>
    public class NodeEditorSetup : MonoBehaviour
    {
        [Header("Auto Setup")]
        public bool setupOnStart = true;
        
        private GameObject editorRoot;
        private VisualNodeCanvas nodeCanvas;
        
        private static NodeEditorSetup instance;
        
        /// <summary>
        /// Auto-initialize when any scene loads (except Boot scene)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnSceneLoaded()
        {
            SceneManager.sceneLoaded += OnSceneLoadedHandler;
            // Also check current scene
            CheckAndSetup();
        }
        
        static void OnSceneLoadedHandler(Scene scene, LoadSceneMode mode)
        {
            CheckAndSetup();
        }
        
        static void CheckAndSetup()
        {
            // Don't setup if we're in the Boot scene
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Boot") || sceneName.Contains("Login"))
                return;
            
            // Don't setup if already exists
            if (instance != null)
                return;
            
            // Skip if BootCanvas is still active (still in login flow)
            var bootCanvas = GameObject.Find("BootCanvas");
            if (bootCanvas != null && bootCanvas.activeInHierarchy)
                return;
            
            // Create the setup manager
            GameObject setupObj = new GameObject("NodeEditorSetup_Auto");
            instance = setupObj.AddComponent<NodeEditorSetup>();
            DontDestroyOnLoad(setupObj);
            
            Debug.Log("[NodeEditorSetup] Auto-initialized in scene: " + sceneName);
        }
        
        void Start()
        {
            if (setupOnStart)
            {
                SetupNodeEditor();
            }
        }
        
        public void SetupNodeEditor()
        {
            // 创建主 Canvas
            GameObject canvasObj = new GameObject("NodeEditorCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // 编辑器根节点（用于 Tab 显示/隐藏）
            editorRoot = new GameObject("EditorRoot");
            editorRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform rootRect = editorRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            
            // === 节点画布区域 ===
            GameObject canvasArea = new GameObject("NodeCanvas");
            canvasArea.transform.SetParent(editorRoot.transform, false);
            RectTransform canvasRect = canvasArea.AddComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = new Vector2(0, 50); // 底部留空间给工具栏
            canvasRect.offsetMax = Vector2.zero;
            
            Image canvasBg = canvasArea.AddComponent<Image>();
            canvasBg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
            
            nodeCanvas = canvasArea.AddComponent<VisualNodeCanvas>();
            
            // 节点容器
            GameObject nodeContainer = new GameObject("NodeContainer");
            nodeContainer.transform.SetParent(canvasArea.transform, false);
            RectTransform nodeContainerRect = nodeContainer.AddComponent<RectTransform>();
            nodeContainerRect.anchorMin = Vector2.zero;
            nodeContainerRect.anchorMax = Vector2.one;
            nodeContainerRect.offsetMin = Vector2.zero;
            nodeContainerRect.offsetMax = Vector2.zero;
            
            // 连接线容器
            GameObject connContainer = new GameObject("ConnectionContainer");
            connContainer.transform.SetParent(canvasArea.transform, false);
            RectTransform connRect = connContainer.AddComponent<RectTransform>();
            connRect.anchorMin = Vector2.zero;
            connRect.anchorMax = Vector2.one;
            connRect.offsetMin = Vector2.zero;
            connRect.offsetMax = Vector2.zero;
            
            // 设置 Canvas 引用（通过反射或 SerializeField）
            var canvasField = typeof(VisualNodeCanvas).GetField("nodeContainer", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (canvasField != null)
                canvasField.SetValue(nodeCanvas, nodeContainerRect);
            
            var connField = typeof(VisualNodeCanvas).GetField("connectionContainer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (connField != null)
                connField.SetValue(nodeCanvas, connRect);
            
            // === 右键菜单 ===
            GameObject contextMenu = CreateContextMenu();
            contextMenu.transform.SetParent(canvasArea.transform, false);
            
            var menuField = typeof(VisualNodeCanvas).GetField("contextMenu",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (menuField != null)
                menuField.SetValue(nodeCanvas, contextMenu);
            
            // 创建节点预制件并设置
            GameObject nodePrefab = NodePrefabCreator.CreateNodePrefab();
            nodePrefab.SetActive(false);
            nodePrefab.transform.SetParent(canvasObj.transform, false);
            
            var prefabField = typeof(VisualNodeCanvas).GetField("nodePrefab",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (prefabField != null)
                prefabField.SetValue(nodeCanvas, nodePrefab);
            
            // === 底部工具栏 ===
            GameObject toolbar = CreateToolbar();
            toolbar.transform.SetParent(editorRoot.transform, false);
            
            // === 主控制器 ===
            var controller = canvasObj.AddComponent<VisualNodeEditorController>();
            
            // 设置控制器引用
            var editorRootField = typeof(VisualNodeEditorController).GetField("editorRoot",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (editorRootField != null)
                editorRootField.SetValue(controller, editorRoot);
            
            var nodeCanvasField = typeof(VisualNodeEditorController).GetField("nodeCanvas",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (nodeCanvasField != null)
                nodeCanvasField.SetValue(controller, nodeCanvas);
            
            // 工具栏按钮引用
            var executeBtn = toolbar.transform.Find("ExecuteButton")?.GetComponent<Button>();
            var clearBtn = toolbar.transform.Find("ClearButton")?.GetComponent<Button>();
            var statusTxt = toolbar.transform.Find("StatusText")?.GetComponent<TMP_Text>();
            
            var executeBtnField = typeof(VisualNodeEditorController).GetField("executeButton",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (executeBtnField != null)
                executeBtnField.SetValue(controller, executeBtn);
            
            var clearBtnField = typeof(VisualNodeEditorController).GetField("clearButton",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (clearBtnField != null)
                clearBtnField.SetValue(controller, clearBtn);
            
            var statusField = typeof(VisualNodeEditorController).GetField("statusText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (statusField != null)
                statusField.SetValue(controller, statusTxt);
            
            // 初始隐藏
            editorRoot.SetActive(false);
            
            Debug.Log("[NodeEditor] Visual Node Editor setup complete!");
        }
        
        private GameObject CreateContextMenu()
        {
            GameObject menu = new GameObject("ContextMenu");
            RectTransform menuRect = menu.AddComponent<RectTransform>();
            menuRect.sizeDelta = new Vector2(160, 200);
            menuRect.pivot = new Vector2(0, 1);
            
            Image bg = menu.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.18f, 0.98f);
            
            var outline = menu.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.6f, 0.7f, 0.5f);
            
            // 垂直布局
            var layout = menu.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.spacing = 3;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            
            // 标题
            CreateMenuLabel(menu.transform, "➕ Add Node");
            
            // 菜单项
            CreateMenuItem(menu.transform, "📝 Text Input", "OnAddTextInputNode");
            CreateMenuItem(menu.transform, "🎨 Text → Image", "OnAddText2ImageNode");
            CreateMenuItem(menu.transform, "🖼️ Image → 3D", "OnAddImage23DNode");
            CreateMenuItem(menu.transform, "✨ Text → 3D", "OnAddText23DNode");
            CreateMenuItem(menu.transform, "👁️ Preview", "OnAddPreviewNode");
            
            menu.SetActive(false);
            return menu;
        }
        
        private void CreateMenuLabel(Transform parent, string text)
        {
            GameObject label = new GameObject("Label");
            label.transform.SetParent(parent, false);
            
            var le = label.AddComponent<LayoutElement>();
            le.preferredHeight = 25;
            
            TextMeshProUGUI tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(1f, 0.7f, 0.8f, 1f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
        }
        
        private void CreateMenuItem(Transform parent, string text, string methodName)
        {
            GameObject item = new GameObject(text);
            item.transform.SetParent(parent, false);
            
            var le = item.AddComponent<LayoutElement>();
            le.preferredHeight = 28;
            
            Image bg = item.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            
            Button btn = item.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.25f, 1f);
            colors.highlightedColor = new Color(1f, 0.6f, 0.7f, 0.5f);
            colors.pressedColor = new Color(1f, 0.5f, 0.6f, 0.7f);
            btn.colors = colors;
            
            // 添加点击事件（需要在运行时绑定）
            btn.onClick.AddListener(() => {
                var canvas = FindObjectOfType<VisualNodeCanvas>();
                if (canvas != null)
                {
                    var method = typeof(VisualNodeCanvas).GetMethod(methodName);
                    if (method != null)
                        method.Invoke(canvas, null);
                }
            });
            
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(item.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-5, 0);
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 12;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
        }
        
        private GameObject CreateToolbar()
        {
            GameObject toolbar = new GameObject("Toolbar");
            RectTransform toolbarRect = toolbar.AddComponent<RectTransform>();
            toolbarRect.anchorMin = new Vector2(0, 0);
            toolbarRect.anchorMax = new Vector2(1, 0);
            toolbarRect.pivot = new Vector2(0.5f, 0);
            toolbarRect.sizeDelta = new Vector2(0, 50);
            toolbarRect.anchoredPosition = Vector2.zero;
            
            Image bg = toolbar.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.15f, 1f);
            
            var layout = toolbar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 8, 8);
            layout.spacing = 15;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            
            // Execute 按钮
            CreateToolbarButton(toolbar.transform, "ExecuteButton", "▶ Execute", 
                new Color(0.4f, 0.8f, 0.5f, 1f));
            
            // Clear 按钮
            CreateToolbarButton(toolbar.transform, "ClearButton", "🗑️ Clear", 
                new Color(0.8f, 0.4f, 0.4f, 1f));
            
            // Status 文本
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(toolbar.transform, false);
            
            var le = statusObj.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            
            TextMeshProUGUI statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Right-click to add nodes";
            statusText.fontSize = 14;
            statusText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            statusText.alignment = TextAlignmentOptions.MidlineRight;
            
            return toolbar;
        }
        
        private void CreateToolbarButton(Transform parent, string name, string text, Color color)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            
            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 120;
            
            Image bg = btnObj.AddComponent<Image>();
            bg.color = color;
            
            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = color;
            colors.highlightedColor = color * 1.2f;
            colors.pressedColor = color * 0.8f;
            btn.colors = colors;
            
            var outline = btnObj.AddComponent<Outline>();
            outline.effectColor = Color.white * 0.3f;
            
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }
    }
}
