using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Morphis.Chat
{
    /// <summary>
    /// API client for the player's human companion chat.
    /// Calls the /human-chat endpoint on the backend server.
    /// </summary>
    public static class HumanChatAPI
    {
        private static string apiUrl => BuildApiUrl();
        private static string sessionId = Guid.NewGuid().ToString();

        [Serializable]
        private class ChatRequest
        {
            public string message;
            public string session_id;
            public string companion_name;
        }

        [Serializable]
        private class ChatResponse
        {
            public string response;
            public string session_id;
        }

        public static void SendMessage(
            string message,
            Action<string> onResponse,
            Action<string> onError = null,
            string companionName = "伴侣")
        {
            HumanChatCoroutineRunner.Instance.StartCoroutine(
                SendMessageCoroutine(message, companionName, onResponse, onError));
        }

        public static void ClearConversation(Action onComplete = null)
        {
            HumanChatCoroutineRunner.Instance.StartCoroutine(ClearConversationCoroutine(onComplete));
        }

        private static string BuildApiUrl()
        {
            var baseUrl = Morphis.Config.AppConfig.Instance.ApiBaseUrl;
            if (baseUrl.EndsWith("/"))
            {
                baseUrl = baseUrl.TrimEnd('/');
            }

            return $"{baseUrl}/human-chat";
        }

        private static IEnumerator SendMessageCoroutine(
            string message,
            string companionName,
            Action<string> onResponse,
            Action<string> onError)
        {
            var request = new ChatRequest
            {
                message = message,
                session_id = sessionId,
                companion_name = companionName
            };

            string jsonBody = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (var webRequest = new UnityWebRequest(apiUrl, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = 30;

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<ChatResponse>(webRequest.downloadHandler.text);
                        if (response != null && !string.IsNullOrWhiteSpace(response.response))
                        {
                            onResponse?.Invoke(response.response);
                        }
                        else
                        {
                            onResponse?.Invoke(GetPlaceholderResponse(message, companionName));
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[HumanChatAPI] Failed to parse response: {e.Message}");
                        onResponse?.Invoke(GetPlaceholderResponse(message, companionName));
                    }
                }
                else
                {
                    string error = webRequest.error;
                    Debug.LogWarning($"[HumanChatAPI] Request failed: {error}. Using placeholder response.");
                    if (onError != null)
                    {
                        onError.Invoke(error);
                    }
                    else
                    {
                        onResponse?.Invoke(GetPlaceholderResponse(message, companionName));
                    }
                }
            }
        }

        private static IEnumerator ClearConversationCoroutine(Action onComplete)
        {
            string clearUrl = $"{apiUrl}/clear?session_id={sessionId}";

            using (var webRequest = UnityWebRequest.PostWwwForm(clearUrl, string.Empty))
            {
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("[HumanChatAPI] Conversation cleared");
                }

                sessionId = Guid.NewGuid().ToString();
                onComplete?.Invoke();
            }
        }

        private static string GetPlaceholderResponse(string message, string companionName)
        {
            string messageLower = message.ToLower();

            if (messageLower.Contains("hello") || messageLower.Contains("hi") || message.Contains("你好") || message.Contains("在吗"))
            {
                return $"我在呢。见到你我就安心了，{companionName}一直陪着你。";
            }

            if (message.Contains("累") || messageLower.Contains("tired") || message.Contains("辛苦"))
            {
                return "今天辛苦了，先歇一下吧。我就在你身边。";
            }

            if (message.Contains("爱") || messageLower.Contains("love"))
            {
                return "我也爱你。你不用证明什么，被你需要这件事本身就很珍贵。";
            }

            if (message.Contains("难过") || message.Contains("伤心") || messageLower.Contains("sad"))
            {
                return "来，慢慢告诉我。我先陪你把这阵情绪熬过去。";
            }

            string[] defaultResponses = new string[]
            {
                "我在认真听，你继续说。",
                "嗯，我陪着你，不着急。",
                "你说吧，我现在只想把注意力放在你身上。",
                "没关系，想到什么就告诉我什么。",
                "我在这里，慢慢聊。"
            };

            return defaultResponses[UnityEngine.Random.Range(0, defaultResponses.Length)];
        }
    }

    /// <summary>
    /// Helper class to run coroutines from static context for human chat.
    /// </summary>
    public class HumanChatCoroutineRunner : MonoBehaviour
    {
        private static HumanChatCoroutineRunner instance;

        public static HumanChatCoroutineRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("HumanChatCoroutineRunner");
                    instance = go.AddComponent<HumanChatCoroutineRunner>();
                    UnityEngine.Object.DontDestroyOnLoad(go);
                }

                return instance;
            }
        }
    }
}
