using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Morphis.UI.HotBar
{
    /// <summary>
    /// HotBar 单个格子 - 显示预览图并支持拖拽
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class HotBarSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image previewImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TMP_Text tooltipText;

        private HotBarManager _manager;
        private PlaceableItem _item;
        private RectTransform _dragIcon;
        private Canvas _dragCanvas;
        private Image _backgroundImage;
        private Color _originalColor;

        public void Init(HotBarManager manager)
        {
            _manager = manager;
            _backgroundImage = GetComponent<Image>();
            if (_backgroundImage != null)
                _originalColor = _backgroundImage.color;

            // 自动查找子组件
            if (previewImage == null)
            {
                var previewChild = transform.Find("Preview");
                if (previewChild != null)
                    previewImage = previewChild.GetComponent<Image>();
                
                // 如果没有 Preview 子物体，创建一个
                if (previewImage == null)
                {
                    var previewGO = new GameObject("Preview");
                    previewGO.transform.SetParent(transform, false);
                    var rt = previewGO.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.1f, 0.1f);
                    rt.anchorMax = new Vector2(0.9f, 0.9f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    previewImage = previewGO.AddComponent<Image>();
                    previewImage.preserveAspect = true;
                }
            }

            if (nameLabel == null)
                nameLabel = GetComponentInChildren<TMP_Text>();

            // 隐藏 tooltip
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }

        public void SetItem(PlaceableItem item)
        {
            _item = item;
            gameObject.SetActive(true);

            // 显示名称
            if (nameLabel != null)
            {
                string displayName = item.Name;
                if (displayName.Length > 8)
                    displayName = displayName.Substring(0, 7) + "..";
                nameLabel.text = displayName;
            }

            // 清除预览图（等待异步加载）
            if (previewImage != null)
            {
                previewImage.sprite = null;
                previewImage.color = new Color(1, 1, 1, 0.3f);
            }
        }

        public void SetPreview(Sprite preview)
        {
            if (previewImage != null && preview != null)
            {
                previewImage.sprite = preview;
                previewImage.color = Color.white;
            }
        }

        public void SetEmpty()
        {
            _item = default;
            
            if (previewImage != null)
            {
                previewImage.sprite = null;
                previewImage.color = new Color(1, 1, 1, 0.1f);
            }

            if (nameLabel != null)
                nameLabel.text = "";
            
            // 保持格子可见但显示为空
            gameObject.SetActive(true);
        }

        #region Drag Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_item.IsEmpty || _manager == null) return;

            _dragCanvas = _manager.Canvas;
            if (_dragCanvas == null) return;

            // 创建拖拽图标
            var iconGO = new GameObject("DragIcon");
            iconGO.transform.SetParent(_dragCanvas.transform, false);
            _dragIcon = iconGO.AddComponent<RectTransform>();
            _dragIcon.sizeDelta = new Vector2(80, 80);

            var img = iconGO.AddComponent<Image>();
            if (previewImage != null && previewImage.sprite != null)
            {
                img.sprite = previewImage.sprite;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.3f, 0.5f, 0.3f, 0.9f);
            }

            // 添加名称标签
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(iconGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(1, 0);
            labelRT.pivot = new Vector2(0.5f, 1);
            labelRT.sizeDelta = new Vector2(0, 20);
            labelRT.anchoredPosition = new Vector2(0, -5);

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = _item.Name;
            tmp.fontSize = 10;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            // 添加背景
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(labelGO.transform, false);
            bgGO.transform.SetAsFirstSibling();
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = new Vector2(-5, 0);
            bgRT.offsetMax = new Vector2(5, 0);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.7f);
            bgImg.raycastTarget = false;

            UpdateDragIcon(eventData);

            // 高亮当前格子
            if (_backgroundImage != null)
                _backgroundImage.color = new Color(_originalColor.r * 1.3f, _originalColor.g * 1.3f, _originalColor.b * 1.3f, _originalColor.a);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateDragIcon(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // 恢复格子颜色
            if (_backgroundImage != null)
                _backgroundImage.color = _originalColor;

            if (_dragIcon != null)
            {
                Destroy(_dragIcon.gameObject);
                _dragIcon = null;
            }

            if (_item.IsEmpty || _manager == null) return;

            // 检查是否在 UI 上释放
            if (EventSystem.current.IsPointerOverGameObject())
            {
                // 检查是否在 HotBar 区域内
                var hotBarRect = _manager.GetComponent<RectTransform>();
                if (hotBarRect != null && RectTransformUtility.RectangleContainsScreenPoint(hotBarRect, eventData.position, _dragCanvas?.worldCamera))
                {
                    return; // 在 HotBar 内释放，不放置
                }
            }

            // 放置物品
            _manager.TryPlace(_item, eventData.position);
        }

        private void UpdateDragIcon(PointerEventData eventData)
        {
            if (_dragIcon == null || _dragCanvas == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _dragCanvas.transform as RectTransform,
                eventData.position,
                _dragCanvas.worldCamera,
                out var localPos
            );
            _dragIcon.anchoredPosition = localPos;
        }

        #endregion

        #region Tooltip

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_item.IsEmpty) return;

            if (tooltipPanel != null && tooltipText != null)
            {
                tooltipText.text = _item.Name;
                tooltipPanel.SetActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }

        #endregion
    }
}
