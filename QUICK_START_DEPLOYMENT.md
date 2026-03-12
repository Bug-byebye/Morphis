# 快速部署指南（5 分钟上手）

## 前提条件

- ✅ 已购买 Ubuntu 云服务器（2 核 4GB+）
- ✅ 已获得服务器 IP 和 SSH 访问权限
- ✅ Unity 项目可本地运行

---

## 第一步：部署 Backend（2 分钟）

### 在本地执行

```bash
# 上传部署脚本
scp deploy/setup-server.sh your-username@your-server-ip:/tmp/
```

### 在服务器上执行

```bash
# SSH 连接
ssh your-username@your-server-ip

# 运行部署脚本
cd /tmp
chmod +x setup-server.sh
./setup-server.sh
```

按提示输入：
- 数据库密码（随意设置，记住即可）
- 部署目录（直接回车使用默认）
- World ID（直接回车使用默认）

等待脚本完成（约 2-3 分钟）。

### 验证

```bash
curl http://localhost:8000/health
```

看到 `{"status":"ok"}` 即成功。

---

## 第二步：构建 Unity Server（3 分钟）

### 在 Unity 中

1. `File > Build Settings`
2. 选择 `Dedicated Server` 平台
3. 选择 `Linux` 目标
4. 点击 `Switch Platform`（等待完成）
5. 复制配置文件：
   ```bash
   copy deploy\client-config-production.json Assets\StreamingAssets\config.json
   ```
6. 编辑 `Assets/StreamingAssets/config.json`：
   ```json
   {
     "GameServerAddress": "0.0.0.0",
     "GameServerPort": 7777,
     "ApiBaseUrl": "http://localhost:8000",
     "DefaultWorldId": "prod-world-001"
   }
   ```
7. 点击 `Build`，选择输出目录（如 `Builds/LinuxServer`）
8. 等待构建完成

---

## 第三步：部署 Unity Server（2 分钟）

### 在本地执行

```bash
# 上传 Server 文件
scp -r Builds/LinuxServer/* your-username@your-server-ip:/home/your-username/MorphisServer/
scp deploy/setup-unity-server.sh your-username@your-server-ip:/tmp/
```

### 在服务器上执行

```bash
# 运行部署脚本
cd /tmp
chmod +x setup-unity-server.sh
./setup-unity-server.sh /home/your-username/MorphisServer
```

按提示输入（或直接回车使用默认）：
- World ID
- Backend URL

### 验证

```bash
sudo netstat -tulpn | grep 7777
```

看到端口监听即成功。

---

## 第四步：配置客户端（1 分钟）

### 创建配置文件

在项目根目录创建 `config.json`：

```json
{
  "GameServerAddress": "your-server-ip",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://your-server-ip:8000",
  "DefaultWorldId": "prod-world-001"
}
```

替换 `your-server-ip` 为你的服务器 IP。

### 构建客户端

1. `File > Build Settings`
2. 选择 `Windows` 平台
3. 确保 `Dedicated Server` 未勾选
4. 点击 `Build`

---

## 第五步：测试（1 分钟）

1. 运行客户端 exe
2. 登录（默认: `111111` / `111111`）
3. 选择空间
4. 进入游戏

成功！🎉

---

## 常用命令

### 查看服务状态
```bash
sudo systemctl status morphis-backend
sudo systemctl status morphis-server
```

### 查看日志
```bash
sudo journalctl -u morphis-backend -f
sudo journalctl -u morphis-server -f
```

### 重启服务
```bash
sudo systemctl restart morphis-backend
sudo systemctl restart morphis-server
```

---

## 故障排查

### Backend 无法访问
```bash
# 检查服务
sudo systemctl status morphis-backend

# 查看日志
sudo journalctl -u morphis-backend -n 50

# 检查防火墙
sudo ufw status
```

### 客户端无法连接
```bash
# 检查端口
sudo netstat -tulpn | grep 7777

# 查看 Server 日志
tail -f /var/log/morphis-server.log

# 测试连通性（在客户端执行）
telnet your-server-ip 7777
```

### 云服务商安全组
确保在云服务商控制台开放：
- TCP 7777（Unity Server）
- TCP 8000（Backend API）

---

## 下一步

- 配置域名和 SSL：`DEPLOYMENT_GUIDE.md`
- 设置监控和备份：`DEPLOYMENT_CHECKLIST.md`
- 详细配置说明：`CONFIG_SETUP.md`

---

## 完整文档

- `DEPLOYMENT_GUIDE.md` - 详细部署指南
- `DEPLOYMENT_CHECKLIST.md` - 部署检查清单
- `CLIENT_BUILD_GUIDE.md` - 客户端构建指南
- `CONFIG_SETUP.md` - 配置文件说明
- `deploy/README.md` - 部署脚本说明
