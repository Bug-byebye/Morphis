# 配置文件设置指南

## 概述

Morphis 项目使用 `config.json` 文件来配置网络地址、后端 API 和世界 ID。该文件是**强制必需**的，缺失或配置错误会导致应用无法启动。

## 配置文件位置

配置文件会按以下优先级查找：

1. **StreamingAssets 目录**（推荐用于打包后的构建）
   - Windows: `<游戏目录>/游戏名_Data/StreamingAssets/config.json`
   - Linux: `<游戏目录>/游戏名_Data/StreamingAssets/config.json`

2. **项目根目录**（推荐用于开发环境）
   - 位置: `<项目根目录>/config.json`
   - 与 `Assets/` 文件夹同级

## 快速开始

### 开发环境配置

1. 复制示例文件：
   ```bash
   cp config.json.example config.json
   ```

2. 编辑 `config.json`，使用本地开发配置：
   ```json
   {
     "GameServerAddress": "127.0.0.1",
     "GameServerPort": 7777,
     "ApiBaseUrl": "http://127.0.0.1:8000",
     "DefaultWorldId": "dev-world"
   }
   ```

3. 启动 Python 后端：
   ```bash
   cd Backend
   python server.py
   ```

4. 在 Unity 中运行项目

### 生产环境配置

#### 客户端配置

1. 创建 `config.json`：
   ```json
   {
     "GameServerAddress": "game-server.yourdomain.com",
     "GameServerPort": 7777,
     "ApiBaseUrl": "https://api.yourdomain.com",
     "DefaultWorldId": "prod-world-001"
   }
   ```

2. 将文件放置在：
   - **方式 1**（推荐）: 复制到 `Assets/StreamingAssets/config.json`，然后构建
   - **方式 2**: 构建后手动复制到 `<游戏目录>/游戏名_Data/StreamingAssets/config.json`

#### 服务器配置

1. 创建 `config.json`：
   ```json
   {
     "GameServerAddress": "0.0.0.0",
     "GameServerPort": 7777,
     "ApiBaseUrl": "https://api.yourdomain.com",
     "DefaultWorldId": "prod-world-001"
   }
   ```

2. 将文件放置在服务器构建的根目录（与可执行文件同级）

3. 使用命令行参数启动：
   ```bash
   ./MorphisServer.x86_64 --mode=server --worldId=prod-world-001
   ```

## 配置字段说明

| 字段 | 类型 | 必填 | 说明 | 示例 |
|------|------|------|------|------|
| `GameServerAddress` | string | ✅ | 游戏服务器地址。客户端连接此地址，服务器监听此地址 | `"game.example.com"` 或 `"127.0.0.1"` |
| `GameServerPort` | number | ✅ | 游戏服务器端口。必须 > 0 | `7777` |
| `ApiBaseUrl` | string | ✅ | Python 后端 API 地址（用于登录、世界数据等） | `"https://api.example.com"` |
| `DefaultWorldId` | string | ✅ | 默认世界 ID。如果命令行未指定 `--worldId`，则使用此值 | `"default-world"` |

## 配置验证

应用启动时会自动验证配置文件：

- ✅ 文件存在且可读
- ✅ JSON 格式正确
- ✅ 所有必填字段存在且非空
- ✅ `GameServerPort` 为正整数

如果验证失败：
- **开发环境（Unity Editor）**: 抛出异常并停止运行
- **生产环境（构建后）**: 记录错误日志并退出应用

## 常见问题

### Q: 为什么不能使用硬编码的默认值？

A: 为了安全和灵活性：
- 避免将开发环境地址（如 `localhost`）打包到生产构建中
- 强制开发者明确配置网络地址
- 防止意外连接到错误的服务器

### Q: 如何在不同环境使用不同配置？

A: 推荐方式：
1. 创建多个配置文件：`config.dev.json`, `config.staging.json`, `config.prod.json`
2. 构建前复制对应文件为 `config.json`
3. 或使用构建脚本自动化此过程

### Q: 客户端和服务器的配置有什么区别？

A: 主要区别：
- **客户端**: `GameServerAddress` 是要连接的服务器地址（域名或 IP）
- **服务器**: `GameServerAddress` 通常是 `"0.0.0.0"`（监听所有网卡）或 `"127.0.0.1"`（仅本地）

### Q: 如何在运行时修改配置？

A: 不推荐在运行时修改配置文件。如需动态配置：
- 使用命令行参数覆盖（如 `--worldId`）
- 或在代码中直接修改 `AppConfig.Instance` 的属性（仅限特殊场景）

## 安全建议

1. **不要将生产环境配置提交到版本控制**
   - 将 `config.json` 添加到 `.gitignore`
   - 仅提交 `config.json.example` 作为模板

2. **使用 HTTPS**
   - 生产环境的 `ApiBaseUrl` 必须使用 HTTPS
   - 避免在公网传输明文数据

3. **限制服务器访问**
   - 使用防火墙限制 `GameServerPort` 的访问
   - 仅允许可信 IP 访问管理接口

## 部署检查清单

部署前请确认：

- [ ] `config.json` 文件存在于正确位置
- [ ] 所有字段填写正确（无占位符如 `your-server.example.com`）
- [ ] `GameServerAddress` 可从客户端网络访问
- [ ] `GameServerPort` 未被防火墙阻止
- [ ] `ApiBaseUrl` 指向正确的后端服务
- [ ] Python 后端服务已启动并可访问
- [ ] 服务器使用 `--mode=server` 参数启动
- [ ] 客户端构建不包含 `--mode=server` 参数

## 示例配置

### 本地开发（单机测试）

```json
{
  "GameServerAddress": "127.0.0.1",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://127.0.0.1:8000",
  "DefaultWorldId": "local-test"
}
```

### 局域网测试

```json
{
  "GameServerAddress": "192.168.1.100",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://192.168.1.100:8000",
  "DefaultWorldId": "lan-test"
}
```

### 生产环境（云服务器）

**客户端配置**:
```json
{
  "GameServerAddress": "game.morphis.io",
  "GameServerPort": 7777,
  "ApiBaseUrl": "https://api.morphis.io",
  "DefaultWorldId": "world-001"
}
```

**服务器配置**:
```json
{
  "GameServerAddress": "0.0.0.0",
  "GameServerPort": 7777,
  "ApiBaseUrl": "https://api.morphis.io",
  "DefaultWorldId": "world-001"
}
```

## 技术细节

### 加载时机

配置文件在 Unity 启动的最早阶段加载：
```
RuntimeInitializeLoadType.BeforeSceneLoad
  → ConfigLoader.LoadOnStartup()
  → 验证并设置 AppConfig.Instance
```

### 代码访问

```csharp
// 读取配置
var config = Morphis.Config.AppConfig.Instance;
string serverAddress = config.GameServerAddress;
int serverPort = config.GameServerPort;
string apiUrl = config.ApiBaseUrl;
string worldId = config.DefaultWorldId;

// AppSession 会自动从配置初始化
string baseUrl = AppSession.BaseUrl; // 自动读取 config.ApiBaseUrl
```

### 错误处理

```csharp
// 配置加载失败时的行为
private static void FailFast(string message)
{
    Debug.LogError($"[ConfigLoader] {message}");
    #if UNITY_EDITOR
        throw new Exception(message); // 编辑器中抛出异常
    #else
        Application.Quit(); // 构建版本中退出应用
    #endif
}
```

## 相关文件

- `config.json.example` - 配置模板
- `Assets/Scripts/Config/ConfigLoader.cs` - 配置加载器
- `Assets/Scripts/Config/AppConfig.cs` - 配置数据结构
- `Assets/Scripts/AppFlow/AppSession.cs` - 会话管理（使用 ApiBaseUrl）
- `Assets/Scripts/Bootstrap/AppBootstrap.cs` - 网络启动（使用 GameServerAddress/Port）

## 更新日志

- **2026-02-25**: 移除 `AppSession.BaseUrl` 硬编码默认值，强制从配置文件读取
- **2026-02-25**: 创建配置文件文档和示例
