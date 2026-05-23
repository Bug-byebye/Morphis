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
        private static HttpWorldService instance;

        private string GetBaseUrl() => Morphis.Config.AppConfig.Instance.ApiBaseUrl;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private string GetAppSessionToken()
        {
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
            var json = WorldSnapshotJson.Serialize(snapshot);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            Debug.Log($"[HttpWorldService] POST {url} objects={snapshot.objects?.Count ?? 0}");

            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                // 如果已登录，添加认证头
                if (IsAppSessionLoggedIn())
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
                if (IsAppSessionLoggedIn())
                {
                    var token = GetAppSessionToken();
                    if (!string.IsNullOrEmpty(token))
                    {
                        req.SetRequestHeader("Authorization", $"Bearer {token}");
                    }
                }

                yield return req.SendWebRequest();

                // Check for 404 first - this is a valid "world not found" response, not a network error
                if (req.responseCode == 404)
                {
                    var error = $"World '{worldId}' not found on server";
                    Debug.LogWarning($"[HttpWorldService] {error}");
                    onError?.Invoke(error);
                    yield break;
                }

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

                try
                {
                    var json = req.downloadHandler.text;
                    var snapshot = WorldSnapshotJson.Deserialize(json);

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
        /// 同步保存世界快照（阻塞主线程，仅用于 OnApplicationQuit / OnStopServer 等无法等待协程的场合）。
        /// 返回 true 表示已被服务器成功接收（HTTP 2xx）。
        /// </summary>
        public bool SaveToServerBlocking(WorldSnapshot snapshot, float timeoutSeconds = 5f, Action<string> onError = null)
        {
            if (snapshot == null)
            {
                onError?.Invoke("Snapshot is null");
                return false;
            }
            if (string.IsNullOrEmpty(snapshot.world_id))
            {
                onError?.Invoke("world_id is null or empty");
                return false;
            }

            var url = GetWorldUrl(snapshot.world_id);
            var json = WorldSnapshotJson.Serialize(snapshot);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            Debug.Log($"[HttpWorldService] (sync) POST {url} objects={snapshot.objects?.Count ?? 0}");

            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                if (IsAppSessionLoggedIn())
                {
                    var token = GetAppSessionToken();
                    if (!string.IsNullOrEmpty(token))
                        req.SetRequestHeader("Authorization", $"Bearer {token}");
                }

                var op = req.SendWebRequest();
                var start = DateTime.UtcNow;
                while (!op.isDone)
                {
                    if ((DateTime.UtcNow - start).TotalSeconds > timeoutSeconds)
                    {
                        var err = $"Timeout after {timeoutSeconds:F1}s";
                        Debug.LogWarning($"[HttpWorldService] (sync) {err}");
                        onError?.Invoke(err);
                        return false;
                    }
                    System.Threading.Thread.Sleep(10);
                }

                if (req.result != UnityWebRequest.Result.Success || req.responseCode >= 400)
                {
                    var err = req.result != UnityWebRequest.Result.Success
                        ? $"Network error: {req.error}"
                        : $"HTTP {req.responseCode}: {req.downloadHandler.text}";
                    Debug.LogError($"[HttpWorldService] (sync) {err}");
                    onError?.Invoke(err);
                    return false;
                }

                Debug.Log($"[HttpWorldService] (sync) Saved world '{snapshot.world_id}'");
                return true;
            }
        }

        /// <summary>
        /// 静态便捷方法：获取或创建 HttpWorldService 实例
        /// </summary>
        public static HttpWorldService GetOrCreate()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<HttpWorldService>();
            if (instance != null)
            {
                DontDestroyOnLoad(instance.gameObject);
                return instance;
            }

            var go = new GameObject("HttpWorldService");
            instance = go.AddComponent<HttpWorldService>();
            return instance;
        }
    }
}
