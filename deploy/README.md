# 部署脚本说明

本目录包含自动化部署脚本，用于快速在云服务器上部署 Morphis 项目。

## 文件说明

- `setup-server.sh` - Python Backend 自动部署脚本
- `setup-unity-server.sh` - Unity Server 自动部署脚本
- `client-config-production.json` - 客户端生产环境配置模板
- `health-check.sh` - 服务健康检查脚本
- `backup.sh` - 数据备份脚本

## 快速开始

### 1. 部署 Python Backend

```bash
# 上传脚本到服务器
scp deploy/setup-server.sh user@your-server:/tmp/

# SSH 连接到服务器
ssh user@your-server

# 运行部署脚本
cd /tmp
chmod +x setup-server.sh
./setup-server.sh
```

脚本会自动：
- 安装所有依赖（Python、PostgreSQL、Nginx）
- 配置数据库
- 创建虚拟环境并安装 Python 包
- 创建 Systemd 服务
- 配置防火墙

### 2. 构建 Unity Server

在 Windows 开发机上：

1. 打开 Unity 项目
2. `File > Build Settings`
3. 选择 `Dedicated Server` 平台
4. 选择 `Linux` 目标
5. 点击 `Switch Platform`
6. 复制 `deploy/client-config-production.json` 到 `Assets/StreamingAssets/config.json`
7. 编辑配置文件，填入你的服务器地址
8. 点击 `Build`，选择输出目录（如 `Builds/LinuxServer`）

### 3. 上传 Unity Server

```bash
# 在本地 Windows 上（PowerShell 或 Git Bash）
scp -r Builds/LinuxServer/* user@your-server:/home/user/MorphisServer/
scp deploy/setup-unity-server.sh user@your-server:/tmp/
```

### 4. 部署 Unity Server

```bash
# SSH 连接到服务器
ssh user@your-server

# 运行部署脚本
cd /tmp
chmod +x setup-unity-server.sh
./setup-unity-server.sh /home/user/MorphisServer
```

### 5. 验证部署

```bash
# 检查服务状态
sudo systemctl status morphis-backend
sudo systemctl status morphis-server

# 测试 API
curl http://localhost:8000/health

# 检查端口
sudo netstat -tulpn | grep 7777
sudo netstat -tulpn | grep 8000

# 查看日志
sudo journalctl -u morphis-backend -f
sudo journalctl -u morphis-server -f
```

## 配置客户端

### 开发环境

使用 `config.json`:
```json
{
  "GameServerAddress": "127.0.0.1",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://127.0.0.1:8000",
  "DefaultWorldId": "dev-world"
}
```

### 生产环境

1. 复制 `deploy/client-config-production.json` 到项目根目录
2. 重命名为 `config.json`
3. 编辑文件，替换 `your-server-ip-or-domain` 为实际地址
4. 构建客户端

示例：
```json
{
  "GameServerAddress": "game.example.com",
  "GameServerPort": 7777,
  "ApiBaseUrl": "https://api.example.com",
  "DefaultWorldId": "prod-world-001"
}
```

## 服务管理

### 启动/停止服务

```bash
# Backend
sudo systemctl start morphis-backend
sudo systemctl stop morphis-backend
sudo systemctl restart morphis-backend

# Unity Server
sudo systemctl start morphis-server
sudo systemctl stop morphis-server
sudo systemctl restart morphis-server
```

### 查看日志

```bash
# 实时日志
sudo journalctl -u morphis-backend -f
sudo journalctl -u morphis-server -f

# 最近 100 行
sudo journalctl -u morphis-backend -n 100

# 错误日志
sudo journalctl -u morphis-backend -p err
```

### 查看服务状态

```bash
sudo systemctl status morphis-backend
sudo systemctl status morphis-server
```

## 故障排查

### Backend 无法启动

```bash
# 查看详细日志
sudo journalctl -u morphis-backend -n 100 --no-pager

# 检查数据库连接
psql -U morphis_user -d morphis_db -h localhost

# 手动启动测试
cd /home/user/Morphis/Backend
source venv/bin/activate
python server.py
```

### Unity Server 无法启动

```bash
# 查看日志
tail -f /var/log/morphis-server.log

# 检查配置
cat /home/user/MorphisServer/config.json

# 手动启动测试
cd /home/user/MorphisServer
./Morphis.x86_64 --mode=server --worldId=test -batchmode -nographics
```

### 客户端无法连接

```bash
# 检查防火墙
sudo ufw status

# 检查端口监听
sudo netstat -tulpn | grep 7777

# 测试连通性（在客户端执行）
telnet your-server-ip 7777
```

## 安全建议

1. **修改默认数据库密码**
2. **配置 SSL 证书**（使用 Let's Encrypt）
3. **启用 fail2ban** 防止暴力破解
4. **定期备份数据**
5. **监控服务器资源**

## 更多信息

详细部署指南请参考：`DEPLOYMENT_GUIDE.md`
