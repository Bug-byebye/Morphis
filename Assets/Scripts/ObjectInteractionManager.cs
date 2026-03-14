using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Morphis.InputControl;
using Morphis.AppFlow;
using Morphis.WorldSnapshot;

/// <summary>
/// 物体交互管理器 - 处理留言输入对话框和悬浮提示
/// </summary>
public class ObjectInteractionManager : MonoBehaviour
{
    public static ObjectInteractionManager Instance { get; private set; }
    
    [Header("UI 引用")]
    private Canvas mainCanvas;
    private GameObject commentDialog;
    private GameObject tooltipPanel;
    private TMP_InputField commentInput;
    private TextMeshProUGUI tooltipText;
    
    // 当前交互的对象
    private InteractableObject currentTarget;
    private InteractableObject hoveredObject;
    private InteractableObject draggingObject;
    private Vector3 dragOffset;
    private float dragGroundY = 0f;
    
    void Awake()
    {
        Debug.Log($"[Interaction] Manager Awake. InstanceID: {GetInstanceID()}");
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"[Interaction] Duplicate Manager detected! Destroying new instance: {GetInstanceID()}");
            Destroy(gameObject);
            return;
        }
    }

    /// <summary> 清空当前引用，避免访问已被销毁的物体（如 WorldSnapshot 加载时清空世界后） </summary>
    public static void ClearTargetsIfExists()
    {
        if (Instance == null) return;
        Instance.currentTarget = null;
        Instance.hoveredObject = null;
        Instance.draggingObject = null;
        if (Instance.commentDialog != null && Instance.commentDialog)
            Instance.commentDialog.SetActive(false);
        GameplayInputBlocker.SetBlocked(Instance, false);
        if (Instance.tooltipPanel != null && Instance.tooltipPanel)
            Instance.tooltipPanel.SetActive(false);
    }
    
    void Start()
    {
        // Prevent double initialization if OnObjectClicked triggered creation already
        if (commentDialog != null && tooltipPanel != null)
        {
            Debug.Log("[Interaction] UI already initialized, skipping Start creation.");
            return;
        }

        FindOrCreateCanvas();
        CreateCommentDialog();
        CreateTooltipPanel();
    }
    
        void FindOrCreateCanvas()
        {
            // Always create/use a dedicated canvas for Interactions to ensure it's on top
            // Do not reuse random canvases from the scene (like BootFlow or ContextMenu)
            GameObject canvasObj = GameObject.Find("InteractionCanvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("InteractionCanvas");
                mainCanvas = canvasObj.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                mainCanvas.sortingOrder = 200; // High priority (Topmost)
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            else
            {
                mainCanvas = canvasObj.GetComponent<Canvas>();
                if (mainCanvas == null) mainCanvas = canvasObj.AddComponent<Canvas>();
                
                // Enforce proper settings
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                mainCanvas.sortingOrder = 200;
                if (canvasObj.GetComponent<GraphicRaycaster>() == null)
                    canvasObj.AddComponent<GraphicRaycaster>();
            }
        }
    
    void CreateCommentDialog()
    {
        // 创建对话框容器
        commentDialog = new GameObject("CommentDialog");
        commentDialog.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform dialogRect = commentDialog.AddComponent<RectTransform>();
        dialogRect.sizeDelta = new Vector2(400, 200);
        dialogRect.anchoredPosition = Vector2.zero;
        
        // 背景
        Image bg = commentDialog.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        
        // 标题
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(commentDialog.transform, false);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "添加评论";
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.raycastTarget = false;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 35);
        titleRect.anchoredPosition = new Vector2(0, -5);

        // Close 'X' Button
        GameObject closeBtnObj = new GameObject("CloseBtn");
        closeBtnObj.transform.SetParent(commentDialog.transform, false);
        RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.pivot = new Vector2(1, 1);
        closeRect.sizeDelta = new Vector2(30, 30);
        closeRect.anchoredPosition = new Vector2(-5, -5);
        
        Image closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(OnCancelComment);

        GameObject xTextObj = new GameObject("X");
        xTextObj.transform.SetParent(closeBtnObj.transform, false);
        var xText = xTextObj.AddComponent<TextMeshProUGUI>();
        xText.text = "X";
        xText.fontSize = 16;
        xText.alignment = TextAlignmentOptions.Center;
        xText.color = Color.white;
        xText.raycastTarget = false;
        RectTransform xRect = xTextObj.GetComponent<RectTransform>();
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.offsetMin = Vector2.zero;
        xRect.offsetMax = Vector2.zero;
        
        // 输入框区域
        GameObject inputArea = new GameObject("InputArea");
        inputArea.transform.SetParent(commentDialog.transform, false);
        RectTransform inputRect = inputArea.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.05f, 0.35f);
        inputRect.anchorMax = new Vector2(0.95f, 0.75f);
        inputRect.offsetMin = Vector2.zero;
        inputRect.offsetMax = Vector2.zero;
        
        Image inputBg = inputArea.AddComponent<Image>();
        inputBg.color = new Color(0.2f, 0.2f, 0.22f);
        
        commentInput = inputArea.AddComponent<TMP_InputField>();
        commentInput.lineType = TMP_InputField.LineType.MultiLineNewline;
        
        // 输入框文字
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(inputArea.transform, false);
        RectTransform textRect = textArea.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 5);
        textRect.offsetMax = new Vector2(-10, -5);
        textArea.AddComponent<RectMask2D>();
        
        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(textArea.transform, false);
        var inputText = inputTextObj.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 14;
        inputText.color = Color.white;
        RectTransform itRect = inputTextObj.GetComponent<RectTransform>();
        itRect.anchorMin = Vector2.zero;
        itRect.anchorMax = Vector2.one;
        itRect.offsetMin = Vector2.zero;
        itRect.offsetMax = Vector2.zero;
        
        commentInput.textViewport = textRect;
        commentInput.textComponent = inputText;
        
        // 占位符
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(textArea.transform, false);
        var placeholder = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholder.text = "输入你的评论...";
        placeholder.fontSize = 14;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(0.5f, 0.5f, 0.5f);
        RectTransform phRect = placeholderObj.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;
        commentInput.placeholder = placeholder;
        
        // 按钮区域
        GameObject btnsArea = new GameObject("Buttons");
        btnsArea.transform.SetParent(commentDialog.transform, false);
        RectTransform btnsRect = btnsArea.AddComponent<RectTransform>();
        btnsRect.anchorMin = new Vector2(0, 0);
        btnsRect.anchorMax = new Vector2(1, 0.3f);
        btnsRect.offsetMin = new Vector2(10, 10);
        btnsRect.offsetMax = new Vector2(-10, -5);
        
        HorizontalLayoutGroup hlg = btnsArea.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childForceExpandWidth = true;
        
        CreateDialogButton(btnsArea.transform, "保存", new Color(0.3f, 0.65f, 0.35f), OnSaveComment);
        CreateDialogButton(btnsArea.transform, "删除", new Color(0.7f, 0.3f, 0.3f), OnDeleteComment);
        CreateDialogButton(btnsArea.transform, "取消", new Color(0.5f, 0.5f, 0.55f), OnCancelComment);
        
        commentDialog.SetActive(false);
    }
    
    void CreateDialogButton(Transform parent, string text, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(text + "Btn");
        btnObj.transform.SetParent(parent, false);
        
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = color;
        
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(onClick);
        
        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.preferredHeight = 35;
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = text;
        btnText.fontSize = 14;
        btnText.fontStyle = FontStyles.Bold;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
        btnText.raycastTarget = false; // Important: Don't block button clicks
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
    }
    
    void CreateTooltipPanel()
    {
        tooltipPanel = new GameObject("TooltipPanel");
        tooltipPanel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform tooltipRect = tooltipPanel.AddComponent<RectTransform>();
        tooltipRect.sizeDelta = new Vector2(250, 80);
        tooltipRect.pivot = new Vector2(0, 1);
        
        Image bg = tooltipPanel.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.18f, 0.92f);
        bg.raycastTarget = false; // Important: Don't block clicks
        
        // Tooltip 文字
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(tooltipPanel.transform, false);
        tooltipText = textObj.AddComponent<TextMeshProUGUI>();
        tooltipText.fontSize = 13;
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAlignmentOptions.TopLeft;
        tooltipText.raycastTarget = false; // Important: Don't block clicks
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);
        
        tooltipPanel.SetActive(false);
    }
    
    void Update()
    {
        // 若引用已被销毁，清空（避免 MissingReferenceException）
        if (currentTarget != null && !currentTarget) currentTarget = null;
        if (hoveredObject != null && !hoveredObject) hoveredObject = null;
        if (draggingObject != null && !draggingObject) draggingObject = null;
        // Add ESC key support to close dialog
        if (commentDialog != null && commentDialog && commentDialog.activeSelf)
        {
            // Check both Legacy and New Input System
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("[Interaction] ESC pressed (Legacy)");
                OnCancelComment();
            }
            else if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Debug.Log("[Interaction] ESC pressed (InputSystem)");
                OnCancelComment();
            }
        }

        // 处理交互逻辑（点击 / 悬浮 / 拖拽）
        HandleInteraction();
        
        // 更新 tooltip 位置跟随鼠标
        if (tooltipPanel != null && tooltipPanel && tooltipPanel.activeSelf)
        {
            Vector2 mousePos = Vector2.zero;
            if (UnityEngine.InputSystem.Mouse.current != null)
                mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            else
                mousePos = Input.mousePosition;

            RectTransform tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            tooltipRect.position = mousePos + new Vector2(15, -10);
        }
    }
    
    private void HandleInteraction()
    {
        if (commentDialog == null || !commentDialog) return;
        // 如果对话框打开，不处理交互
        if (commentDialog.activeSelf) return;
        
        // 获取鼠标位置 (兼容新输入系统)
        Vector2 mousePos = Vector2.zero;
        bool rightClickDown = false;
        
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            rightClickDown = UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame; 
        }
        else
        {
            mousePos = Input.mousePosition;
            rightClickDown = Input.GetMouseButtonDown(1);
        }

        // 发射射线
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            // 使用 GetComponentInParent，保证点击子物体 Collider 也能找到根节点上的 InteractableObject
            InteractableObject obj = hit.collider.GetComponentInParent<InteractableObject>();
            if (obj != null)
            {
                // 处理悬浮
                if (hoveredObject != obj)
                {
                    if (hoveredObject != null) OnObjectHoverExit(hoveredObject);
                    InteractableObject newHover = obj;
                    OnObjectHoverEnter(newHover);
                }
                
                // Note: Click interaction (Left Click) is now handled by PlaceableObjectMover 
                // to show the Context Menu (Move vs Message).
                // We keep this Manager focused on Dialogs and Tooltips.
                
                // 右键：打开留言对话框
                if (rightClickDown)
                {
                    OnObjectClicked(obj);
                }
                return;
            }
        }
        
        // 如果没有打中任何物体
        if (hoveredObject != null)
        {
            OnObjectHoverExit(hoveredObject);
        }
    }
    
    // ========== 事件处理 ==========
    
    public void OnObjectClicked(InteractableObject obj)
    {
        if (commentInput == null)
        {
            FindOrCreateCanvas();
            CreateCommentDialog();
            CreateTooltipPanel();
        }

        currentTarget = obj;
        commentInput.text = obj.comment;
        commentDialog.SetActive(true);
        GameplayInputBlocker.SetBlocked(this, true);
        
        // 隐藏 tooltip
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }
    
    public void OnObjectHoverEnter(InteractableObject obj)
    {
        hoveredObject = obj;
        if (obj.HasComment && !commentDialog.activeSelf)
        {
            tooltipText.text = obj.comment;
            tooltipPanel.SetActive(true);
        }
    }
    
    public void OnObjectHoverExit(InteractableObject obj)
    {
        hoveredObject = null;
        tooltipPanel.SetActive(false);
    }
    
    // ========== 对话框按钮 ==========
    
    void OnSaveComment()
    {
        if (currentTarget != null)
        {
            currentTarget.SetComment(commentInput.text);
            Debug.Log($"[Interaction] Comment saved: {commentInput.text}");

            // 联机模式下：先通知服务器更新权威 comment，再根据需要触发保存
            if (Mirror.NetworkClient.active && StarterAssets.NetworkPlayerSetup.Local != null)
            {
                var worldObj = currentTarget.GetComponent<Morphis.WorldSnapshot.WorldObject>();
                if (worldObj != null)
                {
                    bool ok = StarterAssets.NetworkPlayerSetup.Local.RequestSetComment(worldObj.ObjectId, commentInput.text);
                    if (!ok)
                    {
                        Debug.LogWarning("[Interaction] RequestSetComment failed, falling back to direct autosave.");
                        RequestServerAutosave();
                    }
                }
                else
                {
                    RequestServerAutosave();
                }
            }
            else
            {
                RequestServerAutosave();
            }
        }
        CloseDialog();
    }
    
    void OnDeleteComment()
    {
        if (currentTarget != null)
        {
            currentTarget.ClearComment();
            Debug.Log("[Interaction] Comment deleted");

            if (Mirror.NetworkClient.active && StarterAssets.NetworkPlayerSetup.Local != null)
            {
                var worldObj = currentTarget.GetComponent<Morphis.WorldSnapshot.WorldObject>();
                if (worldObj != null)
                {
                    bool ok = StarterAssets.NetworkPlayerSetup.Local.RequestSetComment(worldObj.ObjectId, "");
                    if (!ok)
                    {
                        Debug.LogWarning("[Interaction] RequestSetComment (delete) failed, falling back to direct autosave.");
                        RequestServerAutosave();
                    }
                }
                else
                {
                    RequestServerAutosave();
                }
            }
            else
            {
                RequestServerAutosave();
            }
        }
        CloseDialog();
    }
    
    void OnCancelComment()
    {
        CloseDialog();
    }
    
    void CloseDialog()
    {
        Debug.Log($"[Interaction] Closing Dialog. Manager Instance: {GetInstanceID()}");
        commentDialog.SetActive(false);
        GameplayInputBlocker.SetBlocked(this, false);
        commentInput.text = "";
        currentTarget = null;
    }

    private void OnDisable()
    {
        GameplayInputBlocker.SetBlocked(this, false);
    }

    private void OnDestroy()
    {
        GameplayInputBlocker.SetBlocked(this, false);
    }

    private static void RequestServerAutosave()
    {
        if (!AppSession.IsLoggedIn) return;
        if (WorldSnapshotManager.Instance == null) return;
        WorldSnapshotManager.Instance.SaveWorldServer(
            onError: err => Debug.LogWarning($"[Interaction] Autosave failed after comment edit: {err}")
        );
    }
}
