using System;

namespace Morphis.AppFlow
{
    /// <summary>
    /// 极简会话（可替换为 ScriptableObject/持久化存储）
    /// 注意：BaseUrl 必须在运行时从 AppConfig 初始化，不允许使用硬编码默认值
    /// </summary>
    public static class AppSession
    {
        private static string _baseUrl;
        
        public static string BaseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_baseUrl))
                {
                    // 强制从配置文件读取，不允许 fallback
                    var config = Morphis.Config.AppConfig.Instance;
                    if (config == null || string.IsNullOrEmpty(config.ApiBaseUrl))
                    {
                        var msg = "[AppSession] BaseUrl not initialized. AppConfig.Instance.ApiBaseUrl is null or empty. " +
                                  "Ensure ConfigLoader has run and config.json is valid.";
                        UnityEngine.Debug.LogError(msg);
#if UNITY_EDITOR
                        throw new System.Exception(msg);
#else
                        UnityEngine.Application.Quit();
#endif
                    }
                    _baseUrl = config.ApiBaseUrl;
                }
                return _baseUrl;
            }
            set => _baseUrl = value;
        }

        public static string Token { get; private set; }
        public static string Username { get; private set; }

        public static string WorkspaceId { get; private set; }
        public static string WorkspaceName { get; private set; }
        
        // World 服务器连接信息（动态分配）
        public static string ServerAddress { get; private set; }
        public static int ServerPort { get; private set; }

        public static bool IsLoggedIn => !string.IsNullOrEmpty(Token);

        public static void SetAuth(string username, string token)
        {
            Username = username;
            Token = token;
        }

        public static void SetWorkspace(string id, string name)
        {
            WorkspaceId = id;
            WorkspaceName = name;
        }
        
        public static void SetServerConnection(string address, int port)
        {
            ServerAddress = address;
            ServerPort = port;
            UnityEngine.Debug.Log($"[AppSession] Server connection set: {address}:{port}");
        }

        public static void ClearWorkspaceSession()
        {
            WorkspaceId = null;
            WorkspaceName = null;
            ServerAddress = null;
            ServerPort = 0;
        }

        public static void Clear()
        {
            Token = null;
            Username = null;
            ClearWorkspaceSession();
        }
    }
}

