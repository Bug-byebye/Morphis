# 配置说明 - 新架构

## 配置文件变更

在多 World 动态调度架构中，配置文件已经简化。

### 旧架构 vs 新架构

#### 旧架构配置（已废弃）
```json
{
  "GameServerAddress": "your-server-ip",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://your-server-ip:8000",
  "DefaultWorldId": "default-world"
}
```

#### 新架构配置

**客户端配置** (`config.json`):
```json
{
  "ApiBaseUrl": "http://your-server-ip:8000"
}
```

**Unity Server 配置** (`config.json`):
```json
{
  "ApiBaseUrl": "http://localhost:8000",
  "ServerListenAddress": "0.0.0.0",
  "ServerPort": 7777,
  "DefaultWorldId": "default-world"
}
```

---

## 为什么简化？

### 客户端不再需要服务器地址和端口

**原因**:
- 服务器地址和端口由 Backend 动态分配
- 客户端通过 `/workspaces/join` API 获取连接信息
- 每个 World 可能在不同的端口上运行

**流程**:
```
客户端登录 → 选择 World → 调用 /workspaces/join
                              ↓
                    Backend 返回: {server_address, server_port}
                              ↓
                    客户端连接到指定地址
```

### Unity Server 端口由环境变量覆盖

**原因**:
- Backend 为每个 World 分配不同的端口
- 通过环境变量 `WORLD_PORT` 传递给 Unity Server
- 配置文件中的端口只是默认值

**流程**:
```
Backend 启动 World 进程
    ↓
设置环境变量: WORLD_PORT=7778
    ↓
Unity Server 读取环境变量并覆盖配置
```

---

## 配置字段说明

### ApiBaseUrl
- **类型**: string
- **必填**: ✅ 是
- **说明**: Backend API 地址
- **客户端**: 用于登录、获取 World 列表等
- **Server**: 用于上报玩家数量、保存 World 快照等
- **示例**: 
  - 开发: `http://127.0.0.1:8000`
  - 生产: `http://your-server-ip:8000`

### ServerListenAddress
- **类型**: string
- **必填**: ❌ 否（默认: `0.0.0.0`）
- **说明**: Unity Server 监听地址
- **仅 Server 使用**
- **推荐值**: `0.0.0.0`（监听所有网卡）
- **示例**: `0.0.0.0`

### ServerPort
- **类型**: number
- **必填**: ❌ 否（默认: `7777`）
- **说明**: Unity Server 默认端口
- **仅 Server 使用**
- **会被环境变量 `WORLD_PORT` 覆盖**
- **示例**: `7777`

### DefaultWorldId
- **类型**: string
- **必填**: ❌ 否（默认: `dev-world`）
- **说明**: 默认 World ID（开发模式使用）
- **仅 Server 使用**
- **生产环境由命令行参数 `--worldId` 指定**
- **示例**: `dev-world`

---

## 配置示例

### 开发环境

**客户端** (`config.json`):
```json
{
  "ApiBaseUrl": "http://127.0.0.1:8000"
}
```

**Unity Server** (直接 Play 测试):
```json
{
  "ApiBaseUrl": "http://127.0.0.1:8000",
  "ServerListenAddress": "0.0.0.0",
  "ServerPort": 7777,
  "DefaultWorldId": "dev-world"
}
```

### 生产环境

**客户端** (`config.json`):
```json
{
  "ApiBaseUrl": "http://123.45.67.89:8000"
}
```

**Unity Server** (`Assets/StreamingAssets/config.json`):
```json
{
  "ApiBaseUrl": "http://localhost:8000",
  "ServerListenAddress": "0.0.0.0",
  "ServerPort": 7777
}
```

**注意**: 
- Server 的 `ApiBaseUrl` 使用 `localhost`（内网访问）
- Server 的 `ServerPort` 会被环境变量覆盖
- Server 的 `DefaultWorldId` 不需要，由命令行参数指定

---

## 配置文件位置

### 客户端
- **开发**: 项目根目录 `config.json`
- **构建后**: 可执行文件同级目录 `config.json`

### Unity Server
- **开发**: 项目根目录 `config.json`
- **构建前**: `Assets/StreamingAssets/config.json`
- **构建后**: `Morphis_Data/StreamingAssets/config.json`

---

## 迁移指南

### 从旧配置迁移

**旧配置**:
```json
{
  "GameServerAddress": "123.45.67.89",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://123.45.67.89:8000",
  "DefaultWorldId": "prod-world"
}
```

**新配置（客户端）**:
```json
{
  "ApiBaseUrl": "http://123.45.67.89:8000"
}
```

**新配置（Server）**:
```json
{
  "ApiBaseUrl": "http://localhost:8000",
  "ServerListenAddress": "0.0.0.0",
  "ServerPort": 7777
}
```

### 删除的字段
- ❌ `GameServerAddress` - 客户端不再需要，Server 使用 `ServerListenAddress`
- ❌ `GameServerPort` - 客户端不再需要，Server 使用 `ServerPort`
- ❌ `DefaultWorldId` - 客户端不再需要，Server 可选

---

## 常见问题

### Q: 客户端如何知道连接哪个服务器？
A: 客户端通过 `/workspaces/join` API 获取动态分配的服务器地址和端口。

### Q: 如果 Backend 宕机，客户端还能连接吗？
A: 不能。客户端必须先通过 Backend 获取服务器连接信息。

### Q: Unity Server 的端口会冲突吗？
A: 不会。Backend 为每个 World 分配不同的端口（7777-7826）。

### Q: 开发时如何测试？
A: 
1. 启动 Backend: `cd Backend && python server.py`
2. 在 Unity 中直接 Play（会使用 localhost 连接）

### Q: 配置文件缺失会怎样？
A: 应用会拒绝启动并显示错误信息。

---

## 验证配置

### 客户端
```bash
# 检查配置文件
cat config.json

# 应该只包含 ApiBaseUrl
```

### Unity Server
```bash
# 检查配置文件
cat Assets/StreamingAssets/config.json

# 应该包含 ApiBaseUrl, ServerListenAddress, ServerPort
```

---

## 相关文档

- [多 World 架构](MULTI_WORLD_ARCHITECTURE.md)
- [部署指南](DEPLOY_MULTI_WORLD.md)
- [快速开始](QUICK_START.md)
