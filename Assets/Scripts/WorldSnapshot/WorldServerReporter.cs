using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Mirror;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// Unity Server 端：定期向 Backend 上报玩家数量
    /// 用于 World 进程管理器监控和自动清理空闲 World
    /// </summary>
    public class WorldServerReporter : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("上报间隔（秒）")]
        [SerializeField] private float reportInterval = 30f;

        private string _worldId;
        private string _apiBaseUrl;
        private Coroutine _reportCoroutine;

        private void Start()
        {
            // 仅在 Server 模式下运行
            if (!AppRuntime.IsServer)
            {
                Debug.Log("[WorldServerReporter] Not server mode, disabling.");
                enabled = false;
                return;
            }

            _worldId = AppRuntime.WorldId;
            _apiBaseUrl = AppFlow.AppSession.BaseUrl;

            if (string.IsNullOrEmpty(_worldId))
            {
                Debug.LogWarning("[WorldServerReporter] WorldId is empty, cannot report.");
                enabled = false;
                return;
            }

            Debug.Log($"[WorldServerReporter] Starting reporter for world: {_worldId}");
            _reportCoroutine = StartCoroutine(ReportLoop());
        }

        private void OnDestroy()
        {
            if (_reportCoroutine != null)
            {
                StopCoroutine(_reportCoroutine);
            }
        }

        private IEnumerator ReportLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(reportInterval);

                if (NetworkServer.active)
                {
                    int playerCount = NetworkServer.connections.Count;
                    yield return ReportPlayerCount(playerCount);
                }
            }
        }

        private IEnumerator ReportPlayerCount(int count)
        {
            var url = $"{_apiBaseUrl}/worlds/manage/player-count";
            var body = $"{{\"world_id\":\"{EscapeJson(_worldId)}\",\"count\":{count}}}";
            var bodyRaw = Encoding.UTF8.GetBytes(body);

            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[WorldServerReporter] Failed to report player count: {req.error}");
                }
                else
                {
                    Debug.Log($"[WorldServerReporter] Reported player count: {count}");
                }
            }
        }

        private string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
