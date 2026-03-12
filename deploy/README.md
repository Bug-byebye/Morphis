# Morphis 部署脚本

本目录包含 Morphis 多 World 动态调度架构的自动化部署脚本。

## 架构说明

新架构特点：
- ✅ 每个 World 独立进程
- ✅ 自动启动/停止 World
- ✅ 动态端口分配（7777-7826）
- ✅ 空闲 World 自动清理（5 分钟）
- ✅ 玩家数量实时监控

## 脚本列表

### 1. setup-server.sh
**用途**: 一键部署 Backend 和数据库

**功能**:
- 安装 PostgreSQL 数据库
- 配置 Python Backend
- 创建 World 进程管理器
- 配置防火墙（端口 7777-7826）
- 创建 systemd 服务

**使用方法**:
```bash
# 上传脚本到服务器
scp deploy/setup-server.sh your-username@your-server-ip:/tmp/

# SSH 连接到服务器
ssh your-username@your-server-ip

# 运行脚本
cd /tmp
chmod +x setup-server.sh
./setup-server.sh
```

**交互提示**:
1. 数据库密码（默认: morphis123）
2. 部署目录（默认: /home/your-username/Morphis）
3. 是否克隆 Git 仓库（如果目录不存在）

### 2. health-check.sh
**用途**: 检查服务健康状态

**功能**:
- 检查 Backend 服务状态
- 检查数据库连接
- 检查端口监听
- 检查 World 进程

**使用方法**:
```bash
chmod +x deploy/health-check.sh
./deploy/health-check.sh
```

### 3. backup.sh
**用途**: 备份数据库和配置

**功能**:
- 备份 PostgreSQL 数据库
- 备份 Backend 配置
- 压缩并保存到指定目录

**使用方法**:
```bash
chmod +x deploy/backup.sh
./deploy/backup.sh
```

### 4. client-config-production.json
**用途**: 生产环境客户端配置模板

**使用方法**:
```bash
# 复制到项目根目录
cp deploy/client-config-production.json config.json

# 编辑配置
nano config.json
```

## 快速部署（30 分钟）

### 步骤 1: 上传项目代码

```bash
# 方式 A: 使用 Git（推荐）
ssh your-username@your-server-ip
git clone <your-repo-url> /home/your-username/Morphis

# 方式 B: 手动上传
scp -r Backend your-username@your-server-ip:/home/your-username/Morphis/
```

### 步骤 2: 部署 Backend

```bash
scp deploy/setup-server.sh your-username@your-server-ip:/tmp/
ssh your-username@your-server-ip
cd /tmp
chmod +x setup-server.sh
./setup-server.sh
```

### 步骤 3: 构建 Unity Server

1. Unity 切换到 `Dedicated Server` 平台（Linux）
2. 配置 `Assets/StreamingAssets/config.json`:
   ```json
   {
     "GameServerAddress": "0.0.0.0",
     "GameServerPort": 7777,
     "ApiBaseUrl": "http://localhost:8000",
     "DefaultWorldId": "default-world"
   }
   ```
3. 构建到 `Builds/LinuxServer`

### 步骤 4: 上传 Unity Server

```bash
scp -r Builds/LinuxServer/* your-username@your-server-ip:/home/your-username/Morphis/MorphisServer/
ssh your-username@your-server-ip
chmod +x /home/your-username/Morphis/MorphisServer/Morphis.x86_64
```

### 步骤 5: 测试部署

```bash
cd /home/your-username/Morphis/Backend
source venv/bin/activate
python test_world_manager.py
```

## 配置说明

### Backend 环境变量 (.env)

自动生成：
```bash
DATABASE_URL=postgresql://morphis_user:password@localhost:5432/morphis_db
HOST=0.0.0.0
PORT=8000
UNITY_SERVER_PATH=/home/your-username/Morphis/MorphisServer/Morphis.x86_64
SERVER_PUBLIC_IP=<自动检测>
API_BASE_URL=http://localhost:8000
```

### Unity Server 配置 (config.json)

需要手动创建：
```json
{
  "GameServerAddress": "0.0.0.0",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://localhost:8000",
  "DefaultWorldId": "default-world"
}
```

### 客户端配置 (config.json)

```json
{
  "GameServerAddress": "your-server-ip",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://your-server-ip:8000",
  "DefaultWorldId": "default-world"
}
```

## 管理命令

### Backend 管理

```bash
sudo systemctl status morphis-backend
sudo systemctl restart morphis-backend
sudo journalctl -u morphis-backend -f
```

### World 管理

```bash
# 列出所有 World
curl http://localhost:8000/worlds/manage/list

# 查看 World 状态
curl http://localhost:8000/worlds/manage/status/<world-id>

# 启动 World
curl -X POST http://localhost:8000/worlds/manage/start \
  -H "Content-Type: application/json" \
  -d '{"world_id":"<world-id>"}'

# 停止 World
curl -X POST http://localhost:8000/worlds/manage/stop \
  -H "Content-Type: application/json" \
  -d '{"world_id":"<world-id>","force":false}'
```

### 日志查看

```bash
# Backend 日志
sudo journalctl -u morphis-backend -f

# World 日志
tail -f /var/log/morphis-worlds/<world-id>.log
ls -la /var/log/morphis-worlds/
```

## 故障排查

### Backend 无法启动

```bash
sudo journalctl -u morphis-backend -n 100
psql -U morphis_user -d morphis_db -h localhost
cd /home/your-username/Morphis/Backend && source venv/bin/activate && python server.py
```

### World 无法启动

```bash
ls -la /home/your-username/Morphis/MorphisServer/Morphis.x86_64
chmod +x /home/your-username/Morphis/MorphisServer/Morphis.x86_64
cd /home/your-username/Morphis/MorphisServer
./Morphis.x86_64 --mode=server --worldId=test -batchmode -nographics
tail -f /var/log/morphis-worlds/test.log
```

### 端口冲突

```bash
sudo netstat -tulpn | grep 7777
sudo kill -9 <PID>
```

## 相关文档

- [完整部署指南](../DEPLOY_MULTI_WORLD.md)
- [多 World 架构说明](../MULTI_WORLD_ARCHITECTURE.md)
- [架构迁移总结](../ARCHITECTURE_MIGRATION_SUMMARY.md)
