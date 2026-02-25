# 多 World 架构部署指南

## 快速开始（30 分钟）

本指南适用于新的多 World 动态调度架构。

---

## 准备工作

### 服务器要求
- Ubuntu 20.04+ 
- 4 核 8GB 内存（推荐）
- 50GB 磁盘空间
- 公网 IP

### 本地要求
- Unity 2021.3+
- Git 或 SCP 工具
- SSH 客户端

### 需要的信息
- 服务器 IP: `___________________`
- SSH 用户名: `___________________`
- SSH 密码或密钥

---

## 阶段 1: 部署 Backend（10 分钟）

### 步骤 1.1: 上传项目代码

```bash
# 方式 A: 使用 Git（推荐）
ssh your-username@your-server-ip
git clone <your-repo-url> /home/your-username/Morphis
cd /home/your-username/Morphis

# 方式 B: 使用 SCP 上传
# 在本地执行
scp -r Backend your-username@your-server-ip:/home/your-username/Morphis/
```

### 步骤 1.2: 运行部署脚本

```bash
# SSH 连接到服务器
ssh your-username@your-server-ip

# 上传并运行部署脚本
cd /home/your-username/Morphis
chmod +x deploy/setup-server.sh
./deploy/setup-server.sh
```

**脚本会询问**:
1. 数据库密码: 输入强密码（记住它！）
2. 部署目录: 直接回车使用默认
3. World ID: 直接回车（新架构不需要）

**预期结果**:
```
✅ PostgreSQL 已安装
✅ Python Backend 已安装
✅ 防火墙已配置（端口 7777-7826）
✅ Backend 服务已启动
✅ World 日志目录已创建
```

### 步骤 1.3: 验证 Backend

```bash
# 测试健康检查
curl http://localhost:8000/health

# 应该返回
# {"status":"ok","services":["text2image","image2image","image23d","text23d"]}

# 查看服务状态
sudo systemctl status morphis-backend
```

---

## 阶段 2: 构建并部署 Unity Server（15 分钟）

### 步骤 2.1: 配置 Unity Server

在 Unity 中创建 Server 配置文件：

```bash
# 在本地项目根目录
# 创建 Assets/StreamingAssets/config.json
```

**内容**:
```json
{
  "GameServerAddress": "0.0.0.0",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://localhost:8000",
  "DefaultWorldId": "default-world"
}
```

**重要**:
- `GameServerAddress` 必须是 `0.0.0.0`（监听所有网卡）
- `GameServerPort` 会被环境变量动态覆盖
- `ApiBaseUrl` 是 Backend 的内网地址

### 步骤 2.2: 切换到 Linux Dedicated Server

1. 打开 Unity 项目
2. `File > Build Settings`
3. 选择 `Dedicated Server` 平台
4. 选择 `Linux` 目标
5. 点击 `Switch Platform`
6. 等待切换完成

### 步骤 2.3: 构建 Unity Server

1. 在 `Build Settings` 中点击 `Build`
2. 选择输出目录: `Builds/LinuxServer`
3. 点击保存
4. 等待构建完成（10-20 分钟）

### 步骤 2.4: 上传到服务器

```bash
# 在本地项目根目录执行
scp -r Builds/LinuxServer/* your-username@your-server-ip:/home/your-username/Morphis/MorphisServer/
```

### 步骤 2.5: 设置权限

```bash
# SSH 连接到服务器
ssh your-username@your-server-ip

# 设置可执行权限
chmod +x /home/your-username/Morphis/MorphisServer/Morphis.x86_64

# 验证文件
ls -la /home/your-username/Morphis/MorphisServer/
```

**预期结果**:
```
-rwxr-xr-x  1 user user  Morphis.x86_64
drwxr-xr-x  2 user user  Morphis_Data/
-rw-r--r--  1 user user  UnityPlayer.so
```

---

## 阶段 3: 测试多 World 架构（5 分钟）

### 步骤 3.1: 测试 World 管理

```bash
# SSH 连接到服务器
ssh your-username@your-server-ip

# 运行测试脚本
cd /home/your-username/Morphis/Backend
source venv/bin/activate
python test_world_manager.py
```

**预期输出**:
```
✅ World 已启动，端口: 7777
✅ 玩家数更新为 2
✅ World 已停止
```

### 步骤 3.2: 查看 World 列表

```bash
curl http://localhost:8000/worlds/manage/list
```

**预期响应**:
```json
{
  "worlds": [
    {
      "id": "test-world-001",
      "name": "test-world-001",
      "status": "stopped",
      "port": null,
      "player_count": 0,
      "process_id": null
    }
  ]
}
```

---

## 阶段 4: 配置客户端（5 分钟）

### 步骤 4.1: 切换回 Windows 平台

1. 在 Unity 中 `File > Build Settings`
2. 选择 `Windows` 平台
3. **取消勾选** `Dedicated Server`
4. 点击 `Switch Platform`

### 步骤 4.2: 配置客户端

编辑项目根目录的 `config.json`:

```json
{
  "GameServerAddress": "your-server-ip",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://your-server-ip:8000",
  "DefaultWorldId": "default-world"
}
```

**替换 `your-server-ip` 为实际 IP**，例如:
```json
{
  "GameServerAddress": "121.43.141.248",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://121.43.141.248:8000",
  "DefaultWorldId": "default-world"
}
```

**注意**: 这些地址会被动态覆盖，只是作为 fallback。

### 步骤 4.3: 构建客户端

1. 在 `Build Settings` 中点击 `Build`
2. 选择输出目录: `Builds/WindowsClient`
3. 等待构建完成

### 步骤 4.4: 测试连接

1. 运行 `Builds/WindowsClient/Morphis.exe`
2. 登录（默认: `111111` / `111111`）
3. 选择 World
4. 观察日志：
   ```
   [BootFlow] Requesting world: My World ...
   [BootFlow] World ready: 121.43.141.248:7777
   [AppBootstrap] Client connecting to dynamic server: 121.43.141.248:7777
   ```
5. 进入游戏

**预期结果**:
- ✅ 可以登录
- ✅ 可以看到 World 列表
- ✅ 选择 World 后自动启动服务器
- ✅ 客户端连接成功
- ✅ 可以放置和移动物体

---

## 验证多 World 功能

### 测试 1: 多个 World 同时运行

```bash
# 在服务器上
curl -X POST http://localhost:8000/worlds/manage/start \
  -H "Content-Type: application/json" \
  -d '{"world_id":"world-1"}'

curl -X POST http://localhost:8000/worlds/manage/start \
  -H "Content-Type: application/json" \
  -d '{"world_id":"world-2"}'

# 查看列表
curl http://localhost:8000/worlds/manage/list
```

**预期**: 两个 World 在不同端口运行（7777, 7778）

### 测试 2: 自动清理空闲 World

1. 启动一个 World
2. 等待 5 分钟（无玩家连接）
3. 查看状态：World 应该自动停止

```bash
# 查看 World 状态
curl http://localhost:8000/worlds/manage/status/world-1
```

### 测试 3: 玩家数量上报

1. 客户端连接到 World
2. 在服务器上查看玩家数：

```bash
curl http://localhost:8000/worlds/manage/status/<world-id>
```

**预期**: `player_count` 应该显示当前玩家数

---

## 监控和管理

### 查看 Backend 日志

```bash
sudo journalctl -u morphis-backend -f
```

### 查看 World 日志

```bash
# 查看特定 World 的日志
tail -f /var/log/morphis-worlds/<world-id>.log

# 查看所有 World 日志
ls -la /var/log/morphis-worlds/
```

### 查看所有 World 状态

```bash
curl http://localhost:8000/worlds/manage/list | python3 -m json.tool
```

### 手动停止 World

```bash
curl -X POST http://localhost:8000/worlds/manage/stop \
  -H "Content-Type: application/json" \
  -d '{"world_id":"<world-id>","force":false}'
```

### 查看进程

```bash
# 查看所有 Unity Server 进程
ps aux | grep Morphis

# 查看端口占用
sudo netstat -tulpn | grep 777
```

---

## 故障排查

### 问题 1: World 无法启动

**症状**: `/worlds/manage/start` 返回错误

**解决方案**:

1. 检查 Unity Server 路径:
   ```bash
   ls -la /home/your-username/Morphis/MorphisServer/Morphis.x86_64
   ```

2. 检查权限:
   ```bash
   chmod +x /home/your-username/Morphis/MorphisServer/Morphis.x86_64
   ```

3. 手动测试启动:
   ```bash
   cd /home/your-username/Morphis/MorphisServer
   ./Morphis.x86_64 --mode=server --worldId=test -batchmode -nographics
   ```

4. 查看日志:
   ```bash
   tail -f /var/log/morphis-worlds/test.log
   ```

### 问题 2: 客户端无法连接

**症状**: 客户端一直显示 "Connecting..."

**解决方案**:

1. 检查云服务商安全组:
   - 确保开放了 7777-7826 端口

2. 检查防火墙:
   ```bash
   sudo ufw status
   ```

3. 测试端口连通性:
   ```bash
   # 在本地 Windows 上
   Test-NetConnection -ComputerName your-server-ip -Port 7777
   ```

4. 查看 World 状态:
   ```bash
   curl http://your-server-ip:8000/worlds/manage/list
   ```

### 问题 3: Backend 无法启动

**症状**: `systemctl status morphis-backend` 显示失败

**解决方案**:

1. 查看详细日志:
   ```bash
   sudo journalctl -u morphis-backend -n 100
   ```

2. 检查数据库连接:
   ```bash
   psql -U morphis_user -d morphis_db -h localhost
   ```

3. 检查 .env 配置:
   ```bash
   cat /home/your-username/Morphis/Backend/.env
   ```

4. 手动启动测试:
   ```bash
   cd /home/your-username/Morphis/Backend
   source venv/bin/activate
   python server.py
   ```

---

## 性能优化

### 调整空闲超时

编辑 `Backend/services/world_manager.py`:

```python
self.idle_timeout_minutes = 10  # 改为 10 分钟
```

### 限制最大 World 数

```python
self.max_worlds = 20  # 改为 20 个
```

### 调整上报间隔

编辑 Unity 场景中的 `WorldServerReporter` 组件:
- Report Interval: 30 秒（默认）

---

## 下一步

部署成功后，建议：

1. **配置域名和 SSL**
   - 购买域名
   - 配置 DNS 解析
   - 使用 Let's Encrypt 配置 SSL

2. **设置监控**
   - 配置 Prometheus + Grafana
   - 监控 World 数量、玩家分布、资源使用

3. **配置备份**
   - 定期备份数据库
   - 定期备份 World 快照

4. **性能测试**
   - 测试最大并发 World 数
   - 测试最大玩家数
   - 优化资源使用

---

## 相关文档

- [多 World 架构详解](MULTI_WORLD_ARCHITECTURE.md)
- [架构迁移总结](ARCHITECTURE_MIGRATION_SUMMARY.md)
- [完整部署指南](DEPLOYMENT_GUIDE.md)
- [故障排查](TROUBLESHOOTING.md)

---

## 常用命令速查

```bash
# Backend 管理
sudo systemctl status morphis-backend
sudo systemctl restart morphis-backend
sudo journalctl -u morphis-backend -f

# World 管理
curl http://localhost:8000/worlds/manage/list
curl http://localhost:8000/worlds/manage/status/<world-id>

# 日志查看
tail -f /var/log/morphis-worlds/<world-id>.log
ls -la /var/log/morphis-worlds/

# 进程管理
ps aux | grep Morphis
sudo netstat -tulpn | grep 777

# 测试
cd Backend && source venv/bin/activate
python test_world_manager.py
python test_world_manager.py join
```

---

**准备好了吗？开始部署吧！** 🚀
