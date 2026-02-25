# 故障排查指南

## Unity 编译错误

### 错误 1: TelepathyTransport 类型找不到

**错误信息**:
```
Assets\Scripts\Bootstrap\AppBootstrap.cs(105,50): error CS0246: 
The type or namespace name 'TelepathyTransport' could not be found 
(are you missing a using directive or an assembly reference?)
```

**原因**: 
`Bootstrap.asmdef` 程序集定义文件缺少对 `Telepathy` 程序集的引用。

**解决方案**:

#### 方法 1: 使用自动修复脚本（推荐）

```powershell
# 在项目根目录执行
.\fix-unity-compilation.ps1
```

脚本会自动：
1. 删除 Library、Temp、obj 文件夹
2. 验证关键文件存在
3. 提示下一步操作

然后：
1. 打开 Unity Hub
2. 打开项目
3. 等待重新导入（5-10 分钟）

#### 方法 2: 手动清理（如果脚本失败）

1. **关闭 Unity Editor**

2. **删除缓存文件夹**
   ```powershell
   Remove-Item -Recurse -Force Library
   Remove-Item -Recurse -Force Temp
   Remove-Item -Recurse -Force obj
   ```

3. **重新打开 Unity**
   - 打开 Unity Hub
   - 打开项目
   - 等待重新导入

#### 方法 3: 在 Unity 中重新导入

如果不想关闭 Unity：
1. `Assets > Reimport All`
2. 等待完成
3. 如果还有错误，右键点击 `Bootstrap.asmdef` > `Reimport`

**详细说明**: 查看 `FIX_COMPILATION_ERROR.md`

**验证**:
1. 关闭 Unity
2. 运行修复脚本或手动删除文件夹
3. 重新打开 Unity
4. 等待编译完成
5. 检查 Console 无错误

---

### 错误 2: BindingFlags 找不到

**错误信息**:
```
Assets\Scripts\WorldSnapshot\HttpWorldService.cs(34,73): error CS0103: 
The name 'BindingFlags' does not exist in the current context
```

**原因**: 
缺少 `using System.Reflection;` 语句。

**解决方案**:
已修复。`HttpWorldService.cs` 现在包含了必要的 using 语句：

```csharp
using System;
using System.Collections;
using System.Reflection;  // ← 已添加
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
```

**验证**:
Unity 会自动重新编译，错误应该消失。

---

### 错误 2: UnityConnectWebRequestException

**错误信息**:
```
UnityConnectWebRequestException: Token Exchange failed due a failure with the web request.
```

**原因**: 
Unity 服务连接失败（网络问题或 Unity 账号未登录）。

**影响**: 
不影响项目运行，只是 Unity 云服务功能不可用。

**解决方案**:
- 忽略此错误（不影响开发和部署）
- 或登录 Unity 账号：`Edit > Preferences > Account`
- 或检查网络连接

---

## 配置文件问题

### 问题 1: 配置文件找不到

**错误信息**:
```
[ConfigLoader] Config file not found. Expected at: ...
```

**原因**: 
`config.json` 文件不存在。

**解决方案**:
```bash
# 复制示例配置
cp config.json.example config.json

# 编辑配置文件
# 填入正确的服务器地址和端口
```

**验证**:
```bash
# 检查文件存在
ls config.json

# 查看内容
cat config.json
```

---

### 问题 2: 配置文件格式错误

**错误信息**:
```
[ConfigLoader] Failed to parse config.json
```

**原因**: 
JSON 格式错误（缺少逗号、引号等）。

**解决方案**:
1. 使用 JSON 验证工具检查格式
2. 确保所有字段都有值
3. 确保字符串用双引号包裹
4. 确保数字不用引号

**正确格式**:
```json
{
  "GameServerAddress": "127.0.0.1",
  "GameServerPort": 7777,
  "ApiBaseUrl": "http://127.0.0.1:8000",
  "DefaultWorldId": "dev-world"
}
```

---

### 问题 3: AppSession.BaseUrl 为空

**错误信息**:
```
[AppSession] BaseUrl not initialized. AppConfig.Instance.ApiBaseUrl is null or empty.
```

**原因**: 
配置文件中 `ApiBaseUrl` 字段为空或配置文件加载失败。

**解决方案**:
1. 检查 `config.json` 中 `ApiBaseUrl` 字段
2. 确保配置文件在正确位置
3. 检查 ConfigLoader 日志

---

## 网络连接问题

### 问题 1: 客户端无法连接服务器

**症状**: 
客户端启动后一直显示 "Connecting..." 或立即断开。

**排查步骤**:

1. **检查客户端配置**
   ```json
   // config.json
   {
     "GameServerAddress": "your-server-ip",  // ← 检查这里
     "GameServerPort": 7777,
     ...
   }
   ```

2. **检查服务器状态**
   ```bash
   # 在服务器上执行
   sudo systemctl status morphis-server
   sudo netstat -tulpn | grep 7777
   ```

3. **测试网络连通性**
   ```bash
   # 在客户端执行
   ping your-server-ip
   telnet your-server-ip 7777
   ```

4. **检查防火墙**
   ```bash
   # 服务器防火墙
   sudo ufw status
   
   # 云服务商安全组
   # 登录控制台检查
   ```

5. **查看服务器日志**
   ```bash
   sudo journalctl -u morphis-server -f
   tail -f /var/log/morphis-server.log
   ```

---

### 问题 2: Backend API 无法访问

**症状**: 
登录失败或无法加载空间列表。

**排查步骤**:

1. **测试 API**
   ```bash
   curl http://your-server-ip:8000/health
   ```

2. **检查 Backend 服务**
   ```bash
   sudo systemctl status morphis-backend
   sudo journalctl -u morphis-backend -n 50
   ```

3. **检查端口**
   ```bash
   sudo netstat -tulpn | grep 8000
   ```

4. **检查数据库**
   ```bash
   psql -U morphis_user -d morphis_db -h localhost
   ```

---

## 部署问题

### 问题 1: Unity Server 无法启动

**症状**: 
服务启动后立即退出。

**排查步骤**:

1. **查看日志**
   ```bash
   tail -f /var/log/morphis-server.log
   sudo journalctl -u morphis-server -n 100
   ```

2. **检查配置文件**
   ```bash
   cat /home/user/MorphisServer/config.json
   ```

3. **检查文件权限**
   ```bash
   ls -la /home/user/MorphisServer/*.x86_64
   chmod +x /home/user/MorphisServer/*.x86_64
   ```

4. **手动启动测试**
   ```bash
   cd /home/user/MorphisServer
   ./Morphis.x86_64 --mode=server --worldId=test -batchmode -nographics
   ```

5. **检查依赖**
   ```bash
   ldd /home/user/MorphisServer/*.x86_64
   ```

---

### 问题 2: Backend 数据库连接失败

**错误信息**:
```
sqlalchemy.exc.OperationalError: could not connect to server
```

**排查步骤**:

1. **检查 PostgreSQL 状态**
   ```bash
   sudo systemctl status postgresql
   ```

2. **测试数据库连接**
   ```bash
   psql -U morphis_user -d morphis_db -h localhost
   ```

3. **检查 .env 文件**
   ```bash
   cat /home/user/Morphis/Backend/.env
   ```

4. **检查数据库密码**
   ```bash
   sudo -u postgres psql
   \du  # 查看用户
   \l   # 查看数据库
   ```

5. **重置数据库密码**
   ```bash
   sudo -u postgres psql
   ALTER USER morphis_user WITH PASSWORD 'new_password';
   \q
   
   # 更新 .env 文件
   nano /home/user/Morphis/Backend/.env
   ```

---

## 性能问题

### 问题 1: 服务器 CPU 使用率过高

**排查步骤**:

1. **查看进程**
   ```bash
   htop
   ps aux --sort=-%cpu | head -10
   ```

2. **检查日志大小**
   ```bash
   du -sh /var/log/morphis-*.log
   ```

3. **优化日志**
   ```bash
   # 配置日志轮转
   sudo nano /etc/logrotate.d/morphis
   ```

---

### 问题 2: 内存不足

**症状**: 
服务频繁重启或 OOM (Out of Memory)。

**解决方案**:

1. **查看内存使用**
   ```bash
   free -h
   ps aux --sort=-%mem | head -10
   ```

2. **创建 Swap**
   ```bash
   sudo fallocate -l 4G /swapfile
   sudo chmod 600 /swapfile
   sudo mkswap /swapfile
   sudo swapon /swapfile
   echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
   ```

3. **优化 Unity Server**
   - 减少同时在线玩家数
   - 优化世界对象数量
   - 增加服务器内存

---

## 常用诊断命令

### 服务状态
```bash
# 查看所有服务
sudo systemctl status morphis-*

# 查看特定服务
sudo systemctl status morphis-backend
sudo systemctl status morphis-server
```

### 日志查看
```bash
# 实时日志
sudo journalctl -u morphis-backend -f
sudo journalctl -u morphis-server -f

# 最近 N 行
sudo journalctl -u morphis-backend -n 100

# 错误日志
sudo journalctl -u morphis-backend -p err

# 时间范围
sudo journalctl -u morphis-backend --since "1 hour ago"
```

### 网络诊断
```bash
# 端口监听
sudo netstat -tulpn | grep -E '7777|8000'

# 连接状态
sudo netstat -an | grep ESTABLISHED

# 防火墙状态
sudo ufw status verbose

# 测试连接
telnet localhost 7777
curl http://localhost:8000/health
```

### 资源监控
```bash
# CPU/内存
htop
top

# 磁盘
df -h
du -sh /var/log/*

# 网络流量
sudo nethogs

# 磁盘 I/O
sudo iotop
```

---

## 获取帮助

如果以上方法都无法解决问题：

1. **收集信息**
   ```bash
   # 服务状态
   sudo systemctl status morphis-backend > status.txt
   sudo systemctl status morphis-server >> status.txt
   
   # 日志
   sudo journalctl -u morphis-backend -n 200 > backend.log
   sudo journalctl -u morphis-server -n 200 > server.log
   
   # 配置
   cat config.json > config.txt
   ```

2. **检查文档**
   - `DEPLOYMENT_GUIDE.md`
   - `CONFIG_SETUP.md`
   - `DEPLOYMENT_CHECKLIST.md`

3. **提交 Issue**
   - 包含错误信息
   - 包含日志文件
   - 说明复现步骤
   - 说明环境信息（OS、Unity 版本等）

---

## 预防措施

### 定期维护
```bash
# 每周执行
sudo apt update && sudo apt upgrade -y
sudo systemctl restart morphis-backend
sudo systemctl restart morphis-server

# 每月执行
# 清理日志
sudo journalctl --vacuum-time=30d

# 检查磁盘空间
df -h

# 备份数据
/home/user/backup.sh
```

### 监控告警
- 配置 `health-check.sh` 定时检查
- 配置磁盘空间告警
- 配置内存使用告警
- 配置服务状态告警

---

## 更新日志

### 2026-02-25
- ✅ 修复 TelepathyTransport 编译错误
- ✅ 添加程序集引用故障排查
- ✅ 添加网络连接诊断步骤
- ✅ 添加性能问题排查方法
