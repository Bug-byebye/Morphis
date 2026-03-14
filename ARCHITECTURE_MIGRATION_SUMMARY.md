# 架构迁移总结

## 从单实例到多 World 动态调度

### 迁移概述

本次架构调整将项目从"单一 Unity Server 实例"升级为"多 World 动态调度"架构，实现了：

✅ 每个 World 独立进程  
✅ 动态启动/停止 World  
✅ 自动端口分配  
✅ 玩家数量监控  
✅ 空闲 World 自动清理  
✅ 客户端动态连接  

---

## 修改文件清单

### Backend (Python)

#### 新增文件
1. `Backend/services/world_manager.py` - World 进程管理器
2. `Backend/routers/world_manager.py` - World 管理 API 路由
3. `Backend/test_world_manager.py` - 测试脚本

#### 修改文件
1. `Backend/models/world.py`
   - 添加 `WorldStatus` 枚举
   - 添加进程管理字段：`status`, `process_id`, `port`, `player_count`, `last_active_at`

2. `Backend/server.py`
   - 导入 `world_manager` 路由
   - 注册 World 管理路由
   - 修改 `WorkspaceDto` 添加状态字段
   - 修改 `/workspaces` API 返回运行状态
   - 添加 `/workspaces/join` API

3. `Backend/requirements.txt`
   - 添加 `psutil>=5.9.0`

4. `Backend/.env.example`
   - 添加 `UNITY_SERVER_PATH`
   - 添加 `SERVER_PUBLIC_IP`
   - 添加 `API_BASE_URL`

### Unity (C#)

#### 新增文件
1. `Assets/Scripts/WorldSnapshot/WorldServerReporter.cs` - Server 端玩家数上报
2. `Assets/Scripts/WorldSnapshot/WorldServerReporter.cs.meta`

#### 修改文件
1. `Assets/Scripts/AppFlow/AppSession.cs`
   - 添加 `ServerAddress` 和 `ServerPort` 属性
   - 添加 `SetServerConnection()` 方法
   - 修改 `Clear()` 方法

2. `Assets/Scripts/AppFlow/BootFlowManager.cs`
   - 添加 `JoinWorldUrl` 属性
   - 修改 `EnterMainScene()` 方法，调用 `/workspaces/join` API
   - 保存动态服务器连接信息到 `AppSession`

3. `Assets/Scripts/Bootstrap/AppBootstrap.cs`
   - 修改 `ConfigureNetworkManager()` 方法
   - 客户端优先使用 `AppSession` 中的动态地址
   - Server 使用配置文件地址

4. `Assets/Scripts/Config/ConfigLoader.cs`
   - Server 模式支持环境变量 `WORLD_PORT` 覆盖端口

### 文档

#### 新增文档
1. `MULTI_WORLD_ARCHITECTURE.md` - 多 World 架构详细说明
2. `ARCHITECTURE_MIGRATION_SUMMARY.md` - 本文档

---

## 数据库变更

### 迁移 SQL

```sql
-- 添加进程管理字段到 worlds 表
ALTER TABLE worlds 
ADD COLUMN status VARCHAR(20) NOT NULL DEFAULT 'stopped',
ADD COLUMN process_id INTEGER,
ADD COLUMN port INTEGER,
ADD COLUMN player_count INTEGER NOT NULL DEFAULT 0,
ADD COLUMN last_active_at TIMESTAMP;

-- 创建索引
CREATE INDEX idx_worlds_status ON worlds(status);
CREATE INDEX idx_worlds_port ON worlds(port);
```

### 自动迁移

数据库表会在 Backend 启动时自动创建/更新（SQLAlchemy）。

---

## API 变更

### 新增 API

1. **POST /workspaces/join** - 客户端请求加入 World
2. **POST /worlds/manage/start** - 启动 World 进程
3. **POST /worlds/manage/stop** - 停止 World 进程
4. **GET /worlds/manage/status/{world_id}** - 获取 World 状态
5. **POST /worlds/manage/player-count** - 上报玩家数量
6. **GET /worlds/manage/list** - 列出所有 World

### 修改 API

1. **GET /workspaces** - 响应添加 `status`, `port`, `player_count` 字段

---

## 配置变更

### Backend 环境变量 (.env)

新增：
```bash
UNITY_SERVER_PATH=/home/morphis/MorphisServer/Morphis.x86_64
SERVER_PUBLIC_IP=123.45.67.89
API_BASE_URL=http://localhost:8000
```

### Unity Server 配置 (config.json)

```json
{
  "GameServerAddress": "0.0.0.0",  // 必须监听所有网卡
  "GameServerPort": 7777,           // 会被环境变量覆盖
  "ApiBaseUrl": "http://localhost:8000",
  "DefaultWorldId": "default-world"
}
```

### 客户端配置 (config.json)

```json
{
  "GameServerAddress": "123.45.67.89",  // 会被动态地址覆盖
  "GameServerPort": 7777,                // 会被动态端口覆盖
  "ApiBaseUrl": "http://123.45.67.89:8000",
  "DefaultWorldId": "default-world"
}
```

---

## 部署变更

### 旧部署方式（单实例）

```bash
# 启动单一 Unity Server
./Morphis.x86_64 --mode=server --worldId=prod-world-001 -batchmode -nographics
```

### 新部署方式（多实例）

```bash
# 1. 启动 Backend（自动管理所有 World 进程）
cd Backend
python server.py

# 2. Unity Server 由 Backend 自动启动，无需手动启动
# 3. 客户端连接时，Backend 自动分配端口并启动对应 World
```

---

## 测试步骤

### 1. 测试 World 生命周期

```bash
cd Backend
python test_world_manager.py
```

预期输出：
- ✅ World 启动成功
- ✅ 端口分配正确
- ✅ 玩家数更新成功
- ✅ World 停止成功

### 2. 测试客户端加入流程

```bash
python test_world_manager.py join
```

预期输出：
- ✅ 用户登录成功
- ✅ 获取 Workspace 列表
- ✅ 请求加入 World
- ✅ 返回服务器连接信息

### 3. 测试 Unity 客户端

1. 启动 Backend
2. 打开 Unity 客户端
3. 登录
4. 选择 World
5. 观察日志：
   ```
   [BootFlow] Requesting world: My World ...
   [BootFlow] World ready: 123.45.67.89:7777
   [AppSession] Server connection set: 123.45.67.89:7777
   [AppBootstrap] Client connecting to dynamic server: 123.45.67.89:7777
   ```

---

## 兼容性

### 向后兼容

✅ 现有 World 数据完全兼容  
✅ 现有用户账号完全兼容  
✅ 现有 World 快照完全兼容  
✅ 客户端配置文件兼容（会被动态覆盖）  

### 不兼容变更

❌ 旧的单实例部署方式不再推荐  
❌ 手动启动 Unity Server 不再需要  
❌ 静态端口配置不再有效（会被动态分配）  

---

## 性能影响

### 资源消耗

- Backend 额外内存: ~50MB（进程管理器）
- 每个 World 进程: 500MB-2GB 内存
- CPU: 每个 World 10-30%

### 优化建议

1. 调整空闲超时（默认 5 分钟）
2. 限制最大 World 数（默认 50）
3. 使用 SSD 加快进程启动
4. 配置进程资源限制（cgroups）

---

## 故障恢复

### 进程崩溃

- Backend 自动检测进程状态
- 更新数据库状态为 `stopped`
- 下次请求时自动重启

### Backend 重启

- 所有 World 进程继续运行
- Backend 启动后重新扫描进程
- 自动恢复数据库状态

### 数据库连接失败

- Backend 启动时检查连接
- 连接失败则拒绝启动
- 避免静默失败

---

## 安全考虑

### 已实现

✅ 用户权限验证（只能加入自己的 World）  
✅ 进程隔离（独立进程组）  
✅ 端口范围限制（7777-7826）  
✅ 日志审计（所有操作记录）  

### 待实现

⚠️ API 速率限制  
⚠️ 进程资源限制（cgroups）  
⚠️ Docker 容器隔离  
⚠️ SSL/TLS 加密  

---

## 监控指标

### 关键指标

1. **World 数量**: 运行中/停止/错误
2. **玩家分布**: 每个 World 的玩家数
3. **端口使用**: 已分配/可用端口
4. **进程状态**: CPU/内存使用率
5. **启动失败率**: 失败次数/原因

### 监控 API

```bash
# 获取所有 World 状态
curl http://localhost:8000/worlds/manage/list

# 获取特定 World 状态
curl http://localhost:8000/worlds/manage/status/ws-abc123
```

---

## 回滚方案

如果需要回滚到单实例架构：

1. 停止所有 World 进程
2. 恢复旧版 Backend 代码
3. 手动启动单一 Unity Server
4. 数据库保持不变（新字段不影响旧逻辑）

---

## 下一步计划

### 短期（1-2 周）

- [ ] 添加 World 预热机制
- [ ] 实现进程池
- [ ] 添加监控面板

### 中期（1-2 月）

- [ ] 跨服务器调度
- [ ] 负载均衡
- [ ] 自动扩缩容

### 长期（3-6 月）

- [ ] 多区域部署
- [ ] 热迁移
- [ ] CDN 加速

---

## 相关文档

- [多 World 架构详解](MULTI_WORLD_ARCHITECTURE.md)
- [部署指南](DEPLOYMENT_GUIDE.md)
- [配置说明](CONFIG_SETUP.md)
- [API 文档](Backend/server.py)

---

## 联系方式

如有问题，请查看：
- 架构文档: `MULTI_WORLD_ARCHITECTURE.md`
- 测试脚本: `Backend/test_world_manager.py`
- 代码注释: 所有新增代码都有详细注释

---

**迁移完成日期**: 2024-01-01  
**架构版本**: v2.0 (Multi-World Dynamic Scheduling)  
**向后兼容**: 是  
**数据迁移**: 自动  
