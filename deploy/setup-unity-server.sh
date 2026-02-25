#!/bin/bash
#
# Unity Server 部署脚本
# 前提：Unity Server 构建已上传到服务器
#
# 使用方法：
#   chmod +x setup-unity-server.sh
#   ./setup-unity-server.sh /path/to/unity/build
#

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

if [ "$EUID" -eq 0 ]; then 
    echo -e "${RED}请不要使用 root 用户运行此脚本${NC}"
    exit 1
fi

# 获取 Unity Server 路径
if [ -z "$1" ]; then
    read -p "请输入 Unity Server 构建目录: " SERVER_DIR
else
    SERVER_DIR=$1
fi

if [ ! -d "$SERVER_DIR" ]; then
    echo -e "${RED}错误：目录不存在: $SERVER_DIR${NC}"
    exit 1
fi

# 查找可执行文件
EXECUTABLE=$(find "$SERVER_DIR" -maxdepth 1 -name "*.x86_64" -type f | head -n 1)
if [ -z "$EXECUTABLE" ]; then
    echo -e "${RED}错误：未找到 .x86_64 可执行文件${NC}"
    exit 1
fi

echo -e "${GREEN}找到可执行文件: $EXECUTABLE${NC}"

# 获取配置
read -p "请输入 worldId（默认: prod-world-001）: " WORLD_ID
WORLD_ID=${WORLD_ID:-prod-world-001}

read -p "请输入 Backend URL（默认: http://localhost:8000）: " API_URL
API_URL=${API_URL:-http://localhost:8000}

echo ""
echo -e "${YELLOW}[1/4] 配置 Unity Server...${NC}"

# 给予执行权限
chmod +x "$EXECUTABLE"

# 创建配置文件
cat > "$SERVER_DIR/config.json" << EOF
{
  "GameServerAddress": "0.0.0.0",
  "GameServerPort": 7777,
  "ApiBaseUrl": "$API_URL",
  "DefaultWorldId": "$WORLD_ID"
}
EOF

echo "配置文件已创建: $SERVER_DIR/config.json"

echo ""
echo -e "${YELLOW}[2/4] 创建启动脚本...${NC}"

cat > "$SERVER_DIR/start-server.sh" << EOF
#!/bin/bash
cd "$SERVER_DIR"
$EXECUTABLE \\
  --mode=server \\
  --worldId=$WORLD_ID \\
  -batchmode \\
  -nographics \\
  -logFile /var/log/morphis-server.log
EOF

chmod +x "$SERVER_DIR/start-server.sh"

echo ""
echo -e "${YELLOW}[3/4] 创建 Systemd 服务...${NC}"

sudo tee /etc/systemd/system/morphis-server.service > /dev/null << EOF
[Unit]
Description=Morphis Unity Game Server
After=network.target morphis-backend.service

[Service]
Type=simple
User=$USER
WorkingDirectory=$SERVER_DIR
ExecStart=$SERVER_DIR/start-server.sh
Restart=always
RestartSec=10

LimitNOFILE=65536
LimitNPROC=4096

StandardOutput=append:/var/log/morphis-server.log
StandardError=append:/var/log/morphis-server-error.log

[Install]
WantedBy=multi-user.target
EOF

# 创建日志文件
sudo touch /var/log/morphis-server.log
sudo touch /var/log/morphis-server-error.log
sudo chown $USER:$USER /var/log/morphis-server*.log

echo ""
echo -e "${YELLOW}[4/4] 启动服务...${NC}"

sudo systemctl daemon-reload
sudo systemctl enable morphis-server
sudo systemctl start morphis-server

echo ""
echo -e "${GREEN}=========================================${NC}"
echo -e "${GREEN}  Unity Server 部署完成！${NC}"
echo -e "${GREEN}=========================================${NC}"
echo ""
echo "服务状态："
sudo systemctl status morphis-server --no-pager -l
echo ""
echo "检查端口监听："
sleep 3
sudo netstat -tulpn | grep 7777 || echo "端口 7777 未监听，请检查日志"
echo ""
echo "查看日志："
echo "  sudo journalctl -u morphis-server -f"
echo "  tail -f /var/log/morphis-server.log"
echo ""
