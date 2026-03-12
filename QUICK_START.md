# 快速开始 - 多 World 架构部署

## 30 分钟完成部署

### 前提条件

- Ubuntu 20.04+ 服务器（4核8GB推荐）
- 服务器公网 IP
- SSH 访问权限
- Unity 2021.3+

---

## 步骤 1: 上传项目代码（5 分钟）

```bash
# 在服务器上克隆项目
ssh your-username@your-server-ip
git clone <your-repo-url> /home/your-username/Morphis

# 或手动上传 Backend
scp -r Backend your-username@your-server-ip:/home/your-username/Morphis/
```

---

## 步骤 2: 部署 Backend（10 分钟）

```bash
# 上传部署脚本
scp deploy/setup-server.sh your-username@your-server-ip:/tmp/

# SSH 连接并运行
ssh your-username@your-server-ip
cd /tmp
chmod +x setup-server.sh
./setup-server.sh
```

**按提示输入**:
- 数据库密码: 输入强密码（记住它！）
- 部署目录: 直接回车
- Git 仓库: 如果已上传代码，选择 n

**预期结果**:
```
✅ PostgreSQL 已安装
✅ Python Backend 已安装
✅ World 进程管理器已配置
✅ Backend 服务已启动
```

---

## 步骤 3: 构建 Unity Server（10 分钟）

### 3.1 配置 Unity Server

创建 `Assets/StreamingAssets/config.json`:

```json
{
  "ApiBaseUrl": "http://localhost:8000",
  "ServerListenAddress": "0.0.0.0",
  "ServerPort": 7777
}
```

**重要**: `ServerListenAddress` 必须是 `0.0.0.0`（监听所有网卡）

### 3.2 构建

1. `File > Build Settings`
2. 选择 `Dedicated Server` 平台
3. 选择 `Linux` 目标
4. 点击 `Switch Platform`
5. 点击 `Build`，输出到 `Builds/LinuxServer`

---

## 步骤 4: 上传 Unity Server（5 分钟）

```bash
# 上传构建文件
scp -r Builds/LinuxServer/* your-username@your-server-ip:/home/your-username/Morphis/MorphisServer/

# 设置权限
ssh your-username@your-server-ip
chmod +x /home/your-username/Morphis/MorphisServer/Morphis.x86_64
```

---

## 步骤 5: 测试部署

```bash
# 在服务器上
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

---

## 步骤 6: 配置客户端

编辑项目根目录 `config.json`:

```json
{
  "ApiBaseUrl": "http://your-server-ip:8000"
}
```

**示例**:
```json
{
  "ApiBaseUrl": "http://123.45.67.89:8000"
}
```

**注意**: 客户端不再需要配置服务器地址和端口，这些信息会从 Backend API 动态获取。

构建 Windows 客户端并测试。

---

## 验证部署

### 检查 Backend

```bash
curl http://your-server-ip:8000/health
curl http://your-server-ip:8000/worlds/manage/list
```

### 检查防火墙

确保云服务商安全组开放了：
- 22 (SSH)
- 7777-7826 (World 端口)
- 8000 (Backend API)

---

## 常用命令

```bash
# 查看 Backend 状态
sudo systemctl status morphis-backend

# 查看 World 列表
curl http://localhost:8000/worlds/manage/list

# 查看日志
sudo journalctl -u morphis-backend -f
tail -f /var/log/morphis-worlds/<world-id>.log
```

---

## 故障排查

### Backend 无法启动

```bash
sudo journalctl -u morphis-backend -n 100
```

### World 无法启动

```bash
tail -f /var/log/morphis-worlds/test.log
```

### 客户端无法连接

1. 检查安全组端口
2. 检查防火墙: `sudo ufw status`
3. 测试端口: `telnet your-server-ip 7777`

---

## 下一步

- 详细文档: `DEPLOY_MULTI_WORLD.md`
- 架构说明: `MULTI_WORLD_ARCHITECTURE.md`
- 管理命令: `deploy/README.md`

---

**部署完成！** 🎉
