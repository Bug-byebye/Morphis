using UnityEngine;

namespace Morphis.AppFlow
{
    /// <summary>
    /// 启动时自动创建登录 + 空间选择流程（BootFlowManager）。
    ///
    /// 设计目标：
    /// - 无论你当前的“首场景”是哪个（SampleScene / Playground / 其它），
    ///   一启动就先看到 2D 登录/空间选择界面；
    /// - 该界面为全屏不透明 UI，完全遮挡底层 3D 场景；
    /// - 登录 + 选空间后，BootFlowManager 再按选择加载目标 3D 场景（如 Playground）。
    ///
    /// 注意：底层首场景在技术上仍会被 Unity 加载，但对玩家是“不可见”的。
    /// 如需绝对极简的首场景，可以在 Build Settings 中将首场景设置为一个空场景。
    /// </summary>
    public static class BootFlowAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateBootFlow()
        {
            // 避免重复创建
            if (Object.FindFirstObjectByType<BootFlowManager>() != null) return;

            Debug.Log("[BootFlow] Creating BootFlowManager after scene load...");
            var go = new GameObject("BootFlowManager(Auto)");
            go.AddComponent<BootFlowManager>();
        }
    }
}

