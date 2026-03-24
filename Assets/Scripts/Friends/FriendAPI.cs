using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Morphis.AppFlow;

namespace Morphis.Friends
{
    /// <summary>
    /// Backend API wrapper for friend list and friend requests.
    /// </summary>
    public static class FriendAPI
    {
        [Serializable]
        public class FriendDto
        {
            public string username;
        }

        [Serializable]
        public class FriendRequestDto
        {
            public int id;
            public string sender_username;
            public string receiver_username;
            public string status;
            public string created_at;
        }

        [Serializable]
        public class FriendsStateResponse
        {
            public FriendDto[] friends;
            public FriendRequestDto[] incoming_requests;
            public FriendRequestDto[] outgoing_requests;

            public void Normalize()
            {
                if (friends == null) friends = Array.Empty<FriendDto>();
                if (incoming_requests == null) incoming_requests = Array.Empty<FriendRequestDto>();
                if (outgoing_requests == null) outgoing_requests = Array.Empty<FriendRequestDto>();
            }
        }

        [Serializable]
        public class FriendActionResponse
        {
            public string status;
            public string message;
        }

        [Serializable]
        private class SendFriendRequestPayload
        {
            public string target_username;
        }

        public static void FetchState(
            Action<FriendsStateResponse> onSuccess,
            Action<string> onError = null)
        {
            Runner.Instance.StartCoroutine(FetchStateCoroutine(onSuccess, onError));
        }

        public static void SendFriendRequest(
            string targetUsername,
            Action<FriendActionResponse> onSuccess,
            Action<string> onError = null)
        {
            var payload = new SendFriendRequestPayload { target_username = targetUsername };
            Runner.Instance.StartCoroutine(PostActionCoroutine(
                BuildUrl("/friends/requests"),
                JsonUtility.ToJson(payload),
                onSuccess,
                onError));
        }

        public static void AcceptFriendRequest(
            int requestId,
            Action<FriendActionResponse> onSuccess,
            Action<string> onError = null)
        {
            Runner.Instance.StartCoroutine(PostActionCoroutine(
                BuildUrl($"/friends/requests/{requestId}/accept"),
                "{}",
                onSuccess,
                onError));
        }

        public static void DeclineFriendRequest(
            int requestId,
            Action<FriendActionResponse> onSuccess,
            Action<string> onError = null)
        {
            Runner.Instance.StartCoroutine(PostActionCoroutine(
                BuildUrl($"/friends/requests/{requestId}/decline"),
                "{}",
                onSuccess,
                onError));
        }

        private static IEnumerator FetchStateCoroutine(
            Action<FriendsStateResponse> onSuccess,
            Action<string> onError)
        {
            if (!EnsureAuthenticated(onError))
            {
                yield break;
            }

            using (var request = UnityWebRequest.Get(BuildUrl("/friends")))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                ApplyAuthHeaders(request);
                request.timeout = 15;

                yield return request.SendWebRequest();

                if (IsSuccess(request))
                {
                    FriendsStateResponse response = null;
                    try
                    {
                        response = JsonUtility.FromJson<FriendsStateResponse>(request.downloadHandler.text);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse friend state: {e.Message}");
                        yield break;
                    }

                    if (response == null)
                    {
                        onError?.Invoke("Friend state response was empty");
                        yield break;
                    }

                    response.Normalize();
                    onSuccess?.Invoke(response);
                }
                else
                {
                    onError?.Invoke(ExtractError(request));
                }
            }
        }

        private static IEnumerator PostActionCoroutine(
            string url,
            string jsonBody,
            Action<FriendActionResponse> onSuccess,
            Action<string> onError)
        {
            if (!EnsureAuthenticated(onError))
            {
                yield break;
            }

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody ?? "{}");

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 15;
                request.SetRequestHeader("Content-Type", "application/json");
                ApplyAuthHeaders(request);

                yield return request.SendWebRequest();

                if (IsSuccess(request))
                {
                    var response = new FriendActionResponse
                    {
                        status = "ok",
                        message = "ok"
                    };

                    if (!string.IsNullOrWhiteSpace(request.downloadHandler.text))
                    {
                        try
                        {
                            var parsed = JsonUtility.FromJson<FriendActionResponse>(request.downloadHandler.text);
                            if (parsed != null)
                            {
                                response = parsed;
                            }
                        }
                        catch
                        {
                            response.message = request.downloadHandler.text;
                        }
                    }

                    onSuccess?.Invoke(response);
                }
                else
                {
                    onError?.Invoke(ExtractError(request));
                }
            }
        }

        private static bool EnsureAuthenticated(Action<string> onError)
        {
            if (!AppSession.IsLoggedIn || string.IsNullOrWhiteSpace(AppSession.Token))
            {
                onError?.Invoke("Not logged in");
                return false;
            }

            return true;
        }

        private static void ApplyAuthHeaders(UnityWebRequest request)
        {
            if (!string.IsNullOrWhiteSpace(AppSession.Token))
            {
                request.SetRequestHeader("Authorization", $"Bearer {AppSession.Token}");
            }
        }

        private static bool IsSuccess(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success && request.responseCode < 400;
        }

        private static string ExtractError(UnityWebRequest request)
        {
            var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (!string.IsNullOrWhiteSpace(body))
            {
                return body;
            }

            if (!string.IsNullOrWhiteSpace(request.error))
            {
                return request.error;
            }

            return $"HTTP {request.responseCode}";
        }

        private static string BuildUrl(string path)
        {
            var baseUrl = AppSession.BaseUrl;
            if (baseUrl.EndsWith("/"))
            {
                baseUrl = baseUrl.TrimEnd('/');
            }

            return $"{baseUrl}{path}";
        }

        private sealed class Runner : MonoBehaviour
        {
            private static Runner _instance;

            public static Runner Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        var go = new GameObject("FriendAPIRunner");
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        _instance = go.AddComponent<Runner>();
                    }

                    return _instance;
                }
            }
        }
    }
}
