using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIPipeline
{
    /// <summary>
    /// 管线图（包含所有节点和连接）
    /// </summary>
    public class PipelineGraph : MonoBehaviour
    {
        [Header("Pipeline Settings")]
        public string pipelineName = "New Pipeline";
        public string serverUrl = "";
        
        [Header("Nodes")]
        public List<PipelineNode> nodes = new List<PipelineNode>();
        
        [Header("Connections")]
        public List<NodeConnection> connections = new List<NodeConnection>();
        
        /// <summary>
        /// 添加节点
        /// </summary>
        public T AddNode<T>(Vector2 position) where T : PipelineNode
        {
            GameObject nodeObj = new GameObject(typeof(T).Name);
            nodeObj.transform.SetParent(transform);
            
            T node = nodeObj.AddComponent<T>();
            node.nodeRect.position = position;
            nodes.Add(node);
            
            return node;
        }
        
        /// <summary>
        /// 连接两个节点
        /// </summary>
        public bool Connect(PipelineNode from, PipelineNode to)
        {
            // 检查类型兼容性
            if (from.OutputType == null || to.InputType == null)
                return false;
            
            if (from.OutputType != to.InputType)
            {
                Debug.LogWarning($"Type mismatch: {from.OutputType} -> {to.InputType}");
                return false;
            }
            
            to.inputNode = from;
            connections.Add(new NodeConnection { fromNode = from, toNode = to });
            return true;
        }
        
        /// <summary>
        /// 执行整个管线
        /// </summary>
        public void Execute(Action onComplete, Action<string> onError)
        {
            // 找到没有输入的起始节点
            var startNodes = nodes.FindAll(n => n.InputType == null || n.inputNode == null);
            
            if (startNodes.Count == 0)
            {
                onError?.Invoke("No start nodes found");
                return;
            }
            
            ExecuteChain(startNodes[0], onComplete, onError);
        }
        
        private void ExecuteChain(PipelineNode node, Action onComplete, Action<string> onError)
        {
            node.state = NodeState.Running;
            
            node.Execute(
                result =>
                {
                    node.outputData = result;
                    node.state = NodeState.Complete;
                    
                    // 找到下一个节点
                    var nextConnection = connections.Find(c => c.fromNode == node);
                    if (nextConnection != null)
                    {
                        ExecuteChain(nextConnection.toNode, onComplete, onError);
                    }
                    else
                    {
                        onComplete?.Invoke();
                    }
                },
                error =>
                {
                    node.state = NodeState.Error;
                    onError?.Invoke($"[{node.nodeName}] {error}");
                }
            );
        }
    }
    
    [Serializable]
    public class NodeConnection
    {
        public PipelineNode fromNode;
        public PipelineNode toNode;
    }
}
