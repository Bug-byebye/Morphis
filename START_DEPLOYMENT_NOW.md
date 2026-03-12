# 🚀 开始部署到云服务器

## 📋 部署前检查清单

### 你需要准备的信息

- [ ] 服务器 IP 地址: `___________________`
- [ ] SSH 用户名: `___________________`
- [ ] SSH 密码或密钥
- [ ] 服务器系统: Ubuntu 20.04+ ✓

### 本地准备

- [x] Unity 项目编译无错误 ✓
- [ ] 已安装 Git Bash 或 PowerShell（用于 SCP）
- [ ] 可以 SSH 连接到服务器

---

## 🎯 部署流程（3 个阶段）

```
阶段 1: 部署 Python Backend (10 分钟)
   ↓
阶段 2: 构建并部署 Unity Server (15 分钟)
   ↓
阶段 3: 构建客户端并测试 (10 分钟)
```

---

## 阶段 1: 部署 Python Backend

### 步骤 1.1: 测试 SSH 连接

```bash
# 在本地 PowerShell 或 Git Bash 中执行
ssh your-username@your-server-ip

# 如果使用密钥
ssh -i path/to/your-key.pem your-username@your-server-ip
```

**预期结果**: 成功连接到服务器

**如果连接失败**:
- 检查 IP 地址是否正确
- 检查用户名是否正确
- 检查云服务商的安全组是否开放了 22 端口

---

### 步骤 1.2: 上传部署脚本

```bash
# 在本地项目根目录执行
scp deploy/setup-server.sh your-username@your-server-ip:/tmp/

# 如果使用密钥
scp -i path/to/your-key.pem deploy/setup-server.sh your-username@your-server-ip:/tmp/
```

**预期结果**: 文件上传成功

---

### 步骤 1.3: 运行部署脚本

```bash
# SSH 连接到服务器
ssh your-username@your-server-ip

# 运行部署脚本
cd /tmp
chmod +x setup-server.sh
./setup-server.sh
```

**脚本会询问**:
1. **数据库密码**: 输入一个强密码（记住它！）
2. **部署目录**: 直接回车使用默认 `/home/your-username/Morphis`
3. **World ID**: 直接回车使用默认 `prod-world-001`
4. **Git 仓库地址**: 如果没有 Git 仓库，输入 `skip` 跳过

**预期结果**: 
- 脚本执行完成
- 显示 "部署完成！"
- Backend 服务已启动

---

### 步骤 1.4: 验证 Backend

```bash
# 在服务器上执行
curl http://localhost:8000/health

# 应该返回
# {"status":"ok","services":["text2image","image2image","image23d","text23d"]}
```

**如果失败**:
```bash
# 查看服务状态
sudo systemctl status morphis-backend

# 查看日志
sudo journalctl -u morphis-backend -n 50
```

---

## 阶段 2: 构建并部署 Unity Server

### 步骤 2.1: 在 Unity 中切换平台

1. 打开 Unity 项目
2. `File > Build Settings`
3. 选择 `Dedicated Server` 平台
4. 选择 `Linux` 目标
5. 点击 `Switch Platform`
6. **等待切换完成**（可能需要几分钟）

---

### 步骤 2.2: 配置 Server 配置文件

```bash
# 在本地项目根目录执行（PowerShell）

# 1. 复制配置模板到 StreamingAssets
Copy-Item deploy\client-config-production.json Assets\StreamingAssets\config.json

# 2. 编辑配置文件
notepad Assets\StreamingAssets\config.json
```

**配置内容**:
```json
{
  "GameServerAddress": "0.0.0.0",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://localhost:8000",
  "DefaultWorldId": "prod-world-001"
}
```

**重要**:
- `GameServerAddress` 必须是 `0.0.0.0`（监听所有网卡）
- `ApiBaseUrl` 是 `http://localhost:8000`（Server 访问本地 Backend）

---

### 步骤 2.3: 构建 Unity Server

1. 在 `Build Settings` 中点击 `Build`
2. 选择输出目录: `Builds/LinuxServer`
3. 点击保存
4. **等待构建完成**（可能需要 10-20 分钟）

**预期结果**: 
- 构建成功
- `Builds/LinuxServer` 目录包含 `.x86_64` 文件和 `_Data` 文件夹

---

### 步骤 2.4: 上传 Unity Server 到服务器

```bash
# 在本地项目根目录执行（PowerShell 或 Git Bash）

# 1. 上传 Server 文件
scp -r Builds/LinuxServer/* your-username@your-server-ip:/home/your-username/MorphisServer/

# 2. 上传部署脚本
scp deploy/setup-unity-server.sh your-username@your-server-ip:/tmp/
```

**注意**: 上传可能需要几分钟，取决于网络速度。

---

### 步骤 2.5: 部署 Unity Server

```bash
# SSH 连接到服务器
ssh your-username@your-server-ip

# 运行部署脚本
cd /tmp
chmod +x setup-unity-server.sh
./setup-unity-server.sh /home/your-username/MorphisServer
```

**脚本会询问**:
1. **World ID**: 直接回车使用默认 `prod-world-001`
2. **Backend URL**: 直接回车使用默认 `http://localhost:8000`

**预期结果**:
- 脚本执行完成
- Unity Server 服务已启动
- 端口 7777 正在监听

---

### 步骤 2.6: 验证 Unity Server

```bash
# 在服务器上执行

# 1. 检查服务状态
sudo systemctl status morphis-server

# 2. 检查端口监听
sudo netstat -tulpn | grep 7777

# 3. 查看日志
tail -f /var/log/morphis-server.log
# 按 Ctrl+C 退出日志查看
```

**预期结果**:
- 服务状态为 `active (running)`
- 端口 7777 正在监听
- 日志显示 "Starting Mirror in SERVER mode"

---

## 阶段 3: 构建客户端并测试

### 步骤 3.1: 切换回 Windows 平台

1. 在 Unity 中 `File > Build Settings`
2. 选择 `Windows` 平台
3. **取消勾选** `Dedicated Server`
4. 点击 `Switch Platform`
5. 等待切换完成

---

### 步骤 3.2: 配置客户端配置文件

```bash
# 在本地项目根目录执行（PowerShell）

# 编辑配置文件
notepad config.json
```

**配置内容**（替换 `your-server-ip` 为实际 IP）:
```json
{
  "GameServerAddress": "your-server-ip",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://your-server-ip:8000",
  "DefaultWorldId": "prod-world-001"
}
```

**示例**:
```json
{
  "GameServerAddress": "123.45.67.89",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://123.45.67.89:8000",
  "DefaultWorldId": "prod-world-001"
}
```

---

### 步骤 3.3: 构建客户端

1. 在 `Build Settings` 中点击 `Build`
2. 选择输出目录: `Builds/WindowsClient`
3. 点击保存
4. 等待构建完成

---

### 步骤 3.4: 测试连接

1. 进入 `Builds/WindowsClient` 目录
2. 运行 `Morphis.exe`
3. 登录（默认账号: `111111` / `111111`）
4. 选择空间
5. 进入游戏

**预期结果**:
- ✅ 可以登录
- ✅ 可以看到空间列表
- ✅ 可以进入游戏
- ✅ 可以放置和移动物体

---

## 🎉 部署完成！

如果所有步骤都成功，恭喜你！服务器已经部署完成。

---

## 🔍 故障排查

### 问题 1: 客户端无法连接服务器

**症状**: 客户端一直显示 "Connecting..." 或立即断开

**解决方案**:

1. **检查云服务商安全组**
   - 登录云服务商控制台（阿里云/腾讯云/AWS 等）
   - 找到安全组设置
   - 确保开放了以下端口：
     - TCP 7777（Unity Server）
     - TCP 8000（Backend API）

2. **检查服务器防火墙**
   ```bash
   sudo ufw status
   # 应该显示 7777 和 8000 端口已开放
   ```

3. **测试端口连通性**
   ```bash
   # 在本地 Windows 上执行（PowerShell）
   Test-NetConnection -ComputerName your-server-ip -Port 7777
   Test-NetConnection -ComputerName your-server-ip -Port 8000
   ```

4. **查看服务器日志**
   ```bash
   sudo journalctl -u morphis-server -f
   ```

---

### 问题 2: 登录失败

**症状**: 无法登录或提示网络错误

**解决方案**:

1. **测试 Backend API**
   ```bash
   # 在本地执行
   curl http://your-server-ip:8000/health
   ```

2. **检查 Backend 服务**
   ```bash
   # 在服务器上执行
   sudo systemctl status morphis-backend
   sudo journalctl -u morphis-backend -n 50
   ```

3. **检查数据库**
   ```bash
   # 在服务器上执行
   psql -U morphis_user -d morphis_db -h localhost
   # 输入之前设置的数据库密码
   # 输入 \q 退出
   ```

---

### 问题 3: Unity Server 无法启动

**解决方案**:

1. **查看日志**
   ```bash
   tail -f /var/log/morphis-server.log
   ```

2. **手动启动测试**
   ```bash
   cd /home/your-username/MorphisServer
   ./Morphis.x86_64 --mode=server --worldId=test -batchmode -nographics
   ```

3. **检查配置文件**
   ```bash
   cat /home/your-username/MorphisServer/config.json
   ```

---

## 📚 下一步

部署成功后，建议：

1. **配置监控和备份**
   - 查看 `DEPLOYMENT_GUIDE.md` 的监控章节
   - 设置 `health-check.sh` 和 `backup.sh`

2. **配置域名和 SSL**（可选但推荐）
   - 购买域名
   - 配置 DNS 解析
   - 使用 Let's Encrypt 配置 SSL

3. **性能优化**
   - 监控服务器资源使用
   - 根据需要调整配置

---

## 🆘 需要帮助？

- 详细部署指南: `DEPLOYMENT_GUIDE.md`
- 故障排查: `TROUBLESHOOTING.md`
- 部署检查清单: `DEPLOYMENT_CHECKLIST.md`

---

## 📞 常用命令

```bash
# 查看服务状态
sudo systemctl status morphis-backend
sudo systemctl status morphis-server

# 重启服务
sudo systemctl restart morphis-backend
sudo systemctl restart morphis-server

# 查看日志
sudo journalctl -u morphis-backend -f
sudo journalctl -u morphis-server -f

# 查看端口
sudo netstat -tulpn | grep -E '7777|8000'
```

---

**准备好了吗？开始部署吧！** 🚀
