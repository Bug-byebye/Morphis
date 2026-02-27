# 多 World 架构部署检查清单

## 部署前准备

### 服务器准备
- [ ] Ubuntu 20.04+ 服务器
- [ ] 4 核 8GB 内存（推荐）
- [ ] 50GB 磁盘空间
- [ ] 公网 IP 地址
- [ ] SSH 访问权限
- [ ] 记录服务器 IP: `___________________`
- [ ] 记录 SSH 用户名: `___________________`

### 本地准备
- [ ] Unity 2021.3+ 已安装
- [ ] Git 或 SCP 工具已安装
- [ ] SSH 客户端已安装
- [ ] 项目代码已准备

---

## 阶段 1: Backend 部署

### 1.1 上传项目代码
- [ ] 方式 A: Git 克隆到服务器
  ```bash
  ssh your-username@your-server-ip
  git clone <repo-url> /home/your-username/Morphis
  ```
- [ ] 方式 B: SCP 上传 Backend
  ```bash
  scp -r Backend your-username@your-server-ip:/home/your-username/Morphis/
  ```

### 1.2 运行部署脚本
- [ ] 上传部署脚本
  ```bash
  scp deploy/setup-server.sh your-username@your-server-ip:/tmp/
  ```
- [ ] SSH 连接到服务器
  ```bash
  ssh your-username@your-server-ip
  ```
- [ ] 运行部署脚本
  ```bash
  cd /tmp
  chmod +x setup-server.sh
  ./setup-server.sh
  ```

### 1.3 配置信息
- [ ] 数据库密码: `___________________` (记住它！)
- [ ] 部署目录: `/home/your-username/Morphis`
- [ ] Git 仓库: 已上传则选择 n

### 1.4 验证 Backend
- [ ] Backend 服务已启动
  ```bash
  sudo systemctl status morphis-backend
  ```
- [ ] API 健康检查通过
  ```bash
  curl http://localhost:8000/health
  ```
- [ ] 防火墙已配置
  ```bash
  sudo ufw status
  # 应显示: 22, 7777:7826, 8000 已开放
  ```
- [ ] World 日志目录已创建
  ```bash
  ls -la /var/log/morphis-worlds/
  ```

---

## 阶段 2: Unity Server 构建

### 2.1 配置 Unity Server
- [ ] 创建 `Assets/StreamingAssets/config.json`
  ```json
  {
    "GameServerAddress": "0.0.0.0",
    "GameServerPort": 7777,
    "ApiBaseUrl": "http://localhost:8000",
    "DefaultWorldId": "default-world"
  }
  ```
- [ ] 确认 `GameServerAddress` 是 `0.0.0.0`
- [ ] 确认 `ApiBaseUrl` 是 `http://localhost:8000`

### 2.2 切换平台
- [ ] 打开 Unity 项目
- [ ] `File > Build Settings`
- [ ] 选择 `Dedicated Server` 平台
- [ ] 选择 `Linux` 目标
- [ ] 点击 `Switch Platform`
- [ ] 等待切换完成

### 2.3 构建 Server
- [ ] 点击 `Build`
- [ ] 选择输出目录: `Builds/LinuxServer`
- [ ] 等待构建完成（10-20 分钟）
- [ ] 验证构建文件:
  - [ ] `Morphis.x86_64` 存在
  - [ ] `Morphis_Data/` 目录存在
  - [ ] `UnityPlayer.so` 存在

---

## 阶段 3: Unity Server 部署

### 3.1 上传构建文件
- [ ] 上传到服务器
  ```bash
  scp -r Builds/LinuxServer/* your-username@your-server-ip:/home/your-username/Morphis/MorphisServer/
  ```
- [ ] 验证上传成功
  ```bash
  ssh your-username@your-server-ip
  ls -la /home/your-username/Morphis/MorphisServer/
  ```

### 3.2 设置权限
- [ ] 设置可执行权限
  ```bash
  chmod +x /home/your-username/Morphis/MorphisServer/Morphis.x86_64
  ```
- [ ] 验证权限
  ```bash
  ls -la /home/your-username/Morphis/MorphisServer/Morphis.x86_64
  # 应显示: -rwxr-xr-x
  ```

### 3.3 测试 World 管理
- [ ] 运行测试脚本
  ```bash
  cd /home/your-username/Morphis/Backend
  source venv/bin/activate
  python test_world_manager.py
  ```
- [ ] 验证输出:
  - [ ] ✅ World 已启动，端口: 7777
  - [ ] ✅ 玩家数更新为 2
  - [ ] ✅ World 已停止

### 3.4 验证 World 列表
- [ ] 查看 World 列表
  ```bash
  curl http://localhost:8000/worlds/manage/list
  ```
- [ ] 应返回 JSON 格式的 World 列表

---

## 阶段 4: 客户端配置

### 4.1 切换回 Windows 平台
- [ ] `File > Build Settings`
- [ ] 选择 `Windows` 平台
- [ ] **取消勾选** `Dedicated Server`
- [ ] 点击 `Switch Platform`
- [ ] 等待切换完成

### 4.2 配置客户端
- [ ] 编辑项目根目录 `config.json`
  ```json
  {
    "GameServerAddress": "your-server-ip",
    "GameServerPort": 7777,
    "ApiBaseUrl": "http://your-server-ip:8000",
    "DefaultWorldId": "default-world"
  }
  ```
- [ ] 替换 `your-server-ip` 为实际 IP
- [ ] 确认 IP 地址正确: `___________________`

### 4.3 构建客户端
- [ ] 点击 `Build`
- [ ] 选择输出目录: `Builds/WindowsClient`
- [ ] 等待构建完成

---

## 阶段 5: 测试部署

### 5.1 测试客户端连接
- [ ] 运行 `Builds/WindowsClient/Morphis.exe`
- [ ] 登录（默认: `111111` / `111111`）
- [ ] 查看 World 列表
- [ ] 选择一个 World
- [ ] 观察日志输出:
  - [ ] `[BootFlow] Requesting world: ...`
  - [ ] `[BootFlow] World ready: IP:Port`
  - [ ] `[AppBootstrap] Client connecting to dynamic server: ...`
- [ ] 进入游戏
- [ ] 测试功能:
  - [ ] 可以移动角色
  - [ ] 可以放置物体
  - [ ] 可以看到其他玩家（如果有）

### 5.2 验证 World 自动启动
- [ ] 在服务器上查看 World 状态
  ```bash
  curl http://localhost:8000/worlds/manage/list
  ```
- [ ] 应显示 World 状态为 `running`
- [ ] 应显示正确的端口号
- [ ] 应显示玩家数量 > 0

### 5.3 验证玩家数上报
- [ ] 等待 30 秒（上报间隔）
- [ ] 查看 World 状态
  ```bash
  curl http://localhost:8000/worlds/manage/status/<world-id>
  ```
- [ ] `player_count` 应显示当前玩家数

### 5.4 验证自动清理
- [ ] 所有玩家退出 World
- [ ] 等待 5 分钟
- [ ] 查看 World 状态
  ```bash
  curl http://localhost:8000/worlds/manage/list
  ```
- [ ] World 状态应变为 `stopped`

---

## 阶段 6: 云服务商配置

### 6.1 安全组配置
- [ ] 登录云服务商控制台
- [ ] 找到安全组设置
- [ ] 确认开放端口:
  - [ ] TCP 22 (SSH)
  - [ ] TCP 7777-7826 (World 端口范围)
  - [ ] TCP 8000 (Backend API)
- [ ] 保存配置

### 6.2 防火墙验证
- [ ] 在本地测试端口连通性
  ```powershell
  Test-NetConnection -ComputerName your-server-ip -Port 7777
  Test-NetConnection -ComputerName your-server-ip -Port 8000
  ```
- [ ] 两个端口都应显示 `TcpTestSucceeded: True`

---

## 阶段 7: 监控和日志

### 7.1 配置日志查看
- [ ] Backend 日志
  ```bash
  sudo journalctl -u morphis-backend -f
  ```
- [ ] World 日志
  ```bash
  tail -f /var/log/morphis-worlds/<world-id>.log
  ```
- [ ] 查看所有 World 日志
  ```bash
  ls -la /var/log/morphis-worlds/
  ```

### 7.2 配置监控
- [ ] 记录监控命令:
  ```bash
  # 查看 World 列表
  curl http://localhost:8000/worlds/manage/list
  
  # 查看系统资源
  htop
  
  # 查看进程
  ps aux | grep Morphis
  ```

---

## 阶段 8: 备份和恢复

### 8.1 配置备份
- [ ] 测试备份脚本
  ```bash
  cd /home/your-username/Morphis/deploy
  chmod +x backup.sh
  ./backup.sh
  ```
- [ ] 验证备份文件已创建
- [ ] 配置定时备份（可选）
  ```bash
  crontab -e
  # 添加: 0 2 * * * /home/your-username/Morphis/deploy/backup.sh
  ```

### 8.2 测试恢复
- [ ] 记录备份文件位置
- [ ] 测试数据库恢复（可选）

---

## 最终验证

### 功能验证
- [ ] 用户可以注册
- [ ] 用户可以登录
- [ ] 用户可以看到 World 列表
- [ ] 用户可以创建新 World
- [ ] 用户可以加入 World
- [ ] World 自动启动
- [ ] 多个 World 可以同时运行
- [ ] 玩家数量正确显示
- [ ] 空闲 World 自动关闭
- [ ] World 状态正确保存

### 性能验证
- [ ] Backend CPU 使用率 < 50%
- [ ] Backend 内存使用 < 500MB
- [ ] World 进程正常运行
- [ ] 网络延迟可接受 (< 100ms)
- [ ] 无明显卡顿

### 安全验证
- [ ] 数据库密码已修改
- [ ] 防火墙已配置
- [ ] 只开放必要端口
- [ ] SSH 密钥认证（推荐）
- [ ] 定期备份已配置

---

## 故障排查

### 常见问题
- [ ] Backend 无法启动
  - 查看日志: `sudo journalctl -u morphis-backend -n 100`
  - 检查数据库: `psql -U morphis_user -d morphis_db -h localhost`
  
- [ ] World 无法启动
  - 查看日志: `tail -f /var/log/morphis-worlds/<world-id>.log`
  - 检查权限: `chmod +x /path/to/Morphis.x86_64`
  
- [ ] 客户端无法连接
  - 检查安全组端口
  - 检查防火墙: `sudo ufw status`
  - 测试端口: `telnet your-server-ip 7777`

---

## 部署完成

### 记录信息
- [ ] 服务器 IP: `___________________`
- [ ] 数据库密码: `___________________`
- [ ] Backend URL: `http://your-server-ip:8000`
- [ ] World 端口范围: `7777-7826`

### 下一步
- [ ] 配置域名和 SSL（可选）
- [ ] 配置监控告警
- [ ] 优化性能
- [ ] 定期维护

---

## 相关文档

- [快速开始](QUICK_START.md)
- [完整部署指南](DEPLOY_MULTI_WORLD.md)
- [架构说明](MULTI_WORLD_ARCHITECTURE.md)
- [故障排查](TROUBLESHOOTING.md)

---

**部署完成日期**: `___________________`  
**部署人员**: `___________________`  
**服务器 IP**: `___________________`

✅ 部署成功！
