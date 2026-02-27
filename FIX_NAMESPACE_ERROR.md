# 命名空间错误修复

## 问题
```
Assets\Scripts\Bootstrap\AppBootstrap.cs(119,43): error CS0234: 
The type or namespace name 'AppFlow' does not exist in the namespace 'Morphis'
```

## 原因
`AppBootstrap.cs` 在 `Morphis` 命名空间下，但直接使用了 `Morphis.AppFlow.AppSession`，缺少 using 语句。

## 修复
在 `Assets/Scripts/Bootstrap/AppBootstrap.cs` 顶部添加：
```csharp
using Morphis.AppFlow;
```

然后将代码中的 `Morphis.AppFlow.AppSession` 改为 `AppSession`。

## 验证
所有相关文件编译通过：
- ✅ AppBootstrap.cs
- ✅ AppSession.cs
- ✅ ConfigLoader.cs
- ✅ HttpWorldService.cs

## 状态
✅ 已修复
