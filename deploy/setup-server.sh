#!/bin/bash
#
# Morphis 服务器自动部署脚本（多 World 动态调度架构）
# 用途：在全新的 Ubuntu 服务器上一键部署 Python Backend
#
# 使用方法：
#   chmod +x setup-server.sh
#   ./setup-server.sh
#

set -e  # 遇到错误立即退出

echo "========================================="
echo "  Morphis Server Deployment Script"
echo "  Multi-World Dynamic Scheduling"
echo "========================================="
echo ""

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 确定默认部署目录（处理 root 用户的情况）
if [ "$USER" = "root" ]; then
    DEFAULT_DEPLOY_DIR="/root/Morphis"
else
    DEFAULT_DEPLOY_DIR="/home/$USER/Morphis"
fi

# 获取配置
read -p "请输入数据库密码（默认: morphis123）: " DB_PASSWORD
DB_PASSWORD=${DB_PASSWORD:-morphis123}

read -p "请输入部署目录（默认: $DEFAULT_DEPLOY_DIR）: " DEPLOY_DIR
DEPLOY_DIR=${DEPLOY_DIR:-$DEFAULT_DEPLOY_DIR}

echo ""
echo -e "${GREEN}配置信息：${NC}"
echo "  部署目录: $DEPLOY_DIR"
echo "  数据库密码: ********"
echo ""
read -p "确认继续？(y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    exit 1
fi

echo ""
echo -e "${YELLOW}[1/9] 更新系统...${NC}"
sudo apt update
sudo apt upgrade -y

echo ""
echo -e "${YELLOW}[2/9] 安装基础依赖...${NC}"
sudo apt install -y curl wget git unzip
sudo apt install -y python3.10 python3.10-venv python3-pip
sudo apt install -y postgresql postgresql-contrib
sudo apt install -y net-tools  # netstat

echo ""
echo -e "${YELLOW}[3/9] 配置 PostgreSQL...${NC}"
# 切换到 /tmp 目录避免权限问题
cd /tmp
sudo -u postgres psql -c "CREATE DATABASE morphis_db;" 2>/dev/null || echo "数据库已存在"
sudo -u postgres psql -c "CREATE USER morphis_user WITH PASSWORD '$DB_PASSWORD';" 2>/dev/null || echo "用户已存在"
sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE morphis_db TO morphis_user;"
sudo -u postgres psql -c "ALTER DATABASE morphis_db OWNER TO morphis_user;"

echo ""
echo -e "${YELLOW}[4/9] 准备项目目录...${NC}"

# 智能检测：如果脚本在 deploy 目录下运行，自动使用父目录
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ "$SCRIPT_DIR" == */deploy ]]; then
    AUTO_DEPLOY_DIR="$(dirname "$SCRIPT_DIR")"
    echo "检测到脚本在 deploy 目录下运行"
    echo "自动检测到项目目录: $AUTO_DEPLOY_DIR"
    
    # 如果用户没有修改默认值，使用自动检测的目录
    if [ "$DEPLOY_DIR" = "$DEFAULT_DEPLOY_DIR" ]; then
        DEPLOY_DIR="$AUTO_DEPLOY_DIR"
        echo -e "${GREEN}使用自动检测的目录: $DEPLOY_DIR${NC}"
    fi
fi

if [ ! -d "$DEPLOY_DIR" ]; then
    echo -e "${RED}错误：项目目录不存在: $DEPLOY_DIR${NC}"
    echo "请先上传项目代码到服务器，或使用 git clone"
    echo ""
    read -p "是否现在克隆 Git 仓库？(y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        read -p "请输入 Git 仓库地址: " GIT_REPO
        if [ -n "$GIT_REPO" ]; then
            git clone "$GIT_REPO" "$DEPLOY_DIR"
        else
            echo -e "${RED}未提供仓库地址，退出${NC}"
            exit 1
        fi
    else
        echo -e "${RED}请手动上传项目后重新运行此脚本${NC}"
        exit 1
    fi
fi

# 验证目录结构
if [ ! -d "$DEPLOY_DIR/Backend" ]; then
    echo -e "${RED}错误：Backend 目录不存在: $DEPLOY_DIR/Backend${NC}"
    echo "请确保项目结构正确"
    exit 1
fi

echo -e "${GREEN}项目目录验证通过: $DEPLOY_DIR${NC}"

cd "$DEPLOY_DIR"

echo ""
echo -e "${YELLOW}[5/9] 配置 Python Backend...${NC}"
cd "$DEPLOY_DIR/Backend"

# 创建虚拟环境
python3 -m venv venv
source venv/bin/activate

# 安装依赖
pip install --upgrade pip
pip install -r requirements.txt

# 获取服务器公网 IP
echo "正在检测服务器公网 IP..."
SERVER_IP=$(curl -s ifconfig.me || curl -s icanhazip.com || echo "127.0.0.1")
echo "检测到服务器 IP: $SERVER_IP"

# 创建 .env 文件
cat > .env << EOF
# 数据库配置
DATABASE_URL=postgresql://morphis_user:$DB_PASSWORD@localhost:5432/morphis_db

# 服务器配置
HOST=0.0.0.0
PORT=8000

# World 进程管理配置
UNITY_SERVER_PATH=$DEPLOY_DIR/MorphisServer/Morphis.x86_64
SERVER_PUBLIC_IP=$SERVER_IP
API_BASE_URL=http://localhost:8000
EOF

chmod 600 .env
echo ".env 文件已创建"

# 创建 World 日志目录
sudo mkdir -p /var/log/morphis-worlds
sudo chown $USER:$USER /var/log/morphis-worlds
echo "World 日志目录已创建: /var/log/morphis-worlds"

echo ""
echo -e "${YELLOW}[6/9] 初始化数据库...${NC}"
# 启动服务器一次以创建表结构
timeout 10 python server.py || echo "数据库初始化完成"

echo ""
echo -e "${YELLOW}[7/9] 创建 Systemd 服务...${NC}"

# Backend 服务
sudo tee /etc/systemd/system/morphis-backend.service > /dev/null << EOF
[Unit]
Description=Morphis Backend API (Multi-World Manager)
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
echo -e "${YELLOW}[8/9] 配置防火墙...${NC}"
sudo ufw allow 22/tcp
sudo ufw allow 7777:7826/tcp  # World 端口范围（最多 50 个 World）
sudo ufw allow 8000/tcp
sudo ufw --force enable

echo ""
echo -e "${YELLOW}[9/9] 创建 Unity Server 目录...${NC}"
mkdir -p "$DEPLOY_DIR/MorphisServer"
echo "Unity Server 目录已创建: $DEPLOY_DIR/MorphisServer"

echo ""
echo -e "${GREEN}=========================================${NC}"
echo -e "${GREEN}  Backend 部署完成！${NC}"
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
echo -e "${GREEN}架构信息：${NC}"
echo "  ✅ 多 World 动态调度架构"
echo "  ✅ 端口范围: 7777-7826 (最多 50 个 World)"
echo "  ✅ 空闲超时: 5 分钟自动关闭"
echo "  ✅ 服务器 IP: $SERVER_IP"
echo ""
echo -e "${YELLOW}下一步：${NC}"
echo "  1. 在 Unity 中构建 Linux Dedicated Server"
echo "  2. 上传构建文件到: $DEPLOY_DIR/MorphisServer/"
echo "     scp -r Builds/LinuxServer/* $USER@$SERVER_IP:$DEPLOY_DIR/MorphisServer/"
echo "  3. 设置可执行权限:"
echo "     chmod +x $DEPLOY_DIR/MorphisServer/Morphis.x86_64"
echo "  4. 测试 World 管理:"
echo "     cd $DEPLOY_DIR/Backend && source venv/bin/activate"
echo "     python test_world_manager.py"
echo ""
echo -e "${YELLOW}管理命令：${NC}"
echo "  查看 Backend 日志: sudo journalctl -u morphis-backend -f"
echo "  查看 World 列表: curl http://localhost:8000/worlds/manage/list"
echo "  查看 World 日志: tail -f /var/log/morphis-worlds/<world-id>.log"
echo "  重启 Backend: sudo systemctl restart morphis-backend"
echo ""
echo -e "${YELLOW}配置文件位置：${NC}"
echo "  Backend .env: $DEPLOY_DIR/Backend/.env"
echo "  Unity Server config: $DEPLOY_DIR/MorphisServer/config.json (需要创建)"
echo ""
echo -e "${GREEN}部署文档：${NC}"
echo "  完整指南: $DEPLOY_DIR/DEPLOY_MULTI_WORLD.md"
echo "  架构说明: $DEPLOY_DIR/MULTI_WORLD_ARCHITECTURE.md"
echo ""

