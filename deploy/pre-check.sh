#!/bin/bash
# 部署前环境检查

echo "========================================="
echo "  Morphis Deployment Pre-Check"
echo "========================================="
echo ""

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

ERRORS=0
WARNINGS=0

# 检查当前目录
echo -e "${YELLOW}[1/8] 检查当前目录...${NC}"
CURRENT_DIR=$(pwd)
if [[ "$CURRENT_DIR" == */deploy ]]; then
    PROJECT_DIR="$(dirname "$CURRENT_DIR")"
    echo -e "${GREEN}✓ 在 deploy 目录下${NC}"
    echo "  项目目录: $PROJECT_DIR"
else
    echo -e "${RED}✗ 请在 deploy 目录下运行此脚本${NC}"
    ERRORS=$((ERRORS + 1))
fi

# 检查 Backend 目录
echo ""
echo -e "${YELLOW}[2/8] 检查项目结构...${NC}"
if [ -d "$PROJECT_DIR/Backend" ]; then
    echo -e "${GREEN}✓ Backend 目录存在${NC}"
else
    echo -e "${RED}✗ Backend 目录不存在${NC}"
    ERRORS=$((ERRORS + 1))
fi

if [ -f "$PROJECT_DIR/Backend/requirements.txt" ]; then
    echo -e "${GREEN}✓ requirements.txt 存在${NC}"
else
    echo -e "${RED}✗ requirements.txt 不存在${NC}"
    ERRORS=$((ERRORS + 1))
fi

# 检查 Python
echo ""
echo -e "${YELLOW}[3/8] 检查 Python...${NC}"
if command -v python3 &> /dev/null; then
    PYTHON_VERSION=$(python3 --version)
    echo -e "${GREEN}✓ Python 已安装: $PYTHON_VERSION${NC}"
else
    echo -e "${RED}✗ Python 未安装${NC}"
    ERRORS=$((ERRORS + 1))
fi

# 检查 PostgreSQL
echo ""
echo -e "${YELLOW}[4/8] 检查 PostgreSQL...${NC}"
if command -v psql &> /dev/null; then
    echo -e "${GREEN}✓ PostgreSQL 已安装${NC}"
else
    echo -e "${YELLOW}⚠ PostgreSQL 未安装（脚本会自动安装）${NC}"
    WARNINGS=$((WARNINGS + 1))
fi

# 检查网络
echo ""
echo -e "${YELLOW}[5/8] 检查网络连接...${NC}"
if ping -c 1 8.8.8.8 &> /dev/null; then
    echo -e "${GREEN}✓ 网络连接正常${NC}"
else
    echo -e "${RED}✗ 网络连接失败${NC}"
    ERRORS=$((ERRORS + 1))
fi

# 检查磁盘空间
echo ""
echo -e "${YELLOW}[6/8] 检查磁盘空间...${NC}"
AVAILABLE=$(df -h / | awk 'NR==2 {print $4}')
echo "  可用空间: $AVAILABLE"
if [ $(df / | awk 'NR==2 {print $4}') -gt 5000000 ]; then
    echo -e "${GREEN}✓ 磁盘空间充足${NC}"
else
    echo -e "${YELLOW}⚠ 磁盘空间可能不足${NC}"
    WARNINGS=$((WARNINGS + 1))
fi

# 检查权限
echo ""
echo -e "${YELLOW}[7/8] 检查权限...${NC}"
if [ "$EUID" -eq 0 ]; then
    echo -e "${GREEN}✓ 以 root 用户运行${NC}"
else
    echo -e "${YELLOW}⚠ 非 root 用户，可能需要 sudo${NC}"
    WARNINGS=$((WARNINGS + 1))
fi

# 检查端口占用
echo ""
echo -e "${YELLOW}[8/8] 检查端口占用...${NC}"
if command -v netstat &> /dev/null; then
    if netstat -tuln | grep -q ":8000 "; then
        echo -e "${YELLOW}⚠ 端口 8000 已被占用${NC}"
        WARNINGS=$((WARNINGS + 1))
    else
        echo -e "${GREEN}✓ 端口 8000 可用${NC}"
    fi
else
    echo -e "${YELLOW}⚠ netstat 未安装，无法检查端口${NC}"
fi

# 总结
echo ""
echo "========================================="
if [ $ERRORS -eq 0 ]; then
    echo -e "${GREEN}✓ 预检查通过！${NC}"
    echo ""
    echo "可以运行部署脚本："
    echo "  ./setup-server.sh"
else
    echo -e "${RED}✗ 发现 $ERRORS 个错误${NC}"
    echo "请先解决上述问题"
fi

if [ $WARNINGS -gt 0 ]; then
    echo -e "${YELLOW}⚠ $WARNINGS 个警告（可忽略）${NC}"
fi
echo "========================================="
