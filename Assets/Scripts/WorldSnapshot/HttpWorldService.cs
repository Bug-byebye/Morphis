using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// HTTP 世界服务：与后端交互，保存/加载世界快照
    /// </summary>
    public class HttpWorldService : MonoBehaviour
    {
        [Header("Backend Configuration")]
        [SerializeField] private string baseUrl = "http://localhost:8000";

        [Tooltip("是否自动从 AppSession 获取 BaseUrl 和 Token（如果存在）")]
        [SerializeField] private bool useAppSession = true;

        private string GetBaseUrl()
        {
            // 尝试从 AppSession 获取 BaseUrl（如果启用且存在）
            if (useAppSession)
            {
                var appSessionUrl = GetAppSessionBaseUrl();
                if (!string.IsNullOrEmpty(appSessionUrl))
                {
                    return appSessionUrl;
                }
            }
            return baseUrl;
        }

        private string GetAppSessionBaseUrl()
        {
            if (!useAppSession) return null;

            try
            {
                // 使用反射访问 AppSession.BaseUrl（避免编译时依赖）
                // 尝试多种方式查找类型
                Type appSessionType = null;
                
                // 方式1：通过完整类型名查找（适用于默认程序集）
                appSessionType = Type.GetType("Morphis.AppFlow.AppSession");
                
                // 方式2：如果方式1失败，尝试从所有已加载的程序集中查找
                if (appSessionType == null)
                {
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        appSessionType = assembly.GetType("Morphis.AppFlow.AppSession");
                        if (appSessionType != null) break;
                    }
                }

                if (appSessionType != null)
                {
                    var baseUrlProp = appSessionType.GetProperty("BaseUrl", BindingFlags.Public | BindingFlags.Static);
                    if (baseUrlProp != null)
                    {
                        var value = baseUrlProp.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // 静默失败，不影响功能
                Debug.LogWarning($"[HttpWorldService] Failed to get AppSession.BaseUrl: {e.Message}");
            }
            return null;
        }

        private string GetAppSessionToken()
        {
            if (!useAppSession) return null;

            try
            {
                Type appSessionType = null;
                appSessionType = Type.GetType("Morphis.AppFlow.AppSession");
                
                if (appSessionType == null)
                {
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        appSessionType = assembly.GetType("Morphis.AppFlow.AppSession");
                        if (appSessionType != null) break;
                    }
                }

                if (appSessionType != null)
                {
                    var tokenProp = appSessionType.GetProperty("Token", BindingFlags.Public | BindingFlags.Static);
                    if (tokenProp != null)
                    {
                        return tokenProp.GetValue(null) as string;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HttpWorldService] Failed to get AppSession.Token: {e.Message}");
            }
            return null;
        }

        private bool IsAppSessionLoggedIn()
        {
            if (!useAppSession) return false;

            try
            {
                Type appSessionType = null;
                appSessionType = Type.GetType("Morphis.AppFlow.AppSession");
                
                if (appSessionType == null)
                {
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        appSessionType = assembly.GetType("Morphis.AppFlow.AppSession");
                        if (appSessionType != null) break;
                    }
                }

                if (appSessionType != null)
                {
                    var isLoggedInProp = appSessionType.GetProperty("IsLoggedIn", BindingFlags.Public | BindingFlags.Static);
                    if (isLoggedInProp != null)
                    {
                        return (bool)isLoggedInProp.GetValue(null);
                    }
                }
            }
            catch
            {
                // 静默失败
            }
            return false;
        }

        private string GetWorldUrl(string worldId)
        {
            return $"{GetBaseUrl()}/world/{Uri.EscapeDataString(worldId)}";
        }

        /// <summary>
        /// 保存世界快照到服务器（POST）
        /// </summary>
        /// <param name="snapshot">要保存的快照</param>
        /// <param name="onSuccess">成功回调</param>
        /// <param name="onError">失败回调</param>
        public void SaveToServer(WorldSnapshot snapshot, Action onSuccess = null, Action<string> onError = null)
        {
            if (snapshot == null)
            {
                onError?.Invoke("Snapshot is null");
                return;
            }

            if (string.IsNullOrEmpty(snapshot.world_id))
            {
                onError?.Invoke("world_id is null or empty");
                return;
            }

            StartCoroutine(SaveToServerCoroutine(snapshot, onSuccess, onError));
        }

        private IEnumerator SaveToServerCoroutine(WorldSnapshot snapshot, Action onSuccess, Action<string> onError)
        {
            var url = GetWorldUrl(snapshot.world_id);
            var json = JsonUtility.ToJson(snapshot, prettyPrint: false);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            Debug.Log($"[HttpWorldService] POST {url}");

            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                // 如果已登录，添加认证头
                if (useAppSession && IsAppSessionLoggedIn())
                {
                    var token = GetAppSessionToken();
                    if (!string.IsNullOrEmpty(token))
                    {
                        req.SetRequestHeader("Authorization", $"Bearer {token}");
                    }
                }

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    var error = $"Network error: {req.error}";
                    Debug.LogError($"[HttpWorldService] {error}");
                    onError?.Invoke(error);
                    yield break;
                }

                if (req.responseCode >= 400)
                {
                    var error = $"HTTP {req.responseCode}: {req.downloadHandler.text}";
                    Debug.LogError($"[HttpWorldService] {error}");
                    onError?.Invoke(error);
                    yield break;
                }

                Debug.Log($"[HttpWorldService] Successfully saved world '{snapshot.world_id}' to server");
                onSuccess?.Invoke();
            }
        }

        /// <summary>
        /// 从服务器加载世界快照（GET）
        /// </summary>
        /// <param name="worldId">世界 ID</param>
        /// <param name="onSuccess">成功回调（返回快照）</param>
        /// <param name="onError">失败回调</param>
        public void LoadFromServer(string worldId, Action<WorldSnapshot> onSuccess = null, Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(worldId))
            {
                onError?.Invoke("world_id is null or empty");
                return;
            }

            StartCoroutine(LoadFromServerCoroutine(worldId, onSuccess, onError));
        }

        private IEnumerator LoadFromServerCoroutine(string worldId, Action<WorldSnapshot> onSuccess, Action<string> onError)
        {
            var url = GetWorldUrl(worldId);

            Debug.Log($"[HttpWorldService] GET {url}");

            using (var req = UnityWebRequest.Get(url))
            {
                // 如果已登录，添加认证头
                if (useAppSession && IsAppSessionLoggedIn())
                {
                    var token = GetAppSessionToken();
                    if (!string.IsNullOrEmpty(token))
                    {
                        req.SetRequestHeader("Authorization", $"Bearer {token}");
                    }
                }

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    var error = $"Network error: {req.error}";
                    Debug.LogError($"[HttpWorldService] {error}");
                    onError?.Invoke(error);
                    yield break;
                }

                if (req.responseCode == 404)
                {
                    var error = $"World '{worldId}' not found on server";
                    Debug.LogWarning($"[HttpWorldService] {error}");
                    onError?.Invoke(error);
                    yield break;
                }

                if (req.responseCode >= 400)
                {
                    var error = $"HTTP {req.responseCode}: {req.downloadHandler.text}";
                    Debug.LogError($"[HttpWorldService] {error}");
                    onError?.Invoke(error);
                    yield break;
                }

                try
                {
                    var json = req.downloadHandler.text;
                    var snapshot = JsonUtility.FromJson<WorldSnapshot>(json);

                    if (snapshot == null)
                    {
                        var error = "Failed to parse JSON response";
                        Debug.LogError($"[HttpWorldService] {error}");
                        onError?.Invoke(error);
                        yield break;
                    }

                    Debug.Log($"[HttpWorldService] Successfully loaded world '{worldId}' from server, version: {snapshot.version}, objects: {snapshot.objects?.Count ?? 0}");
                    onSuccess?.Invoke(snapshot);
                }
                catch (Exception e)
                {
                    var error = $"JSON parse error: {e.Message}";
                    Debug.LogError($"[HttpWorldService] {error}");
                    onError?.Invoke(error);
                }
            }
        }

        /// <summary>
        /// 静态便捷方法：获取或创建 HttpWorldService 实例
        /// </summary>
        public static HttpWorldService GetOrCreate()
        {
            var instance = FindFirstObjectByType<HttpWorldService>();
            if (instance != null) return instance;

            var go = new GameObject("HttpWorldService");
            return go.AddComponent<HttpWorldService>();
        }
    }
}
