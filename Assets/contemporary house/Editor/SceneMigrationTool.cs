using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Morphis.Editor
{
    /// <summary>
    /// 场景迁移工具：将主场景从第三方资源包目录迁移到项目标准目录
    /// 
    /// 大厂标准：
    /// - 主场景应位于 Assets/Game/Scenes/ 或 Assets/Scenes/
    /// - 不应放在第三方资源包目录（如 StarterAssets）
    /// - 场景命名应清晰（如 MainScene.unity）
    /// 
    /// 注意：登录和空间选择界面不是场景，而是通过 BootFlowAutoStart 在运行时自动创建的 UI
    /// </summary>
    public static class SceneMigrationTool
    {
        // 源场景路径（第三方资源包）
        private const string SourceScenePath = "Assets/StarterAssets/ThirdPersonController/Scenes/Playground.unity";
        
        // 目标场景路径（项目标准目录）
        private const string TargetScenePath = "Assets/Game/Scenes/MainScene.unity";
        
        // Boot 场景路径（旧位置）
        private const string OldBootScenePath = "Assets/Game/Scenes/BootScene.unity";
        
        // Boot 场景路径（新位置）
        private const string NewBootScenePath = "Assets/Game/Scenes/BootScene.unity";
        
        // 旧场景名称（用于代码中的字符串引用）
        private const string OldSceneName = "Playground";
        
        // 新场景名称
        private const string NewSceneName = "MainScene";

        [MenuItem("Tools/Morphis/场景迁移/迁移主场景到标准目录", false, 1)]
        public static void MigrateMainScene()
        {
            // 转换为系统路径
            string sourceSystemPath = Path.Combine(Application.dataPath, "StarterAssets", "ThirdPersonController", "Scenes", "Playground.unity");
            string targetSystemPath = Path.Combine(Application.dataPath, "Game", "Scenes", "MainScene.unity");
            
            // 检查源场景是否存在
            if (!File.Exists(sourceSystemPath))
            {
                EditorUtility.DisplayDialog("错误", 
                    $"源场景不存在：\n{SourceScenePath}\n\n系统路径：{sourceSystemPath}\n\n请确认场景路径是否正确。", 
                    "确定");
                return;
            }

            // 检查目标场景是否已存在
            if (File.Exists(targetSystemPath))
            {
                bool overwrite = EditorUtility.DisplayDialog("场景已存在", 
                    $"目标场景已存在：\n{TargetScenePath}\n\n是否覆盖？", 
                    "覆盖", "取消");
                
                if (!overwrite)
                {
                    return;
                }
                
                // 删除已存在的文件
                File.Delete(targetSystemPath);
                // 如果存在 .meta 文件也删除
                string metaPath = targetSystemPath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }

            // 确保目标目录存在
            string targetDir = Path.GetDirectoryName(targetSystemPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // 使用 File.Copy 复制场景文件
            try
            {
                File.Copy(sourceSystemPath, targetSystemPath, true);
                
                // 复制 .meta 文件（如果存在）
                string sourceMetaPath = sourceSystemPath + ".meta";
                string targetMetaPath = targetSystemPath + ".meta";
                if (File.Exists(sourceMetaPath))
                {
                    File.Copy(sourceMetaPath, targetMetaPath, true);
                }
                
                // 刷新 AssetDatabase
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                
                Debug.Log($"[Morphis][场景迁移] 场景文件已复制：{sourceSystemPath} -> {targetSystemPath}");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("错误", 
                    $"复制场景失败！\n\n错误信息：{ex.Message}\n\n源路径：{SourceScenePath}\n目标路径：{TargetScenePath}", 
                    "确定");
                Debug.LogError($"[Morphis][场景迁移] 复制失败：{ex}");
                return;
            }

            // 更新 Build Settings
            UpdateBuildSettings();

            // 显示更新代码引用的提示
            bool updateCode = EditorUtility.DisplayDialog("场景迁移成功", 
                $"场景已成功迁移到：\n{TargetScenePath}\n\n" +
                $"是否自动更新代码中的场景名称引用？\n" +
                $"(将 'Playground' 替换为 'MainScene')", 
                "更新代码", "稍后手动更新");

            if (updateCode)
            {
                UpdateCodeReferences();
            }

            Debug.Log($"[Morphis][场景迁移] 主场景已迁移：{SourceScenePath} -> {TargetScenePath}");
        }

        [MenuItem("Tools/Morphis/场景迁移/更新代码中的场景名称引用", false, 2)]
        public static void UpdateCodeReferences()
        {
            string[] scriptGuids = AssetDatabase.FindAssets("t:Script", new[] { "Assets/Scripts" });
            int updatedCount = 0;

            foreach (string guid in scriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs"))
                    continue;

                string fullPath = Path.Combine(Application.dataPath, path.Replace("Assets/", ""));
                if (!File.Exists(fullPath))
                    continue;

                string content = File.ReadAllText(fullPath);
                string originalContent = content;

                // 替换硬编码的场景名称
                // 注意：只替换字符串字面量，避免误替换注释或变量名
                content = System.Text.RegularExpressions.Regex.Replace(
                    content,
                    @"(""|')Playground(""|')",
                    m => m.Groups[1].Value + NewSceneName + m.Groups[2].Value,
                    System.Text.RegularExpressions.RegexOptions.None);

                // 替换场景路径引用（如果存在）
                content = content.Replace(
                    "Assets/StarterAssets/ThirdPersonController/Scenes/Playground.unity",
                    TargetScenePath);

                if (content != originalContent)
                {
                    File.WriteAllText(fullPath, content);
                    updatedCount++;
                    Debug.Log($"[Morphis][场景迁移] 已更新：{path}");
                }
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("代码更新完成", 
                $"已更新 {updatedCount} 个脚本文件中的场景名称引用。\n\n" +
                $"请检查更改并测试项目。", 
                "确定");

            Debug.Log($"[Morphis][场景迁移] 代码引用更新完成，共更新 {updatedCount} 个文件");
        }

        [MenuItem("Tools/Morphis/场景迁移/更新 Build Settings", false, 3)]
        public static void UpdateBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // 移除旧的 Playground 场景
            scenes.RemoveAll(s => s.path == SourceScenePath);

            // 添加新的 MainScene 场景（如果不存在）
            bool exists = false;
            foreach (var scene in scenes)
            {
                if (scene.path == TargetScenePath)
                {
                    exists = true;
                    if (!scene.enabled)
                    {
                        var entry = scene;
                        entry.enabled = true;
                        int index = scenes.IndexOf(scene);
                        scenes[index] = entry;
                    }
                    break;
                }
            }

            if (!exists)
            {
                scenes.Add(new EditorBuildSettingsScene(TargetScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[Morphis][场景迁移] Build Settings 已更新");
        }

        [MenuItem("Tools/Morphis/场景迁移/验证场景迁移状态", false, 4)]
        public static void ValidateMigration()
        {
            string sourceSystemPath = Path.Combine(Application.dataPath, "StarterAssets", "ThirdPersonController", "Scenes", "Playground.unity");
            string targetSystemPath = Path.Combine(Application.dataPath, "Game", "Scenes", "MainScene.unity");
            
            bool sourceExists = File.Exists(sourceSystemPath);
            bool targetExists = File.Exists(targetSystemPath);
            bool inBuildSettings = false;

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path == TargetScenePath && scene.enabled)
                {
                    inBuildSettings = true;
                    break;
                }
            }

            string message = "场景迁移状态：\n\n";
            message += $"源场景存在: {(sourceExists ? "✓" : "✗")}\n";
            message += $"目标场景存在: {(targetExists ? "✓" : "✗")}\n";
            message += $"在 Build Settings 中: {(inBuildSettings ? "✓" : "✗")}\n\n";

            if (targetExists && inBuildSettings)
            {
                message += "✓ 迁移状态正常";
            }
            else
            {
                message += "⚠ 需要完成迁移";
            }

            EditorUtility.DisplayDialog("场景迁移验证", message, "确定");
        }

        [MenuItem("Tools/Morphis/场景迁移/创建/迁移 Boot 场景到标准目录", false, 5)]
        public static void CreateOrMigrateBootScene()
        {
            string oldBootSystemPath = Path.Combine(Application.dataPath, "Scenes", "Boot.unity");
            string newBootSystemPath = Path.Combine(Application.dataPath, "Game", "Scenes", "BootScene.unity");
            
            // 检查旧位置是否存在 Boot 场景
            bool oldExists = File.Exists(oldBootSystemPath);
            bool newExists = File.Exists(newBootSystemPath);

            if (newExists)
            {
                EditorUtility.DisplayDialog("提示", 
                    $"Boot 场景已存在于标准位置：\n{NewBootScenePath}\n\n无需迁移。", 
                    "确定");
                return;
            }

            if (oldExists)
            {
                // 迁移现有 Boot 场景
                bool migrate = EditorUtility.DisplayDialog("发现 Boot 场景", 
                    $"在旧位置发现 Boot 场景：\n{OldBootScenePath}\n\n是否迁移到标准位置？\n{NewBootScenePath}", 
                    "迁移", "取消");
                
                if (!migrate)
                {
                    return;
                }

                // 确保目标目录存在
                string targetDir = Path.GetDirectoryName(newBootSystemPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                try
                {
                    // 复制场景文件
                    File.Copy(oldBootSystemPath, newBootSystemPath, true);
                    
                    // 复制 .meta 文件
                    string oldMetaPath = oldBootSystemPath + ".meta";
                    string newMetaPath = newBootSystemPath + ".meta";
                    if (File.Exists(oldMetaPath))
                    {
                        File.Copy(oldMetaPath, newMetaPath, true);
                    }
                    
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
                    
                    Debug.Log($"[Morphis][场景迁移] Boot 场景已迁移：{OldBootScenePath} -> {NewBootScenePath}");
                }
                catch (System.Exception ex)
                {
                    EditorUtility.DisplayDialog("错误", 
                        $"迁移 Boot 场景失败！\n\n错误信息：{ex.Message}", 
                        "确定");
                    Debug.LogError($"[Morphis][场景迁移] 迁移 Boot 场景失败：{ex}");
                    return;
                }
            }
            else
            {
                // 创建新的 Boot 场景
                bool create = EditorUtility.DisplayDialog("创建 Boot 场景", 
                    $"Boot 场景不存在。\n\n是否在标准位置创建新的 Boot 场景？\n{NewBootScenePath}\n\n" +
                    $"注意：登录和空间选择界面由 BootFlowAutoStart 自动创建，\n" +
                    $"Boot 场景只是一个空场景作为启动场景。", 
                    "创建", "取消");
                
                if (!create)
                {
                    return;
                }

                CreateBootScene();
            }

            // 更新 Build Settings 和代码引用
            UpdateBootSceneBuildSettings();
            UpdateBootSceneCodeReferences();

            EditorUtility.DisplayDialog("完成", 
                $"Boot 场景已设置完成：\n{NewBootScenePath}\n\n" +
                $"已更新 Build Settings 和代码引用。", 
                "确定");
        }

        private static void CreateBootScene()
        {
            string newBootSystemPath = Path.Combine(Application.dataPath, "Game", "Scenes", "BootScene.unity");
            
            // 确保目录存在
            string targetDir = Path.GetDirectoryName(newBootSystemPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // 创建空场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            // 添加标记物体
            var go = new GameObject("BootSceneMarker");
            var marker = go.AddComponent<BootSceneMarkerComponent>();
            
            // 添加一个简单的 2D 摄像机（只用于背景，不渲染任何 3D 内容）
            var cameraGO = new GameObject("BootUICamera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1.0f); // 深色背景，与 BootFlowManager 默认背景一致
            camera.cullingMask = 0; // 不渲染任何 Layer，只显示纯色背景
            camera.depth = -1; // 确保在 UI Canvas 下方（Canvas 使用 ScreenSpaceOverlay，会在最上层）
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10f;
            
            // 保存场景
            EditorSceneManager.SaveScene(scene, NewBootScenePath);
            AssetDatabase.Refresh();
            
            Debug.Log($"[Morphis][场景迁移] Boot 场景已创建（包含 2D UI 摄像机）：{NewBootScenePath}");
        }

        private static void UpdateBootSceneBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // 移除旧位置的 Boot 场景
            scenes.RemoveAll(s => s.path == OldBootScenePath);

            // 添加新位置的 Boot 场景到第一个位置（如果不存在）
            bool exists = false;
            foreach (var scene in scenes)
            {
                if (scene.path == NewBootScenePath)
                {
                    exists = true;
                    // 如果存在但不在第一个位置，移动到第一个
                    if (scenes.IndexOf(scene) != 0)
                    {
                        scenes.Remove(scene);
                        scenes.Insert(0, new EditorBuildSettingsScene(NewBootScenePath, true));
                    }
                    break;
                }
            }

            if (!exists)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(NewBootScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[Morphis][场景迁移] Boot 场景 Build Settings 已更新");
        }

        private static void UpdateBootSceneCodeReferences()
        {
            // 更新 AppFlowSetup.cs 中的路径
            string[] scriptGuids = AssetDatabase.FindAssets("t:Script", new[] { "Assets/Editor" });
            int updatedCount = 0;

            foreach (string guid in scriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs"))
                    continue;

                string fullPath = Path.Combine(Application.dataPath, path.Replace("Assets/", ""));
                if (!File.Exists(fullPath))
                    continue;

                string content = File.ReadAllText(fullPath);
                string originalContent = content;

                // 替换 Boot 场景路径
                content = content.Replace(OldBootScenePath, NewBootScenePath);
                content = content.Replace("Assets/Game/Scenes/BootScene.unity", NewBootScenePath);

                if (content != originalContent)
                {
                    File.WriteAllText(fullPath, content);
                    updatedCount++;
                    Debug.Log($"[Morphis][场景迁移] 已更新 Boot 场景路径：{path}");
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Morphis][场景迁移] Boot 场景代码引用更新完成，共更新 {updatedCount} 个文件");
        }

        [MenuItem("Tools/Morphis/场景迁移/修复 Boot 场景（添加摄像机）", false, 6)]
        public static void FixBootSceneCamera()
        {
            string bootScenePath = NewBootScenePath;
            string bootSystemPath = Path.Combine(Application.dataPath, "Game", "Scenes", "BootScene.unity");
            
            if (!File.Exists(bootSystemPath))
            {
                EditorUtility.DisplayDialog("错误", 
                    $"Boot 场景不存在：\n{bootScenePath}\n\n请先创建 Boot 场景。", 
                    "确定");
                return;
            }

            // 打开场景
            var scene = EditorSceneManager.OpenScene(bootScenePath, OpenSceneMode.Single);
            
            // 检查是否已有摄像机
            Camera existingCamera = Object.FindFirstObjectByType<Camera>();
            if (existingCamera != null)
            {
                bool replace = EditorUtility.DisplayDialog("摄像机已存在", 
                    $"Boot 场景中已存在摄像机：{existingCamera.name}\n\n是否替换为标准的 2D UI 摄像机？", 
                    "替换", "取消");
                
                if (replace)
                {
                    Object.DestroyImmediate(existingCamera.gameObject);
                }
                else
                {
                    EditorUtility.DisplayDialog("取消", "操作已取消。", "确定");
                    return;
                }
            }

            // 创建新的 2D UI 摄像机
            var cameraGO = new GameObject("BootUICamera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1.0f); // 深色背景，与 BootFlowManager 默认背景一致
            camera.cullingMask = 0; // 不渲染任何 Layer，只显示纯色背景
            camera.depth = -1; // 确保在 UI Canvas 下方（Canvas 使用 ScreenSpaceOverlay，会在最上层）
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10f;

            // 保存场景
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("完成", 
                $"Boot 场景已修复！\n\n已添加 2D UI 摄像机（只渲染纯色背景，不渲染任何 3D 内容）。", 
                "确定");
            
            Debug.Log($"[Morphis][场景迁移] Boot 场景已修复，添加了 2D UI 摄像机");
        }

        [MenuItem("Tools/Morphis/场景迁移/一键迁移所有场景", false, 10)]
        public static void MigrateAllScenes()
        {
            bool proceed = EditorUtility.DisplayDialog("一键迁移", 
                "此操作将：\n\n" +
                "1. 迁移主场景（Playground -> MainScene）\n" +
                "2. 创建/迁移 Boot 场景到标准位置\n" +
                "3. 更新所有代码引用\n" +
                "4. 更新 Build Settings\n\n" +
                "是否继续？", 
                "继续", "取消");
            
            if (!proceed)
            {
                return;
            }

            // 迁移主场景
            MigrateMainScene();
            
            // 创建/迁移 Boot 场景
            CreateOrMigrateBootScene();

            EditorUtility.DisplayDialog("完成", 
                "所有场景迁移完成！\n\n" +
                "请检查 Build Settings 和测试项目。", 
                "确定");
        }
    }

    /// <summary>
    /// Boot 场景标记组件（用于标识这是启动场景）
    /// </summary>
    public sealed class BootSceneMarkerComponent : MonoBehaviour
    {
        [SerializeField] private string note = "Boot scene marker. Actual login/workspace UI is spawned by BootFlowAutoStart at runtime.";
    }
}
