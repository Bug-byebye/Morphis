using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Morphis.AppFlow;
using Morphis.Chat;
using Morphis.Companion;
using Morphis.Friends;
using Morphis.ModelPlacement;
using AIPipeline.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Morphis.UI
{
    /// <summary>
    /// Builds a polished in-game HUD for MainScene using imported PNG art.
    /// It hides legacy launch buttons but keeps core functions reachable from the new layout.
    /// </summary>
    public sealed class MainSceneHudController : MonoBehaviour
    {
        private const string MainSceneName = "MainScene";
        private static readonly Color UiTextColor = new(0.08f, 0.07f, 0.12f, 1f);

        private static bool s_sceneHookInstalled;

        private Canvas _canvas;
        private RectTransform _hudRoot;
        private TMP_FontAsset _font;
        private Sprite _companionPanelSprite;
        private Sprite _actionDockSprite;
        private Sprite _capsuleSprite;
        private Sprite _bubbleSprite;
        private Sprite _primaryButtonSprite;
        private Sprite _goalCardSprite;

        private TMP_Text _toastText;
        private Image _toastBackground;
        private TMP_Text _companionNameText;
        private TMP_Text _workspaceStatusText;
        private GameObject _topLeftProfile;
        private GameObject _topCenterStatus;
        private GameObject _topRightButtons;
        private GameObject _leftMenu;
        private GameObject _rightCompanionPanel;
        private GameObject _bottomActionDock;
        private GameObject _bottomLeftGoalCard;
        private Button _workbenchButton;
        private Button _mailButton;
        private Button _settingsButton;
        private GameObject _quickWorkbenchPanel;
        private TMP_InputField _quickPromptInput;
        private TMP_Text _quickImageStatusText;
        private TMP_Text _quickWorkbenchStatusText;
        private Button _quickSelectImageButton;
        private Button _quickTextTo3DButton;
        private Button _quickImageTo3DButton;
        private Button _quickProfessionalModeButton;
        private byte[] _quickSelectedImageData;
        private string _quickSelectedImageName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (!s_sceneHookInstalled)
            {
                SceneManager.sceneLoaded += (_, _) => EnsureInstance();
                s_sceneHookInstalled = true;
            }

            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, MainSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (FindFirstObjectByType<MainSceneHudController>() != null)
            {
                return;
            }

            var go = new GameObject("MainSceneHudController(Auto)");
            DontDestroyOnLoad(go);
            go.AddComponent<MainSceneHudController>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            LoadAssets();
            if (IsMainSceneActive())
            {
                BuildUi();
            }
            RefreshSceneVisibility();
            if (IsMainSceneActive())
            {
                RefreshDynamicLabels();
                HideLegacyUi();
            }
        }

        private void Update()
        {
            if (!IsMainSceneActive())
            {
                RefreshSceneVisibility();
                return;
            }

            if (_canvas == null)
            {
                BuildUi();
            }

            RefreshSceneVisibility();
            RefreshDynamicLabels();
            RefreshWorkbenchHudState();

            if (Time.frameCount % 30 == 0)
            {
                HideLegacyUi();
            }
        }

        private bool IsMainSceneActive()
        {
            return string.Equals(SceneManager.GetActiveScene().name, MainSceneName, StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshSceneVisibility()
        {
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(IsMainSceneActive());
            }
        }

        private void LoadAssets()
        {
            if (_font == null)
            {
                _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansSC-VariableFont_wght SDF");
                if (_font == null)
                {
                    _font = TMP_Settings.defaultFontAsset;
                }
            }

#if UNITY_EDITOR
            _companionPanelSprite ??= LoadSpriteAsset("Assets/Game/ui/ChatGPT Image 2026年5月8日 03_56_48 (1).png");
            _actionDockSprite ??= LoadSpriteAsset("Assets/Game/ui/ChatGPT Image 2026年5月8日 03_56_48 (2).png");
            _capsuleSprite ??= LoadSpriteAsset("Assets/Game/ui/ChatGPT Image 2026年5月8日 03_56_48 (3).png");
            _bubbleSprite ??= LoadSpriteAsset("Assets/Game/ui/ChatGPT Image 2026年5月8日 03_56_49 (4).png");
            _primaryButtonSprite ??= LoadSpriteAsset("Assets/Game/ui/ChatGPT Image 2026年5月8日 03_56_49 (5).png");
            _goalCardSprite ??= LoadSpriteAsset("Assets/Game/ui/ChatGPT Image 2026年5月8日 03_56_49 (6).png");
#endif
        }

#if UNITY_EDITOR
        private static Sprite LoadSpriteAsset(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite subSprite)
                {
                    return subSprite;
                }
            }

            Debug.LogWarning($"[MainSceneHUD] Failed to load sprite at path: {path}");
            return null;
        }
#endif

        private void BuildUi()
        {
            if (_canvas != null)
            {
                return;
            }

            var canvasGo = new GameObject("MainSceneHudCanvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 120;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _hudRoot = canvasGo.GetComponent<RectTransform>();
            Stretch(_hudRoot);

            var root = new GameObject("HUDRoot");
            root.transform.SetParent(canvasGo.transform, false);
            var rootRect = root.AddComponent<RectTransform>();
            Stretch(rootRect);

            BuildTopLeftProfile(rootRect);
            BuildTopCenterStatus(rootRect);
            BuildTopRightButtons(rootRect);
            BuildLeftMenu(rootRect);
            BuildBottomActionDock(rootRect);
            BuildBottomLeftGoalCard(rootRect);
            BuildToast(rootRect);
            BuildQuickWorkbenchPanel(rootRect);
        }

        private void BuildTopLeftProfile(RectTransform parent)
        {
            var root = CreateUiImage(parent, "TopLeftProfile", _capsuleSprite, new Vector2(384f, 122f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -30f), new Color(0.40f, 0.58f, 0.82f, 0.82f));
            _topLeftProfile = root.gameObject;
            root.color = new Color(0.76f, 0.86f, 0.98f, 0.90f);
            var rootRect = root.rectTransform;

            var avatar = CreateUiImage(rootRect, "AvatarFrame", _bubbleSprite, new Vector2(94f, 94f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Color(1f, 1f, 1f, 0.95f));
            avatar.color = new Color(0.95f, 0.97f, 1f, 0.96f);
            CreateText(avatar.rectTransform, "AvatarGlyph", "旅", 34f, FontStyles.Bold, TextAlignmentOptions.Center, UiTextColor, Vector2.zero, new Vector2(1f, 1f));

            CreateText(rootRect, "ProfileName", "星语旅人", 35f, FontStyles.Bold, TextAlignmentOptions.Left, UiTextColor, new Vector2(126f, 18f), new Vector2(210f, 44f), new Vector2(0f, 0.5f));

            var levelPill = CreateUiImage(rootRect, "LevelPill", _primaryButtonSprite, new Vector2(98f, 38f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(126f, -30f), new Color(1f, 1f, 1f, 0.96f));
            levelPill.color = new Color(0.93f, 0.73f, 0.88f, 0.96f);
            CreateText(levelPill.rectTransform, "LevelText", "Lv. 12", 22f, FontStyles.Bold, TextAlignmentOptions.Center, UiTextColor, Vector2.zero, new Vector2(1f, 1f));

            CreateText(rootRect, "AccentSparkle", "·", 30f, FontStyles.Bold, TextAlignmentOptions.Center, WithTextAlpha(0.45f), new Vector2(-24f, 0f), new Vector2(36f, 36f), new Vector2(1f, 0.5f));
        }

        private void BuildTopCenterStatus(RectTransform parent)
        {
            var root = CreateUiImage(parent, "TopCenterStatus", _capsuleSprite, new Vector2(332f, 66f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Color(0.45f, 0.60f, 0.86f, 0.76f));
            _topCenterStatus = root.gameObject;
            root.color = new Color(0.77f, 0.87f, 0.98f, 0.88f);
            _workspaceStatusText = CreateText(root.rectTransform, "StatusText", "宁静湖畔", 30f, FontStyles.Normal, TextAlignmentOptions.Center, UiTextColor, new Vector2(-8f, 0f), new Vector2(220f, 50f));
            _workspaceStatusText.alpha = 0.95f;
            CreateText(root.rectTransform, "StatusBadge", "晴", 24f, FontStyles.Bold, TextAlignmentOptions.Center, WithTextAlpha(0.82f), new Vector2(108f, 0f), new Vector2(46f, 46f));
        }

        private void BuildTopRightButtons(RectTransform parent)
        {
            var root = new GameObject("TopRightButtons");
            root.transform.SetParent(parent, false);
            _topRightButtons = root;
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-36f, -30f);
            rootRect.sizeDelta = new Vector2(456f, 78f);

            var workbench = CreateSpriteButton(rootRect, "WorkbenchButton", _primaryButtonSprite, new Vector2(246f, 76f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), "工作台", WithTextAlpha(0.98f), new Color(1f, 0.57f, 0.82f, 0.94f));
            _workbenchButton = workbench;
            workbench.GetComponent<Image>().color = new Color(0.95f, 0.73f, 0.86f, 0.96f);
            workbench.onClick.AddListener(() =>
            {
                var editor = FindFirstObjectByType<SimpleNodeEditor>();
                if (editor != null && SimpleNodeEditor.IsEditorOpen)
                {
                    editor.ToggleEditor();
                    RefreshWorkbenchHudState();
                }
                else if (_quickWorkbenchPanel != null && _quickWorkbenchPanel.activeSelf)
                {
                    SetQuickWorkbenchOpen(false);
                }
                else if (editor != null)
                {
                    SetQuickWorkbenchOpen(true);
                }
                else
                {
                    ShowToast("工作台暂时不可用");
                }
            });

            var mail = CreateCircleTextButton(rootRect, "MailButton", "信", new Vector2(270f, 0f), 74f, new Vector2(0f, 0.5f));
            _mailButton = mail;
            mail.GetComponent<Image>().color = new Color(0.94f, 0.96f, 1f, 0.96f);
            mail.onClick.AddListener(() =>
            {
                var friend = FindFirstObjectByType<FriendSystemUI>();
                if (friend != null)
                {
                    friend.ToggleFromHud();
                }
                else
                {
                    ShowToast("消息功能稍后接入");
                }
            });

            var settings = CreateCircleTextButton(rootRect, "SettingsButton", "设", new Vector2(362f, 0f), 74f, new Vector2(0f, 0.5f));
            _settingsButton = settings;
            settings.GetComponent<Image>().color = new Color(0.94f, 0.96f, 1f, 0.96f);
            settings.onClick.AddListener(() =>
            {
                var controller = FindFirstObjectByType<GlobalSceneController>();
                if (controller != null)
                {
                    controller.ToggleExitDialogFromUi();
                }
                else
                {
                    ShowToast("设置面板暂时不可用");
                }
            });
        }

        private void BuildLeftMenu(RectTransform parent)
        {
            var root = new GameObject("LeftMenu");
            root.transform.SetParent(parent, false);
            _leftMenu = root;
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0.5f);
            rootRect.anchorMax = new Vector2(0f, 0.5f);
            rootRect.pivot = new Vector2(0f, 0.5f);
            rootRect.anchoredPosition = new Vector2(40f, -8f);
            rootRect.sizeDelta = new Vector2(198f, 292f);

            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            CreateLeftMenuButton(rootRect, "CompanionButton", "伴侣", "伴", () =>
            {
                var dog = FindFirstObjectByType<DogCompanion>();
                if (dog != null)
                {
                    dog.OpenChatPanel();
                }
                else
                {
                    ShowToast("伴侣暂时不在场景里");
                }
            });

            CreateLeftMenuButton(rootRect, "TaskButton", "任务", "任", () =>
            {
                ShowToast("任务系统 UI 先预留在这里");
            });

            CreateLeftMenuButton(rootRect, "BagButton", "背包", "包", () =>
            {
                var library = FindFirstObjectByType<ModelLibraryUI>();
                if (library != null)
                {
                    library.ToggleFromHud();
                }
                else
                {
                    ShowToast("背包功能稍后接入");
                }
            });
        }

        private void BuildRightCompanionPanel(RectTransform parent)
        {
            var root = CreateUiImage(parent, "RightCompanionPanel", _companionPanelSprite, new Vector2(452f, 556f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-58f, -10f), new Color(0.62f, 0.83f, 0.74f, 0.74f));
            _rightCompanionPanel = root.gameObject;
            root.color = new Color(0.83f, 0.92f, 0.83f, 0.88f);
            var rootRect = root.rectTransform;

            _companionNameText = CreateText(rootRect, "CompanionName", "小星", 42f, FontStyles.Bold, TextAlignmentOptions.Left, UiTextColor, new Vector2(34f, -42f), new Vector2(220f, 56f), new Vector2(0f, 1f));
            CreateText(rootRect, "CompanionBadge", "陪伴中", 20f, FontStyles.Bold, TextAlignmentOptions.Left, WithTextAlpha(0.80f), new Vector2(34f, -88f), new Vector2(108f, 32f), new Vector2(0f, 1f));

            var infoPanel = CreateUiImage(rootRect, "CompanionInfoPanel", null, new Vector2(356f, 132f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Color(0.44f, 0.54f, 0.46f, 0.18f));
            CreateText(infoPanel.rectTransform, "CompanionMood", "心情：平静", 25f, FontStyles.Normal, TextAlignmentOptions.Left, WithTextAlpha(0.96f), new Vector2(26f, -22f), new Vector2(180f, 34f), new Vector2(0f, 1f));
            CreateText(infoPanel.rectTransform, "CompanionMoodStatus", "舒缓", 23f, FontStyles.Bold, TextAlignmentOptions.Right, WithTextAlpha(0.86f), new Vector2(-24f, -22f), new Vector2(90f, 34f), new Vector2(1f, 1f));
            CreateText(infoPanel.rectTransform, "CompanionAffinityLabel", "亲密度", 25f, FontStyles.Normal, TextAlignmentOptions.Left, WithTextAlpha(0.96f), new Vector2(26f, -76f), new Vector2(120f, 34f), new Vector2(0f, 1f));
            CreateText(infoPanel.rectTransform, "CompanionAffinityValue", "680 / 1000", 23f, FontStyles.Normal, TextAlignmentOptions.Right, WithTextAlpha(0.92f), new Vector2(-24f, -76f), new Vector2(150f, 34f), new Vector2(1f, 1f));

            var progressBack = CreateUiImage(rootRect, "AffinityTrack", null, new Vector2(330f, 14f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -322f), new Color(1f, 1f, 1f, 0.24f));
            var progressFill = CreateUiImage(progressBack.rectTransform, "AffinityFill", null, new Vector2(224f, 14f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Color(0.95f, 0.55f, 0.75f, 0.96f));
            progressFill.rectTransform.anchoredPosition = Vector2.zero;

            var callButton = CreateSpriteButton(rootRect, "CallCompanionButton", _primaryButtonSprite, new Vector2(314f, 82f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), "呼唤伴侣", UiTextColor, new Color(1f, 0.60f, 0.82f, 0.95f));
            callButton.GetComponent<Image>().color = new Color(0.95f, 0.72f, 0.84f, 0.97f);
            callButton.onClick.AddListener(() =>
            {
                var dog = FindFirstObjectByType<DogCompanion>();
                if (dog != null)
                {
                    dog.OpenChatPanel();
                    ShowToast("小星已经在你身边啦");
                }
                else
                {
                    ShowToast("伴侣功能暂时不可用");
                }
            });
        }

        private void BuildBottomActionDock(RectTransform parent)
        {
            var root = CreateUiImage(parent, "BottomActionDock", _actionDockSprite, new Vector2(696f, 176f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Color(0.48f, 0.58f, 0.72f, 0.86f));
            _bottomActionDock = root.gameObject;
            root.color = new Color(0.82f, 0.89f, 0.96f, 0.90f);
            var rootRect = root.rectTransform;

            var items = new[]
            {
                ("聊天", "聊", new Vector2(-210f, 8f), 106f, true, (Action)(() =>
                {
                    var chat = FindFirstObjectByType<HumanChatUI>();
                    if (chat != null)
                    {
                        chat.Toggle();
                    }
                    else
                    {
                        ShowToast("聊天功能当前不可用");
                    }
                })),
                ("散步", "步", new Vector2(-70f, 0f), 92f, false, (Action)(() => ShowToast("散步玩法先保留交互入口"))),
                ("拍照", "拍", new Vector2(70f, 0f), 92f, false, (Action)(() => ShowToast("拍照功能 UI 已预留"))),
                ("休息", "休", new Vector2(210f, 0f), 92f, false, (Action)(() => ShowToast("休息功能 UI 已预留")))
            };

            foreach (var item in items)
            {
                var slot = new GameObject($"{item.Item1}Slot");
                slot.transform.SetParent(rootRect, false);
                var slotRect = slot.AddComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0.5f, 0.5f);
                slotRect.anchorMax = new Vector2(0.5f, 0.5f);
                slotRect.pivot = new Vector2(0.5f, 0.5f);
                slotRect.anchoredPosition = item.Item3;
                slotRect.sizeDelta = new Vector2(120f, 130f);

                if (item.Item5)
                {
                    var accent = CreateUiImage(slotRect, "Highlight", _primaryButtonSprite, new Vector2(128f, 128f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Color(1f, 1f, 1f, 0.68f));
                    accent.color = new Color(0.82f, 0.70f, 0.95f, 0.74f);
                }

                var button = CreateCircleTextButton(slotRect, $"{item.Item1}Button", item.Item2, new Vector2(0f, 8f), item.Item4);
                button.GetComponent<Image>().color = item.Item5 ? new Color(0.88f, 0.77f, 0.96f, 0.98f) : new Color(0.94f, 0.96f, 1f, 0.96f);
                button.onClick.AddListener(() => item.Item6.Invoke());

                var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.fontSize = item.Item5 ? 34f : 30f;
                }

                CreateText(slotRect, $"{item.Item1}Label", item.Item1, item.Item5 ? 30f : 27f, FontStyles.Bold, TextAlignmentOptions.Center, UiTextColor, new Vector2(0f, -48f), new Vector2(120f, 34f), new Vector2(0.5f, 0.5f));
            }
        }

        private void BuildBottomLeftGoalCard(RectTransform parent)
        {
            var root = CreateUiImage(parent, "BottomLeftGoalCard", _goalCardSprite, new Vector2(382f, 148f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 30f), new Color(0.68f, 0.80f, 0.95f, 0.84f));
            _bottomLeftGoalCard = root.gameObject;
            root.color = new Color(0.80f, 0.89f, 0.97f, 0.90f);
            var rootRect = root.rectTransform;

            CreateText(rootRect, "GoalMark", "今", 26f, FontStyles.Bold, TextAlignmentOptions.Center, UiTextColor, new Vector2(26f, -26f), new Vector2(42f, 42f), new Vector2(0f, 1f));
            CreateText(rootRect, "GoalTitle", "今日小目标", 30f, FontStyles.Bold, TextAlignmentOptions.Left, UiTextColor, new Vector2(74f, -24f), new Vector2(220f, 34f), new Vector2(0f, 1f));
            CreateText(rootRect, "GoalBody", "和小星一起散步，\n享受湖畔的微风吧。", 23f, FontStyles.Normal, TextAlignmentOptions.TopLeft, WithTextAlpha(0.95f), new Vector2(74f, -62f), new Vector2(244f, 66f), new Vector2(0f, 1f));

            var arrowButton = CreateCircleTextButton(rootRect, "GoalArrowButton", "前", new Vector2(-22f, 20f), 48f, new Vector2(1f, 0f));
            arrowButton.GetComponent<Image>().color = new Color(0.94f, 0.96f, 1f, 0.96f);
            arrowButton.onClick.AddListener(() => ShowToast("小目标面板先保留 UI 入口"));
        }

        private void BuildToast(RectTransform parent)
        {
            _toastBackground = CreateUiImage(parent, "HudToast", _capsuleSprite, new Vector2(380f, 52f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Color(0.45f, 0.58f, 0.86f, 0.86f));
            _toastBackground.gameObject.SetActive(false);
            _toastText = CreateText(_toastBackground.rectTransform, "ToastText", string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.Center, UiTextColor, Vector2.zero, new Vector2(1f, 1f));
        }

        private void BuildQuickWorkbenchPanel(RectTransform parent)
        {
            var overlay = new GameObject("QuickWorkbenchPanel");
            overlay.transform.SetParent(parent, false);
            _quickWorkbenchPanel = overlay;

            var overlayRect = overlay.AddComponent<RectTransform>();
            Stretch(overlayRect);

            var overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0.03f, 0.05f, 0.08f, 0.48f);
            overlayImage.raycastTarget = true;

            var card = CreateUiImage(overlayRect, "QuickWorkbenchCard", _companionPanelSprite, new Vector2(620f, 452f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.86f, 0.92f, 0.98f, 0.95f));
            card.color = new Color(0.86f, 0.92f, 0.98f, 0.95f);
            var cardRect = card.rectTransform;

            CreateText(cardRect, "WorkbenchTitle", "工作台", 38f, FontStyles.Bold, TextAlignmentOptions.Center, UiTextColor, new Vector2(0f, -34f), new Vector2(280f, 46f), new Vector2(0.5f, 1f));
            CreateText(cardRect, "WorkbenchSubtitle", "快捷模式", 22f, FontStyles.Normal, TextAlignmentOptions.Center, WithTextAlpha(0.75f), new Vector2(0f, -76f), new Vector2(240f, 34f), new Vector2(0.5f, 1f));

            var closeButton = CreateCircleTextButton(cardRect, "QuickWorkbenchClose", "关", new Vector2(-26f, -24f), 48f, new Vector2(1f, 1f));
            closeButton.GetComponent<Image>().color = new Color(0.94f, 0.96f, 1f, 0.96f);
            closeButton.onClick.AddListener(() => SetQuickWorkbenchOpen(false));

            CreateText(cardRect, "TextModeLabel", "输入文字出 3D", 28f, FontStyles.Bold, TextAlignmentOptions.Left, UiTextColor, new Vector2(48f, -126f), new Vector2(240f, 36f), new Vector2(0f, 1f));
            _quickPromptInput = CreateInputField(cardRect, "QuickPromptInput", new Vector2(524f, 68f), new Vector2(0f, -178f), "例如：一只发光的梦幻湖畔小鹿");

            _quickTextTo3DButton = CreateSpriteButton(cardRect, "QuickTextTo3DButton", _primaryButtonSprite, new Vector2(240f, 68f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-136f, -262f), "文字出3D", UiTextColor, new Color(0.95f, 0.72f, 0.84f, 0.97f));
            _quickTextTo3DButton.GetComponent<Image>().color = new Color(0.95f, 0.72f, 0.84f, 0.97f);
            _quickTextTo3DButton.onClick.AddListener(OnQuickTextTo3DClicked);

            _quickSelectImageButton = CreateSpriteButton(cardRect, "QuickSelectImageButton", _capsuleSprite, new Vector2(240f, 62f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(136f, -262f), "选择图片", UiTextColor, new Color(0.80f, 0.89f, 0.97f, 0.92f));
            _quickSelectImageButton.GetComponent<Image>().color = new Color(0.80f, 0.89f, 0.97f, 0.92f);
            _quickSelectImageButton.onClick.AddListener(SelectQuickWorkbenchImage);

            _quickImageStatusText = CreateText(cardRect, "QuickImageStatus", "未选择图片", 20f, FontStyles.Normal, TextAlignmentOptions.Center, WithTextAlpha(0.75f), new Vector2(0f, -318f), new Vector2(460f, 32f), new Vector2(0.5f, 1f));

            _quickImageTo3DButton = CreateSpriteButton(cardRect, "QuickImageTo3DButton", _primaryButtonSprite, new Vector2(240f, 68f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -360f), "图片出3D", UiTextColor, new Color(0.95f, 0.72f, 0.84f, 0.97f));
            _quickImageTo3DButton.GetComponent<Image>().color = new Color(0.95f, 0.72f, 0.84f, 0.97f);
            _quickImageTo3DButton.onClick.AddListener(OnQuickImageTo3DClicked);

            _quickProfessionalModeButton = CreateSpriteButton(cardRect, "QuickProfessionalModeButton", _capsuleSprite, new Vector2(280f, 60f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), "专业模式", UiTextColor, new Color(0.80f, 0.89f, 0.97f, 0.92f));
            _quickProfessionalModeButton.GetComponent<Image>().color = new Color(0.80f, 0.89f, 0.97f, 0.92f);
            _quickProfessionalModeButton.onClick.AddListener(OpenProfessionalWorkbenchMode);

            _quickWorkbenchStatusText = CreateText(cardRect, "QuickWorkbenchStatus", "直接走背后的 pipeline，不需要你手动连节点。", 18f, FontStyles.Normal, TextAlignmentOptions.Center, WithTextAlpha(0.72f), new Vector2(0f, 82f), new Vector2(440f, 28f), new Vector2(0.5f, 0f));
            _quickWorkbenchPanel.SetActive(false);
        }

        private void RefreshWorkbenchHudState()
        {
            bool isWorkbenchOpen = SimpleNodeEditor.IsEditorOpen || (_quickWorkbenchPanel != null && _quickWorkbenchPanel.activeSelf);

            SetVisible(_topLeftProfile, !isWorkbenchOpen);
            SetVisible(_topCenterStatus, !isWorkbenchOpen);
            SetVisible(_leftMenu, !isWorkbenchOpen);
            SetVisible(_rightCompanionPanel, !isWorkbenchOpen);
            SetVisible(_bottomActionDock, !isWorkbenchOpen);
            SetVisible(_bottomLeftGoalCard, !isWorkbenchOpen);

            if (_mailButton != null)
            {
                _mailButton.gameObject.SetActive(!isWorkbenchOpen);
            }

            if (_settingsButton != null)
            {
                _settingsButton.gameObject.SetActive(!isWorkbenchOpen);
            }

            if (_workbenchButton != null)
            {
                var workbenchRect = _workbenchButton.GetComponent<RectTransform>();
                if (workbenchRect != null)
                {
                    workbenchRect.anchoredPosition = isWorkbenchOpen ? new Vector2(210f, 0f) : Vector2.zero;
                }
            }
        }

        private void SetQuickWorkbenchOpen(bool open)
        {
            if (_quickWorkbenchPanel == null)
            {
                return;
            }

            if (!open)
            {
                SetQuickWorkbenchBusy(false, "直接走背后的 pipeline，不需要你手动连节点。");
            }

            _quickWorkbenchPanel.SetActive(open);
            RefreshWorkbenchHudState();
        }

        private void OnQuickTextTo3DClicked()
        {
            var editor = FindFirstObjectByType<SimpleNodeEditor>();
            if (editor == null)
            {
                ShowToast("工作台组件还没有准备好。");
                return;
            }

            string prompt = _quickPromptInput != null ? _quickPromptInput.text?.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                SetQuickWorkbenchBusy(false, "请输入文字描述后再生成。");
                return;
            }

            SetQuickWorkbenchBusy(true, "正在根据文字生成 3D 模型...");
            editor.GenerateTextTo3DQuick(prompt, UpdateQuickWorkbenchStatus, (success, message) =>
            {
                SetQuickWorkbenchBusy(false, message);
                if (success)
                {
                    SetQuickWorkbenchOpen(false);
                }
                ShowToast(message);
            });
        }

        private void OnQuickImageTo3DClicked()
        {
            var editor = FindFirstObjectByType<SimpleNodeEditor>();
            if (editor == null)
            {
                ShowToast("工作台组件还没有准备好。");
                return;
            }

            if (_quickSelectedImageData == null || _quickSelectedImageData.Length == 0)
            {
                SetQuickWorkbenchBusy(false, "请先选择一张图片。");
                return;
            }

            SetQuickWorkbenchBusy(true, $"正在根据图片生成 3D：{_quickSelectedImageName}");
            editor.GenerateImageTo3DQuick(_quickSelectedImageData, _quickSelectedImageName, UpdateQuickWorkbenchStatus, (success, message) =>
            {
                SetQuickWorkbenchBusy(false, message);
                if (success)
                {
                    SetQuickWorkbenchOpen(false);
                }
                ShowToast(message);
            });
        }

        private void OpenProfessionalWorkbenchMode()
        {
            var editor = FindFirstObjectByType<SimpleNodeEditor>();
            if (editor == null)
            {
                ShowToast("专业工作台暂时不可用。");
                return;
            }

            SetQuickWorkbenchOpen(false);
            if (!SimpleNodeEditor.IsEditorOpen)
            {
                editor.ToggleEditor();
            }
            RefreshWorkbenchHudState();
        }

        private void SelectQuickWorkbenchImage()
        {
#if UNITY_EDITOR
            string path = EditorUtility.OpenFilePanel("选择图片", "", "png,jpg,jpeg,webp,bmp");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                _quickSelectedImageData = File.ReadAllBytes(path);
                _quickSelectedImageName = Path.GetFileName(path);
                UpdateQuickImageStatus(_quickSelectedImageName);
                UpdateQuickWorkbenchStatus($"已选择图片：{_quickSelectedImageName}");
            }
            catch (Exception e)
            {
                UpdateQuickWorkbenchStatus($"读取图片失败：{e.Message}");
            }
#else
            UpdateQuickWorkbenchStatus("当前构建暂未集成系统文件选择器，请先在 Unity Editor 中使用图片出 3D。");
#endif
        }

        private void SetQuickWorkbenchBusy(bool busy, string statusMessage)
        {
            if (_quickPromptInput != null)
            {
                _quickPromptInput.interactable = !busy;
            }

            if (_quickSelectImageButton != null)
            {
                _quickSelectImageButton.interactable = !busy;
            }

            if (_quickTextTo3DButton != null)
            {
                _quickTextTo3DButton.interactable = !busy;
            }

            if (_quickImageTo3DButton != null)
            {
                _quickImageTo3DButton.interactable = !busy;
            }

            if (_quickProfessionalModeButton != null)
            {
                _quickProfessionalModeButton.interactable = !busy;
            }

            UpdateQuickWorkbenchStatus(statusMessage);
        }

        private void UpdateQuickWorkbenchStatus(string message)
        {
            if (_quickWorkbenchStatusText != null)
            {
                _quickWorkbenchStatusText.text = message;
            }
        }

        private void UpdateQuickImageStatus(string message)
        {
            if (_quickImageStatusText != null)
            {
                _quickImageStatusText.text = message;
            }
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target != null && target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }

        private void RefreshDynamicLabels()
        {
            if (_companionNameText != null)
            {
                var dog = FindFirstObjectByType<DogCompanion>();
                if (dog != null && !string.IsNullOrWhiteSpace(dog.DogName) && !string.Equals(dog.DogName, "Buddy", StringComparison.OrdinalIgnoreCase))
                {
                    _companionNameText.text = dog.DogName;
                }
            }
        }

        private void HideLegacyUi()
        {
            var humanChat = FindFirstObjectByType<HumanChatUI>();
            humanChat?.SetToggleButtonVisible(false);

            var modelLibrary = FindFirstObjectByType<ModelLibraryUI>();
            modelLibrary?.SetLauncherVisible(false);

            var friendUi = FindFirstObjectByType<FriendSystemUI>();
            friendUi?.SetToggleButtonVisible(false);

            var nodeEditor = FindFirstObjectByType<SimpleNodeEditor>();
            nodeEditor?.SetLauncherVisible(false);

            var saveCanvas = GameObject.Find("WorldSaveButtonCanvas");
            if (saveCanvas != null)
            {
                saveCanvas.SetActive(false);
            }

            var legacyButton = GameObject.Find("WorkflowStationButton");
            if (legacyButton != null)
            {
                legacyButton.SetActive(false);
            }

            foreach (var text in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text == null)
                {
                    continue;
                }

                string lowerName = text.name.ToLowerInvariant();
                string lowerText = (text.text ?? string.Empty).ToLowerInvariant();
                if (lowerName.Contains("test1") || lowerText.Contains("test1"))
                {
                    text.gameObject.SetActive(false);
                }
            }
        }

        private void ShowToast(string message)
        {
            if (_toastBackground == null || _toastText == null)
            {
                return;
            }

            _toastBackground.gameObject.SetActive(true);
            _toastText.text = message;
            CancelInvoke(nameof(HideToast));
            Invoke(nameof(HideToast), 2.2f);
        }

        private void HideToast()
        {
            if (_toastBackground != null)
            {
                _toastBackground.gameObject.SetActive(false);
            }
        }

        private Button CreateLeftMenuButton(RectTransform parent, string name, string label, string iconText, Action onClick)
        {
            var button = CreateSpriteButton(parent, name, _capsuleSprite, new Vector2(196f, 78f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, label, UiTextColor, new Color(0.42f, 0.62f, 0.80f, 0.78f));
            var buttonImage = button.GetComponent<Image>();
            buttonImage.color = new Color(0.78f, 0.89f, 0.98f, 0.90f);
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 196f;
            layout.preferredHeight = 78f;
            var labelText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (labelText != null)
            {
                labelText.alignment = TextAlignmentOptions.Left;
                labelText.rectTransform.offsetMin = new Vector2(58f, 0f);
                labelText.fontSize = 30f;
            }

            var iconBubble = CreateUiImage(buttonImage.rectTransform, "IconBubble", _bubbleSprite, new Vector2(40f, 40f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), new Color(1f, 1f, 1f, 0.96f));
            iconBubble.color = new Color(0.95f, 0.97f, 1f, 0.98f);
            iconBubble.raycastTarget = false;
            CreateText(iconBubble.rectTransform, "IconText", iconText, 21f, FontStyles.Bold, TextAlignmentOptions.Center, UiTextColor, Vector2.zero, new Vector2(1f, 1f));

            button.onClick.AddListener(() => onClick?.Invoke());
            return button;
        }

        private Button CreateCircleTextButton(RectTransform parent, string name, string text, Vector2 position, float size, Vector2? anchorOverride = null)
        {
            Vector2 anchor = anchorOverride ?? new Vector2(0.5f, 0.5f);
            var button = CreateSpriteButton(parent, name, _bubbleSprite, new Vector2(size, size), anchor, anchor, new Vector2(0.5f, 0.5f), position, text, UiTextColor, new Color(1f, 1f, 1f, 0.96f));
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.fontSize = Mathf.Max(28f, size * 0.42f);
            }

            return button;
        }

        private TMP_InputField CreateInputField(RectTransform parent, string name, Vector2 size, Vector2 position, string placeholder)
        {
            var background = CreateUiImage(parent, name, null, size, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Color(0.94f, 0.97f, 1f, 0.95f));
            background.color = new Color(0.94f, 0.97f, 1f, 0.95f);

            var input = background.gameObject.AddComponent<TMP_InputField>();

            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(background.transform, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(20f, 10f);
            textAreaRect.offsetMax = new Vector2(-20f, -10f);
            textArea.AddComponent<RectMask2D>();

            var text = new GameObject("Text").AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(textArea.transform, false);
            var textRect = text.GetComponent<RectTransform>();
            Stretch(textRect);
            text.font = _font;
            text.fontSize = 24f;
            text.color = UiTextColor;
            text.alignment = TextAlignmentOptions.Left;
            text.enableWordWrapping = false;

            var placeholderText = new GameObject("Placeholder").AddComponent<TextMeshProUGUI>();
            placeholderText.transform.SetParent(textArea.transform, false);
            var placeholderRect = placeholderText.GetComponent<RectTransform>();
            Stretch(placeholderRect);
            placeholderText.font = _font;
            placeholderText.text = placeholder;
            placeholderText.fontSize = 22f;
            placeholderText.color = WithTextAlpha(0.45f);
            placeholderText.alignment = TextAlignmentOptions.Left;
            placeholderText.fontStyle = FontStyles.Italic;

            input.textViewport = textAreaRect;
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.targetGraphic = background;
            return input;
        }

        private Button CreateSpriteButton(RectTransform parent, string name, Sprite sprite, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, string label, Color textColor, Color fallbackColor)
        {
            var image = CreateUiImage(parent, name, sprite, size, anchorMin, anchorMax, pivot, position, fallbackColor);
            var button = image.gameObject.AddComponent<Button>();
            ConfigureButton(button, image.color);

            var textComp = CreateText(image.rectTransform, "Label", label, 26f, FontStyles.Bold, TextAlignmentOptions.Center, textColor, Vector2.zero, new Vector2(1f, 1f));
            textComp.margin = new Vector4(12f, 8f, 12f, 8f);
            return button;
        }

        private Image CreateUiImage(RectTransform parent, string name, Sprite sprite, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Color fallbackColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;

            var image = go.AddComponent<Image>();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
            }
            else
            {
                image.color = fallbackColor;
            }

            image.raycastTarget = true;
            return image;
        }

        private TMP_Text CreateText(RectTransform parent, string name, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2? pivotOverride = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            if (Mathf.Abs(sizeDelta.x) <= 1.01f && Mathf.Abs(sizeDelta.y) <= 1.01f && anchoredPosition == Vector2.zero)
            {
                Stretch(rt);
            }
            else
            {
                Vector2 anchor = pivotOverride ?? new Vector2(0.5f, 0.5f);
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
                rt.pivot = anchor;
                rt.sizeDelta = sizeDelta;
                rt.anchoredPosition = anchoredPosition;
            }

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = _font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void ConfigureButton(Button button, Color normalColor)
        {
            var colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.08f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;
        }

        private static Color WithTextAlpha(float alpha)
        {
            return new Color(UiTextColor.r, UiTextColor.g, UiTextColor.b, alpha);
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
