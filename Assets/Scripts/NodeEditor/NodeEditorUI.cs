using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using AIPipeline.Nodes;

namespace AIPipeline
{
    /// <summary>
    /// 运行时节点编辑器 UI
    /// 支持创建、连接和执行节点
    /// </summary>
    public class NodeEditorUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject editorPanel;
        [SerializeField] private TMP_InputField promptInput;
        [SerializeField] private Button executeButton;
        [SerializeField] private Button createPipelineButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Dropdown nodeTypeDropdown;
        
        [Header("Settings")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        
        private PipelineGraph currentGraph;
        private bool isVisible = false;
        
        // 鼠标状态备份
        private CursorLockMode savedLockMode;
        private bool savedCursorVisible;
        
        // 玩家输入组件
        private PlayerInput playerInput;
        
        void Start()
        {
            if (editorPanel != null)
                editorPanel.SetActive(false);
                
            if (executeButton != null)
                executeButton.onClick.AddListener(OnExecuteClicked);
                
            if (createPipelineButton != null)
                createPipelineButton.onClick.AddListener(OnCreatePipelineClicked);
            
            // 查找 PlayerInput 组件
            playerInput = FindObjectOfType<PlayerInput>();
                
            SetupDropdown();
            UpdateStatus("Press Tab to open Node Editor");
        }
        
        void Update()
        {
            // Tab 键切换编辑器
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleEditor();
            }
        }
        
        public void ToggleEditor()
        {
            isVisible = !isVisible;
            
            if (isVisible)
            {
                // 保存并解锁光标
                savedLockMode = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                
                // 禁用玩家输入
                if (playerInput != null)
                    playerInput.enabled = false;
            }
            else
            {
                // 恢复光标状态
                Cursor.lockState = savedLockMode;
                Cursor.visible = savedCursorVisible;
                
                // 恢复玩家输入
                if (playerInput != null)
                    playerInput.enabled = true;
            }
            
            if (editorPanel != null)
                editorPanel.SetActive(isVisible);
        }
        
        private void SetupDropdown()
        {
            if (nodeTypeDropdown == null) return;
            
            nodeTypeDropdown.ClearOptions();
            nodeTypeDropdown.AddOptions(new List<string>
            {
                "Text Input",
                "Text to 3D",
                "3D Preview"
            });
        }
        
        private void OnCreatePipelineClicked()
        {
            // 创建新的 Pipeline Graph
            if (currentGraph != null)
            {
                Destroy(currentGraph.gameObject);
            }
            
            GameObject graphObj = new GameObject("PipelineGraph");
            currentGraph = graphObj.AddComponent<PipelineGraph>();
            currentGraph.pipelineName = "My Pipeline";
            
            // 自动创建标准管线: TextInput -> Text23D -> Preview3D
            var textNode = currentGraph.AddNode<TextInputNode>(new Vector2(50, 100));
            var t23dNode = currentGraph.AddNode<Text23DNode>(new Vector2(300, 100));
            var previewNode = currentGraph.AddNode<Preview3DNode>(new Vector2(550, 100));
            
            // 连接节点
            currentGraph.Connect(textNode, t23dNode);
            currentGraph.Connect(t23dNode, previewNode);
            
            UpdateStatus("Pipeline created: TextInput -> Text23D -> Preview3D");
        }
        
        private void OnExecuteClicked()
        {
            if (currentGraph == null)
            {
                UpdateStatus("No pipeline! Click 'Create Pipeline' first.");
                return;
            }
            
            // 设置 Prompt
            string prompt = promptInput != null ? promptInput.text : "";
            if (string.IsNullOrWhiteSpace(prompt))
            {
                UpdateStatus("Please enter a prompt");
                return;
            }
            
            // 找到 TextInputNode 并设置 prompt
            var textNode = currentGraph.GetComponentInChildren<TextInputNode>();
            if (textNode != null)
            {
                textNode.promptText = prompt;
            }
            
            UpdateStatus("Executing pipeline...");
            
            currentGraph.Execute(
                () => UpdateStatus("Pipeline complete! Model generated."),
                error => UpdateStatus($"Error: {error}")
            );
        }
        
        private void UpdateStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
            Debug.Log($"[NodeEditor] {message}");
        }
    }
}
