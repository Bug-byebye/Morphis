using System;
using System.Collections;
using System.Text;
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
        // Backend API URL - adjust if your server is running elsewhere
        private static string apiUrl = "http://localhost:8000/chat";
        
        // Session ID for conversation continuity
        private static string sessionId = Guid.NewGuid().ToString();

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
            CoroutineRunner.Instance.StartCoroutine(SendMessageCoroutine(message, dogName, onResponse, onError));
        }

        private static IEnumerator SendMessageCoroutine(string message, string dogName, Action<string> onResponse, Action<string> onError)
        {
            // Create request body
            var request = new ChatRequest
            {
                message = message,
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
                        onResponse?.Invoke(response.response);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[DogChatAPI] Failed to parse response: {e.Message}");
                        onResponse?.Invoke(GetPlaceholderResponse(message, dogName));
                    }
                }
                else
                {
                    Debug.LogWarning($"[DogChatAPI] Request failed: {webRequest.error}. Using placeholder response.");
                    // Fallback to placeholder response
                    onResponse?.Invoke(GetPlaceholderResponse(message, dogName));
                }
            }
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
        /// Set a custom API URL (useful for different server configurations).
        /// </summary>
        public static void SetApiUrl(string url)
        {
            apiUrl = url;
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
