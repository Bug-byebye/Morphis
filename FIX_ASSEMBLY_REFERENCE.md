# 程序集引用错误修复

## 问题
```
Assets\Scripts\Bootstrap\AppBootstrap.cs(4,15): error CS0234: 
The type or namespace name 'AppFlow' does not exist in the namespace 'Morphis'
```

## 根本原因
- `AppBootstrap.cs` 在独立程序集 `Morphis.Bootstrap` 中
- `AppSession.cs` 在默认程序集 `Assembly-CSharp` 中
- Unity 的独立程序集不能引用默认程序集中的类型

## 解决方案
为 `AppFlow` 目录创建独立的程序集定义，并建立正确的引用关系。

### 1. 创建 AppFlow 程序集定义
**文件**: `Assets/Scripts/AppFlow/AppFlow.asmdef`

```json
{
  "name": "Morphis.AppFlow",
  "rootNamespace": "Morphis.AppFlow",
  "references": [
    "Morphis.Config",
    "Unity.TextMeshPro",
    "Mirror",
    "Unity.InputSystem"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

### 2. 更新 Bootstrap 程序集定义
**文件**: `Assets/Scripts/Bootstrap/Bootstrap.asmdef`

添加对 `Morphis.AppFlow` 的引用：

```json
{
  "name": "Morphis.Bootstrap",
  "rootNamespace": "Morphis",
  "references": [
    "Morphis.Config",
    "Morphis.AppFlow",  // 新增
    "Mirror",
    "Mirror.Transports"
  ],
  ...
}
```

## 程序集依赖关系

```
Assembly-CSharp (默认程序集)
    ↓ 可以引用所有独立程序集
    
Morphis.Bootstrap
    ↓ 引用
    ├─ Morphis.Config
    ├─ Morphis.AppFlow
    ├─ Mirror
    └─ Mirror.Transports
    
Morphis.AppFlow
    ↓ 引用
    ├─ Morphis.Config
    ├─ Unity.TextMeshPro
    ├─ Mirror
    └─ Unity.InputSystem
    
Morphis.Config
    └─ (无外部引用)
```

## 验证步骤

1. Unity 会自动检测到新的 `.asmdef` 文件
2. 等待 Unity 重新编译所有程序集
3. 检查 Console 是否还有编译错误
4. 如果出现其他程序集引用错误，继续添加必要的引用

## 注意事项

- 程序集定义文件 (`.asmdef`) 必须放在对应目录的根目录
- 修改 `.asmdef` 后，Unity 会自动重新编译
- 独立程序集可以提高编译速度（只重新编译修改的程序集）
- 避免循环引用（A 引用 B，B 又引用 A）

## 状态
✅ 已创建程序集定义
✅ 已更新 Bootstrap 程序集引用
⏳ 等待 Unity 重新编译

## 预期结果
- `Morphis.AppFlow` 程序集将被创建
- `Morphis.Bootstrap` 可以引用 `Morphis.AppFlow`
- 默认程序集 `Assembly-CSharp` 可以引用所有独立程序集
- 编译错误应该消失

## 如果仍有错误
检查以下内容：
1. Unity 是否完成了程序集重新编译（查看 Console 底部进度条）
2. 是否有其他文件也需要程序集引用
3. 检查 `.asmdef` 文件的 JSON 格式是否正确
