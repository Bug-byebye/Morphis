# 服务器部署步骤

## 前提条件
- Ubuntu 20.04+ 服务器
- 服务器 IP: 121.43.141.248
- SSH 访问权限

## 步骤 1：上传项目到服务器

### 方案 A：使用 Git（推荐）

```bash
# 在服务器上
cd /root
git clone <你的仓库地址> Morphis
cd Morphis
```

### 方案 B：使用 SCP 上传

```bash
# 在本地 Windows 上（PowerShell）
# 压缩项目（排除不必要的文件）
tar -czf morphis.tar.gz --exclude=Library --exclude=Temp --exclude=Logs --exclude=obj --exclude=.git .

# 上传到服务器
scp morphis.tar.gz root@121.43.141.248:/root/

# 在服务器上解压
ssh root@121.43.141.248
cd /root
mkdir -p Morphis
tar -xzf morphis.tar.gz -C Morphis
cd Morphis
```

### 方案 C：使用 rsync（最快）

```bash
# 在本地 Windows 上（需要安装 rsync）
rsync -avz --exclude='Library' --exclude='Temp' --exclude='Logs' --exclude='obj' --exclude='.git' \
  ./ root@121.43.141.248:/root/Morphis/
```

## 步骤 2：上传 Unity Server 构建

```bash
# 在本地构建 Unity Server（Linux Dedicated Server）
# Build Settings -> Platform: Linux Dedicated Server
# Build Settings -> Target Platform: Linux
# Build

# 上传到服务器
scp -r build.app/MorphisServer root@121.43.141.248:/root/

# 在服务器上设置权限
ssh root@121.43.141.248
chmod +x /root/MorphisServer/Morphis.x86_64
```

## 步骤 3：运行部署脚本

```bash
# 在服务器上
cd /root/Morphis/deploy
chmod +x setup-server.sh
./setup-server.sh

# 按提示输入：
# - 数据库密码：morphis123（或自定义）
# - 部署目录：/root/Morphis（默认）
```

## 步骤 4：验证部署

```bash
# 检查 Backend 服务
curl http://localhost:8000/health

# 检查数据库连接
cd /root/Morphis/Backend
source venv/bin/activate
python -c "from database import engine; print('DB OK')"

# 检查 Unity Server 路径
ls -la /root/MorphisServer/Morphis.x86_64
```

## 步骤 5：启动服务

```bash
# 启动 Backend
sudo systemctl start morphis-backend
sudo systemctl status morphis-backend

# 查看日志
sudo journalctl -u morphis-backend -f
```

## 常见问题

### 问题 1：权限错误
```bash
# 确保脚本有执行权限
chmod +x deploy/setup-server.sh

# 确保 Unity Server 有执行权限
chmod +x /root/MorphisServer/Morphis.x86_64
```

### 问题 2：PostgreSQL 权限问题
```bash
# 如果遇到 "could not change directory" 错误
# 这是正常的警告，不影响功能
# 脚本已修复，会切换到 /tmp 目录执行 psql 命令
```

### 问题 3：项目目录不存在
```bash
# 确保项目已上传到正确位置
ls -la /root/Morphis

# 如果不存在，使用上面的方案 A、B 或 C 上传
```

### 问题 4：Python 版本问题
```bash
# 检查 Python 版本
python3 --version  # 应该是 3.10+

# 如果版本不对，安装 Python 3.10
sudo apt install -y python3.10 python3.10-venv
```

## 目录结构

部署完成后的目录结构：

```
/root/
├── Morphis/                    # 项目代码
│   ├── Backend/               # Python Backend
│   │   ├── venv/             # Python 虚拟环境
│   │   ├── main.py           # FastAPI 入口
│   │   └── ...
│   ├── deploy/               # 部署脚本
│   └── ...
└── MorphisServer/            # Unity Server 构建
    ├── Morphis.x86_64       # 可执行文件
    ├── Morphis_Data/        # 数据文件
    └── UnityPlayer.so       # Unity 运行时
```

## 下一步

部署完成后，参考 `QUICK_START.md` 进行测试和验证。
