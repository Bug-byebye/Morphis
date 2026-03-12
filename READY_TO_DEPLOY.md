# 准备就绪！

## ✅ 当前状态
- 代码位置：`/root/Morphis`
- 脚本已修复：自动检测项目目录
- PostgreSQL 权限问题已解决

## 🚀 立即开始部署

### 步骤 1：预检查（可选但推荐）

```bash
cd /root/Morphis/deploy
chmod +x pre-check.sh
./pre-check.sh
```

这会检查：
- 项目结构是否完整
- Python 是否已安装
- 网络连接是否正常
- 磁盘空间是否充足
- 端口是否可用

### 步骤 2：运行部署脚本

```bash
cd /root/Morphis/deploy
chmod +x setup-server.sh
./setup-server.sh
```

### 步骤 3：按提示输入配置

1. **数据库密码**：
   ```
   请输入数据库密码（默认: morphis123）: 
   ```
   直接回车使用默认密码

2. **部署目录**：
   ```
   请输入部署目录（默认: /root/Morphis）:
   ```
   直接回车，脚本会自动检测并使用 `/root/Morphis`

3. **确认**：
   ```
   确认继续？(y/n)
   ```
   输入 `y` 并回车

## ⏱️ 预计时间
- 预检查：30 秒
- 部署脚本：5-10 分钟

## 📋 脚本会自动完成

1. ✅ 更新系统包
2. ✅ 安装 Python 3.10、PostgreSQL 等依赖
3. ✅ 配置数据库（创建 morphis_db 和 morphis_user）
4. ✅ 创建 Python 虚拟环境
5. ✅ 安装 Python 依赖包
6. ✅ 初始化数据库表结构
7. ✅ 配置环境变量和服务器配置
8. ✅ 创建 systemd 服务
9. ✅ 启动 Backend 服务

## ✅ 部署完成后

### 验证服务状态

```bash
# 检查服务状态
sudo systemctl status morphis-backend

# 查看日志
sudo journalctl -u morphis-backend -f

# 测试 API
curl http://localhost:8000/health
```

### 预期输出

```json
{"status":"ok","message":"Morphis Backend is running"}
```

## 📚 下一步

部署完成后，参考以下文档：

1. `QUICK_START.md` - 快速测试指南
2. `ARCHITECTURE_VERIFICATION.md` - 架构验证报告
3. `DEPLOY_MULTI_WORLD.md` - 完整部署文档

## ❓ 遇到问题？

### 常见问题

1. **权限错误**：确保以 root 用户运行或使用 sudo
2. **网络超时**：检查服务器网络连接
3. **端口占用**：使用 `netstat -tuln | grep 8000` 检查

### 获取帮助

查看日志文件：
```bash
# Backend 日志
sudo journalctl -u morphis-backend -n 100

# 系统日志
tail -f /var/log/syslog
```

## 🎉 准备好了吗？

运行预检查，然后开始部署！

```bash
cd /root/Morphis/deploy
./pre-check.sh && ./setup-server.sh
```
