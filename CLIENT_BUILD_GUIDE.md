# 客户端构建指南

## 概述

本指南说明如何为不同环境（开发/测试/生产）构建 Morphis 客户端。

---

## 构建前准备

### 1. 确认项目状态

- [ ] Unity 项目可正常运行
- [ ] 本地测试通过
- [ ] 服务器已部署并运行

### 2. 准备配置文件

根据目标环境选择配置：

#### 开发环境（本地测试）
```json
{
  "GameServerAddress": "127.0.0.1",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://127.0.0.1:8000",
  "DefaultWorldId": "dev-world"
}
```

#### 测试环境（局域网）
```json
{
  "GameServerAddress": "192.168.1.100",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://192.168.1.100:8000",
  "DefaultWorldId": "test-world"
}
```

#### 生产环境（云服务器）
```json
{
  "GameServerAddress": "game.yourdomain.com",
  "GameServerPort": 7777,
  "ApiBaseUrl": "https://api.yourdomain.com",
  "DefaultWorldId": "prod-world-001"
}
```

---

## 构建步骤

### 方式 A: 使用 StreamingAssets（推荐）

这种方式将配置文件打包到客户端中，用户无需手动配置。

#### 1. 复制配置文件

```bash
# Windows (PowerShell)
Copy-Item config.json Assets\StreamingAssets\config.json

# 或手动复制
# 从项目根目录的 config.json
# 复制到 Assets/StreamingAssets/config.json
```

#### 2. 验证配置

打开 `Assets/StreamingAssets/config.json`，确认内容正确。

#### 3. 构建客户端

1. 打开 Unity
2. `File > Build Settings`
3. 确保平台为 `Windows`
4. 确保 `Dedicated Server` **未勾选**
5. 点击 `Build`
6. 选择输出目录（如 `Builds/WindowsClient`）
7. 等待构建完成

#### 4. 测试构建

1. 进入构建目录
2. 运行 `.exe` 文件
3. 验证可以连接到服务器

---

### 方式 B: 外部配置文件

这种方式允许用户自行修改配置，适合需要灵活配置的场景。

#### 1. 构建客户端（不包含配置）

1. 确保 `Assets/StreamingAssets/config.json` **不存在**或为空模板
2. 按照上述步骤构建

#### 2. 创建配置文件

在构建输出目录创建 `config.json`:

```
Builds/WindowsClient/
├── Morphis.exe
├── Morphis_Data/
├── UnityPlayer.dll
└── config.json  ← 在这里创建
```

#### 3. 分发说明

提供给用户的说明：

```
1. 解压游戏文件
2. 编辑 config.json，填入服务器地址
3. 运行 Morphis.exe
```

---

## 多环境构建策略

### 策略 1: 分别构建

为每个环境单独构建：

```
Builds/
├── Dev/          # 开发环境
├── Test/         # 测试环境
└── Production/   # 生产环境
```

每次构建前切换配置文件。

### 策略 2: 使用构建脚本

创建自动化构建脚本（Unity Editor Script）：

```csharp
// Assets/Editor/BuildScript.cs
using UnityEditor;
using System.IO;

public class BuildScript
{
    [MenuItem("Build/Build Development")]
    public static void BuildDevelopment()
    {
        CopyConfig("config.dev.json");
        BuildClient("Builds/Dev");
    }

    [MenuItem("Build/Build Production")]
    public static void BuildProduction()
    {
        CopyConfig("config.prod.json");
        BuildClient("Builds/Production");
    }

    static void CopyConfig(string sourceFile)
    {
        string dest = "Assets/StreamingAssets/config.json";
        File.Copy(sourceFile, dest, true);
        AssetDatabase.Refresh();
    }

    static void BuildClient(string outputPath)
    {
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/BootScene.unity" },
            locationPathName = outputPath + "/Morphis.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildPipeline.BuildPlayer(options);
    }
}
```

---

## 构建优化

### 1. 减小构建体积

在 `Build Settings` 中：
- 取消勾选 `Development Build`
- 勾选 `Compression Method: LZ4` 或 `LZ4HC`

在 `Player Settings` 中：
- `Managed Stripping Level`: `High`
- `Strip Engine Code`: 勾选

### 2. 优化启动速度

- 减少首场景资源加载
- 使用 Addressables 异步加载资源
- 优化 Shader 编译

### 3. 代码混淆（可选）

使用 IL2CPP 而非 Mono：
- `Player Settings > Configuration > Scripting Backend`: `IL2CPP`

---

## 分发准备

### 1. 创建安装包

使用 Inno Setup 或 NSIS 创建安装程序：

```
Morphis_Setup_v1.0.exe
├── 游戏文件
├── 配置文件
├── 运行时依赖（如 VC++ Redistributable）
└── 卸载程序
```

### 2. 准备文档

创建 `README.txt`:

```
Morphis 游戏客户端
==================

系统要求：
- Windows 10 64-bit 或更高
- 4 GB RAM
- 2 GB 可用磁盘空间
- 稳定的网络连接

安装步骤：
1. 解压所有文件到任意目录
2. 运行 Morphis.exe

配置服务器：
如需连接到自定义服务器，请编辑 config.json 文件。

故障排查：
- 无法连接：检查 config.json 中的服务器地址
- 闪退：查看日志文件（位于 %APPDATA%/../LocalLow/YourCompany/Morphis/）

联系支持：
Email: support@example.com
```

### 3. 版本管理

在文件名中包含版本号：

```
Morphis_v1.0.0_Windows.zip
Morphis_v1.0.1_Windows.zip
```

---

## 测试检查清单

构建后必须测试：

- [ ] 客户端可以启动
- [ ] 可以连接到服务器
- [ ] 可以登录
- [ ] 可以创建/加入空间
- [ ] 可以放置和移动物体
- [ ] 可以保存世界数据
- [ ] 多个客户端可以互相看到
- [ ] 断线重连正常
- [ ] 性能流畅（60 FPS+）
- [ ] 无明显 Bug

---

## 常见问题

### Q: 构建后配置文件在哪里？

A: 
- 如果使用 StreamingAssets: `Morphis_Data/StreamingAssets/config.json`
- 如果使用外部配置: 与 `.exe` 同级目录的 `config.json`

### Q: 如何让用户自己配置服务器？

A: 
1. 不要将 `config.json` 放入 StreamingAssets
2. 在构建输出目录创建 `config.json.example`
3. 提供配置说明文档

### Q: 如何更新客户端配置而不重新构建？

A: 使用外部配置文件方式，用户可以直接编辑 `config.json`。

### Q: 构建后体积很大怎么办？

A: 
1. 检查是否包含了不必要的资源
2. 使用 Addressables 按需加载
3. 启用代码剥离（Stripping）
4. 使用压缩

### Q: 如何支持多语言？

A: 
1. 使用 Unity Localization Package
2. 在配置文件中添加 `Language` 字段
3. 根据配置加载对应语言资源

---

## 自动化构建（CI/CD）

### 使用 GitHub Actions

创建 `.github/workflows/build.yml`:

```yaml
name: Build Client

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v2
      
      - name: Setup Unity
        uses: game-ci/unity-builder@v2
        with:
          targetPlatform: StandaloneWindows64
          
      - name: Upload Build
        uses: actions/upload-artifact@v2
        with:
          name: Morphis-Windows
          path: build/
```

---

## 下一步

构建完成后：

1. 测试客户端
2. 准备分发包
3. 编写用户文档
4. 发布到分发平台（Steam、Itch.io 等）

---

## 相关文档

- `CONFIG_SETUP.md` - 配置文件详细说明
- `DEPLOYMENT_GUIDE.md` - 服务器部署指南
- `DEPLOYMENT_CHECKLIST.md` - 部署检查清单
