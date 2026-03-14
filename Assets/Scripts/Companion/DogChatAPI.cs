using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Morphis.Companion
{
    /// <summary>
    /// API client for dog chat with LLM backend.
    /// Calls the /chat endpoint on the backend server.
    /// </summary>
    public static class DogChatAPI
    {
        // Backend API URL - from AppConfig.ApiBaseUrl
        private static string apiUrl => BuildApiUrl();
        private static string BuildApiUrl()
        {
            var baseUrl = Morphis.Config.AppConfig.Instance.ApiBaseUrl;
            if (baseUrl.EndsWith("/"))
                baseUrl = baseUrl.TrimEnd('/');
            return $"{baseUrl}/chat";
        }
        
        // Session ID for conversation continuity
        private static string sessionId = Guid.NewGuid().ToString();
        private static int actionCategoryCount = 8;
        private static readonly Regex ActionTagRegex = new Regex(@"\[\[\s*ACTION\s*:\s*(-?\d+)\s*\]\]", RegexOptions.IgnoreCase);

        [Serializable]
        private class ChatRequest
        {
            public string message;
            public string session_id;
            public string dog_name;
        }

        [Serializable]
        private class ChatResponse
        {
            public string response;
            public string session_id;
        }

        /// <summary>
        /// Send a message to the dog and get a response from the LLM.
        /// </summary>
        /// <param name="message">User's message</param>
        /// <param name="dogName">Name of the dog</param>
        /// <param name="onResponse">Callback with the dog's response</param>
        /// <param name="onError">Callback if error occurs</param>
        public static void SendMessage(string message, Action<string> onResponse, Action<string> onError = null, string dogName = "Buddy")
        {
            SendMessage(
                message,
                (response, _) => onResponse?.Invoke(response),
                onError,
                dogName
            );
        }

        /// <summary>
        /// Send message and optionally receive a model-suggested action category parsed from [[ACTION:N]] tag.
        /// </summary>
        public static void SendMessage(string message, Action<string, int?> onResponse, Action<string> onError = null, string dogName = "Buddy")
        {
            CoroutineRunner.Instance.StartCoroutine(SendMessageCoroutine(message, dogName, onResponse, onError));
        }

        private static IEnumerator SendMessageCoroutine(string message, string dogName, Action<string, int?> onResponse, Action<string> onError)
        {
            string promptedMessage = BuildMessageWithActionPrompt(message, dogName);

            // Create request body
            var request = new ChatRequest
            {
                message = promptedMessage,
                session_id = sessionId,
                dog_name = dogName
            };
            
            string jsonBody = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            
            using (var webRequest = new UnityWebRequest(apiUrl, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = 30; // 30 second timeout for LLM response
                
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<ChatResponse>(webRequest.downloadHandler.text);
                        var parsed = ParseActionTag(response.response);
                        onResponse?.Invoke(parsed.cleanText, parsed.actionCategory);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[DogChatAPI] Failed to parse response: {e.Message}");
                        onResponse?.Invoke(GetPlaceholderResponse(message, dogName), null);
                    }
                }
                else
                {
                    Debug.LogWarning($"[DogChatAPI] Request failed: {webRequest.error}. Using placeholder response.");
                    // Fallback to placeholder response
                    onResponse?.Invoke(GetPlaceholderResponse(message, dogName), null);
                }
            }
        }

        private static string BuildMessageWithActionPrompt(string userMessage, string dogName)
        {
            int maxCategory = Mathf.Max(1, actionCategoryCount);
            var sb = new StringBuilder();
            sb.AppendLine($"你是一只名字叫 {dogName} 的狗，用可爱、自然的语气聊天。");
            sb.AppendLine($"回复末尾必须追加动作标签，格式严格为 [[ACTION:N]]。N 的范围是 0 到 {maxCategory}。");
            sb.AppendLine("如果不需要动作，返回 [[ACTION:0]]。不要输出任何解释该标签规则的内容。");
            sb.AppendLine();
            sb.AppendLine($"用户消息：{userMessage}");
            return sb.ToString();
        }

        private static (string cleanText, int? actionCategory) ParseActionTag(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return (string.Empty, null);
            }

            Match m = ActionTagRegex.Match(responseText);
            if (!m.Success)
            {
                return (responseText.Trim(), null);
            }

            int parsedAction = 0;
            int.TryParse(m.Groups[1].Value, out parsedAction);

            string clean = ActionTagRegex.Replace(responseText, "").Trim();
            if (parsedAction <= 0)
            {
                return (clean, null);
            }

            return (clean, parsedAction);
        }

        /// <summary>
        /// Clear the conversation history on the server.
        /// </summary>
        public static void ClearConversation(Action onComplete = null)
        {
            CoroutineRunner.Instance.StartCoroutine(ClearConversationCoroutine(onComplete));
        }

        private static IEnumerator ClearConversationCoroutine(Action onComplete)
        {
            string clearUrl = $"{apiUrl}/clear?session_id={sessionId}";
            
            using (var webRequest = UnityWebRequest.PostWwwForm(clearUrl, ""))
            {
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("[DogChatAPI] Conversation cleared");
                }
                
                // Generate new session ID
                sessionId = Guid.NewGuid().ToString();
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Fallback placeholder responses when API is unavailable.
        /// </summary>
        private static string GetPlaceholderResponse(string message, string dogName)
        {
            message = message.ToLower();
            
            if (message.Contains("hello") || message.Contains("hi"))
                return $"*wags tail excitedly* Woof! Hello friend! I'm {dogName}!";
            if (message.Contains("good") && message.Contains("boy"))
                return "*spins in circles* Woof woof! Thank you! <3";
            if (message.Contains("treat") || message.Contains("food"))
                return "*ears perk up* Did someone say treats?!";
            if (message.Contains("walk"))
                return "*runs to the door* Walk?! WALK?! Let's go!";
            if (message.Contains("love"))
                return "*licks your face* I love you too, human! <3";
            if (message.Contains("sit"))
                return "*sits down proudly* Look at me! I'm a good boy!";
            if (message.Contains("play"))
                return "*brings a ball* Throw it! Throw it!";
            if (message.Contains("name"))
                return $"*tail wagging* My name is {dogName}! Nice to meet you!";
            
            string[] defaultResponses = new string[]
            {
                "*wags tail* Woof! *tilts head curiously*",
                "*happy panting* Bark bark!",
                "*sniffs around* Interesting... tell me more!",
                "*rolls over* Belly rubs?",
                "*playful bark* Woof woof! <3"
            };
            
            return defaultResponses[UnityEngine.Random.Range(0, defaultResponses.Length)];
        }

        /// <summary>
        /// Set a custom API URL (kept for API compatibility, now no-op).
        /// </summary>
        public static void SetApiUrl(string url)
        {
            // URL 统一由 AppConfig 提供，此处保留方法以避免调用方编译错误但不做任何事。
        }

        /// <summary>
        /// Set count of valid action categories expected from the model.
        /// Model should return category in [1..count], or 0 for no action.
        /// </summary>
        public static void SetActionCategoryCount(int count)
        {
            actionCategoryCount = Mathf.Max(1, count);
        }
    }

    /// <summary>
    /// Helper class to run coroutines from static context.
    /// </summary>
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("DogChatCoroutineRunner");
                    _instance = go.AddComponent<CoroutineRunner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
    }
}
