using System;
using System.IO;
using UnityEngine;

namespace Morphis.Config
{
    public static class ConfigLoader
    {
        private const string FileName = "config.json";
        private static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStartup()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                var path = ResolveConfigPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    var msg = $"Config file not found. Expected at: {path ?? "<null>"}";
                    FailFast(msg);
                    return;
                }

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    var msg = $"Config file is empty. Path: {path}";
                    FailFast(msg);
                    return;
                }

                var data = JsonUtility.FromJson<AppConfigData>(json);
                if (data == null)
                {
                    var msg = $"Failed to parse config.json. Path: {path}";
                    FailFast(msg);
                    return;
                }

                Validate(data, path);
                AppConfig.Instance = data;

                // Server 模式：允许环境变量覆盖端口（用于动态端口分配）
                var envPort = Environment.GetEnvironmentVariable("WORLD_PORT");
                if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out int port))
                {
                    data.ServerPort = port;
                    Debug.Log($"[ConfigLoader] Server port overridden by WORLD_PORT env: {port}");
                }

                // Server 模式：允许环境变量覆盖 Backend API 地址。
                // 后端 world_manager 以 API_BASE_URL 环境变量启动 Unity Server 进程；
                // 若部署目录的 config.json 缺失/过期，这里作为权威来源，避免服务端 GET /world 指向错误地址而无法加载快照。
                var envApiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
                if (!string.IsNullOrWhiteSpace(envApiBaseUrl))
                {
                    data.ApiBaseUrl = envApiBaseUrl.Trim();
                    Debug.Log($"[ConfigLoader] ApiBaseUrl overridden by API_BASE_URL env: {data.ApiBaseUrl}");
                }

                Debug.Log("=== CONFIG LOADED ===");
                Debug.Log($"ApiBaseUrl:       {data.ApiBaseUrl}");
                Debug.Log($"ServerListenAddress: {data.ServerListenAddress}");
                Debug.Log($"ServerPort:       {data.ServerPort}");
                Debug.Log($"DefaultWorldId:   {data.DefaultWorldId}");
                Debug.Log($"ApiBaseUrl:       {data.ApiBaseUrl}");
                Debug.Log($"DefaultWorldId:   {data.DefaultWorldId}");
            }
            catch (Exception e)
            {
                var msg = $"Exception while loading config. {e.GetType().Name}: {e.Message}";
                FailFast(msg);
            }
        }

        private static string ResolveConfigPath()
        {
            try
            {
                // 优先 StreamingAssets/config.json
                var streamingPath = Path.Combine(Application.streamingAssetsPath, FileName);
                if (File.Exists(streamingPath))
                    return streamingPath;

                // 其次 Application.dataPath 上级目录的 config.json
                var dataDir = Application.dataPath;
                if (!string.IsNullOrEmpty(dataDir))
                {
                    var parentDir = Directory.GetParent(dataDir)?.FullName;
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        var upperPath = Path.Combine(parentDir, FileName);
                        if (File.Exists(upperPath))
                            return upperPath;

                        return upperPath; // 即便不存在，也返回作为提示
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConfigLoader] Failed to resolve config path: {e.GetType().Name}: {e.Message}");
            }

            return null;
        }

        private static void Validate(AppConfigData data, string path)
        {
            if (data == null)
            {
                FailFast($"Config data is null. Path: {path}");
                return;
            }

            if (string.IsNullOrWhiteSpace(data.ApiBaseUrl))
            {
                FailFast($"ApiBaseUrl is empty. Path: {path}");
            }

            // ServerListenAddress 和 ServerPort 有默认值，不强制要求
            // DefaultWorldId 有默认值，不强制要求
        }

        private static void FailFast(string message)
        {
            var full = $"[ConfigLoader] {message}";
            Debug.LogError(full);
#if UNITY_EDITOR
            throw new Exception(full);
#else
            Application.Quit();
#endif
        }
    }
}

