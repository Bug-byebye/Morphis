# 快速上传指南

## 当前问题
你在服务器上运行 `./setup-server.sh` 时遇到错误：
- 项目目录不存在: `/home/root/Morphis`（路径错误）
- PostgreSQL 权限警告（已修复）

## 解决方案

### 步骤 1：修复脚本（已完成）
脚本已更新，现在会：
- 正确处理 root 用户的路径（`/root/Morphis` 而不是 `/home/root/Morphis`）
- 在 `/tmp` 目录执行 PostgreSQL 命令，避免权限问题

### 步骤 2：上传项目到服务器

#### 方法 A：使用 PowerShell 脚本（推荐）

```powershell
# 在项目根目录运行
cd "D:\3D projects\Morphis"
.\deploy\upload-to-server.ps1
```

这会自动：
1. 打包 Backend 和部署脚本
2. 上传到服务器
3. 在服务器上解压

#### 方法 B：手动使用 SCP

```powershell
# 1. 打包 Backend 目录
Compress-Archive -Path Backend,deploy,config.json.example,README.md -DestinationPath morphis.zip

# 2. 上传到服务器
scp morphis.zip root@121.43.141.248:/tmp/

# 3. SSH 到服务器
ssh root@121.43.141.248

# 4. 在服务器上解压
mkdir -p /root/Morphis
cd /root/Morphis
unzip /tmp/morphis.zip
chmod +x deploy/*.sh
```

#### 方法 C：使用 Git（如果有仓库）

```bash
# 在服务器上
cd /root
git clone <你的仓库地址> Morphis
cd Morphis
```

### 步骤 3：重新运行部署脚本

```bash
# 在服务器上
cd /root/Morphis/deploy
./setup-server.sh

# 输入配置：
# - 数据库密码：morphis123
# - 部署目录：/root/Morphis（直接回车使用默认值）
```

### 步骤 4：上传 Unity Server 构建

```powershell
# 在本地 Windows 上
# 假设你的 Unity Server 构建在 build.app/MorphisServer

scp -r "D:\3D projects\Morphis\build.app\MorphisServer" root@121.43.141.248:/root/

# 在服务器上设置权限
ssh root@121.43.141.248
chmod +x /root/MorphisServer/Morphis.x86_64
```

## 验证

```bash
# 在服务器上检查目录结构
ls -la /root/Morphis
ls -la /root/MorphisServer

# 应该看到：
# /root/Morphis/Backend/
# /root/Morphis/deploy/
# /root/MorphisServer/Morphis.x86_64
```

## 常见问题

### Q: scp 命令不存在
A: 在 Windows 上安装 OpenSSH Client：
   Settings -> Apps -> Optional Features -> Add OpenSSH Client

### Q: 权限被拒绝
A: 确保使用 root 用户或有 sudo 权限的用户

### Q: 连接超时
A: 检查服务器 IP 和防火墙设置

## 下一步

上传完成后，继续运行 `./setup-server.sh` 完成部署。
