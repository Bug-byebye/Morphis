using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// MainScene 加载时自动创建 WorldSnapshotManager（若不存在），以便场景保存/加载生效。
    /// </summary>
    public static class WorldSnapshotBootstrap
    {
        private const string TargetSceneName = "MainScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            var scene = SceneManager.GetActiveScene();
            if (string.Equals(scene.name, TargetSceneName, System.StringComparison.OrdinalIgnoreCase))
                EnsureManager();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, TargetSceneName, System.StringComparison.OrdinalIgnoreCase))
                return;
            EnsureManager();
        }

        private static void EnsureManager()
        {
            if (WorldSnapshotManager.Instance != null)
                return;
            var go = new GameObject("WorldSnapshotManager(Auto)");
            go.AddComponent<WorldSnapshotManager>();
        }
    }
}
