把“可放置的模型资源”放到本目录下：

Assets/Resources/Placeables/*.prefab
Assets/Resources/Placeables/*.glb

运行 `Assets/StarterAssets/ThirdPersonController/Scenes/Playground.unity` 时，
左侧“Models”按钮展开的模型库会自动读取并列出这些 prefab。

说明：
- 这里使用 Resources 目录是为了最省事地在运行时发现资源（Resources.LoadAll）。
- 后续做正式项目时，更推荐改成 Addressables + 资源清单（更适合大型项目与热更新）。

