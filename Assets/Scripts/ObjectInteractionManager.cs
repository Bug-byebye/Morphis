using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        FindOrCreateCanvas();
        CreateCommentDialog();
        CreateTooltipPanel();
    }
    
    void FindOrCreateCanvas()
    {
        mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            GameObject canvasObj = new GameObject("InteractionCanvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();
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
        titleText.text = "Add Comment";
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 35);
        titleRect.anchoredPosition = new Vector2(0, -5);
        
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
        placeholder.text = "Enter your comment...";
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
        
        CreateDialogButton(btnsArea.transform, "Save", new Color(0.3f, 0.65f, 0.35f), OnSaveComment);
        CreateDialogButton(btnsArea.transform, "Delete", new Color(0.7f, 0.3f, 0.3f), OnDeleteComment);
        CreateDialogButton(btnsArea.transform, "Cancel", new Color(0.5f, 0.5f, 0.55f), OnCancelComment);
        
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
        
        // Tooltip 文字
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(tooltipPanel.transform, false);
        tooltipText = textObj.AddComponent<TextMeshProUGUI>();
        tooltipText.fontSize = 13;
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAlignmentOptions.TopLeft;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);
        
        tooltipPanel.SetActive(false);
    }
    
    void Update()
    {
        // 处理交互逻辑
        HandleInteraction();
        
        // 更新 tooltip 位置跟随鼠标
        if (tooltipPanel.activeSelf)
        {
            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            RectTransform tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            tooltipRect.position = mousePos + new Vector2(15, -10);
        }
    }
    
    private void HandleInteraction()
    {
        // 如果对话框打开，不处理交互
        if (commentDialog.activeSelf) return;
        
        // 获取鼠标位置 (兼容新输入系统)
        Vector2 mousePos = Vector2.zero;
        bool leftClick = false;
        
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            leftClick = UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
        }
        else
        {
            mousePos = Input.mousePosition;
            leftClick = Input.GetMouseButtonDown(0);
        }
        
        // 发射射线
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            InteractableObject obj = hit.collider.GetComponent<InteractableObject>();
            if (obj != null)
            {
                // 处理悬浮
                if (hoveredObject != obj)
                {
                    if (hoveredObject != null) OnObjectHoverExit(hoveredObject);
                    InteractableObject newHover = obj;
                    OnObjectHoverEnter(newHover);
                }
                
                // 处理点击
                if (leftClick)
                {
                    OnObjectClicked(obj);
                }
                return;
            }
        }
        
        // 如果没有击中当前悬浮物体，或者击中其他非交互物体
        if (hoveredObject != null)
        {
            OnObjectHoverExit(hoveredObject);
        }
    }
    
    // ========== 事件处理 ==========
    
    public void OnObjectClicked(InteractableObject obj)
    {
        currentTarget = obj;
        commentInput.text = obj.comment;
        commentDialog.SetActive(true);
        
        // 隐藏 tooltip
        tooltipPanel.SetActive(false);
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
        }
        CloseDialog();
    }
    
    void OnDeleteComment()
    {
        if (currentTarget != null)
        {
            currentTarget.ClearComment();
            Debug.Log("[Interaction] Comment deleted");
        }
        CloseDialog();
    }
    
    void OnCancelComment()
    {
        CloseDialog();
    }
    
    void CloseDialog()
    {
        commentDialog.SetActive(false);
        commentInput.text = "";
        currentTarget = null;
    }
}
