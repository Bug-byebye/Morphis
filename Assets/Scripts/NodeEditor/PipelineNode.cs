using System;
using UnityEngine;

namespace AIPipeline
{
    /// <summary>
    /// 节点端口数据类型
    /// </summary>
    public enum PortType
    {
        Text,
        Image,
        Model3D
    }

    /// <summary>
    /// 节点执行状态
    /// </summary>
    public enum NodeState
    {
        Idle,
        Running,
        Complete,
        Error
    }

    /// <summary>
    /// 管线节点基类
    /// </summary>
    [Serializable]
    public abstract class PipelineNode : MonoBehaviour
    {
        [Header("Node Info")]
        public string nodeName = "Node";
        public NodeState state = NodeState.Idle;
        
        [Header("Visual")]
        public Color nodeColor = new Color(1f, 0.71f, 0.76f); // 浅粉红
        public Rect nodeRect = new Rect(100, 100, 200, 150);
        
        // 输入输出连接
        [HideInInspector] public PipelineNode inputNode;
        [HideInInspector] public object outputData;
        
        /// <summary>
        /// 获取输入数据类型
        /// </summary>
        public abstract PortType? InputType { get; }
        
        /// <summary>
        /// 获取输出数据类型
        /// </summary>
        public abstract PortType? OutputType { get; }
        
        /// <summary>
        /// 执行节点（异步）
        /// </summary>
        public abstract void Execute(Action<object> onComplete, Action<string> onError);
        
        /// <summary>
        /// 获取输入数据（从连接的节点）
        /// </summary>
        protected T GetInputData<T>()
        {
            if (inputNode == null || inputNode.outputData == null)
                return default;
            
            return (T)inputNode.outputData;
        }
    }
}
