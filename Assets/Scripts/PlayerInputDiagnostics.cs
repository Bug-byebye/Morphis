using UnityEngine;
using Mirror;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 玩家输入诊断和修复工具
/// 挂载到Player对象上，用于检测和修复输入问题
/// </summary>
public class PlayerInputDiagnostics : MonoBehaviour
{
    [Header("诊断信息")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool autoFix = true;
    
    private CharacterController characterController;
    private NetworkIdentity networkIdentity;
#if ENABLE_INPUT_SYSTEM
    private PlayerInput playerInput;
#endif
    private MonoBehaviour thirdPersonController;
    private MonoBehaviour starterInputs;
    
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        networkIdentity = GetComponent<NetworkIdentity>();
#if ENABLE_INPUT_SYSTEM
        playerInput = GetComponent<PlayerInput>();
#endif
        
        var tpcType = System.Type.GetType("StarterAssets.ThirdPersonController, Assembly-CSharp");
        if (tpcType != null) thirdPersonController = GetComponent(tpcType) as MonoBehaviour;
        
        var inputsType = System.Type.GetType("StarterAssets.StarterAssetsInputs, Assembly-CSharp");
        if (inputsType != null) starterInputs = GetComponent(inputsType) as MonoBehaviour;
    }
    
    private void Start()
    {
        if (autoFix && ShouldDiagnoseLocalPlayer())
        {
            Invoke(nameof(DiagnoseAndFix), 0.5f); // 延迟0.5秒确保所有组件都初始化完成
        }
    }
    
    private void Update()
    {
        if (!ShouldDiagnoseLocalPlayer())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            DiagnoseAndFix();
        }
        
        if (showDebugInfo && Input.GetKeyDown(KeyCode.F2))
        {
            LogCurrentState();
        }
    }
    
    [ContextMenu("Diagnose and Fix Input Issues")]
    public void DiagnoseAndFix()
    {
        if (!ShouldDiagnoseLocalPlayer())
        {
            return;
        }

        Debug.Log("========== 玩家输入诊断开始 ==========");
        
        bool hasIssues = false;
        
        // 1. 检查CharacterController
        if (characterController == null)
        {
            Debug.LogError("❌ CharacterController 缺失!");
            hasIssues = true;
        }
        else if (!characterController.enabled)
        {
            Debug.LogWarning("⚠️ CharacterController 被禁用，正在启用...");
            characterController.enabled = true;
            hasIssues = true;
        }
        else
        {
            Debug.Log("✅ CharacterController 正常");
        }
        
        // 2. 检查ThirdPersonController
        if (thirdPersonController == null)
        {
            Debug.LogWarning("⚠️ ThirdPersonController 不存在（将使用备用控制）");
        }
        else if (!thirdPersonController.enabled)
        {
            Debug.LogWarning("⚠️ ThirdPersonController 被禁用，正在启用...");
            thirdPersonController.enabled = true;
            hasIssues = true;
        }
        else
        {
            Debug.Log("✅ ThirdPersonController 正常");
        }
        
        // 3. 检查StarterAssetsInputs
        if (starterInputs == null)
        {
            Debug.LogWarning("⚠️ StarterAssetsInputs 不存在");
        }
        else if (!starterInputs.enabled)
        {
            Debug.LogWarning("⚠️ StarterAssetsInputs 被禁用，正在启用...");
            starterInputs.enabled = true;
            hasIssues = true;
        }
        else
        {
            Debug.Log("✅ StarterAssetsInputs 正常");
        }
        
#if ENABLE_INPUT_SYSTEM
        // 4. 检查PlayerInput
        if (playerInput == null)
        {
            Debug.LogError("❌ PlayerInput 组件缺失!");
            hasIssues = true;
        }
        else
        {
            if (!playerInput.enabled)
            {
                Debug.LogWarning("⚠️ PlayerInput 被禁用，正在启用...");
                playerInput.enabled = true;
                hasIssues = true;
            }
            
            if (playerInput.currentActionMap == null)
            {
                Debug.LogError("❌ PlayerInput 没有激活的 ActionMap!");
                hasIssues = true;
            }
            else
            {
                Debug.Log($"✅ PlayerInput ActionMap: {playerInput.currentActionMap.name}");
                
                // 检查Move action是否启用
                var moveAction = playerInput.currentActionMap.FindAction("Move");
                if (moveAction != null)
                {
                    if (!moveAction.enabled)
                    {
                        Debug.LogWarning("⚠️ Move action 被禁用，正在启用...");
                        moveAction.Enable();
                        hasIssues = true;
                    }
                    else
                    {
                        Debug.Log("✅ Move action 已启用");
                    }
                }
                else
                {
                    Debug.LogError("❌ 找不到 Move action!");
                    hasIssues = true;
                }
            }
            
            // 强制刷新PlayerInput
            playerInput.enabled = false;
            playerInput.enabled = true;
            Debug.Log("🔄 PlayerInput 已刷新");
        }
#else
        Debug.Log("ℹ️ 使用旧Input系统（非InputSystem Package）");
#endif
        
        if (!hasIssues)
        {
            Debug.Log("========== ✅ 所有组件正常 ==========");
        }
        else
        {
            Debug.Log("========== ⚠️ 已尝试修复问题 ==========");
            Debug.Log("如果仍然无法移动，请按F2查看当前状态详情");
        }
    }
    
    [ContextMenu("Log Current State")]
    public void LogCurrentState()
    {
        if (!ShouldDiagnoseLocalPlayer())
        {
            return;
        }

        Debug.Log("========== 当前玩家状态 ==========");
        Debug.Log($"Position: {transform.position}");
        Debug.Log($"Rotation: {transform.rotation.eulerAngles}");
        
        if (characterController != null)
        {
            Debug.Log($"CharacterController.enabled: {characterController.enabled}");
            Debug.Log($"CharacterController.isGrounded: {characterController.isGrounded}");
            Debug.Log($"CharacterController.velocity: {characterController.velocity}");
        }
        
        if (thirdPersonController != null)
        {
            Debug.Log($"ThirdPersonController.enabled: {thirdPersonController.enabled}");
        }
        
        if (starterInputs != null)
        {
            Debug.Log($"StarterAssetsInputs.enabled: {starterInputs.enabled}");
            
            var inputsType = starterInputs.GetType();
            var moveField = inputsType.GetField("move");
            if (moveField != null)
            {
                var moveValue = moveField.GetValue(starterInputs);
                Debug.Log($"StarterAssetsInputs.move: {moveValue}");
            }
        }
        
#if ENABLE_INPUT_SYSTEM
        if (playerInput != null)
        {
            Debug.Log($"PlayerInput.enabled: {playerInput.enabled}");
            Debug.Log($"PlayerInput.currentActionMap: {playerInput.currentActionMap?.name}");
            Debug.Log($"PlayerInput.currentControlScheme: {playerInput.currentControlScheme}");
            
            // 测试读取当前输入
            var moveAction = playerInput.currentActionMap?.FindAction("Move");
            if (moveAction != null)
            {
                Debug.Log($"Move action.enabled: {moveAction.enabled}");
                Debug.Log($"Move action.ReadValue: {moveAction.ReadValue<Vector2>()}");
            }
        }
#endif
        
        Debug.Log($"Input.GetAxis Horizontal: {Input.GetAxis("Horizontal")}");
        Debug.Log($"Input.GetAxis Vertical: {Input.GetAxis("Vertical")}");
        
        Debug.Log("=====================================");
    }

    private bool ShouldDiagnoseLocalPlayer()
    {
        if (Application.isBatchMode)
        {
            return false;
        }

        return networkIdentity == null || networkIdentity.isLocalPlayer;
    }
}
