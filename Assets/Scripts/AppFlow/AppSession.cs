using System;

namespace Morphis.AppFlow
{
    /// <summary>
    /// 极简会话（可替换为 ScriptableObject/持久化存储）
    /// </summary>
    public static class AppSession
    {
        public static string BaseUrl { get; set; } = "http://localhost:8000";

        public static string Token { get; private set; }
        public static string Username { get; private set; }

        public static string WorkspaceId { get; private set; }
        public static string WorkspaceName { get; private set; }

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

        public static void Clear()
        {
            Token = null;
            Username = null;
            WorkspaceId = null;
            WorkspaceName = null;
        }
    }
}

