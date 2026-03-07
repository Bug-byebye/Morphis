using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Morphis.VoiceRecognition;
using Morphis.InputControl;

namespace Morphis.Companion
{
    /// <summary>
    /// 带语音识别功能的狗狗聊天UI
    /// 按住麦克风按钮说话，松开自动识别并发送
    /// </summary>
    public class DogChatUIWithVoice : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string dogName = "Buddy";
        [SerializeField] private Color userMessageColor = new Color(0.2f, 0.6f, 1f);
        [SerializeField] private Color dogMessageColor = new Color(0.9f, 0.7f, 0.3f);
        [SerializeField] private Color systemMessageColor = new Color(0.7f, 0.7f, 0.7f);
        
        [Header("Voice Recognition")]
        [SerializeField] private bool enableVoiceRecognition = true;

        // UI References (created at runtime)
        private GameObject chatPanel;
        private ScrollRect scrollRect;
        private RectTransform contentRect;
        private TMP_InputField inputField;
        private Button sendButton;
        private Button closeButton;
        private Button micButton;
        private Image micButtonImage;
        private TextMeshProUGUI micButtonText;
        
        // Voice recognition
        private WhisperASR whisperASR;
        private string pendingVoiceApiToken;

        private bool isOpen = false;
        private List<GameObject> messageObjects = new List<GameObject>();
        private Func<string, string> localCommandHandler;
        private Action<int> modelActionHandler;

        public bool IsOpen => isOpen;
        public event Action OnChatOpened;
        public event Action OnChatClosed;

        /// <summary>
        /// Optional local command handler.
        /// Return a non-empty string to consume the message locally and skip API call.
        /// Return null/empty to continue with normal API chat flow.
        /// </summary>
        public void SetLocalCommandHandler(Func<string, string> handler)
        {
            localCommandHandler = handler;
        }

        /// <summary>
        /// Receives parsed action category from model response (e.g. [[ACTION:3]]).
        /// </summary>
        public void SetModelActionHandler(Action<int> handler)
        {
            modelActionHandler = handler;
        }

        private void Awake()
        {
            // 创建语音识别组件
            if (enableVoiceRecognition)
            {
                var asrObj = new GameObject("WhisperASR");
                asrObj.transform.SetParent(transform);
                whisperASR = asrObj.AddComponent<WhisperASR>();

                if (!string.IsNullOrWhiteSpace(pendingVoiceApiToken))
                {
                    whisperASR.SetApiToken(pendingVoiceApiToken);
                }
            }
            
            CreateUI();
            chatPanel.SetActive(false);
        }

        /// <summary>
        /// Configure Hugging Face token used by Whisper ASR.
        /// </summary>
        public void ConfigureVoiceApiToken(string token)
        {
            pendingVoiceApiToken = token == null ? string.Empty : token.Trim();
            if (whisperASR != null)
            {
                whisperASR.SetApiToken(pendingVoiceApiToken);
            }
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
            
            // 更新麦克风按钮状态
            if (enableVoiceRecognition && whisperASR != null && micButton != null)
            {
                UpdateMicButtonVisual();
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
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameplayInputBlocker.SetBlocked(this, true);
            
            OnChatOpened?.Invoke();
        }

        public void Close()
        {
            if (!isOpen) return;
            
            // 停止录音（如果正在录音）
            if (enableVoiceRecognition && whisperASR != null && whisperASR.IsRecording)
            {
                StopRecording();
            }
            
            isOpen = false;
            chatPanel.SetActive(false);
            GameplayInputBlocker.SetBlocked(this, false);
            
            OnChatClosed?.Invoke();
        }

        private void OnDisable()
        {
            GameplayInputBlocker.SetBlocked(this, false);
        }

        private void OnDestroy()
        {
            GameplayInputBlocker.SetBlocked(this, false);
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

            SendUserMessage(userMessage);
        }
        
        private void SendUserMessage(string message)
        {
            // Display user message
            AddMessage("你", message, userMessageColor);
            inputField.text = "";
            inputField.ActivateInputField();

            // Try local command first (e.g. 动作1 / action1)
            if (localCommandHandler != null)
            {
                string localResponse = null;
                try
                {
                    localResponse = localCommandHandler.Invoke(message);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DogChatUI] Local command handler failed: {e.Message}");
                }

                if (!string.IsNullOrWhiteSpace(localResponse))
                {
                    AddMessage(dogName, localResponse, dogMessageColor);
                    return;
                }
            }

            // Show typing indicator
            var typingMsg = AddMessage(dogName, "...", dogMessageColor);

            // Call API
            DogChatAPI.SendMessage(message, 
                (response, actionCategory) => {
                    if (typingMsg != null) Destroy(typingMsg);
                    AddMessage(dogName, response, dogMessageColor);

                    if (actionCategory.HasValue && modelActionHandler != null)
                    {
                        try
                        {
                            modelActionHandler.Invoke(actionCategory.Value);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[DogChatUI] Model action handler failed: {e.Message}");
                        }
                    }
                },
                error => {
                    if (typingMsg != null) Destroy(typingMsg);
                    AddMessage(dogName, "*呜呜* 出了点问题...", dogMessageColor);
                    Debug.LogError($"[DogChatUI] API Error: {error}");
                }
            );
        }
        
        /// <summary>
        /// 麦克风按钮按下 - 开始录音
        /// </summary>
        public void OnMicButtonDown()
        {
            if (!enableVoiceRecognition || whisperASR == null) return;
            
            if (whisperASR.IsRecording)
            {
                Debug.LogWarning("[DogChatUI] 已经在录音中");
                return;
            }
            
            AddMessage("系统", "开始录音... (松开按钮停止)", systemMessageColor);
            whisperASR.StartRecording();
        }
        
        /// <summary>
        /// 麦克风按钮松开 - 停止录音并识别
        /// </summary>
        public void OnMicButtonUp()
        {
            if (!enableVoiceRecognition || whisperASR == null) return;
            
            if (!whisperASR.IsRecording)
            {
                return;
            }
            
            StopRecording();
        }
        
        private void StopRecording()
        {
            var processingMsg = AddMessage("系统", "正在识别语音...", systemMessageColor);
            
            whisperASR.StopRecordingAndRecognize(
                recognizedText => {
                    // 移除处理消息
                    if (processingMsg != null) Destroy(processingMsg);
                    
                    if (!string.IsNullOrEmpty(recognizedText))
                    {
                        // 发送识别的文本
                        SendUserMessage(recognizedText);
                    }
                    else
                    {
                        AddMessage("系统", "没有识别到语音，请重试", systemMessageColor);
                    }
                },
                error => {
                    if (processingMsg != null) Destroy(processingMsg);
                    string shortError = error;
                    if (!string.IsNullOrEmpty(shortError) && shortError.Length > 180)
                    {
                        shortError = shortError.Substring(0, 180) + "...";
                    }

                    AddMessage("系统", $"识别失败: {shortError}", systemMessageColor);
                    Debug.LogError($"[DogChatUI] 语音识别失败: {error}");
                }
            );
        }
        
        private void UpdateMicButtonVisual()
        {
            if (whisperASR.IsRecording)
            {
                // 录音中 - 红色闪烁
                float pulse = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
                micButtonImage.color = Color.Lerp(new Color(0.8f, 0.2f, 0.2f), Color.red, pulse);
                micButtonText.text = "Stop";
            }
            else
            {
                // 待机 - 蓝色
                micButtonImage.color = new Color(0.3f, 0.5f, 0.8f);
                micButtonText.text = "Mic";
            }
        }

        private GameObject AddMessage(string sender, string message, Color color)
        {
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

            // Input area (with mic button)
            var inputArea = CreateChild(chatPanel, "InputArea", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 30), new Vector2(-20, 50));
            inputArea.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

            // Microphone button (if enabled)
            float inputWidth = enableVoiceRecognition ? 0.65f : 0.8f;
            
            if (enableVoiceRecognition)
            {
                var micBtnObj = CreateChild(inputArea, "MicButton", new Vector2(0, 0), new Vector2(0.15f, 1),
                    Vector2.zero, Vector2.zero);
                var micRect = micBtnObj.GetComponent<RectTransform>();
                micRect.anchoredPosition = new Vector2(5, 0);
                micRect.sizeDelta = new Vector2(-10, 0);

                micButtonImage = micBtnObj.AddComponent<Image>();
                micButtonImage.color = new Color(0.3f, 0.5f, 0.8f);
                micButton = micBtnObj.AddComponent<Button>();
                
                // 添加EventTrigger以支持按住和松开
                var eventTrigger = micBtnObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                
                var pointerDownEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
                pointerDownEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
                pointerDownEntry.callback.AddListener((data) => { OnMicButtonDown(); });
                eventTrigger.triggers.Add(pointerDownEntry);
                
                var pointerUpEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
                pointerUpEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
                pointerUpEntry.callback.AddListener((data) => { OnMicButtonUp(); });
                eventTrigger.triggers.Add(pointerUpEntry);
                
                micButtonText = CreateTextChild(micBtnObj, "Text", "Mic");
                micButtonText.alignment = TextAlignmentOptions.Center;
                micButtonText.fontSize = 28;
            }

            // Input field
            var inputObj = CreateChild(inputArea, "InputField", 
                new Vector2(enableVoiceRecognition ? 0.17f : 0f, 0), 
                new Vector2(enableVoiceRecognition ? 0.17f + inputWidth : inputWidth, 1),
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

            var placeholder = CreateTextChild(inputObj, "Placeholder", "输入消息或按住麦克风说话...");
            placeholder.color = new Color(0.5f, 0.5f, 0.5f);
            placeholder.fontStyle = FontStyles.Italic;
            inputField.placeholder = placeholder;
            var phRect = placeholder.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = new Vector2(-10, 0);

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
            var sendBtnObj = CreateChild(inputArea, "SendButton", 
                new Vector2(enableVoiceRecognition ? 0.84f : 0.82f, 0), 
                new Vector2(1, 1),
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
            
            if (enableVoiceRecognition)
            {
                AddMessage("系统", "提示: 按住麦克风按钮说话，松开自动识别", systemMessageColor);
            }
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
