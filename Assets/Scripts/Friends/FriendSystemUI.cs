using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mirror;
using Morphis.AppFlow;
using Morphis.InputControl;
using StarterAssets;

namespace Morphis.Friends
{
    /// <summary>
    /// Runtime-built friend list UI and in-world player interaction.
    /// Press F to open, then click another player to send or accept a request.
    /// </summary>
    public class FriendSystemUI : MonoBehaviour
    {
        private enum SelectedAction
        {
            None,
            SendRequest,
            AcceptRequest
        }

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private GameObject _toggleButtonObject;
        private Button _toggleButton;
        private TextMeshProUGUI _toggleLabel;
        private GameObject _panel;
        private TextMeshProUGUI _statusLabel;
        private ScrollRect _scrollRect;
        private RectTransform _contentRoot;
        private GameObject _selectionCard;
        private RectTransform _selectionCardRect;
        private TextMeshProUGUI _selectionTitle;
        private TextMeshProUGUI _selectionHint;
        private Button _selectionActionButton;
        private TextMeshProUGUI _selectionActionLabel;
        private GameObject _toastObject;
        private TextMeshProUGUI _toastLabel;

        private readonly HashSet<int> _knownIncomingRequestIds = new HashSet<int>();
        private FriendAPI.FriendsStateResponse _lastState = new FriendAPI.FriendsStateResponse();

        private bool _isOpen;
        private bool _uiVisible;
        private bool _initialSyncCompleted;
        private string _selectedUsername;
        private int _selectedRequestId = -1;
        private SelectedAction _selectedAction = SelectedAction.None;
        private CursorLockMode _cursorLockBeforeOpen;
        private bool _cursorVisibleBeforeOpen;
        private Coroutine _pollCoroutine;
        private Coroutine _toastCoroutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Application.isBatchMode) return;
            if (FindFirstObjectByType<FriendSystemUI>() != null) return;

            var go = new GameObject("FriendSystemRoot");
            DontDestroyOnLoad(go);
            go.AddComponent<FriendSystemUI>();
        }

        private void Awake()
        {
            _lastState.Normalize();
            CreateUi();
            HideSelectionCard();
            HideToastImmediate();
            SetUiVisible(false);
        }

        private void Start()
        {
            _pollCoroutine = StartCoroutine(PollLoop());
        }

        private void Update()
        {
            var shouldShow = CanUseFriendUi();
            if (shouldShow != _uiVisible)
            {
                SetUiVisible(shouldShow);
            }

            if (!shouldShow)
            {
                if (_isOpen)
                {
                    ClosePanel();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                TogglePanel();
            }

            if (!_isOpen)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0) && !IsPointerOverUi())
            {
                TrySelectPlayerAt(Input.mousePosition);
            }
        }

        private void OnDisable()
        {
            GameplayInputBlocker.SetBlocked(this, false);
        }

        private void OnDestroy()
        {
            GameplayInputBlocker.SetBlocked(this, false);
            if (_pollCoroutine != null)
            {
                StopCoroutine(_pollCoroutine);
                _pollCoroutine = null;
            }
        }

        private bool CanUseFriendUi()
        {
            return !Application.isBatchMode && AppSession.IsLoggedIn && NetworkClient.active;
        }

        private void TogglePanel()
        {
            if (_isOpen) ClosePanel();
            else OpenPanel();
        }

        private void OpenPanel()
        {
            if (_isOpen || !CanUseFriendUi())
            {
                return;
            }

            _isOpen = true;
            _panel.SetActive(true);
            _toggleLabel.text = "关闭好友";
            _cursorLockBeforeOpen = Cursor.lockState;
            _cursorVisibleBeforeOpen = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameplayInputBlocker.SetBlocked(this, true);

            SetStatus("打开后点击世界里的其他玩家可发送好友请求。");
            RefreshState();
        }

        private void ClosePanel()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            _panel.SetActive(false);
            _toggleLabel.text = "好友(F)";
            HideSelectionCard();
            GameplayInputBlocker.SetBlocked(this, false);

            if (!GameplayInputBlocker.IsBlocked)
            {
                Cursor.lockState = _cursorLockBeforeOpen;
                Cursor.visible = _cursorVisibleBeforeOpen;
            }
        }

        private IEnumerator PollLoop()
        {
            while (true)
            {
                if (CanUseFriendUi())
                {
                    RefreshState();
                }

                yield return new WaitForSeconds(5f);
            }
        }

        private void RefreshState()
        {
            FriendAPI.FetchState(
                OnStateLoaded,
                error =>
                {
                    if (_isOpen)
                    {
                        SetStatus($"好友同步失败: {error}");
                    }
                });
        }

        private void OnStateLoaded(FriendAPI.FriendsStateResponse state)
        {
            if (state == null)
            {
                state = new FriendAPI.FriendsStateResponse();
            }
            state.Normalize();
            _lastState = state;

            var currentIncomingIds = new HashSet<int>();
            foreach (var request in state.incoming_requests)
            {
                if (request == null) continue;
                currentIncomingIds.Add(request.id);

                if (_initialSyncCompleted)
                {
                    if (!_knownIncomingRequestIds.Contains(request.id) && !string.IsNullOrWhiteSpace(request.sender_username))
                    {
                        ShowToast($"{request.sender_username} 向你发送了好友请求");
                    }
                }
            }

            _knownIncomingRequestIds.Clear();
            foreach (var id in currentIncomingIds)
            {
                _knownIncomingRequestIds.Add(id);
            }

            _initialSyncCompleted = true;
            RebuildListContent();
            RefreshSelectionCard();
        }

        private void RebuildListContent()
        {
            if (_contentRoot == null)
            {
                return;
            }

            for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_contentRoot.GetChild(i).gameObject);
            }

            BuildSectionHeader("好友列表");
            if (_lastState.friends.Length == 0)
            {
                BuildInfoRow("还没有好友。");
            }
            else
            {
                foreach (var friend in _lastState.friends)
                {
                    if (friend == null) continue;
                    BuildSimpleRow(friend.username, new Color(0.13f, 0.19f, 0.24f, 0.92f));
                }
            }

            BuildSectionHeader("收到的请求");
            if (_lastState.incoming_requests.Length == 0)
            {
                BuildInfoRow("暂时没有新的请求。");
            }
            else
            {
                foreach (var request in _lastState.incoming_requests)
                {
                    if (request == null) continue;
                    BuildIncomingRequestRow(request);
                }
            }

            BuildSectionHeader("发出的请求");
            if (_lastState.outgoing_requests.Length == 0)
            {
                BuildInfoRow("你还没有发出好友请求。");
            }
            else
            {
                foreach (var request in _lastState.outgoing_requests)
                {
                    if (request == null) continue;
                    BuildSimpleRow($"{request.receiver_username} 处理中", new Color(0.22f, 0.26f, 0.17f, 0.92f));
                }
            }

            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 1f;
        }

        private void TrySelectPlayerAt(Vector2 screenPosition)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                SetStatus("没有找到主摄像机，无法选择玩家。");
                return;
            }

            var ray = cam.ScreenPointToRay(screenPosition);
            var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (var hit in hits)
            {
                var player = hit.collider != null ? hit.collider.GetComponentInParent<NetworkPlayerSetup>() : null;
                if (player == null || player.isLocalPlayer)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(player.DisplayName))
                {
                    continue;
                }

                ShowSelectionCard(player.DisplayName, screenPosition);
                return;
            }

            HideSelectionCard();
            SetStatus("点击世界里的玩家模型来发送好友请求。");
        }

        private void ShowSelectionCard(string username, Vector2 screenPosition)
        {
            _selectedUsername = username;
            _selectedRequestId = -1;
            _selectedAction = SelectedAction.None;

            var incoming = FindIncomingFrom(username);
            var outgoing = FindOutgoingTo(username);

            _selectionTitle.text = username;
            _selectionActionButton.interactable = false;
            _selectionActionButton.gameObject.SetActive(true);

            if (IsFriend(username))
            {
                _selectionHint.text = "你们已经是好友。";
                _selectionActionLabel.text = "已是好友";
            }
            else if (incoming != null)
            {
                _selectedAction = SelectedAction.AcceptRequest;
                _selectedRequestId = incoming.id;
                _selectionHint.text = "对方已经向你发来了好友请求。";
                _selectionActionLabel.text = "接受好友";
                _selectionActionButton.interactable = true;
            }
            else if (outgoing != null)
            {
                _selectionHint.text = "好友请求已经发出，等对方处理。";
                _selectionActionLabel.text = "已发送";
            }
            else
            {
                _selectedAction = SelectedAction.SendRequest;
                _selectionHint.text = "向 TA 发送好友请求。";
                _selectionActionLabel.text = "添加好友";
                _selectionActionButton.interactable = true;
            }

            PositionSelectionCard(screenPosition);
            _selectionCard.SetActive(true);
            SetStatus($"已选中 {username}");
        }

        private void RefreshSelectionCard()
        {
            if (!_selectionCard.activeSelf || string.IsNullOrWhiteSpace(_selectedUsername))
            {
                return;
            }

            ShowSelectionCard(_selectedUsername, Input.mousePosition);
        }

        private void PositionSelectionCard(Vector2 screenPosition)
        {
            if (_selectionCardRect == null || _canvasRect == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPosition,
                null,
                out var localPoint);

            var target = localPoint + new Vector2(150f, -30f);
            float halfWidth = _canvasRect.rect.width * 0.5f;
            float halfHeight = _canvasRect.rect.height * 0.5f;
            float panelHalfWidth = _selectionCardRect.sizeDelta.x * 0.5f;
            float panelHalfHeight = _selectionCardRect.sizeDelta.y * 0.5f;

            target.x = Mathf.Clamp(target.x, -halfWidth + panelHalfWidth + 12f, halfWidth - panelHalfWidth - 12f);
            target.y = Mathf.Clamp(target.y, -halfHeight + panelHalfHeight + 12f, halfHeight - panelHalfHeight - 12f);

            _selectionCardRect.anchoredPosition = target;
        }

        private void OnSelectionActionClicked()
        {
            switch (_selectedAction)
            {
                case SelectedAction.SendRequest:
                    SendSelectedFriendRequest();
                    break;
                case SelectedAction.AcceptRequest:
                    AcceptSelectedFriendRequest();
                    break;
            }
        }

        private void SendSelectedFriendRequest()
        {
            var username = _selectedUsername;
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            FriendAPI.SendFriendRequest(
                username,
                response =>
                {
                    var message = !string.IsNullOrWhiteSpace(response?.message) ? response.message : $"已向 {username} 发送请求";
                    SetStatus(message);
                    ShowToast(message);
                    RefreshState();
                },
                error =>
                {
                    SetStatus(error);
                    ShowToast(error);
                    RefreshState();
                });
        }

        private void AcceptSelectedFriendRequest()
        {
            if (_selectedRequestId <= 0)
            {
                return;
            }

            var username = _selectedUsername;
            FriendAPI.AcceptFriendRequest(
                _selectedRequestId,
                response =>
                {
                    var message = !string.IsNullOrWhiteSpace(response?.message) ? response.message : $"已接受 {username} 的好友请求";
                    SetStatus(message);
                    ShowToast(message);
                    RefreshState();
                },
                error =>
                {
                    SetStatus(error);
                    ShowToast(error);
                    RefreshState();
                });
        }

        private void AcceptRequestFromList(FriendAPI.FriendRequestDto request)
        {
            if (request == null) return;

            FriendAPI.AcceptFriendRequest(
                request.id,
                response =>
                {
                    var message = !string.IsNullOrWhiteSpace(response?.message) ? response.message : "好友请求已接受";
                    SetStatus(message);
                    ShowToast(message);
                    RefreshState();
                },
                error =>
                {
                    SetStatus(error);
                    ShowToast(error);
                    RefreshState();
                });
        }

        private void DeclineRequestFromList(FriendAPI.FriendRequestDto request)
        {
            if (request == null) return;

            FriendAPI.DeclineFriendRequest(
                request.id,
                response =>
                {
                    var message = !string.IsNullOrWhiteSpace(response?.message) ? response.message : "好友请求已拒绝";
                    SetStatus(message);
                    ShowToast(message);
                    RefreshState();
                },
                error =>
                {
                    SetStatus(error);
                    ShowToast(error);
                    RefreshState();
                });
        }

        private bool IsFriend(string username)
        {
            foreach (var friend in _lastState.friends)
            {
                if (friend != null && string.Equals(friend.username, username, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private FriendAPI.FriendRequestDto FindIncomingFrom(string username)
        {
            foreach (var request in _lastState.incoming_requests)
            {
                if (request != null && string.Equals(request.sender_username, username, StringComparison.Ordinal))
                {
                    return request;
                }
            }

            return null;
        }

        private FriendAPI.FriendRequestDto FindOutgoingTo(string username)
        {
            foreach (var request in _lastState.outgoing_requests)
            {
                if (request != null && string.Equals(request.receiver_username, username, StringComparison.Ordinal))
                {
                    return request;
                }
            }

            return null;
        }

        private void ShowToast(string message)
        {
            if (_toastObject == null || _toastLabel == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
            }

            _toastObject.SetActive(true);
            _toastLabel.text = message;
            _toastCoroutine = StartCoroutine(HideToastAfterDelay());
        }

        private IEnumerator HideToastAfterDelay()
        {
            yield return new WaitForSeconds(3f);
            HideToastImmediate();
            _toastCoroutine = null;
        }

        private void HideToastImmediate()
        {
            if (_toastObject != null)
            {
                _toastObject.SetActive(false);
            }
        }

        private void HideSelectionCard()
        {
            _selectedUsername = null;
            _selectedRequestId = -1;
            _selectedAction = SelectedAction.None;
            if (_selectionCard != null)
            {
                _selectionCard.SetActive(false);
            }
        }

        private bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void SetUiVisible(bool visible)
        {
            _uiVisible = visible;

            if (_toggleButtonObject != null)
            {
                _toggleButtonObject.SetActive(visible);
            }

            if (!visible)
            {
                ClosePanel();
            }
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message ?? string.Empty;
            }
        }

        private void CreateUi()
        {
            EnsureEventSystem();

            var canvasObj = new GameObject("FriendSystemCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 135;

            _canvasRect = canvasObj.GetComponent<RectTransform>();
            if (_canvasRect == null)
            {
                _canvasRect = canvasObj.AddComponent<RectTransform>();
            }

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            CreateToggleButton(canvasObj.transform);
            CreatePanel(canvasObj.transform);
            CreateSelectionCard(canvasObj.transform);
            CreateToast(canvasObj.transform);
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventSystemObj);
        }

        private void CreateToggleButton(Transform parent)
        {
            _toggleButtonObject = CreatePanelObject(parent, "FriendToggleButton", new Vector2(156f, 54f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 34f), new Color(0.18f, 0.47f, 0.79f, 0.95f));
            _toggleButton = _toggleButtonObject.AddComponent<Button>();
            _toggleButton.onClick.AddListener(TogglePanel);
            _toggleLabel = CreateTextChild(_toggleButtonObject, "Label", "好友(F)", 22f, TextAlignmentOptions.Center);
        }

        private void CreatePanel(Transform parent)
        {
            _panel = CreatePanelObject(parent, "FriendPanel", new Vector2(400f, 660f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Color(0.08f, 0.09f, 0.12f, 0.98f));

            var header = CreatePanelObject(_panel.transform, "Header", new Vector2(0f, 68f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Color(0.14f, 0.16f, 0.2f, 1f));
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.offsetMin = new Vector2(0f, -68f);
            headerRect.offsetMax = Vector2.zero;
            CreateTextChild(header, "Title", "好友系统", 30f, TextAlignmentOptions.Center);

            var closeButtonObj = CreatePanelObject(header.transform, "CloseButton", new Vector2(44f, 44f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Color(0.78f, 0.26f, 0.26f, 1f));
            var closeButton = closeButtonObj.AddComponent<Button>();
            closeButton.onClick.AddListener(ClosePanel);
            CreateTextChild(closeButtonObj, "Label", "X", 24f, TextAlignmentOptions.Center);

            var statusObj = new GameObject("Status");
            statusObj.transform.SetParent(_panel.transform, false);
            var statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.offsetMin = new Vector2(16f, -138f);
            statusRect.offsetMax = new Vector2(-16f, -86f);
            _statusLabel = statusObj.AddComponent<TextMeshProUGUI>();
            _statusLabel.fontSize = 20f;
            _statusLabel.alignment = TextAlignmentOptions.Left;
            _statusLabel.color = new Color(0.82f, 0.87f, 0.95f, 1f);
            _statusLabel.enableWordWrapping = true;
            _statusLabel.text = "打开后点击世界里的其他玩家可发送好友请求。";

            var hintObj = new GameObject("Hint");
            hintObj.transform.SetParent(_panel.transform, false);
            var hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.offsetMin = new Vector2(16f, -188f);
            hintRect.offsetMax = new Vector2(-16f, -144f);
            var hintLabel = hintObj.AddComponent<TextMeshProUGUI>();
            hintLabel.fontSize = 18f;
            hintLabel.alignment = TextAlignmentOptions.Left;
            hintLabel.color = new Color(0.56f, 0.75f, 0.66f, 1f);
            hintLabel.enableWordWrapping = true;
            hintLabel.text = "按 F 打开好友面板，面板开启时点击别的玩家即可操作。";

            var scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(_panel.transform, false);
            var scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(18f, 18f);
            scrollRect.offsetMax = new Vector2(-18f, -206f);
            scrollObj.AddComponent<Image>().color = new Color(0.11f, 0.12f, 0.15f, 0.92f);
            _scrollRect = scrollObj.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.AddComponent<RectMask2D>();
            _scrollRect.viewport = viewportRect;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            _contentRoot = content.AddComponent<RectTransform>();
            _contentRoot.anchorMin = new Vector2(0f, 1f);
            _contentRoot.anchorMax = new Vector2(1f, 1f);
            _contentRoot.pivot = new Vector2(0.5f, 1f);
            _contentRoot.anchoredPosition = Vector2.zero;
            _contentRoot.sizeDelta = new Vector2(0f, 0f);
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scrollRect.content = _contentRoot;

            _panel.SetActive(false);
        }

        private void CreateSelectionCard(Transform parent)
        {
            _selectionCard = CreatePanelObject(parent, "FriendSelectionCard", new Vector2(260f, 150f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.1f, 0.11f, 0.15f, 0.98f));
            _selectionCardRect = _selectionCard.GetComponent<RectTransform>();

            _selectionTitle = CreateTextChild(_selectionCard, "Title", "玩家", 28f, TextAlignmentOptions.Center);
            var titleRect = _selectionTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(12f, -48f);
            titleRect.offsetMax = new Vector2(-12f, -14f);

            _selectionHint = CreateTextChild(_selectionCard, "Hint", string.Empty, 18f, TextAlignmentOptions.Center);
            var hintRect = _selectionHint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0.5f);
            hintRect.anchorMax = new Vector2(1f, 0.5f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.offsetMin = new Vector2(14f, -20f);
            hintRect.offsetMax = new Vector2(-14f, 30f);
            _selectionHint.enableWordWrapping = true;
            _selectionHint.color = new Color(0.82f, 0.88f, 0.93f, 1f);

            var actionButtonObj = CreatePanelObject(_selectionCard.transform, "ActionButton", new Vector2(154f, 42f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Color(0.19f, 0.62f, 0.36f, 1f));
            _selectionActionButton = actionButtonObj.AddComponent<Button>();
            _selectionActionButton.onClick.AddListener(OnSelectionActionClicked);
            _selectionActionLabel = CreateTextChild(actionButtonObj, "Label", "添加好友", 21f, TextAlignmentOptions.Center);
        }

        private void CreateToast(Transform parent)
        {
            _toastObject = CreatePanelObject(parent, "FriendToast", new Vector2(420f, 54f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Color(0.15f, 0.48f, 0.72f, 0.96f));
            _toastLabel = CreateTextChild(_toastObject, "Label", string.Empty, 22f, TextAlignmentOptions.Center);
        }

        private void BuildSectionHeader(string text)
        {
            var go = new GameObject($"{text}Header");
            go.transform.SetParent(_contentRoot, false);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 34f;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 22f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.69f, 0.84f, 0.95f, 1f);
            label.alignment = TextAlignmentOptions.Left;
        }

        private void BuildInfoRow(string text)
        {
            BuildSimpleRow(text, new Color(0.17f, 0.18f, 0.22f, 0.9f));
        }

        private void BuildSimpleRow(string text, Color backgroundColor)
        {
            var row = CreateListRow(backgroundColor, 58f);
            var label = CreateTextChild(row, "Label", text, 20f, TextAlignmentOptions.Left);
            label.rectTransform.offsetMin = new Vector2(16f, 8f);
            label.rectTransform.offsetMax = new Vector2(-16f, -8f);
        }

        private void BuildIncomingRequestRow(FriendAPI.FriendRequestDto request)
        {
            var row = CreateListRow(new Color(0.14f, 0.18f, 0.15f, 0.94f), 72f);

            var label = CreateTextChild(row, "Label", request.sender_username, 20f, TextAlignmentOptions.Left);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.45f, 1f);
            label.rectTransform.offsetMin = new Vector2(16f, 8f);
            label.rectTransform.offsetMax = new Vector2(-8f, -8f);

            var acceptButtonObj = CreatePanelObject(row.transform, "AcceptButton", new Vector2(86f, 36f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-104f, 0f), new Color(0.19f, 0.62f, 0.36f, 1f));
            var acceptButton = acceptButtonObj.AddComponent<Button>();
            acceptButton.onClick.AddListener(() => AcceptRequestFromList(request));
            CreateTextChild(acceptButtonObj, "Label", "接受", 18f, TextAlignmentOptions.Center);

            var declineButtonObj = CreatePanelObject(row.transform, "DeclineButton", new Vector2(86f, 36f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Color(0.67f, 0.28f, 0.28f, 1f));
            var declineButton = declineButtonObj.AddComponent<Button>();
            declineButton.onClick.AddListener(() => DeclineRequestFromList(request));
            CreateTextChild(declineButtonObj, "Label", "拒绝", 18f, TextAlignmentOptions.Center);
        }

        private GameObject CreateListRow(Color backgroundColor, float preferredHeight)
        {
            var row = new GameObject("Row");
            row.transform.SetParent(_contentRoot, false);
            row.AddComponent<Image>().color = backgroundColor;
            var layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            return row;
        }

        private GameObject CreatePanelObject(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            obj.AddComponent<Image>().color = color;
            return obj;
        }

        private TextMeshProUGUI CreateTextChild(GameObject parent, string name, string text, float size, TextAlignmentOptions alignment)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = obj.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.enableWordWrapping = false;
            return label;
        }
    }
}
