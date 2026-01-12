using System;
using UnityEngine;

namespace AIPipeline.Nodes
{
    /// <summary>
    /// 文本输入节点 - 用户输入 Prompt
    /// </summary>
    public class TextInputNode : PipelineNode
    {
        [Header("Text Input")]
        [TextArea(3, 5)]
        public string promptText = "";
        
        public override PortType? InputType => null; // 没有输入
        public override PortType? OutputType => PortType.Text;
        
        private void Awake()
        {
            nodeName = "Text Input";
            nodeColor = new Color(0.6f, 0.8f, 1f); // 浅蓝色
        }
        
        public override void Execute(Action<object> onComplete, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(promptText))
            {
                onError?.Invoke("Prompt is empty");
                return;
            }
            
            outputData = promptText;
            onComplete?.Invoke(promptText);
        }
    }
}
