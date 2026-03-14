# 配置系统变更说明

## 概述

在多 World 动态调度架构中，配置系统已经完全重构，移除了旧架构的残留内容。

---

## 主要变更

### 1. 配置字段变更

#### 旧字段（已删除）
- ❌ `GameServerAddress` - 不再需要
- ❌ `GameServerPort` - 不再需要

#### 新字段
- ✅ `ApiBaseUrl` - Backend API 地址（客户端和服务器都需要）
- ✅ `ServerListenAddress` - 服务器监听地址（仅服务器，默认 `0.0.0.0`）
- ✅ `ServerPort` - 服务器端口（仅服务器，默认 `7777`，会被环境变量覆盖）
- ✅ `DefaultWorldId` - 默认 World ID（仅服务器，可选）

### 2. 配置文件简化

#### 客户端配置
**之前**:
```json
{
  "GameServerAddress": "your-server-ip",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://your-server-ip:8000",
  "DefaultWorldId": "default-world"
}
```

**现在**:
```json
{
  "ApiBaseUrl": "http://your-server-ip:8000"
}
```

#### Unity Server 配置
**之前**:
```json
{
  "GameServerAddress": "0.0.0.0",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://localhost:8000",
  "DefaultWorldId": "default-world"
}
```

**现在**:
```json
{
  "ApiBaseUrl": "http://localhost:8000",
  "ServerListenAddress": "0.0.0.0",
  "ServerPort": 7777
}
```

---

## 代码变更

### 1. AppConfig.cs
- 删除 `GameServerAddress` 字段
- 删除 `GameServerPort` 字段
- 添加 `ServerListenAddress` 字段（默认 `0.0.0.0`）
- 添加 `ServerPort` 字段（默认 `7777`）
- `DefaultWorldId` 添加默认值 `dev-world`

### 2. ConfigLoader.cs
- 移除对 `GameServerAddress` 的验证
- 移除对 `GameServerPort` 的验证
- 环境变量 `WORLD_PORT` 覆盖 `ServerPort` 而不是 `GameServerPort`
- 简化验证逻辑，只强制要求 `ApiBaseUrl`

### 3. AppBootstrap.cs
- Server 模式：使用 `ServerListenAddress` 和 `ServerPort`
- Client 模式：优先使用 `AppSession` 中的动态地址
- Client fallback：使用 `127.0.0.1` 而不是配置文件中的地址

---

## 配置模板更新

### 1. config.json.example
```json
{
  "ApiBaseUrl": "https://your-backend-api.example.com"
}
```

### 2. config.json（开发环境）
```json
{
  "ApiBaseUrl": "http://121.43.141.248:8000"
}
```

### 3. deploy/client-config-production.json
```json
{
  "ApiBaseUrl": "http://your-server-ip:8000"
}
```

### 4. deploy/server-config.json（新增）
```json
{
  "ApiBaseUrl": "http://localhost:8000",
  "ServerListenAddress": "0.0.0.0",
  "ServerPort": 7777,
  "DefaultWorldId": "default-world"
}
```

---

## 文档更新

已更新以下文档中的配置示例：
- ✅ README.md
- ✅ QUICK_START.md
- ✅ DEPLOYMENT_READY.md
- ✅ CONFIG_NEW_ARCHITECTURE.md（新增）

需要手动更新的文档（包含大量旧配置示例）：
- ⚠️ DEPLOY_MULTI_WORLD.md
- ⚠️ MULTI_WORLD_ARCHITECTURE.md
- ⚠️ DEPLOYMENT_CHECKLIST_MULTI_WORLD.md
- ⚠️ deploy/README.md
- ⚠️ CONFIG_SETUP.md
- ⚠️ TROUBLESHOOTING.md
- ⚠️ 其他部署相关文档

---

## 迁移步骤

### 对于现有项目

1. **更新客户端配置**
   ```bash
   # 编辑 config.json
   {
     "ApiBaseUrl": "http://your-server-ip:8000"
   }
   ```

2. **更新 Unity Server 配置**
   ```bash
   # 编辑 Assets/StreamingAssets/config.json
   {
     "ApiBaseUrl": "http://localhost:8000",
     "ServerListenAddress": "0.0.0.0",
     "ServerPort": 7777
   }
   ```

3. **重新构建**
   - 客户端：重新构建 Windows 版本
   - Server：重新构建 Linux Dedicated Server

4. **测试**
   - 客户端应该能正常登录和获取 World 列表
   - 选择 World 后应该能自动连接到动态分配的服务器

---

## 验证清单

### 客户端
- [ ] `config.json` 只包含 `ApiBaseUrl`
- [ ] 可以正常登录
- [ ] 可以看到 World 列表
- [ ] 选择 World 后能自动连接

### Unity Server
- [ ] `config.json` 包含 `ApiBaseUrl`, `ServerListenAddress`, `ServerPort`
- [ ] `ServerListenAddress` 是 `0.0.0.0`
- [ ] 环境变量 `WORLD_PORT` 可以覆盖端口
- [ ] 可以正常启动并监听端口

### Backend
- [ ] 可以动态启动 World 进程
- [ ] 可以正确分配端口
- [ ] `/workspaces/join` API 返回正确的连接信息

---

## 常见问题

### Q: 为什么客户端不需要服务器地址了？
A: 因为服务器地址和端口由 Backend 动态分配，客户端通过 `/workspaces/join` API 获取。

### Q: 旧配置文件还能用吗？
A: 不能。必须更新为新格式，否则会出现编译错误或运行时错误。

### Q: 如何测试新配置？
A: 
1. 启动 Backend
2. 在 Unity 中 Play
3. 登录并选择 World
4. 观察日志，应该显示动态连接信息

### Q: 环境变量 WORLD_PORT 是必须的吗？
A: 对于多 World 架构是必须的。Backend 会为每个 World 设置不同的端口。

---

## 相关文档

- [配置说明（新架构）](CONFIG_NEW_ARCHITECTURE.md)
- [多 World 架构](MULTI_WORLD_ARCHITECTURE.md)
- [部署指南](DEPLOY_MULTI_WORLD.md)
- [快速开始](QUICK_START.md)

---

**更新日期**: 2024-01-01  
**架构版本**: v2.0 (Multi-World Dynamic Scheduling)
