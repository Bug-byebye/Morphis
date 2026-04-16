using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Morphis.InputControl;

namespace Morphis.ModelPlacement
{
    /// <summary>
    /// Context menu that appears when clicking on a placeable object
    /// Allows user to choose: edit, message, or delete object
    /// </summary>
    public class ObjectContextMenu : MonoBehaviour
    {
        public static ObjectContextMenu Instance { get; private set; }

        [Header("UI References")]
        private Canvas _canvas;
        private GameObject _menuPanel;
        private RectTransform _menuRect;
        
        // Button references
        private GameObject _moveBtn;
        private GameObject _rotateBtn;
        private GameObject _scaleBtn;
        private GameObject _messageBtn;
        private GameObject _deleteBtn;
        
        // Current target
        private GameObject _targetObject;
        private Action _onMoveSelected;
        private Action _onRotateSelected;
        private Action _onScaleSelected;
        private Action _onMessageSelected;
        private Action _onDeleteSelected;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                BuildUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void BuildUI()
        {
            // Always use a dedicated canvas so other runtime UI scalers don't stretch the menu.
            var canvasObj = new GameObject("ContextMenuCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 210;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create menu panel
            _menuPanel = new GameObject("ContextMenu");
            _menuPanel.transform.SetParent(_canvas.transform, false);
            _menuRect = _menuPanel.AddComponent<RectTransform>();
            _menuRect.sizeDelta = new Vector2(184f, 100f);
            _menuRect.pivot = new Vector2(0, 1); // Top-left pivot

            // Background
            var bg = _menuPanel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            
            // Add outline
            var outline = _menuPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            outline.effectDistance = new Vector2(2, 2);

            // Vertical layout for buttons
            var layout = _menuPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Move button
            _moveBtn = CreateMenuButton(_menuPanel.transform, "移动物体", new Color(0.2f, 0.5f, 0.9f), OnMoveClicked);
            
            // Rotate button
            _rotateBtn = CreateMenuButton(_menuPanel.transform, "旋转物体", new Color(0.7f, 0.5f, 0.2f), OnRotateClicked);
            
            // Scale button
            _scaleBtn = CreateMenuButton(_menuPanel.transform, "缩放物体", new Color(0.6f, 0.3f, 0.7f), OnScaleClicked);
            
            // Message button
            _messageBtn = CreateMenuButton(_menuPanel.transform, "留言", new Color(0.3f, 0.65f, 0.35f), OnMessageClicked);

            // Delete button
            _deleteBtn = CreateMenuButton(_menuPanel.transform, "删除物体", new Color(0.8f, 0.25f, 0.25f), OnDeleteClicked);

            _menuPanel.SetActive(false);
        }

        private GameObject CreateMenuButton(Transform parent, string text, Color color, Action onClick)
        {
            var btnObj = new GameObject($"Btn_{text}");
            btnObj.transform.SetParent(parent, false);

            var btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(0, 34f);

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = color;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => onClick?.Invoke());

            // Add layout element
            var layoutElem = btnObj.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = 34f;
            layoutElem.minHeight = 34f;

            // Button text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 13f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            
            return btnObj;
        }

        /// <summary>
        /// Show the context menu at mouse position for a specific object (backward-compatible)
        /// </summary>
        public void ShowMenu(GameObject target, Action onMoveSelected, Action onMessageSelected)
        {
            ShowMenu(target, onMoveSelected, null, null, onMessageSelected, null);
        }

        /// <summary>
        /// Show the context menu with all options
        /// </summary>
        public void ShowMenu(GameObject target, Action onMoveSelected, Action onRotateSelected, Action onScaleSelected, Action onMessageSelected, Action onDeleteSelected = null)
        {
            if (_menuPanel == null)
            {
                BuildUI();
            }

            _targetObject = target;
            _onMoveSelected = onMoveSelected;
            _onRotateSelected = onRotateSelected;
            _onScaleSelected = onScaleSelected;
            _onMessageSelected = onMessageSelected;
            _onDeleteSelected = onDeleteSelected;
            
            // Toggle buttons
            if (_moveBtn != null) _moveBtn.SetActive(onMoveSelected != null);
            if (_rotateBtn != null) _rotateBtn.SetActive(onRotateSelected != null);
            if (_scaleBtn != null) _scaleBtn.SetActive(onScaleSelected != null);
            if (_messageBtn != null) _messageBtn.SetActive(onMessageSelected != null);
            if (_deleteBtn != null) _deleteBtn.SetActive(onDeleteSelected != null);
            
            // Adjust height based on active buttons
            float height = 16f; // Padding
            if (onMoveSelected != null) height += 34f + 6f;
            if (onRotateSelected != null) height += 34f + 6f;
            if (onScaleSelected != null) height += 34f + 6f;
            if (onMessageSelected != null) height += 34f + 6f;
            if (onDeleteSelected != null) height += 34f + 6f;
            _menuRect.sizeDelta = new Vector2(_menuRect.sizeDelta.x, height);

            // Force layout rebuild so sizeDelta is accurate immediately
            LayoutRebuilder.ForceRebuildLayoutImmediate(_menuRect);

            // Position menu at mouse
            Vector2 mousePos = GetMousePosition();
            
            // Adjust position so the top-left pivot starts exactly at the mouse pointer
            // We use RectTransformUtility to convert screen point to local point if needed,
            // but since canvas is overlay, raw screen position works directly.
            _menuRect.position = mousePos;

            // Ensure menu stays on screen after layout rebuild
            ClampMenuToScreen();

            _menuPanel.SetActive(true);
            GameplayInputBlocker.SetBlocked(this, true);
            Debug.Log($"[ContextMenu] Showing menu for {target.name}");
        }

        /// <summary>
        /// Hide the context menu
        /// </summary>
        public void HideMenu()
        {
            if (_menuPanel != null)
            {
                _menuPanel.SetActive(false);
            }
            GameplayInputBlocker.SetBlocked(this, false);
            _targetObject = null;
            _onMoveSelected = null;
            _onRotateSelected = null;
            _onScaleSelected = null;
            _onMessageSelected = null;
            _onDeleteSelected = null;
        }

        private void OnMoveClicked()
        {
            Debug.Log($"[ContextMenu] Move selected for {_targetObject?.name}");
            _onMoveSelected?.Invoke();
            HideMenu();
        }

        private void OnRotateClicked()
        {
            Debug.Log($"[ContextMenu] Rotate selected for {_targetObject?.name}");
            _onRotateSelected?.Invoke();
            HideMenu();
        }

        private void OnScaleClicked()
        {
            Debug.Log($"[ContextMenu] Scale selected for {_targetObject?.name}");
            _onScaleSelected?.Invoke();
            HideMenu();
        }

        private void OnMessageClicked()
        {
            Debug.Log($"[ContextMenu] Message selected for {_targetObject?.name}");
            _onMessageSelected?.Invoke();
            HideMenu();
        }

        private void OnDeleteClicked()
        {
            Debug.Log($"[ContextMenu] Delete selected for {_targetObject?.name}");
            _onDeleteSelected?.Invoke();
            HideMenu();
        }

        private Vector2 GetMousePosition()
        {
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            }
            return Input.mousePosition;
        }

        private void ClampMenuToScreen()
        {
            // Get screen bounds
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;

            var pos = _menuRect.position;
            
            // Give RectTransform a layout pass if size is 0
            if (_menuRect.rect.width == 0 || _menuRect.rect.height == 0)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_menuRect);
            }
            
            var size = new Vector2(_menuRect.rect.width, _menuRect.rect.height);

            // Clamp to screen
            if (pos.x + size.x > screenWidth)
            {
                pos.x = screenWidth - size.x;
            }
            if (pos.y - size.y < 0)
            {
                pos.y = size.y;
            }

            _menuRect.position = pos;
        }

        private void Update()
        {
            // Fix: Check if menuPanel is destroyed to avoid MissingReferenceException
            if (_menuPanel == null) return;

            // Close menu if clicking elsewhere
            if (_menuPanel.activeSelf)
            {
                bool clicked = Input.GetMouseButtonDown(0) || 
                              (UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame ?? false);

                if (clicked)
                {
                    // Check if click is outside menu
                    Vector2 mousePos = GetMousePosition();
                    if (!RectTransformUtility.RectangleContainsScreenPoint(_menuRect, mousePos, _canvas.worldCamera))
                    {
                        HideMenu();
                    }
                }
            }
        }

        private void OnDisable()
        {
            GameplayInputBlocker.SetBlocked(this, false);
        }

        private void OnDestroy()
        {
            GameplayInputBlocker.SetBlocked(this, false);
        }
    }
}
