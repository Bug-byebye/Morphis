using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Morphis.AppFlow.Editor
{
    /// <summary>
    /// 一键把启动引导流程接入工程：
    /// - 确保 Build Settings 里包含 StarterAssets 的 Playground 场景
    /// -（可选）创建一个 Boot 场景作为第一个场景
    ///
    /// 说明：
    /// - 本项目已经有 BootFlowAutoStart，会在任意首场景启动前自动创建登录/选空间 UI。
    /// - 但 Unity 通过 SceneManager.LoadSceneAsync("Playground") 加载时，Playground 必须在 Build Settings 中。
    /// </summary>
    public static class AppFlowSetup
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string PlaygroundScenePath = "Assets/StarterAssets/ThirdPersonController/Scenes/Playground.unity";

        [MenuItem("Tools/Morphis/AppFlow/Setup Build Settings (Boot + Playground)")]
        public static void SetupBuildSettings()
        {
            EnsureSceneInBuildSettings(PlaygroundScenePath, enabled: true);

            // 如果用户希望真的有一个 Boot 场景，我们也顺便创建并放到第一个
            if (!File.Exists(BootScenePath))
            {
                CreateBootScene();
            }

            EnsureSceneInBuildSettings(BootScenePath, enabled: true, insertAtStart: true);

            Debug.Log("[Morphis][AppFlow] Build Settings 已配置：Boot -> Playground（Playground 已加入可加载列表）");
        }

        [MenuItem("Tools/Morphis/AppFlow/Create Boot Scene")]
        public static void CreateBootScene()
        {
            // 创建一个空场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 放一个提示物体，方便你一眼看出这是引导场景
            var go = new GameObject("BootSceneMarker");
            go.AddComponent<BootSceneMarker>();

            // 确保目录存在
            var dir = Path.GetDirectoryName(BootScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorSceneManager.SaveScene(scene, BootScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Morphis][AppFlow] Boot 场景已创建：{BootScenePath}");
        }

        private static void EnsureSceneInBuildSettings(string scenePath, bool enabled, bool insertAtStart = false)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[Morphis][AppFlow] Scene 不存在，无法加入 Build Settings：{scenePath}");
                return;
            }

            var scenes = EditorBuildSettings.scenes;
            int idx = -1;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0)
            {
                var entry = scenes[idx];
                entry.enabled = enabled;
                scenes[idx] = entry;
            }
            else
            {
                var newEntry = new EditorBuildSettingsScene(scenePath, enabled);
                if (insertAtStart)
                {
                    var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
                    list.Insert(0, newEntry);
                    scenes = list.ToArray();
                }
                else
                {
                    var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
                    list.Add(newEntry);
                    scenes = list.ToArray();
                }
            }

            EditorBuildSettings.scenes = scenes;
        }
    }

    /// <summary>
    /// 只是用来在 Boot 场景里留一个“这个场景是引导场景”的标记。
    /// 真正的 UI/逻辑由 RuntimeInitializeOnLoadMethod 自动创建（BootFlowAutoStart）。
    /// </summary>
    public sealed class BootSceneMarker : MonoBehaviour
    {
        [SerializeField] private string note = "Boot scene marker. Actual UI is spawned by BootFlowAutoStart.";
    }
}

