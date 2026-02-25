using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

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
            
            // 客户端模式：优先使用 AppSession 中的动态服务器地址（从 /workspaces/join 获取）
            // 服务器模式：使用配置文件中的地址
            if (!AppRuntime.IsServer && !string.IsNullOrEmpty(Morphis.AppFlow.AppSession.ServerAddress))
            {
                // 客户端：使用动态分配的服务器地址
                manager.networkAddress = Morphis.AppFlow.AppSession.ServerAddress;
                
                var telepathy = manager.GetComponent<TelepathyTransport>();
                if (telepathy != null)
                {
                    telepathy.port = (ushort)Morphis.AppFlow.AppSession.ServerPort;
                }
                
                Debug.Log($"[AppBootstrap] Client connecting to dynamic server: {manager.networkAddress}:{Morphis.AppFlow.AppSession.ServerPort}");
            }
            else
            {
                // 服务器或未设置动态地址：使用配置文件
                var config = Morphis.Config.AppConfig.Instance;
                manager.networkAddress = config.GameServerAddress;

                var telepathy = manager.GetComponent<TelepathyTransport>();
                if (telepathy != null)
                {
                    telepathy.port = (ushort)config.GameServerPort;
                }
                
                Debug.Log($"[AppBootstrap] Using config address: {manager.networkAddress}:{config.GameServerPort}");
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
