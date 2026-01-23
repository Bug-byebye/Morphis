using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 确保在 Playground 场景中始终存在一个 ObjectInteractionManager，避免你手动拖脚本遗漏。
/// </summary>
public static class ObjectInteractionBootstrap
{
    private const string TargetSceneName = "Playground";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        var scene = SceneManager.GetActiveScene();
        if (!string.Equals(scene.name, TargetSceneName, System.StringComparison.OrdinalIgnoreCase))
            return;

        if (Object.FindFirstObjectByType<ObjectInteractionManager>() != null)
            return;

        var go = new GameObject("ObjectInteractionManager(Auto)");
        go.AddComponent<ObjectInteractionManager>();
        Object.DontDestroyOnLoad(go);
    }
}

