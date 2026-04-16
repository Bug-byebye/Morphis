using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Morphis.AppFlow;
using Morphis.InputControl;

namespace Morphis.Chat
{
    /// <summary>
    /// Independent human chat widget.
    /// UI is created at runtime: right-side toggle button + bottom-right phone panel.
    /// Talks to the backend human companion API.
    /// </summary>
    public class HumanChatUI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string assistantName = "伴侣";
        [SerializeField] private Color userBubbleColor = new Color(0.18f, 0.56f, 0.96f, 1f);
        [SerializeField] private Color assistantBubbleColor = new Color(0.2f, 0.75f, 0.45f, 1f);
        [SerializeField] private Color userAvatarColor = new Color(0.14f, 0.46f, 0.82f, 1f);
        [SerializeField] private Color assistantAvatarColor = new Color(0.17f, 0.58f, 0.35f, 1f);
        [SerializeField] private float popupDuration = 0.95f;
        [SerializeField] private float maxBubbleTextWidth = 210f;

        private GameObject chatPanel;
        private RectTransform chatPanelRect;
        private CanvasGroup chatPanelCanvasGroup;
        private ScrollRect scrollRect;
        private RectTransform contentRect;
        private TMP_InputField inputField;
        private Button sendButton;
        private Button closeButton;
        private Button toggleButton;
        private TextMeshProUGUI toggleButtonText;
        private bool isOpen;
        private bool isAvailable;
        private Coroutine panelAnimationCoroutine;
        private Vector2 panelShownPosition;
        private Vector2 panelHiddenPosition;

        private readonly List<GameObject> messageObjects = new List<GameObject>();

        private void Awake()
        {
            CreateUI();
            chatPanelRect.anchoredPosition = panelHiddenPosition;
            if (chatPanelCanvasGroup != null) chatPanelCanvasGroup.alpha = 0f;
            chatPanel.SetActive(false);
            UpdateToggleButtonLabel();
            RefreshAvailability();
        }

        private void Update()
        {
            RefreshAvailability();
            if (!isAvailable) return;

            if (!isOpen) return;

            if (Input.GetKeyDown(KeyCode.Return) && !string.IsNullOrWhiteSpace(inputField.text))
            {
                SendMessage();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (isOpen || !isAvailable) return;

            isOpen = true;
            if (!chatPanel.activeSelf)
            {
                chatPanelRect.anchoredPosition = panelHiddenPosition;
                if (chatPanelCanvasGroup != null) chatPanelCanvasGroup.alpha = 0f;
                chatPanel.SetActive(true);
            }

            StartPanelAnimation(chatPanelShownPosition: panelShownPosition, deactivateAfterAnimation: false);
            inputField.text = string.Empty;
            inputField.Select();
            inputField.ActivateInputField();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameplayInputBlocker.SetBlocked(this, true);
            UpdateToggleButtonLabel();
        }

        public void Close()
        {
            if (!isOpen) return;

            isOpen = false;
            StartPanelAnimation(chatPanelShownPosition: panelHiddenPosition, deactivateAfterAnimation: true);
            GameplayInputBlocker.SetBlocked(this, false);
            UpdateToggleButtonLabel();
        }

        private void OnDisable()
        {
            GameplayInputBlocker.SetBlocked(this, false);
        }

        private void OnDestroy()
        {
            StopPanelAnimation();
            GameplayInputBlocker.SetBlocked(this, false);
        }

        private void RefreshAvailability()
        {
            bool shouldBeAvailable = CanUseHumanChatUi();
            if (shouldBeAvailable == isAvailable)
            {
                return;
            }

            isAvailable = shouldBeAvailable;

            if (toggleButton != null)
            {
                toggleButton.gameObject.SetActive(isAvailable);
            }

            if (!isAvailable)
            {
                if (isOpen)
                {
                    isOpen = false;
                    StopPanelAnimation();
                }

                if (chatPanel != null)
                {
                    chatPanel.SetActive(false);
                }

                GameplayInputBlocker.SetBlocked(this, false);
            }

            UpdateToggleButtonLabel();
        }

        private static bool CanUseHumanChatUi()
        {
            if (Application.isBatchMode)
            {
                return false;
            }

            var sceneName = SceneManager.GetActiveScene().name;
            if (string.Equals(sceneName, "BootScene", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return AppSession.IsLoggedIn && !string.IsNullOrEmpty(AppSession.WorkspaceId);
        }

        private void SendMessage()
        {
            string userMessage = inputField.text.Trim();
            if (string.IsNullOrEmpty(userMessage)) return;

            AddMessage("你", userMessage, true);
            inputField.text = string.Empty;
            inputField.ActivateInputField();

            GameObject typingMessage = AddMessage(assistantName, "...", false);
            HumanChatAPI.SendMessage(
                userMessage,
                response =>
                {
                    if (typingMessage != null) Destroy(typingMessage);
                    AddMessage(assistantName, response, false);
                },
                error =>
                {
                    if (typingMessage != null) Destroy(typingMessage);
                    AddMessage(assistantName, "我刚刚有点走神了。你再和我说一次，好吗？", false);
                    Debug.LogError($"[HumanChatUI] API Error: {error}");
                },
                assistantName
            );
        }

        private GameObject AddMessage(string sender, string message, bool isUserMessage)
        {
            GameObject rowObj = new GameObject("HumanMessageRow");
            rowObj.transform.SetParent(contentRect, false);
            var rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.sizeDelta = Vector2.zero;

            var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(8, 8, 4, 4);
            rowLayout.spacing = 10f;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = isUserMessage ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;

            var rowFitter = rowObj.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var rowLayoutElement = rowObj.AddComponent<LayoutElement>();
            rowLayoutElement.preferredWidth = 0f;

            if (isUserMessage)
            {
                CreateBubble(rowObj.transform, message, true);
                CreateAvatar(rowObj.transform, "你", true);
            }
            else
            {
                CreateAvatar(rowObj.transform, string.IsNullOrEmpty(sender) ? "人" : sender.Substring(0, 1), false);
                CreateBubble(rowObj.transform, message, false);
            }

            messageObjects.Add(rowObj);
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
            return rowObj;
        }

        private void CreateUI()
        {
            EnsureEventSystem();

            GameObject canvasObj = new GameObject("HumanChatCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 130;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            CreateRightToggleButton(canvasObj);
            CreatePhonePanel(canvasObj);
            AddMessage(assistantName, "我在这里陪你。今天想和我聊点什么？", false);
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
        }

        private void CreateRightToggleButton(GameObject parent)
        {
            GameObject buttonObj = new GameObject("HumanChatToggleButton");
            buttonObj.transform.SetParent(parent.transform, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(156f, 54f);
            rect.anchoredPosition = new Vector2(-88f, 0f);

            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.18f, 0.62f, 0.36f, 0.95f);

            Outline outline = buttonObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.28f, 0.16f, 1f);
            outline.effectDistance = new Vector2(2f, 2f);

            toggleButton = buttonObj.AddComponent<Button>();
            toggleButton.onClick.AddListener(Toggle);

            toggleButtonText = CreateTextChild(buttonObj, "Text", "打开伴侣");
            toggleButtonText.alignment = TextAlignmentOptions.Center;
            toggleButtonText.fontSize = 22;
        }

        private void CreatePhonePanel(GameObject parent)
        {
            chatPanel = new GameObject("HumanChatPhonePanel");
            chatPanel.transform.SetParent(parent.transform, false);

            RectTransform panelRect = chatPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.sizeDelta = new Vector2(360f, 680f);
            panelRect.anchoredPosition = new Vector2(-26f, 28f);
            chatPanelRect = panelRect;
            panelShownPosition = panelRect.anchoredPosition;
            panelHiddenPosition = panelShownPosition + new Vector2(0f, -panelRect.sizeDelta.y - 80f);
            chatPanelCanvasGroup = chatPanel.AddComponent<CanvasGroup>();
            chatPanelCanvasGroup.alpha = 1f;

            Image panelImage = chatPanel.AddComponent<Image>();
            panelImage.color = new Color(0.04f, 0.04f, 0.05f, 0.98f);

            Outline panelOutline = chatPanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            panelOutline.effectDistance = new Vector2(3f, 3f);

            GameObject speaker = CreateChild(chatPanel, "Speaker", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -13f), new Vector2(92f, 8f));
            speaker.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 1f);

            GameObject screen = CreateChild(chatPanel, "Screen", new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(-20f, -28f));
            screen.AddComponent<Image>().color = new Color(0.1f, 0.11f, 0.12f, 0.98f);

            GameObject header = CreateChild(screen, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -30f), new Vector2(0f, 52f));
            header.AddComponent<Image>().color = new Color(0.16f, 0.18f, 0.2f, 0.98f);

            TextMeshProUGUI title = CreateTextChild(header, "Title", $"与{assistantName}聊天");
            title.alignment = TextAlignmentOptions.Center;
            title.fontSize = 22;

            GameObject closeBtnObj = CreateChild(header, "CloseButton", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(28f, 28f));
            closeBtnObj.AddComponent<Image>().color = new Color(0.82f, 0.3f, 0.3f, 1f);
            closeButton = closeBtnObj.AddComponent<Button>();
            closeButton.onClick.AddListener(Close);

            TextMeshProUGUI closeText = CreateTextChild(closeBtnObj, "Text", "X");
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.fontSize = 18;

            GameObject scrollArea = CreateChild(screen, "ScrollArea", new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 12f), new Vector2(-18f, -168f));
            scrollRect = scrollArea.AddComponent<ScrollRect>();
            scrollArea.AddComponent<RectMask2D>();

            GameObject content = CreateChild(scrollArea, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero);
            contentRect = content.GetComponent<RectTransform>();
            contentRect.pivot = new Vector2(0.5f, 1f);

            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;

            CreateInputArea(screen);
        }

        private void CreateInputArea(GameObject screen)
        {
            GameObject inputArea = CreateChild(screen, "InputArea", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 34f), new Vector2(-18f, 56f));

            GameObject inputObj = CreateChild(inputArea, "InputField", new Vector2(0f, 0f), new Vector2(0.8f, 1f),
                Vector2.zero, Vector2.zero);
            RectTransform inputRect = inputObj.GetComponent<RectTransform>();
            inputRect.anchoredPosition = new Vector2(5f, 0f);
            inputRect.sizeDelta = new Vector2(-10f, 0f);

            inputObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f, 1f);
            inputField = inputObj.AddComponent<TMP_InputField>();

            TextMeshProUGUI inputText = CreateTextChild(inputObj, "Text", string.Empty);
            inputText.color = Color.white;
            inputField.textComponent = inputText;
            RectTransform inputTextRect = inputText.GetComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.sizeDelta = new Vector2(-10f, 0f);

            TextMeshProUGUI placeholder = CreateTextChild(inputObj, "Placeholder", "和TA说点什么...");
            placeholder.color = new Color(0.5f, 0.5f, 0.5f);
            placeholder.fontStyle = FontStyles.Italic;
            inputField.placeholder = placeholder;
            RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = new Vector2(-10f, 0f);

            GameObject textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputObj.transform, false);
            RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.sizeDelta = Vector2.zero;
            textArea.AddComponent<RectMask2D>();
            inputText.transform.SetParent(textArea.transform, false);
            inputField.textViewport = textAreaRect;

            GameObject sendBtnObj = CreateChild(inputArea, "SendButton", new Vector2(0.82f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero);
            RectTransform sendRect = sendBtnObj.GetComponent<RectTransform>();
            sendRect.anchoredPosition = new Vector2(-5f, 0f);
            sendRect.sizeDelta = new Vector2(-10f, 0f);

            sendBtnObj.AddComponent<Image>().color = new Color(0.22f, 0.72f, 0.42f, 1f);
            sendButton = sendBtnObj.AddComponent<Button>();
            sendButton.onClick.AddListener(SendMessage);

            TextMeshProUGUI sendText = CreateTextChild(sendBtnObj, "Text", ">");
            sendText.alignment = TextAlignmentOptions.Center;
            sendText.fontSize = 22;
        }

        private void StartPanelAnimation(Vector2 chatPanelShownPosition, bool deactivateAfterAnimation)
        {
            StopPanelAnimation();
            panelAnimationCoroutine = StartCoroutine(AnimatePanel(chatPanelRect.anchoredPosition, chatPanelShownPosition, deactivateAfterAnimation));
        }

        private void StopPanelAnimation()
        {
            if (panelAnimationCoroutine == null) return;
            StopCoroutine(panelAnimationCoroutine);
            panelAnimationCoroutine = null;
        }

        private IEnumerator AnimatePanel(Vector2 from, Vector2 to, bool deactivateAfterAnimation)
        {
            float elapsed = 0f;
            chatPanelRect.anchoredPosition = from;
            float startAlpha = chatPanelCanvasGroup != null ? chatPanelCanvasGroup.alpha : 1f;
            float endAlpha = deactivateAfterAnimation ? 0f : 1f;

            while (elapsed < popupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popupDuration);
                // Smooth step for slower, softer movement.
                t = t * t * (3f - 2f * t);
                chatPanelRect.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                if (chatPanelCanvasGroup != null)
                {
                    chatPanelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                }
                yield return null;
            }

            chatPanelRect.anchoredPosition = to;
            if (chatPanelCanvasGroup != null) chatPanelCanvasGroup.alpha = endAlpha;
            panelAnimationCoroutine = null;

            if (deactivateAfterAnimation && !isOpen)
            {
                chatPanel.SetActive(false);
            }
        }

        private void UpdateToggleButtonLabel()
        {
            if (toggleButtonText == null) return;
            toggleButtonText.text = isOpen ? "收起伴侣" : "打开伴侣";
        }

        private void CreateAvatar(Transform parent, string label, bool isUserMessage)
        {
            GameObject avatarObj = new GameObject(isUserMessage ? "UserAvatar" : "AssistantAvatar");
            avatarObj.transform.SetParent(parent, false);

            var avatarRect = avatarObj.AddComponent<RectTransform>();
            avatarRect.sizeDelta = new Vector2(38f, 38f);

            var avatarImage = avatarObj.AddComponent<Image>();
            avatarImage.color = isUserMessage ? userAvatarColor : assistantAvatarColor;

            var avatarOutline = avatarObj.AddComponent<Outline>();
            avatarOutline.effectColor = new Color(0f, 0f, 0f, 0.28f);
            avatarOutline.effectDistance = new Vector2(1f, 1f);

            var avatarLayout = avatarObj.AddComponent<LayoutElement>();
            avatarLayout.preferredWidth = 38f;
            avatarLayout.preferredHeight = 38f;

            TextMeshProUGUI avatarText = CreateTextChild(avatarObj, "Label", label);
            avatarText.fontSize = 18;
            avatarText.alignment = TextAlignmentOptions.Center;
            avatarText.color = Color.white;
            avatarText.fontStyle = FontStyles.Bold;
        }

        private void CreateBubble(Transform parent, string message, bool isUserMessage)
        {
            GameObject bubbleObj = new GameObject(isUserMessage ? "UserBubble" : "AssistantBubble");
            bubbleObj.transform.SetParent(parent, false);

            var bubbleImage = bubbleObj.AddComponent<Image>();
            bubbleImage.color = isUserMessage ? userBubbleColor : assistantBubbleColor;

            var bubbleOutline = bubbleObj.AddComponent<Outline>();
            bubbleOutline.effectColor = new Color(0f, 0f, 0f, 0.2f);
            bubbleOutline.effectDistance = new Vector2(1f, 1f);

            var bubbleLayout = bubbleObj.AddComponent<HorizontalLayoutGroup>();
            bubbleLayout.padding = new RectOffset(14, 14, 10, 10);
            bubbleLayout.childControlWidth = true;
            bubbleLayout.childControlHeight = true;
            bubbleLayout.childForceExpandWidth = false;
            bubbleLayout.childForceExpandHeight = false;

            var bubbleFitter = bubbleObj.AddComponent<ContentSizeFitter>();
            bubbleFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            bubbleFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bubbleLayoutElement = bubbleObj.AddComponent<LayoutElement>();
            bubbleLayoutElement.minHeight = 34f;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(bubbleObj.transform, false);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = Vector2.zero;
            text.text = message;
            text.fontSize = 19;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Left;

            float textWidth = Mathf.Max(120f, maxBubbleTextWidth);
            Vector2 preferred = text.GetPreferredValues(message, textWidth, 0f);
            var textLayout = textObj.AddComponent<LayoutElement>();
            textLayout.preferredWidth = Mathf.Min(textWidth, preferred.x);
            textLayout.preferredHeight = preferred.y + 4f;
        }

        private GameObject CreateChild(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            return obj;
        }

        private TextMeshProUGUI CreateTextChild(GameObject parent, string name, string text)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.color = Color.white;
            return tmp;
        }
    }
}
