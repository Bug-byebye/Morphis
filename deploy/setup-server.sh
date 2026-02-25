#!/bin/bash
#
# Morphis 服务器自动部署脚本
# 用途：在全新的 Ubuntu 服务器上一键部署 Python Backend
#
# 使用方法：
#   chmod +x setup-server.sh
#   ./setup-server.sh
#

set -e  # 遇到错误立即退出

echo "========================================="
echo "  Morphis Server Deployment Script"
echo "========================================="
echo ""

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# # 检查是否为 root 用户
# if [ "$EUID" -eq 0 ]; then 
#     echo -e "${RED}请不要使用 root 用户运行此脚本${NC}"
#     echo "建议创建普通用户并使用 sudo"
#     exit 1
# fi

# 获取配置
read -p "请输入数据库密码（默认: morphis123）: " DB_PASSWORD
DB_PASSWORD=${DB_PASSWORD:-morphis123}

read -p "请输入部署目录（默认: /home/$USER/Morphis）: " DEPLOY_DIR
DEPLOY_DIR=${DEPLOY_DIR:-/home/$USER/Morphis}

read -p "请输入 worldId（默认: prod-world-001）: " WORLD_ID
WORLD_ID=${WORLD_ID:-prod-world-001}

echo ""
echo -e "${GREEN}配置信息：${NC}"
echo "  部署目录: $DEPLOY_DIR"
echo "  数据库密码: ********"
echo "  World ID: $WORLD_ID"
echo ""
read -p "确认继续？(y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    exit 1
fi

echo ""
echo -e "${YELLOW}[1/8] 更新系统...${NC}"
sudo apt update
sudo apt upgrade -y

echo ""
echo -e "${YELLOW}[2/8] 安装基础依赖...${NC}"
sudo apt install -y curl wget git unzip screen tmux
sudo apt install -y python3.10 python3.10-venv python3-pip
sudo apt install -y postgresql postgresql-contrib
sudo apt install -y nginx

echo ""
echo -e "${YELLOW}[3/8] 配置 PostgreSQL...${NC}"
sudo -u postgres psql -c "CREATE DATABASE morphis_db;" 2>/dev/null || echo "数据库已存在"
sudo -u postgres psql -c "CREATE USER morphis_user WITH PASSWORD '$DB_PASSWORD';" 2>/dev/null || echo "用户已存在"
sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE morphis_db TO morphis_user;"
sudo -u postgres psql -c "ALTER DATABASE morphis_db OWNER TO morphis_user;"

echo ""
echo -e "${YELLOW}[4/8] 克隆代码...${NC}"
if [ -d "$DEPLOY_DIR" ]; then
    echo "目录已存在，跳过克隆"
    cd "$DEPLOY_DIR"
    git pull || echo "无法更新代码，继续..."
else
    read -p "请输入 Git 仓库地址: " GIT_REPO
    git clone "$GIT_REPO" "$DEPLOY_DIR"
    cd "$DEPLOY_DIR"
fi

echo ""
echo -e "${YELLOW}[5/8] 配置 Python Backend...${NC}"
cd "$DEPLOY_DIR/Backend"

# 创建虚拟环境
python3 -m venv venv
source venv/bin/activate

# 安装依赖
pip install --upgrade pip
pip install -r requirements.txt

# 获取服务器公网 IP
SERVER_IP=$(curl -s ifconfig.me || echo "127.0.0.1")
echo "检测到服务器 IP: $SERVER_IP"

# 创建 .env 文件
cat > .env << EOF
DATABASE_URL=postgresql://morphis_user:$DB_PASSWORD@localhost:5432/morphis_db
HOST=0.0.0.0
PORT=8000

# World 进程管理
UNITY_SERVER_PATH=$DEPLOY_DIR/MorphisServer/Morphis.x86_64
SERVER_PUBLIC_IP=$SERVER_IP
API_BASE_URL=http://localhost:8000
EOF

chmod 600 .env

# 创建 World 日志目录
sudo mkdir -p /var/log/morphis-worlds
sudo chown $USER:$USER /var/log/morphis-worlds
echo "World 日志目录已创建: /var/log/morphis-worlds"

echo ""
echo -e "${YELLOW}[6/8] 初始化数据库...${NC}"
# 启动服务器一次以创建表结构
timeout 10 python server.py || echo "数据库初始化完成"

echo ""
echo -e "${YELLOW}[7/8] 创建 Systemd 服务...${NC}"

# Backend 服务
sudo tee /etc/systemd/system/morphis-backend.service > /dev/null << EOF
[Unit]
Description=Morphis Backend API
After=network.target postgresql.service

[Service]
Type=simple
User=$USER
WorkingDirectory=$DEPLOY_DIR/Backend
Environment="PATH=$DEPLOY_DIR/Backend/venv/bin"
ExecStart=$DEPLOY_DIR/Backend/venv/bin/python server.py
Restart=always
RestartSec=10

StandardOutput=append:/var/log/morphis-backend.log
StandardError=append:/var/log/morphis-backend-error.log

[Install]
WantedBy=multi-user.target
EOF

# 创建日志文件
sudo touch /var/log/morphis-backend.log
sudo touch /var/log/morphis-backend-error.log
sudo chown $USER:$USER /var/log/morphis-backend*.log

# 启动服务
sudo systemctl daemon-reload
sudo systemctl enable morphis-backend
sudo systemctl start morphis-backend

echo ""
echo -e "${YELLOW}[8/8] 配置防火墙...${NC}"
sudo ufw allow 22/tcp
sudo ufw allow 7777:7826/tcp  # World 端口范围
sudo ufw allow 8000/tcp
sudo ufw --force enable

echo ""
echo -e "${GREEN}=========================================${NC}"
echo -e "${GREEN}  部署完成！${NC}"
echo -e "${GREEN}=========================================${NC}"
echo ""
echo "服务状态："
sudo systemctl status morphis-backend --no-pager -l
echo ""
echo "测试 API："
sleep 3
curl http://localhost:8000/health
echo ""
echo ""
echo "架构信息："
echo "  - 多 World 动态调度架构"
echo "  - 端口范围: 7777-7826 (最多 50 个 World)"
echo "  - 空闲超时: 5 分钟自动关闭"
echo "  - 服务器 IP: $SERVER_IP"
echo ""
echo "下一步："
echo "  1. 上传 Unity Server 构建到 $DEPLOY_DIR/MorphisServer"
echo "  2. 设置可执行权限: chmod +x $DEPLOY_DIR/MorphisServer/Morphis.x86_64"
echo "  3. 客户端将自动连接到动态分配的 World"
echo ""
echo "管理命令："
echo "  查看 Backend 日志: sudo journalctl -u morphis-backend -f"
echo "  查看 World 列表: curl http://localhost:8000/worlds/manage/list"
echo "  查看 World 日志: tail -f /var/log/morphis-worlds/<world_id>.log"
echo ""
