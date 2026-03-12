# 云服务器部署检查清单

## 部署前准备

### 服务器信息
- [ ] 服务器 IP 地址: `___________________`
- [ ] SSH 用户名: `___________________`
- [ ] SSH 密码/密钥已配置
- [ ] 服务器系统: Ubuntu 20.04+ ✓
- [ ] 服务器配置: 至少 2 核 4GB RAM ✓

### 域名配置（可选但推荐）
- [ ] 已购买域名: `___________________`
- [ ] DNS A 记录已配置:
  - [ ] `api.yourdomain.com` → 服务器 IP
  - [ ] `game.yourdomain.com` → 服务器 IP
- [ ] DNS 解析生效（可能需要 10-30 分钟）

### 本地准备
- [ ] Unity 项目可正常运行
- [ ] Python Backend 可本地运行
- [ ] 已安装 Git
- [ ] 已安装 SCP/SFTP 工具

---

## 第一步：部署 Python Backend

### 1.1 连接服务器
```bash
ssh your-username@your-server-ip
```
- [ ] 成功连接到服务器

### 1.2 上传部署脚本
```bash
# 在本地执行
scp deploy/setup-server.sh your-username@your-server-ip:/tmp/
```
- [ ] 脚本上传成功

### 1.3 运行部署脚本
```bash
# 在服务器上执行
cd /tmp
chmod +x setup-server.sh
./setup-server.sh
```

脚本会询问：
- 数据库密码（建议使用强密码）
- 部署目录（默认 `/home/user/Morphis`）
- World ID（默认 `prod-world-001`）

- [ ] 脚本执行成功
- [ ] Backend 服务已启动

### 1.4 验证 Backend
```bash
# 测试 API
curl http://localhost:8000/health

# 查看服务状态
sudo systemctl status morphis-backend

# 查看日志
sudo journalctl -u morphis-backend -n 50
```
- [ ] API 返回 `{"status":"ok"}`
- [ ] 服务状态为 `active (running)`
- [ ] 日志无错误

---

## 第二步：构建 Unity Server

### 2.1 配置 Unity 项目

在 Windows 开发机上：

1. 打开 Unity 项目
2. `File > Build Settings`
3. 选择 `Dedicated Server` 平台
4. 选择 `Linux` 目标
5. 点击 `Switch Platform`（等待切换完成）

- [ ] 平台切换成功

### 2.2 配置 Server 配置文件

```bash
# 复制生产配置模板
copy deploy\client-config-production.json Assets\StreamingAssets\config.json
```

编辑 `Assets/StreamingAssets/config.json`:
```json
{
  "GameServerAddress": "0.0.0.0",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://localhost:8000",
  "DefaultWorldId": "prod-world-001"
}
```

- [ ] 配置文件已创建
- [ ] `GameServerAddress` 设置为 `0.0.0.0`
- [ ] `ApiBaseUrl` 指向 Backend（通常是 `http://localhost:8000`）

### 2.3 构建 Server

1. 在 `Build Settings` 中点击 `Build`
2. 选择输出目录（如 `Builds/LinuxServer`）
3. 等待构建完成（可能需要 10-30 分钟）

- [ ] 构建成功
- [ ] 输出目录包含 `.x86_64` 可执行文件
- [ ] 输出目录包含 `_Data` 文件夹

---

## 第三步：部署 Unity Server

### 3.1 上传 Server 文件

```bash
# 在本地 Windows 上（PowerShell 或 Git Bash）
scp -r Builds/LinuxServer/* your-username@your-server-ip:/home/your-username/MorphisServer/
scp deploy/setup-unity-server.sh your-username@your-server-ip:/tmp/
```

- [ ] Server 文件上传成功（可能需要几分钟）
- [ ] 部署脚本上传成功

### 3.2 运行部署脚本

```bash
# SSH 连接到服务器
ssh your-username@your-server-ip

# 运行部署脚本
cd /tmp
chmod +x setup-unity-server.sh
./setup-unity-server.sh /home/your-username/MorphisServer
```

脚本会询问：
- World ID（默认 `prod-world-001`）
- Backend URL（默认 `http://localhost:8000`）

- [ ] 脚本执行成功
- [ ] Unity Server 服务已启动

### 3.3 验证 Unity Server

```bash
# 检查服务状态
sudo systemctl status morphis-server

# 检查端口监听
sudo netstat -tulpn | grep 7777

# 查看日志
tail -f /var/log/morphis-server.log
```

- [ ] 服务状态为 `active (running)`
- [ ] 端口 7777 正在监听
- [ ] 日志显示 "Starting Mirror in SERVER mode"
- [ ] 日志无严重错误

---

## 第四步：配置网络

### 4.1 防火墙配置

```bash
# 查看防火墙状态
sudo ufw status

# 确认以下端口已开放
# 22/tcp   - SSH
# 7777/tcp - Unity Server
# 8000/tcp - Python Backend
```

- [ ] 防火墙已启用
- [ ] 端口 7777 已开放
- [ ] 端口 8000 已开放

### 4.2 云服务商安全组配置

在云服务商控制台（阿里云/腾讯云/AWS 等）：

- [ ] 安全组规则已添加：
  - [ ] TCP 22 (SSH)
  - [ ] TCP 7777 (Unity Server)
  - [ ] TCP 8000 (Backend API)

### 4.3 测试外网连通性

在本地 Windows 上：

```bash
# 测试 Backend
curl http://your-server-ip:8000/health

# 测试 Unity Server 端口
telnet your-server-ip 7777
# 或
Test-NetConnection -ComputerName your-server-ip -Port 7777
```

- [ ] Backend API 可访问
- [ ] Unity Server 端口可连接

---

## 第五步：配置客户端

### 5.1 创建客户端配置

在项目根目录创建 `config.json`:

```json
{
  "GameServerAddress": "your-server-ip-or-domain",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://your-server-ip-or-domain:8000",
  "DefaultWorldId": "prod-world-001"
}
```

替换：
- `your-server-ip-or-domain` → 你的服务器 IP 或域名

- [ ] 配置文件已创建
- [ ] 地址已正确填写

### 5.2 构建客户端

1. 在 Unity 中 `File > Build Settings`
2. 选择 `Windows` 平台
3. 确保 `Dedicated Server` 未勾选
4. 点击 `Build`
5. 选择输出目录（如 `Builds/WindowsClient`）

- [ ] 客户端构建成功

### 5.3 测试客户端连接

1. 运行客户端 exe
2. 登录（默认账号: `111111` / `111111`）
3. 选择空间
4. 进入游戏

- [ ] 客户端成功启动
- [ ] 可以登录
- [ ] 可以连接到服务器
- [ ] 可以看到其他玩家（如果有）

---

## 第六步：配置监控和备份

### 6.1 设置健康检查

```bash
# 上传健康检查脚本
scp deploy/health-check.sh your-username@your-server-ip:/home/your-username/

# 在服务器上
chmod +x /home/your-username/health-check.sh

# 添加到 crontab
crontab -e
# 添加：*/5 * * * * /home/your-username/health-check.sh >> /var/log/morphis-health.log 2>&1
```

- [ ] 健康检查脚本已配置
- [ ] Crontab 已添加

### 6.2 设置自动备份

```bash
# 上传备份脚本
scp deploy/backup.sh your-username@your-server-ip:/home/your-username/

# 在服务器上
chmod +x /home/your-username/backup.sh

# 添加到 crontab
crontab -e
# 添加：0 2 * * * /home/your-username/backup.sh >> /var/log/morphis-backup.log 2>&1
```

- [ ] 备份脚本已配置
- [ ] Crontab 已添加
- [ ] 手动运行一次测试: `./backup.sh`

---

## 第七步：安全加固（推荐）

### 7.1 配置 SSL（如果使用域名）

```bash
# 安装 Certbot
sudo apt install -y certbot python3-certbot-nginx

# 获取证书
sudo certbot --nginx -d api.yourdomain.com

# 测试自动续期
sudo certbot renew --dry-run
```

- [ ] SSL 证书已安装
- [ ] HTTPS 可访问
- [ ] 自动续期已配置

### 7.2 配置 fail2ban

```bash
sudo apt install -y fail2ban
sudo systemctl enable fail2ban
sudo systemctl start fail2ban
```

- [ ] fail2ban 已安装并启动

### 7.3 禁用 root SSH 登录

```bash
sudo nano /etc/ssh/sshd_config
# 设置: PermitRootLogin no
sudo systemctl restart sshd
```

- [ ] root 登录已禁用

---

## 部署完成检查

### 服务状态
```bash
sudo systemctl status morphis-backend
sudo systemctl status morphis-server
```
- [ ] Backend 服务运行正常
- [ ] Unity Server 服务运行正常

### 端口监听
```bash
sudo netstat -tulpn | grep -E '7777|8000'
```
- [ ] 端口 7777 正在监听
- [ ] 端口 8000 正在监听

### 日志检查
```bash
sudo journalctl -u morphis-backend -n 50
sudo journalctl -u morphis-server -n 50
```
- [ ] Backend 日志无错误
- [ ] Unity Server 日志无错误

### 客户端测试
- [ ] 客户端可以连接
- [ ] 可以登录
- [ ] 可以创建/加入空间
- [ ] 可以放置物体
- [ ] 可以保存世界数据
- [ ] 多个客户端可以互相看到

---

## 常用命令

### 服务管理
```bash
# 启动
sudo systemctl start morphis-backend
sudo systemctl start morphis-server

# 停止
sudo systemctl stop morphis-backend
sudo systemctl stop morphis-server

# 重启
sudo systemctl restart morphis-backend
sudo systemctl restart morphis-server

# 查看状态
sudo systemctl status morphis-backend
sudo systemctl status morphis-server
```

### 日志查看
```bash
# 实时日志
sudo journalctl -u morphis-backend -f
sudo journalctl -u morphis-server -f

# 最近 100 行
sudo journalctl -u morphis-backend -n 100

# 错误日志
sudo journalctl -u morphis-backend -p err
```

### 资源监控
```bash
htop              # CPU/内存
df -h             # 磁盘空间
free -h           # 内存使用
sudo netstat -tulpn  # 端口监听
```

---

## 故障排查

### Backend 无法启动
1. 检查日志: `sudo journalctl -u morphis-backend -n 100`
2. 检查数据库: `psql -U morphis_user -d morphis_db -h localhost`
3. 检查端口: `sudo netstat -tulpn | grep 8000`
4. 手动启动测试: `cd ~/Morphis/Backend && source venv/bin/activate && python server.py`

### Unity Server 无法启动
1. 检查日志: `tail -f /var/log/morphis-server.log`
2. 检查配置: `cat ~/MorphisServer/config.json`
3. 检查权限: `ls -la ~/MorphisServer/*.x86_64`
4. 手动启动测试: `cd ~/MorphisServer && ./Morphis.x86_64 --mode=server -batchmode -nographics`

### 客户端无法连接
1. 检查服务器防火墙: `sudo ufw status`
2. 检查云服务商安全组
3. 测试端口: `telnet your-server-ip 7777`
4. 检查客户端配置: `config.json`
5. 查看服务器日志: `sudo journalctl -u morphis-server -f`

---

## 下一步优化

- [ ] 配置 Nginx 反向代理
- [ ] 配置 CDN 加速
- [ ] 配置监控告警（如 Prometheus + Grafana）
- [ ] 配置日志收集（如 ELK Stack）
- [ ] 配置负载均衡（多服务器）
- [ ] 配置数据库主从复制
- [ ] 性能调优

---

## 支持

如遇问题，请查看：
- 详细部署指南: `DEPLOYMENT_GUIDE.md`
- 部署脚本说明: `deploy/README.md`
- 配置文件说明: `CONFIG_SETUP.md`

或查看日志文件：
- Backend: `sudo journalctl -u morphis-backend -f`
- Unity Server: `tail -f /var/log/morphis-server.log`
