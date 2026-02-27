using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Morphis.Companion
{
    /// <summary>
    /// Chat UI panel for talking to the dog companion.
    /// Creates UI at runtime - no prefab needed.
    /// </summary>
    public class DogChatUI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string dogName = "Buddy";
        [SerializeField] private Color userMessageColor = new Color(0.2f, 0.6f, 1f);
        [SerializeField] private Color dogMessageColor = new Color(0.9f, 0.7f, 0.3f);

        // UI References (created at runtime)
        private GameObject chatPanel;
        private ScrollRect scrollRect;
        private RectTransform contentRect;
        private TMP_InputField inputField;
        private Button sendButton;
        private Button closeButton;

        private bool isOpen = false;
        private List<GameObject> messageObjects = new List<GameObject>();

        public bool IsOpen => isOpen;
        public event Action OnChatOpened;
        public event Action OnChatClosed;

        private void Awake()
        {
            CreateUI();
            chatPanel.SetActive(false);
        }

        private void Update()
        {
            // Submit on Enter key
            if (isOpen && Input.GetKeyDown(KeyCode.Return) && !string.IsNullOrEmpty(inputField.text))
            {
                SendMessage();
            }

            // Close on Escape
            if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        public void Open()
        {
            if (isOpen) return;
            
            isOpen = true;
            chatPanel.SetActive(true);
            inputField.text = "";
            inputField.Select();
            inputField.ActivateInputField();
            
            // Unlock cursor for UI interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            OnChatOpened?.Invoke();
        }

        public void Close()
        {
            if (!isOpen) return;
            
            isOpen = false;
            chatPanel.SetActive(false);
            
            OnChatClosed?.Invoke();
        }

        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        private void SendMessage()
        {
            string userMessage = inputField.text.Trim();
            if (string.IsNullOrEmpty(userMessage)) return;

            // Display user message
            AddMessage("你", userMessage, userMessageColor);
            inputField.text = "";
            inputField.ActivateInputField();

            // Show typing indicator
            var typingMsg = AddMessage(dogName, "...", dogMessageColor);

            // Call API
            DogChatAPI.SendMessage(userMessage, 
                response => {
                    // Remove typing indicator and show response
                    if (typingMsg != null) Destroy(typingMsg);
                    AddMessage(dogName, response, dogMessageColor);
                },
                error => {
                    if (typingMsg != null) Destroy(typingMsg);
                    AddMessage(dogName, "*呜呜* 出了点问题...", dogMessageColor);
                    Debug.LogError($"[DogChatUI] API Error: {error}");
                }
            );
        }

        private GameObject AddMessage(string sender, string message, Color color)
        {
            // Create message text
            var msgObj = new GameObject("Message");
            msgObj.transform.SetParent(contentRect, false);

            var text = msgObj.AddComponent<TextMeshProUGUI>();
            text.text = $"<b>{sender}:</b> {message}";
            text.fontSize = 20;
            text.color = color;
            text.enableWordWrapping = true;

            var layoutElement = msgObj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = contentRect.rect.width - 20;

            var fitter = msgObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            messageObjects.Add(msgObj);

            // Scroll to bottom
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;

            return msgObj;
        }

        private void CreateUI()
        {
            // Find or create canvas
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("DogChatCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Main chat panel
            chatPanel = new GameObject("DogChatPanel");
            chatPanel.transform.SetParent(canvas.transform, false);

            var panelRect = chatPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600, 700);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = chatPanel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            // Add rounded corners effect (simple)
            var panelOutline = chatPanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.3f, 0.3f, 0.4f);
            panelOutline.effectDistance = new Vector2(2, 2);

            // Header
            var header = CreateChild(chatPanel, "Header", new Vector2(0, 1), new Vector2(1, 1), 
                new Vector2(0, -40), new Vector2(0, 40));
            var headerImage = header.AddComponent<Image>();
            headerImage.color = new Color(0.15f, 0.15f, 0.2f);

            var titleText = CreateTextChild(header, "Title", $"与 {dogName} 聊天");
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 24;
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.sizeDelta = Vector2.zero;

            // Close button
            var closeBtnObj = CreateChild(header, "CloseButton", new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-25, 0), new Vector2(30, 30));
            var closeBtnImage = closeBtnObj.AddComponent<Image>();
            closeBtnImage.color = new Color(0.8f, 0.3f, 0.3f);
            closeButton = closeBtnObj.AddComponent<Button>();
            closeButton.onClick.AddListener(Close);
            var closeText = CreateTextChild(closeBtnObj, "X", "✕");
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.fontSize = 18;

            // Messages scroll area
            var scrollArea = CreateChild(chatPanel, "ScrollArea", new Vector2(0, 0), new Vector2(1, 1),
                Vector2.zero, new Vector2(-20, -100));
            scrollArea.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 10);
            
            scrollRect = scrollArea.AddComponent<ScrollRect>();
            var scrollMask = scrollArea.AddComponent<RectMask2D>();

            // Content container
            var content = CreateChild(scrollArea, "Content", new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, new Vector2(0, 0));
            contentRect = content.GetComponent<RectTransform>();
            contentRect.pivot = new Vector2(0.5f, 1);
            
            var vertLayout = content.AddComponent<VerticalLayoutGroup>();
            vertLayout.spacing = 10;
            vertLayout.padding = new RectOffset(10, 10, 10, 10);
            vertLayout.childForceExpandWidth = true;
            vertLayout.childForceExpandHeight = false;

            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;

            // Input area
            var inputArea = CreateChild(chatPanel, "InputArea", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 30), new Vector2(-20, 50));
            inputArea.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

            // Input field
            var inputObj = CreateChild(inputArea, "InputField", new Vector2(0, 0), new Vector2(0.8f, 1),
                Vector2.zero, Vector2.zero);
            var inputRect = inputObj.GetComponent<RectTransform>();
            inputRect.anchoredPosition = new Vector2(5, 0);
            inputRect.sizeDelta = new Vector2(-10, 0);

            var inputBg = inputObj.AddComponent<Image>();
            inputBg.color = new Color(0.2f, 0.2f, 0.25f);

            inputField = inputObj.AddComponent<TMP_InputField>();
            
            var inputText = CreateTextChild(inputObj, "Text", "");
            inputText.color = Color.white;
            inputField.textComponent = inputText;
            var inputTextRect = inputText.GetComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.sizeDelta = new Vector2(-10, 0);

            var placeholder = CreateTextChild(inputObj, "Placeholder", "输入消息...");
            placeholder.color = new Color(0.5f, 0.5f, 0.5f);
            placeholder.fontStyle = FontStyles.Italic;
            inputField.placeholder = placeholder;
            var phRect = placeholder.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = new Vector2(-10, 0);

            // Text area for input
            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputObj.transform, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.sizeDelta = Vector2.zero;
            textArea.AddComponent<RectMask2D>();
            inputText.transform.SetParent(textArea.transform, false);
            inputField.textViewport = textAreaRect;

            // Send button
            var sendBtnObj = CreateChild(inputArea, "SendButton", new Vector2(0.82f, 0), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            var sendRect = sendBtnObj.GetComponent<RectTransform>();
            sendRect.anchoredPosition = new Vector2(-5, 0);
            sendRect.sizeDelta = new Vector2(-10, 0);

            var sendBtnImage = sendBtnObj.AddComponent<Image>();
            sendBtnImage.color = new Color(0.2f, 0.7f, 0.4f);
            sendButton = sendBtnObj.AddComponent<Button>();
            sendButton.onClick.AddListener(SendMessage);
            var sendText = CreateTextChild(sendBtnObj, "Text", ">");
            sendText.alignment = TextAlignmentOptions.Center;
            sendText.fontSize = 22;

            // Add welcome message
            AddMessage(dogName, $"*摇尾巴* 汪！你好呀！我是{dogName}！", dogMessageColor);
        }

        private GameObject CreateChild(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, 
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            return obj;
        }

        private TextMeshProUGUI CreateTextChild(GameObject parent, string name, string text)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.color = Color.white;
            return tmp;
        }
    }
}
