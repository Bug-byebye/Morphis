using System;
using System.Threading;
using UnityEngine;

namespace Morphis
{
    public enum AppMode
    {
        Client = 0,
        Server = 1,
    }

    /// <summary>
    /// 全局运行时启动参数（只读）。
    /// - 通过 <see cref="InitializeFromCommandLineArgs"/> 初始化
    /// - 通过 RuntimeInitializeOnLoadMethod 在域重载时重置
    /// </summary>
    public static class AppRuntime
    {
        private static readonly object _lock = new object();
        private static int _initialized; // 0/1

        public static bool IsInitialized => Volatile.Read(ref _initialized) == 1;

        public static AppMode Mode { get; private set; } = AppMode.Client;

        public static bool IsServer => Mode == AppMode.Server;
        public static bool IsClient => Mode == AppMode.Client;

        public static string WorldId { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload()
        {
            lock (_lock)
            {
                _initialized = 0;
                Mode = AppMode.Client;
                WorldId = null;
            }
        }

        /// <summary>
        /// 解析命令行参数并写入只读全局状态。
        /// 支持：
        /// - --mode=server
        /// - --mode=client（或默认）
        /// - --worldId=&lt;string&gt;
        ///
        /// 健壮性：
        /// - 参数缺失/格式错误不会抛异常
        /// - 重复调用是幂等的（后续调用会被忽略）
        /// </summary>
        public static void InitializeFromCommandLineArgs(string[] args = null)
        {
            if (IsInitialized) return;

            lock (_lock)
            {
                if (IsInitialized) return;

                try
                {
                    args ??= Environment.GetCommandLineArgs() ?? Array.Empty<string>();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AppRuntime] Failed to read command line args. Fallback to defaults. {e.GetType().Name}: {e.Message}");
                    args = Array.Empty<string>();
                }

                // defaults
                var mode = AppMode.Client;
                string worldId = null;

                // parse
                for (int i = 0; i < args.Length; i++)
                {
                    var a = args[i];
                    if (string.IsNullOrWhiteSpace(a)) continue;

                    if (TryReadValue(args, ref i, "--mode", out var modeValue))
                    {
                        modeValue = TrimQuotes(modeValue)?.Trim();
                        if (string.IsNullOrEmpty(modeValue))
                        {
                            Debug.LogWarning("[AppRuntime] '--mode' provided without value. Using default: client.");
                        }
                        else if (string.Equals(modeValue, "server", StringComparison.OrdinalIgnoreCase))
                        {
                            mode = AppMode.Server;
                        }
                        else if (string.Equals(modeValue, "client", StringComparison.OrdinalIgnoreCase))
                        {
                            mode = AppMode.Client;
                        }
                        else
                        {
                            Debug.LogWarning($"[AppRuntime] Unknown mode '{modeValue}'. Using default: client.");
                        }

                        continue;
                    }

                    if (TryReadValue(args, ref i, "--worldId", out var worldIdValue))
                    {
                        worldIdValue = TrimQuotes(worldIdValue)?.Trim();
                        if (!string.IsNullOrEmpty(worldIdValue))
                        {
                            worldId = worldIdValue;
                        }

                        continue;
                    }
                }

                Mode = mode;
                WorldId = worldId;
                Volatile.Write(ref _initialized, 1);
            }
        }

        private static bool TryReadValue(string[] args, ref int index, string key, out string value)
        {
            value = null;
            if (args == null || index < 0 || index >= args.Length) return false;

            var a = args[index];
            if (string.IsNullOrEmpty(a)) return false;

            // --key=value
            if (a.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = a.Substring(key.Length + 1);
                return true;
            }

            // --key value
            if (string.Equals(a, key, StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < args.Length)
                {
                    value = args[index + 1];
                    index++; // consume next
                }
                else
                {
                    value = null;
                }

                return true;
            }

            return false;
        }

        private static string TrimQuotes(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
            {
                return s.Substring(1, s.Length - 2);
            }
            return s;
        }
    }
}

