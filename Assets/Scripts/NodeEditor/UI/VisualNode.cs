using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace AIPipeline.UI
{
    /// <summary>
    /// 可视化节点 UI 组件
    /// 支持拖拽、端口连接
    /// </summary>
    public class VisualNode : MonoBehaviour, IDragHandler, IBeginDragHandler, IPointerClickHandler
    {
        [Header("Node Settings")]
        public string nodeId;
        public string nodeTitle = "Node";
        public Color nodeColor = new Color(1f, 0.71f, 0.76f, 1f); // 浅粉红
        
        [Header("UI Elements")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform inputPort;
        [SerializeField] private RectTransform outputPort;
        [SerializeField] private RectTransform contentArea;
        
        [Header("State")]
        public bool isSelected = false;
        public VisualNode connectedInput;
        public VisualNode connectedOutput;
        
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private VisualNodeCanvas parentCanvas;
        
        public RectTransform InputPortTransform => inputPort;
        public RectTransform OutputPortTransform => outputPort;
        
        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
            parentCanvas = GetComponentInParent<VisualNodeCanvas>();
        }
        
        public void Initialize(string id, string title, Color color)
        {
            nodeId = id;
            nodeTitle = title;
            nodeColor = color;
            
            if (titleText != null)
                titleText.text = title;
            
            if (backgroundImage != null)
                backgroundImage.color = color;
        }
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = false;
            transform.SetAsLastSibling(); // 拖拽时置顶
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            rectTransform.anchoredPosition += eventData.delta / parentCanvas.CanvasScale;
            
            // 通知画布更新连接线
            if (parentCanvas != null)
                parentCanvas.UpdateConnections();
        }
        
        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (parentCanvas != null)
                parentCanvas.SelectNode(this);
        }
        
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            // 选中时添加边框效果
            if (backgroundImage != null)
            {
                var outline = backgroundImage.GetComponent<Outline>();
                if (outline == null)
                    outline = backgroundImage.gameObject.AddComponent<Outline>();
                outline.enabled = selected;
                outline.effectColor = new Color(1f, 0.4f, 0.6f, 1f); // 玫瑰色边框
                outline.effectDistance = new Vector2(3, 3);
            }
        }
        
        /// <summary>
        /// 添加自定义内容到节点
        /// </summary>
        public void AddContent(GameObject content)
        {
            if (contentArea != null)
            {
                content.transform.SetParent(contentArea, false);
            }
        }
        
        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            OnDrag(eventData);
        }
    }
}
