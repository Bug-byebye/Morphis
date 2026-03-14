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
