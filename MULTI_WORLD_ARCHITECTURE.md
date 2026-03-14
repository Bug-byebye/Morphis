# 多 World 动态调度架构

## 架构概述

本项目实现了真正的多 World 动态调度系统：

- 每个 World 是独立的 Unity Server 进程
- 用户选择 World 后，Backend 自动启动对应进程
- World 无人时自动关闭，节省资源
- 支持动态端口分配和负载管理

## 系统架构

```
┌─────────────────────────────────────────────────────┐
│  Backend (Python FastAPI)                           │
│  - World 元数据管理 (PostgreSQL)                     │
│  - World 进程调度器 (WorldProcessManager)            │
│  - 玩家路由 (world_id → server_ip:port)             │
│  - 自动清理空闲 World (5 分钟无人自动关闭)           │
└──────────────┬──────────────────────────────────────┘
               │
    ┌──────────┴──────────┬──────────┬──────────┐
    │                     │          │          │
┌───▼────────┐  ┌────▼────────┐  ┌──▼─────────┐  ┌──▼─────────┐
│ World A    │  │ World B     │  │ World C    │  │ World D    │
│ Process 1  │  │ Process 2   │  │ Process 3  │  │ (stopped)  │
│ Port: 7777 │  │ Port: 7778  │  │ Port: 7779 │  │            │
│ 2 players  │  │ 1 player    │  │ 0 players  │  │            │
└────────────┘  └─────────────┘  └────────────┘  └────────────┘
```

## 数据库模型

### World 表扩展

```sql
CREATE TABLE worlds (
    id VARCHAR(64) PRIMARY KEY,
    name VARCHAR(256) NOT NULL,
    owner_user_id INTEGER REFERENCES users(id),
    
    -- 进程管理字段
    status VARCHAR(20) NOT NULL DEFAULT 'stopped',  -- stopped/starting/running/stopping/error
    process_id INTEGER,                              -- 进程 PID
    port INTEGER,                                    -- 监听端口
    player_count INTEGER NOT NULL DEFAULT 0,        -- 当前玩家数
    last_active_at TIMESTAMP,                       -- 最后活跃时间
    
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);
```

## API 端点

### 1. 客户端 API

#### POST /workspaces/join
请求加入 World，自动启动进程并返回连接信息

**请求**:
```json
{
  "world_id": "ws-abc123"
}
```

**响应**:
```json
{
  "status": "ok",
  "world_id": "ws-abc123",
  "server_address": "123.45.67.89",
  "server_port": 7777,
  "message": "World ready on 123.45.67.89:7777"
}
```

#### GET /workspaces
获取用户的 World 列表（包含运行状态）

**响应**:
```json
{
  "items": [
    {
      "id": "ws-abc123",
      "name": "My World",
      "members": ["user1", "user2"],
      "status": "running",
      "port": 7777,
      "player_count": 2
    },
    {
      "id": "ws-def456",
      "name": "Test World",
      "members": ["user1"],
      "status": "stopped",
      "port": null,
      "player_count": 0
    }
  ]
}
```

### 2. 管理 API

#### POST /worlds/manage/start
手动启动 World 进程

**请求**:
```json
{
  "world_id": "ws-abc123"
}
```

**响应**:
```json
{
  "status": "ok",
  "port": 7777,
  "message": "World started on port 7777"
}
```

#### POST /worlds/manage/stop
停止 World 进程

**请求**:
```json
{
  "world_id": "ws-abc123",
  "force": false
}
```

#### GET /worlds/manage/status/{world_id}
获取 World 状态

**响应**:
```json
{
  "status": "ok",
  "world_id": "ws-abc123",
  "world_status": "running",
  "port": 7777,
  "player_count": 2,
  "process_id": 12345,
  "last_active": "2024-01-01T12:00:00Z"
}
```

#### POST /worlds/manage/player-count
Unity Server 上报玩家数量（内部 API）

**请求**:
```json
{
  "world_id": "ws-abc123",
  "count": 2
}
```

#### GET /worlds/manage/list
列出所有 World 状态

## 客户端连接流程

```
1. 用户登录
   ↓
2. 获取 World 列表 (GET /workspaces)
   ↓
3. 选择 World
   ↓
4. 请求加入 (POST /workspaces/join)
   ├─ Backend 检查 World 状态
   ├─ 未运行 → 启动新进程
   └─ 已运行 → 直接返回连接信息
   ↓
5. 客户端连接到 server_address:server_port
   ↓
6. 进入游戏
```

## Unity Server 启动流程

```
1. Backend 收到 /workspaces/join 请求
   ↓
2. WorldProcessManager.start_world()
   ├─ 分配可用端口 (7777, 7778, ...)
   ├─ 更新数据库状态为 "starting"
   └─ 启动进程:
       ./Morphis.x86_64 \
         --mode=server \
         --worldId=ws-abc123 \
         -batchmode -nographics
   ↓
3. 环境变量传递:
   - WORLD_PORT=7777
   - API_BASE_URL=http://localhost:8000
   ↓
4. Unity Server 启动
   ├─ ConfigLoader 从环境变量读取端口
   ├─ AppBootstrap 启动 Mirror Server
   └─ WorldServerReporter 定期上报玩家数
   ↓
5. 更新数据库状态为 "running"
```

## World 生命周期管理

### 启动条件
- 首个玩家请求加入 World
- 管理员手动启动

### 运行状态
- Unity Server 每 30 秒上报玩家数量
- Backend 更新 `player_count` 和 `last_active_at`

### 关闭条件
- 玩家数为 0 且空闲超过 5 分钟
- 管理员手动停止
- 进程崩溃（自动检测并更新状态）

### 清理机制
- Backend 后台线程每分钟检查一次
- 自动关闭空闲 World
- 保存 World 快照到数据库

## 端口分配策略

- 基础端口: 7777
- 最大 World 数: 50
- 端口范围: 7777-7826
- 分配算法: 顺序查找第一个未使用的端口

## 配置说明

### Backend 环境变量 (.env)

```bash
# Unity Server 可执行文件路径
UNITY_SERVER_PATH=/home/morphis/MorphisServer/Morphis.x86_64

# 服务器公网 IP（客户端连接用）
SERVER_PUBLIC_IP=123.45.67.89

# Backend API URL（Unity Server 回调用）
API_BASE_URL=http://localhost:8000

# 数据库连接
DATABASE_URL=postgresql://user:password@localhost:5432/morphis
```

### Unity Server 配置 (config.json)

```json
{
  "GameServerAddress": "0.0.0.0",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://localhost:8000",
  "DefaultWorldId": "default-world"
}
```

**注意**:
- `GameServerAddress` 必须是 `0.0.0.0`（监听所有网卡）
- `GameServerPort` 会被环境变量 `WORLD_PORT` 覆盖
- `ApiBaseUrl` 用于 Unity Server 回调 Backend

### 客户端配置 (config.json)

```json
{
  "GameServerAddress": "123.45.67.89",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://123.45.67.89:8000",
  "DefaultWorldId": "default-world"
}
```

**注意**:
- `GameServerAddress` 和 `GameServerPort` 会被 `/workspaces/join` 返回的动态地址覆盖
- 客户端不再需要手动配置服务器地址

## 部署步骤

### 1. 部署 Backend

```bash
cd Backend
pip install -r requirements.txt

# 配置环境变量
cp .env.example .env
nano .env  # 编辑配置

# 启动服务
python server.py
```

### 2. 构建 Unity Server

1. 在 Unity 中切换到 `Dedicated Server` 平台
2. 选择 `Linux` 目标
3. 配置 `StreamingAssets/config.json`:
   ```json
   {
     "GameServerAddress": "0.0.0.0",
     "GameServerPort": 7777,
     "ApiBaseUrl": "http://localhost:8000",
     "DefaultWorldId": "default-world"
   }
   ```
4. 构建到 `Builds/LinuxServer`
5. 上传到服务器 `/home/morphis/MorphisServer/`

### 3. 配置服务器

```bash
# 设置可执行权限
chmod +x /home/morphis/MorphisServer/Morphis.x86_64

# 创建日志目录
sudo mkdir -p /var/log/morphis-worlds
sudo chown morphis:morphis /var/log/morphis-worlds

# 配置防火墙（开放端口范围）
sudo ufw allow 7777:7826/tcp
```

### 4. 测试

```bash
# 启动 Backend
cd Backend
python server.py

# 在另一个终端测试 API
curl -X POST http://localhost:8000/worlds/manage/start \
  -H "Content-Type: application/json" \
  -d '{"world_id":"test-world"}'

# 检查进程
ps aux | grep Morphis

# 检查端口
sudo netstat -tulpn | grep 7777
```

## 监控和维护

### 查看 World 状态

```bash
curl http://localhost:8000/worlds/manage/list
```

### 查看 World 日志

```bash
tail -f /var/log/morphis-worlds/ws-abc123.log
```

### 手动停止 World

```bash
curl -X POST http://localhost:8000/worlds/manage/stop \
  -H "Content-Type: application/json" \
  -d '{"world_id":"ws-abc123","force":false}'
```

### 查看 Backend 日志

```bash
# 如果使用 systemd
sudo journalctl -u morphis-backend -f

# 如果直接运行
# 查看终端输出
```

## 性能优化

### 资源限制

每个 Unity Server 进程约占用：
- CPU: 10-30%（取决于玩家数和场景复杂度）
- 内存: 500MB-2GB
- 网络: 100KB/s per player

推荐配置：
- 2 核 4GB: 最多 2-3 个 World
- 4 核 8GB: 最多 5-8 个 World
- 8 核 16GB: 最多 10-15 个 World

### 优化建议

1. **调整空闲超时**: 修改 `WorldProcessManager.idle_timeout_minutes`
2. **限制最大 World 数**: 修改 `WorldProcessManager.max_worlds`
3. **使用进程池**: 预启动常用 World
4. **负载均衡**: 多台服务器分担 World

## 故障排查

### World 无法启动

1. 检查 Unity Server 路径:
   ```bash
   ls -la /home/morphis/MorphisServer/Morphis.x86_64
   ```

2. 检查权限:
   ```bash
   chmod +x /home/morphis/MorphisServer/Morphis.x86_64
   ```

3. 手动启动测试:
   ```bash
   cd /home/morphis/MorphisServer
   ./Morphis.x86_64 --mode=server --worldId=test -batchmode -nographics
   ```

4. 查看日志:
   ```bash
   tail -f /var/log/morphis-worlds/test.log
   ```

### 端口冲突

```bash
# 查看占用端口的进程
sudo netstat -tulpn | grep 7777

# 强制停止进程
sudo kill -9 <PID>
```

### 进程僵尸

```bash
# 查找僵尸进程
ps aux | grep Morphis | grep defunct

# 清理数据库中的僵尸状态
# 连接数据库并手动更新
psql -U morphis_user -d morphis_db
UPDATE worlds SET status='stopped', process_id=NULL, port=NULL WHERE status='running';
```

### 客户端无法连接

1. 检查 World 状态:
   ```bash
   curl http://localhost:8000/worlds/manage/status/ws-abc123
   ```

2. 检查防火墙:
   ```bash
   sudo ufw status
   ```

3. 检查云服务商安全组:
   - 确保开放了 7777-7826 端口

4. 测试端口连通性:
   ```bash
   # 在客户端机器上
   telnet 123.45.67.89 7777
   ```

## 安全建议

1. **限制 API 访问**: 添加认证中间件
2. **进程隔离**: 使用 Docker 容器
3. **资源限制**: 使用 cgroups 限制 CPU/内存
4. **日志审计**: 记录所有 World 启停操作
5. **定期备份**: 自动备份 World 快照

## 未来扩展

1. **跨服务器调度**: 多台物理服务器负载均衡
2. **热迁移**: World 在服务器间迁移
3. **自动扩缩容**: 根据负载自动增减服务器
4. **区域部署**: 多地域就近接入
5. **CDN 加速**: 静态资源 CDN 分发

## 相关文档

- [部署指南](DEPLOYMENT_GUIDE.md)
- [快速开始](QUICK_START_DEPLOYMENT.md)
- [配置说明](CONFIG_SETUP.md)
- [故障排查](TROUBLESHOOTING.md)
