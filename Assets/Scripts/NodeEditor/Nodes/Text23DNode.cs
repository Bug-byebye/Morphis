using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AIPipeline.Nodes
{
    /// <summary>
    /// 文字转 3D 节点 - 调用后端 API 生成 GLB
    /// </summary>
    public class Text23DNode : PipelineNode
    {
        [Header("API Settings")]
        public string endpoint = "/generate";
        
        public override PortType? InputType => PortType.Text;
        public override PortType? OutputType => PortType.Model3D;
        
        private void Awake()
        {
            nodeName = "文生3D";
            nodeColor = new Color(1f, 0.6f, 0.8f); // 粉色
        }
        
        public override void Execute(Action<object> onComplete, Action<string> onError)
        {
            string prompt = GetInputData<string>();
            if (string.IsNullOrEmpty(prompt))
            {
                onError?.Invoke("No input prompt");
                return;
            }
            
            // 获取 PipelineGraph / AppConfig 来拿 server URL
            var graph = GetComponentInParent<PipelineGraph>();
            string baseUrl = graph != null && !string.IsNullOrEmpty(graph.serverUrl)
                ? graph.serverUrl
                : Morphis.Config.AppConfig.Instance.ApiBaseUrl;
            string url = baseUrl + endpoint;
            
            StartCoroutine(SendRequest(url, prompt, onComplete, onError));
        }
        
        private IEnumerator SendRequest(string url, string prompt, Action<object> onComplete, Action<string> onError)
        {
            string jsonBody = $"{{\"prompt\": \"{EscapeJson(prompt)}\"}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                yield return request.SendWebRequest();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(request.error);
                    yield break;
                }
                
                byte[] glbData = request.downloadHandler.data;
                outputData = glbData;
                onComplete?.Invoke(glbData);
            }
        }
        
        private string EscapeJson(string str)
        {
            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\n", "\\n");
        }
    }
}
