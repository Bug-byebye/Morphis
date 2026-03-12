# Morphis 云服务器部署指南

## 目录
1. [架构概览](#架构概览)
2. [服务器要求](#服务器要求)
3. [部署前准备](#部署前准备)
4. [Python 后端部署](#python-后端部署)
5. [Unity Server 部署](#unity-server-部署)
6. [网络配置](#网络配置)
7. [监控与维护](#监控与维护)
8. [故障排查](#故障排查)

---

## 架构概览

```
┌─────────────────┐
│  Windows 客户端  │
│   (Unity.exe)   │
└────────┬────────┘
         │
         │ Mirror (TCP 7777)
         │ HTTP API (8000)
         ↓
┌─────────────────────────────────┐
│      Ubuntu 云服务器             │
│                                 │
│  ┌──────────────────────────┐  │
│  │  Unity Server (Headless) │  │
│  │  Port: 7777              │  │
│  └──────────┬───────────────┘  │
│             │                   │
│             │ HTTP              │
│             ↓                   │
│  ┌──────────────────────────┐  │
│  │  Python Backend (FastAPI)│  │
│  │  Port: 8000              │  │
│  └──────────┬───────────────┘  │
│             │                   │
│             │ PostgreSQL        │
│             ↓                   │
│  ┌──────────────────────────┐  │
│  │  PostgreSQL Database     │  │
│  │  Port: 5432 (内部)       │  │
│  └──────────────────────────┘  │
└─────────────────────────────────┘
```

---

## 服务器要求

### 最低配置
- **CPU**: 2 核
- **内存**: 4 GB RAM
- **存储**: 20 GB SSD
- **系统**: Ubuntu 20.04 LTS 或更高
- **网络**: 公网 IP，至少 5 Mbps 上行带宽

### 推荐配置（生产环境）
- **CPU**: 4 核
- **内存**: 8 GB RAM
- **存储**: 50 GB SSD
- **系统**: Ubuntu 22.04 LTS
- **网络**: 公网 IP，至少 10 Mbps 上行带宽

### 端口需求
- **7777**: Unity Server (Mirror 网络)
- **8000**: Python Backend (HTTP API)
- **22**: SSH (管理用)
- **5432**: PostgreSQL (仅内部访问，不对外开放)

---

## 部署前准备

### 1. 连接到服务器

```bash
ssh root@your-server-ip
# 或使用密钥
ssh -i ~/.ssh/your-key.pem ubuntu@your-server-ip
```

### 2. 更新系统

```bash
sudo apt update
sudo apt upgrade -y
```

### 3. 创建部署用户（推荐）

```bash
# 创建专用用户
sudo adduser morphis
sudo usermod -aG sudo morphis

# 切换到新用户
su - morphis
```

### 4. 安装基础依赖

```bash
# 安装必要工具
sudo apt install -y curl wget git unzip screen tmux

# 安装 Python 3.10+
sudo apt install -y python3.10 python3.10-venv python3-pip

# 安装 PostgreSQL
sudo apt install -y postgresql postgresql-contrib

# 安装 Nginx (可选，用于反向代理)
sudo apt install -y nginx
```

---

## Python 后端部署

### 1. 上传代码

**方式 A: 使用 Git（推荐）**
```bash
cd /home/morphis
git clone https://github.com/your-username/Morphis.git
cd Morphis/Backend
```

**方式 B: 使用 SCP**
```bash
# 在本地执行
scp -r Backend/ morphis@your-server-ip:/home/morphis/
```

### 2. 配置数据库

```bash
# 切换到 postgres 用户
sudo -u postgres psql

# 在 PostgreSQL 中执行
CREATE DATABASE morphis_db;
CREATE USER morphis_user WITH PASSWORD 'your_secure_password';
GRANT ALL PRIVILEGES ON DATABASE morphis_db TO morphis_user;
\q
```

### 3. 配置环境变量

```bash
cd /home/morphis/Morphis/Backend

# 创建 .env 文件
cat > .env << 'EOF'
# Database
DATABASE_URL=postgresql://morphis_user:your_secure_password@localhost:5432/morphis_db

# API Keys (如果需要)
OPENAI_API_KEY=your_openai_key_here
VOLCENGINE_ACCESS_KEY=your_volcengine_key_here
VOLCENGINE_SECRET_KEY=your_volcengine_secret_here

# Server
HOST=0.0.0.0
PORT=8000
EOF

chmod 600 .env  # 保护敏感信息
```

### 4. 安装 Python 依赖

```bash
# 创建虚拟环境
python3 -m venv venv
source venv/bin/activate

# 安装依赖
pip install --upgrade pip
pip install -r requirements.txt
```

### 5. 初始化数据库

```bash
# 运行服务器一次以创建表结构
python server.py &
sleep 5
pkill -f server.py

# 或者如果有迁移脚本
# python migrate.py
```

### 6. 创建 Systemd 服务（推荐）

```bash
sudo nano /etc/systemd/system/morphis-backend.service
```

内容：
```ini
[Unit]
Description=Morphis Backend API
After=network.target postgresql.service

[Service]
Type=simple
User=morphis
WorkingDirectory=/home/morphis/Morphis/Backend
Environment="PATH=/home/morphis/Morphis/Backend/venv/bin"
ExecStart=/home/morphis/Morphis/Backend/venv/bin/python server.py
Restart=always
RestartSec=10

# 日志
StandardOutput=append:/var/log/morphis-backend.log
StandardError=append:/var/log/morphis-backend-error.log

[Install]
WantedBy=multi-user.target
```

启动服务：
```bash
sudo systemctl daemon-reload
sudo systemctl enable morphis-backend
sudo systemctl start morphis-backend
sudo systemctl status morphis-backend
```

### 7. 验证后端运行

```bash
# 检查服务状态
sudo systemctl status morphis-backend

# 测试 API
curl http://localhost:8000/health

# 查看日志
sudo journalctl -u morphis-backend -f
```

---

## Unity Server 部署

### 1. 构建 Unity Server

**在 Windows 开发机上执行：**

#### 步骤 1: 配置 Server 构建

1. 打开 Unity 项目
2. 打开 `File > Build Settings`
3. 选择 `Dedicated Server` 平台
4. 选择 `Linux` 目标平台
5. 点击 `Switch Platform`

#### 步骤 2: 创建 Server 配置文件

在项目根目录创建 `config.server.json`:
```json
{
  "GameServerAddress": "0.0.0.0",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://localhost:8000",
  "DefaultWorldId": "prod-world-001"
}
```

#### 步骤 3: 复制配置到 StreamingAssets

```bash
# 在 Windows 上
copy config.server.json Assets\StreamingAssets\config.json
```

#### 步骤 4: 构建

1. 在 `Build Settings` 中点击 `Build`
2. 选择输出目录（如 `Builds/LinuxServer`）
3. 等待构建完成

### 2. 上传 Server 到云服务器

```bash
# 在本地 Windows 上（使用 PowerShell 或 Git Bash）
scp -r Builds/LinuxServer/* morphis@your-server-ip:/home/morphis/MorphisServer/
```

### 3. 配置 Server

```bash
# 在服务器上
cd /home/morphis/MorphisServer

# 给予执行权限
chmod +x Morphis.x86_64

# 创建生产配置
cat > config.json << 'EOF'
{
  "GameServerAddress": "0.0.0.0",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://localhost:8000",
  "DefaultWorldId": "prod-world-001"
}
EOF
```

### 4. 创建启动脚本

```bash
nano /home/morphis/MorphisServer/start-server.sh
```

内容：
```bash
#!/bin/bash
cd /home/morphis/MorphisServer
./Morphis.x86_64 \
  --mode=server \
  --worldId=prod-world-001 \
  -batchmode \
  -nographics \
  -logFile /var/log/morphis-server.log
```

赋予执行权限：
```bash
chmod +x /home/morphis/MorphisServer/start-server.sh
```

### 5. 创建 Systemd 服务

```bash
sudo nano /etc/systemd/system/morphis-server.service
```

内容：
```ini
[Unit]
Description=Morphis Unity Game Server
After=network.target morphis-backend.service

[Service]
Type=simple
User=morphis
WorkingDirectory=/home/morphis/MorphisServer
ExecStart=/home/morphis/MorphisServer/start-server.sh
Restart=always
RestartSec=10

# 资源限制
LimitNOFILE=65536
LimitNPROC=4096

# 日志
StandardOutput=append:/var/log/morphis-server.log
StandardError=append:/var/log/morphis-server-error.log

[Install]
WantedBy=multi-user.target
```

启动服务：
```bash
sudo systemctl daemon-reload
sudo systemctl enable morphis-server
sudo systemctl start morphis-server
sudo systemctl status morphis-server
```

### 6. 验证 Server 运行

```bash
# 检查服务状态
sudo systemctl status morphis-server

# 检查端口监听
sudo netstat -tulpn | grep 7777

# 查看日志
tail -f /var/log/morphis-server.log
```

---

## 网络配置

### 1. 防火墙配置

```bash
# 启用 UFW
sudo ufw enable

# 允许 SSH
sudo ufw allow 22/tcp

# 允许 Unity Server
sudo ufw allow 7777/tcp

# 允许 Python Backend
sudo ufw allow 8000/tcp

# 查看状态
sudo ufw status
```

### 2. 配置 Nginx 反向代理（可选，推荐）

```bash
sudo nano /etc/nginx/sites-available/morphis
```

内容：
```nginx
# Python Backend
server {
    listen 80;
    server_name api.yourdomain.com;

    location / {
        proxy_pass http://localhost:8000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

启用配置：
```bash
sudo ln -s /etc/nginx/sites-available/morphis /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### 3. 配置 SSL（推荐使用 Let's Encrypt）

```bash
# 安装 Certbot
sudo apt install -y certbot python3-certbot-nginx

# 获取证书
sudo certbot --nginx -d api.yourdomain.com

# 自动续期
sudo certbot renew --dry-run
```

### 4. 配置域名

在你的域名提供商处添加 DNS 记录：

```
A    api.yourdomain.com    -> your-server-ip
A    game.yourdomain.com   -> your-server-ip
```

---

## 监控与维护

### 1. 日志管理

```bash
# 查看实时日志
sudo journalctl -u morphis-backend -f
sudo journalctl -u morphis-server -f

# 查看最近的错误
sudo journalctl -u morphis-backend -p err -n 50

# 日志轮转配置
sudo nano /etc/logrotate.d/morphis
```

内容：
```
/var/log/morphis-*.log {
    daily
    rotate 7
    compress
    delaycompress
    missingok
    notifempty
    create 0640 morphis morphis
}
```

### 2. 性能监控

```bash
# 安装监控工具
sudo apt install -y htop iotop nethogs

# 实时监控
htop                    # CPU/内存
sudo iotop              # 磁盘 I/O
sudo nethogs            # 网络流量
```

### 3. 自动备份脚本

```bash
nano /home/morphis/backup.sh
```

内容：
```bash
#!/bin/bash
BACKUP_DIR="/home/morphis/backups"
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p $BACKUP_DIR

# 备份数据库
sudo -u postgres pg_dump morphis_db > $BACKUP_DIR/db_$DATE.sql

# 备份配置文件
cp /home/morphis/MorphisServer/config.json $BACKUP_DIR/config_$DATE.json
cp /home/morphis/Morphis/Backend/.env $BACKUP_DIR/env_$DATE.txt

# 删除 7 天前的备份
find $BACKUP_DIR -type f -mtime +7 -delete

echo "Backup completed: $DATE"
```

添加到 crontab：
```bash
chmod +x /home/morphis/backup.sh
crontab -e

# 添加：每天凌晨 2 点备份
0 2 * * * /home/morphis/backup.sh >> /var/log/morphis-backup.log 2>&1
```

### 4. 健康检查脚本

```bash
nano /home/morphis/health-check.sh
```

内容：
```bash
#!/bin/bash

# 检查 Backend
if ! curl -s http://localhost:8000/health > /dev/null; then
    echo "Backend is down! Restarting..."
    sudo systemctl restart morphis-backend
fi

# 检查 Unity Server
if ! sudo netstat -tulpn | grep :7777 > /dev/null; then
    echo "Unity Server is down! Restarting..."
    sudo systemctl restart morphis-server
fi
```

添加到 crontab：
```bash
chmod +x /home/morphis/health-check.sh
crontab -e

# 添加：每 5 分钟检查一次
*/5 * * * * /home/morphis/health-check.sh >> /var/log/morphis-health.log 2>&1
```

---

## 故障排查

### 问题 1: Backend 无法启动

```bash
# 查看详细日志
sudo journalctl -u morphis-backend -n 100 --no-pager

# 检查数据库连接
psql -U morphis_user -d morphis_db -h localhost

# 检查端口占用
sudo netstat -tulpn | grep 8000

# 手动启动测试
cd /home/morphis/Morphis/Backend
source venv/bin/activate
python server.py
```

### 问题 2: Unity Server 无法启动

```bash
# 查看日志
tail -f /var/log/morphis-server.log

# 检查配置文件
cat /home/morphis/MorphisServer/config.json

# 检查文件权限
ls -la /home/morphis/MorphisServer/

# 手动启动测试
cd /home/morphis/MorphisServer
./Morphis.x86_64 --mode=server --worldId=test -batchmode -nographics
```

### 问题 3: 客户端无法连接

```bash
# 检查防火墙
sudo ufw status

# 检查端口监听
sudo netstat -tulpn | grep 7777

# 测试网络连通性（在客户端执行）
telnet your-server-ip 7777
nc -zv your-server-ip 7777

# 检查服务器日志
sudo journalctl -u morphis-server -f
```

### 问题 4: 数据库连接失败

```bash
# 检查 PostgreSQL 状态
sudo systemctl status postgresql

# 检查连接
psql -U morphis_user -d morphis_db -h localhost

# 查看 PostgreSQL 日志
sudo tail -f /var/log/postgresql/postgresql-*.log

# 重启数据库
sudo systemctl restart postgresql
```

### 问题 5: 内存不足

```bash
# 查看内存使用
free -h

# 查看进程内存
ps aux --sort=-%mem | head -10

# 创建 swap（如果没有）
sudo fallocate -l 4G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

---

## 快速命令参考

```bash
# 服务管理
sudo systemctl start morphis-backend
sudo systemctl stop morphis-backend
sudo systemctl restart morphis-backend
sudo systemctl status morphis-backend

sudo systemctl start morphis-server
sudo systemctl stop morphis-server
sudo systemctl restart morphis-server
sudo systemctl status morphis-server

# 日志查看
sudo journalctl -u morphis-backend -f
sudo journalctl -u morphis-server -f
tail -f /var/log/morphis-server.log

# 端口检查
sudo netstat -tulpn | grep 7777
sudo netstat -tulpn | grep 8000

# 进程检查
ps aux | grep Morphis
ps aux | grep python

# 资源监控
htop
df -h
free -h
```

---

## 安全建议

1. **定期更新系统**
   ```bash
   sudo apt update && sudo apt upgrade -y
   ```

2. **配置 fail2ban 防止暴力破解**
   ```bash
   sudo apt install -y fail2ban
   sudo systemctl enable fail2ban
   ```

3. **禁用 root SSH 登录**
   ```bash
   sudo nano /etc/ssh/sshd_config
   # 设置: PermitRootLogin no
   sudo systemctl restart sshd
   ```

4. **使用密钥认证而非密码**

5. **定期备份数据**

6. **监控异常流量**

---

## 下一步

部署完成后：

1. ✅ 测试客户端连接
2. ✅ 创建测试账号并登录
3. ✅ 验证世界数据保存/加载
4. ✅ 配置监控和告警
5. ✅ 设置自动备份
6. ✅ 配置 SSL 证书
7. ✅ 性能调优

---

## 支持

如有问题，请查看：
- 日志文件: `/var/log/morphis-*.log`
- 系统日志: `sudo journalctl -xe`
- 项目文档: `README.md`
