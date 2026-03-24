using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using Morphis.AppFlow;

namespace Morphis
{
    /// <summary>
    /// 启动引导：在最早期解析命令行参数，设置 <see cref="AppRuntime"/> 并打印模式/WorldId，
    /// 并在首个含 NetworkManager 的场景加载后自动按 mode 启动 Mirror 网络（Server / Client）。
    /// - 默认（无参数或 --mode=client）：仅作为 Client 启动
    /// - --mode=server：仅作为 Server 启动
    /// - 严禁 Host：永不调用 StartHost()
    /// - 严禁网络 UI：自动销毁所有 NetworkManagerHUD 实例
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class AppBootstrap : MonoBehaviour
    {
        private static bool _created;
        private static bool _networkStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (_created) return;

            var existing = FindFirstObjectByType<AppBootstrap>();
            if (existing != null)
            {
                _created = true;
                return;
            }

            var go = new GameObject("AppBootstrap(Auto)");
            DontDestroyOnLoad(go);
            go.AddComponent<AppBootstrap>();
            _created = true;
        }

        private void Awake()
        {
            // 幂等：重复调用不会有副作用
            AppRuntime.InitializeFromCommandLineArgs();

            Debug.Log($"[AppBootstrap] mode={(AppRuntime.IsServer ? "server" : "client")}, worldId='{AppRuntime.WorldId}'");

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            TryStartNetworkForActiveScene();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryStartNetworkForActiveScene();
        }

        private static void TryStartNetworkForActiveScene()
        {
            if (_networkStarted) return;

            // 清理所有 NetworkManagerHUD，避免场景内出现网络控制 UI
            DestroyAllNetworkHud();

            var manager = NetworkManager.singleton;
            if (manager == null)
            {
                manager = FindFirstObjectByType<NetworkManager>();
            }
            if (manager == null)
            {
                return; // 当前场景不含 NetworkManager，等待后续场景
            }

            if (AppRuntime.IsServer)
            {
                // 服务器模式：仅启动 Server，不启动 Client，不允许 Host
                ConfigureNetworkManager(manager);
                EnsureWorldServerReporter();
                Debug.Log("[AppBootstrap] Starting Mirror in SERVER mode (StartServer).");
                manager.StartServer();
                _networkStarted = true;
            }
            else
            {
                // 客户端模式：仅启动 Client，不启动 Server，不允许 Host
                ConfigureNetworkManager(manager);
                Debug.Log("[AppBootstrap] Starting Mirror in CLIENT mode (StartClient).");
                manager.StartClient();
                _networkStarted = true;
            }
        }
        
        private static void ConfigureNetworkManager(NetworkManager manager)
        {
            if (manager == null) return;
            
            var config = Morphis.Config.AppConfig.Instance;
            
            if (AppRuntime.IsServer)
            {
                // Server 模式：使用配置文件中的监听地址和端口
                manager.networkAddress = config.ServerListenAddress;
                
                var telepathy = manager.GetComponent<TelepathyTransport>();
                if (telepathy != null)
                {
                    telepathy.port = (ushort)config.ServerPort;
                }
                
                Debug.Log($"[AppBootstrap] Server listening on: {manager.networkAddress}:{config.ServerPort}");
            }
            else
            {
                // 客户端模式：使用 AppSession 中的动态服务器地址（从 /workspaces/join 获取）
                if (!string.IsNullOrEmpty(AppSession.ServerAddress))
                {
                    manager.networkAddress = AppSession.ServerAddress;
                    
                    var telepathy = manager.GetComponent<TelepathyTransport>();
                    if (telepathy != null)
                    {
                        telepathy.port = (ushort)AppSession.ServerPort;
                    }
                    
                    Debug.Log($"[AppBootstrap] Client connecting to dynamic server: {manager.networkAddress}:{AppSession.ServerPort}");
                }
                else
                {
                    // Fallback：如果没有动态地址（例如直接 Play MainScene），使用 localhost
                    manager.networkAddress = "127.0.0.1";
                    
                    var telepathy = manager.GetComponent<TelepathyTransport>();
                    if (telepathy != null)
                    {
                        telepathy.port = (ushort)config.ServerPort;
                    }
                    
                    Debug.LogWarning($"[AppBootstrap] No dynamic server address, using fallback: {manager.networkAddress}:{config.ServerPort}");
                }
            }
        }

        /// <summary>
        /// 在 Dedicated Server 模式下，确保存在一个 WorldServerReporter，用于定期向后端上报玩家数量，
        /// 以防止 World 进程管理器将仍有玩家的世界误判为“空闲”而自动清理。
        /// </summary>
        private static void EnsureWorldServerReporter()
        {
            if (!AppRuntime.IsServer) return;

            // 避免直接依赖 WorldSnapshot 程序集：通过类型名查找是否已存在 WorldServerReporter
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                if (mb.GetType().Name == "WorldServerReporter")
                {
                    return;
                }
            }

            var go = new GameObject("WorldServerReporter(Auto)");
            Object.DontDestroyOnLoad(go);
            // 通过反射方式添加组件，避免编译期依赖命名空间
            var reporterType = ResolveType("Morphis.WorldSnapshot.WorldServerReporter");
            if (reporterType != null && reporterType.IsSubclassOf(typeof(MonoBehaviour)))
            {
                go.AddComponent(reporterType);
                Debug.Log("[AppBootstrap] Auto-created WorldServerReporter for dedicated server.");
            }
            else
            {
                Object.Destroy(go);
                Debug.LogWarning("[AppBootstrap] Failed to locate Morphis.WorldSnapshot.WorldServerReporter type. Player count reporting will be disabled.");
            }
        }

        private static System.Type ResolveType(string fullTypeName)
        {
            var type = System.Type.GetType(fullTypeName);
            if (type != null)
            {
                return type;
            }

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                type = assembly.GetType(fullTypeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void DestroyAllNetworkHud()
        {
            var huds = FindObjectsByType<NetworkManagerHUD>(FindObjectsSortMode.None);
            foreach (var hud in huds)
            {
                if (hud != null)
                {
                    Debug.Log($"[AppBootstrap] Destroying NetworkManagerHUD on GameObject '{hud.gameObject.name}'.");
                    Destroy(hud.gameObject);
                }
            }
        }
    }
}
