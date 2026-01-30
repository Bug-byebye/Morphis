using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Morphis.ModelPlacement
{
    /// <summary>
    /// Context menu that appears when clicking on a placeable object
    /// Allows user to choose: Move Object or Leave Message
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
        private GameObject _messageBtn;
        
        // Current target
        private GameObject _targetObject;
        private Action _onMoveSelected;
        private Action _onMessageSelected;

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
            // Find or create canvas
            _canvas = FindCanvas();
            if (_canvas == null)
            {
                var canvasObj = new GameObject("ContextMenuCanvas");
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 150; // Higher than other UI
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create menu panel
            _menuPanel = new GameObject("ContextMenu");
            _menuPanel.transform.SetParent(_canvas.transform, false);
            _menuRect = _menuPanel.AddComponent<RectTransform>();
            _menuRect.sizeDelta = new Vector2(200, 100);
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
            _moveBtn = CreateMenuButton(_menuPanel.transform, "✋ Move Object", new Color(0.2f, 0.5f, 0.9f), OnMoveClicked);
            
            // Message button
            _messageBtn = CreateMenuButton(_menuPanel.transform, "💬 Leave Message", new Color(0.3f, 0.65f, 0.35f), OnMessageClicked);

            _menuPanel.SetActive(false);
        }

        private Canvas FindCanvas()
        {
            foreach (var c in FindObjectsOfType<Canvas>())
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= 100)
                {
                    return c;
                }
            }
            return null;
        }

        private GameObject CreateMenuButton(Transform parent, string text, Color color, Action onClick)
        {
            var btnObj = new GameObject($"Btn_{text}");
            btnObj.transform.SetParent(parent, false);

            var btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(0, 36);

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = color;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => onClick?.Invoke());

            // Add layout element
            var layoutElem = btnObj.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = 36;
            layoutElem.minHeight = 36;

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
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            
            return btnObj;
        }

        /// <summary>
        /// Show the context menu at mouse position for a specific object
        /// </summary>
        public void ShowMenu(GameObject target, Action onMoveSelected, Action onMessageSelected)
        {
            if (_menuPanel == null)
            {
                BuildUI();
            }

            _targetObject = target;
            _onMoveSelected = onMoveSelected;
            _onMessageSelected = onMessageSelected;
            
            // Toggle buttons
            if (_moveBtn != null) _moveBtn.SetActive(onMoveSelected != null);
            if (_messageBtn != null) _messageBtn.SetActive(onMessageSelected != null);
            
            // Adjust height based on active buttons
            float height = 16; // Padding
            if (onMoveSelected != null) height += 36 + 6;
            if (onMessageSelected != null) height += 36 + 6;
            _menuRect.sizeDelta = new Vector2(_menuRect.sizeDelta.x, height);

            // Position menu at mouse
            Vector2 mousePos = GetMousePosition();
            _menuRect.position = mousePos;

            // Ensure menu stays on screen
            ClampMenuToScreen();

            _menuPanel.SetActive(true);
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
            _targetObject = null;
            _onMoveSelected = null;
            _onMessageSelected = null;
        }

        private void OnMoveClicked()
        {
            Debug.Log($"[ContextMenu] Move selected for {_targetObject?.name}");
            _onMoveSelected?.Invoke();
            HideMenu();
        }

        private void OnMessageClicked()
        {
            Debug.Log($"[ContextMenu] Message selected for {_targetObject?.name}");
            _onMessageSelected?.Invoke();
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
            var size = _menuRect.sizeDelta;

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
    }
}
