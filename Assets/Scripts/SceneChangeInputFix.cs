using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 场景切换后自动修复玩家输入
/// 添加到Player预制体，解决切换场景后无法移动的问题
/// </summary>
public class SceneChangeInputFix : MonoBehaviour
{
#if ENABLE_INPUT_SYSTEM
    private PlayerInput playerInput;
#endif
    private MonoBehaviour thirdPersonController;
    private MonoBehaviour starterInputs;
    private CharacterController characterController;
    private NetworkIdentity networkIdentity;
    
    private void Awake()
    {
#if ENABLE_INPUT_SYSTEM
        playerInput = GetComponent<PlayerInput>();
#endif
        characterController = GetComponent<CharacterController>();
        networkIdentity = GetComponent<NetworkIdentity>();
        
        var tpcType = System.Type.GetType("StarterAssets.ThirdPersonController, Assembly-CSharp");
        if (tpcType != null) thirdPersonController = GetComponent(tpcType) as MonoBehaviour;
        
        var inputsType = System.Type.GetType("StarterAssets.StarterAssetsInputs, Assembly-CSharp");
        if (inputsType != null) starterInputs = GetComponent(inputsType) as MonoBehaviour;
        
        // 注册场景加载事件
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!ShouldFixLocalPlayer())
        {
            return;
        }

        Debug.Log($"[SceneChangeInputFix] 场景加载: {scene.name}，开始修复输入...");
        
        // 延迟修复，确保所有组件都初始化完成
        Invoke(nameof(FixInputAfterSceneLoad), 0.2f);
    }
    
    private void FixInputAfterSceneLoad()
    {
        if (!ShouldFixLocalPlayer())
        {
            return;
        }

        Debug.Log("[SceneChangeInputFix] 执行修复...");
        
        // 1. 重新启用CharacterController
        if (characterController != null && !characterController.enabled)
        {
            characterController.enabled = true;
            Debug.Log("[SceneChangeInputFix] CharacterController 已启用");
        }
        
        // 2. 重新启用ThirdPersonController
        if (thirdPersonController != null && !thirdPersonController.enabled)
        {
            thirdPersonController.enabled = true;
            Debug.Log("[SceneChangeInputFix] ThirdPersonController 已启用");
        }
        
        // 3. 重新启用StarterAssetsInputs
        if (starterInputs != null && !starterInputs.enabled)
        {
            starterInputs.enabled = true;
            Debug.Log("[SceneChangeInputFix] StarterAssetsInputs 已启用");
        }
        
#if ENABLE_INPUT_SYSTEM
        // 4. 刷新PlayerInput - 这是关键！
        if (playerInput != null)
        {
            // 先禁用再启用，强制刷新
            playerInput.enabled = false;
            playerInput.enabled = true;
            
            // 确保ActionMap激活
            if (playerInput.currentActionMap != null)
            {
                playerInput.currentActionMap.Enable();
                Debug.Log($"[SceneChangeInputFix] PlayerInput ActionMap '{playerInput.currentActionMap.name}' 已激活");
            }
            else
            {
                Debug.LogWarning("[SceneChangeInputFix] PlayerInput 没有 currentActionMap!");
            }
        }
#endif
        
        Debug.Log("[SceneChangeInputFix] ✅ 修复完成！");
    }
    
    // 手动触发修复（按F3键）
    private void Update()
    {
        if (!ShouldFixLocalPlayer())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            Debug.Log("[SceneChangeInputFix] 手动触发修复（按F3）");
            FixInputAfterSceneLoad();
        }
    }

    private bool ShouldFixLocalPlayer()
    {
        if (Application.isBatchMode)
        {
            return false;
        }

        return networkIdentity == null || networkIdentity.isLocalPlayer;
    }
}
