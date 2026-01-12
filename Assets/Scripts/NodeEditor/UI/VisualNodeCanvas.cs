using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

namespace AIPipeline.UI
{
    /// <summary>
    /// 可视化节点编辑器画布
    /// 管理所有节点、连接线、右键菜单
    /// </summary>
    public class VisualNodeCanvas : MonoBehaviour, IPointerClickHandler, IScrollHandler
    {
        [Header("Canvas Settings")]
        [SerializeField] private RectTransform nodeContainer;
        [SerializeField] private RectTransform connectionContainer;
        [SerializeField] private GameObject contextMenu;
        
        [Header("Prefabs")]
        [SerializeField] private GameObject nodePrefab;
        [SerializeField] private GameObject connectionLinePrefab;
        
        [Header("Style - Romantic Theme 💕")]
        [SerializeField] private Color canvasBackground = new Color(0.12f, 0.12f, 0.15f, 1f);
        [SerializeField] private Color textInputColor = new Color(0.6f, 0.8f, 1f, 1f);
        [SerializeField] private Color text2ImageColor = new Color(0.9f, 0.6f, 0.8f, 1f);
        [SerializeField] private Color image23DColor = new Color(1f, 0.7f, 0.75f, 1f);
        [SerializeField] private Color text23DColor = new Color(1f, 0.6f, 0.7f, 1f);
        [SerializeField] private Color previewColor = new Color(0.6f, 0.9f, 0.7f, 1f);
        
        [Header("State")]
        public List<VisualNode> nodes = new List<VisualNode>();
        public List<NodeConnectionLine> connections = new List<NodeConnectionLine>();
        public VisualNode selectedNode;
        
        private Canvas canvas;
        private float currentScale = 1f;
        private Vector2 canvasOffset = Vector2.zero;
        
        public float CanvasScale => canvas != null ? canvas.scaleFactor : 1f;
        
        void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            
            // 设置背景色
            var bg = GetComponent<Image>();
            if (bg != null)
                bg.color = canvasBackground;
        }
        
        void Start()
        {
            if (contextMenu != null)
                contextMenu.SetActive(false);
        }
        
        void Update()
        {
            // ESC 关闭菜单
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (contextMenu != null && contextMenu.activeSelf)
                    contextMenu.SetActive(false);
            }
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            // 右键打开菜单
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                ShowContextMenu(eventData.position);
            }
            // 左键点击空白处取消选择
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (selectedNode != null)
                {
                    selectedNode.SetSelected(false);
                    selectedNode = null;
                }
                if (contextMenu != null)
                    contextMenu.SetActive(false);
            }
        }
        
        public void OnScroll(PointerEventData eventData)
        {
            // 滚轮缩放（预留功能）
            float scroll = eventData.scrollDelta.y;
            currentScale = Mathf.Clamp(currentScale + scroll * 0.1f, 0.5f, 2f);
        }
        
        private void ShowContextMenu(Vector2 screenPos)
        {
            if (contextMenu == null) return;
            
            contextMenu.SetActive(true);
            
            RectTransform menuRect = contextMenu.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                nodeContainer, screenPos, null, out Vector2 localPos);
            menuRect.anchoredPosition = localPos;
        }
        
        /// <summary>
        /// 创建新节点
        /// </summary>
        public VisualNode CreateNode(string type, Vector2 position)
        {
            if (nodePrefab == null)
            {
                Debug.LogError("Node prefab not set!");
                return null;
            }
            
            GameObject nodeObj = Instantiate(nodePrefab, nodeContainer);
            VisualNode node = nodeObj.GetComponent<VisualNode>();
            
            if (node == null)
                node = nodeObj.AddComponent<VisualNode>();
            
            // 根据类型设置颜色和标题
            Color color = text23DColor;
            string title = type;
            
            switch (type)
            {
                case "TextInput":
                    color = textInputColor;
                    title = "📝 Text Input";
                    break;
                case "Text2Image":
                    color = text2ImageColor;
                    title = "🎨 Text → Image";
                    break;
                case "Image23D":
                    color = image23DColor;
                    title = "🖼️ Image → 3D";
                    break;
                case "Text23D":
                    color = text23DColor;
                    title = "✨ Text → 3D";
                    break;
                case "Preview":
                    color = previewColor;
                    title = "👁️ Preview";
                    break;
            }
            
            node.Initialize(System.Guid.NewGuid().ToString(), title, color);
            
            RectTransform rt = nodeObj.GetComponent<RectTransform>();
            rt.anchoredPosition = position;
            
            nodes.Add(node);
            
            if (contextMenu != null)
                contextMenu.SetActive(false);
            
            return node;
        }
        
        /// <summary>
        /// 选中节点
        /// </summary>
        public void SelectNode(VisualNode node)
        {
            if (selectedNode != null)
                selectedNode.SetSelected(false);
            
            selectedNode = node;
            if (node != null)
                node.SetSelected(true);
        }
        
        /// <summary>
        /// 连接两个节点
        /// </summary>
        public void ConnectNodes(VisualNode from, VisualNode to)
        {
            if (from == null || to == null || from == to)
                return;
            
            from.connectedOutput = to;
            to.connectedInput = from;
            
            // 创建连接线
            CreateConnectionLine(from, to);
        }
        
        private void CreateConnectionLine(VisualNode from, VisualNode to)
        {
            if (connectionLinePrefab == null)
            {
                // 如果没有预制件，创建简单的线
                GameObject lineObj = new GameObject("Connection");
                lineObj.transform.SetParent(connectionContainer, false);
                
                var line = lineObj.AddComponent<NodeConnectionLine>();
                line.Initialize(from, to);
                connections.Add(line);
            }
        }
        
        /// <summary>
        /// 更新所有连接线
        /// </summary>
        public void UpdateConnections()
        {
            foreach (var conn in connections)
            {
                if (conn != null)
                    conn.UpdateLine();
            }
        }
        
        // ===== 右键菜单按钮回调 =====
        
        public void OnAddTextInputNode()
        {
            Vector2 pos = contextMenu.GetComponent<RectTransform>().anchoredPosition;
            CreateNode("TextInput", pos);
        }
        
        public void OnAddText2ImageNode()
        {
            Vector2 pos = contextMenu.GetComponent<RectTransform>().anchoredPosition;
            CreateNode("Text2Image", pos);
        }
        
        public void OnAddImage23DNode()
        {
            Vector2 pos = contextMenu.GetComponent<RectTransform>().anchoredPosition;
            CreateNode("Image23D", pos);
        }
        
        public void OnAddText23DNode()
        {
            Vector2 pos = contextMenu.GetComponent<RectTransform>().anchoredPosition;
            CreateNode("Text23D", pos);
        }
        
        public void OnAddPreviewNode()
        {
            Vector2 pos = contextMenu.GetComponent<RectTransform>().anchoredPosition;
            CreateNode("Preview", pos);
        }
    }
}
