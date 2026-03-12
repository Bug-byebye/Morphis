using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AIPipeline.Nodes
{
    /// <summary>
    /// 文字转图片节点 - 调用后端 Text2Image API
    /// </summary>
    public class Text2ImageNode : PipelineNode
    {
        [Header("API Settings")]
        public string endpoint = "/text2image/urls";
        
        [Header("Image Settings")]
        public int width = 1024;
        public int height = 1024;
        
        public override PortType? InputType => PortType.Text;
        public override PortType? OutputType => PortType.Image;
        
        private void Awake()
        {
            nodeName = "文生图";
            nodeColor = new Color(1f, 0.85f, 0.4f); // 橙黄色
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
            // 构建请求 JSON
            string jsonBody = $"{{\"prompt\": \"{EscapeJson(prompt)}\", \"width\": {width}, \"height\": {height}}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            
            Debug.Log($"[Text2Image] Sending request to {url}");
            Debug.Log($"[Text2Image] Prompt: {prompt}");
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                yield return request.SendWebRequest();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Request failed: {request.error}");
                    yield break;
                }
                
                // 解析 JSON 响应
                string responseText = request.downloadHandler.text;
                Debug.Log($"[Text2Image] Response: {responseText}");
                
                var response = JsonUtility.FromJson<Text2ImageResponse>(responseText);
                
                if (response.status == "error")
                {
                    onError?.Invoke(response.error);
                    yield break;
                }
                
                if (response.imageUrls == null || response.imageUrls.Length == 0)
                {
                    onError?.Invoke("No image URLs in response");
                    yield break;
                }
                
                // 下载第一张图片
                string imageUrl = response.imageUrls[0];
                Debug.Log($"[Text2Image] Downloading image from: {imageUrl}");
                
                yield return DownloadImage(imageUrl, onComplete, onError);
            }
        }
        
        private IEnumerator DownloadImage(string imageUrl, Action<object> onComplete, Action<string> onError)
        {
            using (UnityWebRequest imgRequest = UnityWebRequestTexture.GetTexture(imageUrl))
            {
                yield return imgRequest.SendWebRequest();
                
                if (imgRequest.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Failed to download image: {imgRequest.error}");
                    yield break;
                }
                
                Texture2D texture = DownloadHandlerTexture.GetContent(imgRequest);
                outputData = texture;
                
                Debug.Log($"[Text2Image] Image downloaded: {texture.width}x{texture.height}");
                onComplete?.Invoke(texture);
            }
        }
        
        private string EscapeJson(string str)
        {
            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r");
        }
    }
    
    /// <summary>
    /// API 响应结构
    /// </summary>
    [Serializable]
    public class Text2ImageResponse
    {
        public string status;
        public string error;
        public string task_id;
        public string[] imageUrls;
    }
}
