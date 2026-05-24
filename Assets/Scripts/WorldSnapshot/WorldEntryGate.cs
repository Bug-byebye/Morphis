using System;
using Morphis.AppFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 进入空间的唯一入口校验：必须已从服务端（数据库 GET /world）取得快照，禁止本地/空场景回退。
    /// </summary>
    public static class WorldEntryGate
    {
        public static bool HasValidatedSnapshot { get; private set; }
        public static WorldSnapshot ValidatedSnapshot { get; private set; }
        public static string LastFailureMessage { get; private set; }

        public static void SetValidatedSnapshot(WorldSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            ValidatedSnapshot = snapshot;
            HasValidatedSnapshot = true;
            LastFailureMessage = null;
            Debug.Log(
                $"[WorldEntryGate] Snapshot validated for '{snapshot.world_id}', version={snapshot.version}, objects={snapshot.objects?.Count ?? 0}");
        }

        public static void Clear()
        {
            HasValidatedSnapshot = false;
            ValidatedSnapshot = null;
            LastFailureMessage = null;
        }

        /// <summary>进入空间前是否允许（已登录且已选空间时必须已有服务端快照）。</summary>
        public static bool IsEntryAllowed()
        {
            if (!AppSession.IsLoggedIn || string.IsNullOrEmpty(AppSession.WorkspaceId))
                return true;
            return HasValidatedSnapshot && ValidatedSnapshot != null;
        }

        public static void AbortEntry(string reason)
        {
            LastFailureMessage = reason ?? "未知错误";
            HasValidatedSnapshot = false;
            ValidatedSnapshot = null;
            Debug.LogError($"[WorldEntryGate] Entry aborted: {LastFailureMessage}");
        }

        /// <summary>客户端未收到 Mirror 快照时强制退回登录界面。</summary>
        public static void ForceReturnToBoot(string reason)
        {
            AbortEntry(reason);
            if (Mirror.NetworkClient.active)
                Mirror.NetworkClient.Disconnect();
            if (Mirror.NetworkServer.active)
                Mirror.NetworkServer.Shutdown();

            AppSession.ClearWorkspaceSession();
            SceneManager.LoadScene("BootScene");
        }
    }
}
