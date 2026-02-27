using System;
using UnityEngine;

namespace Morphis.Config
{
    /// <summary>
    /// 应用配置数据
    /// 新架构：客户端和服务器使用不同的配置字段
    /// </summary>
    [Serializable]
    public class AppConfigData
    {
        // ===== Backend API 配置（客户端和服务器都需要） =====
        [Tooltip("Backend API 地址")]
        public string ApiBaseUrl;
        
        // ===== 服务器配置（仅 Unity Server 需要） =====
        [Tooltip("服务器监听地址（Server 模式）- 应该是 0.0.0.0")]
        public string ServerListenAddress = "0.0.0.0";
        
        [Tooltip("服务器默认端口（Server 模式）- 会被环境变量 WORLD_PORT 覆盖")]
        public int ServerPort = 7777;
        
        // ===== 开发/调试配置 =====
        [Tooltip("默认 World ID（开发模式使用）")]
        public string DefaultWorldId = "dev-world";
        
        // 注意：客户端不再需要 GameServerAddress 和 GameServerPort
        // 这些信息会从 /workspaces/join API 动态获取
    }

    public static class AppConfig
    {
        public static AppConfigData Instance { get; internal set; }
    }
}

