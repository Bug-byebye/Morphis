# ✅ 部署就绪 - 多 World 架构

## 🎉 项目已完全准备好部署！

所有代码、脚本和文档都已更新为多 World 动态调度架构。

---

## 📦 已完成的工作

### 1. 核心架构实现
- ✅ World 进程管理器 (`Backend/services/world_manager.py`)
- ✅ World 管理 API (`Backend/routers/world_manager.py`)
- ✅ 数据库模型扩展 (`Backend/models/world.py`)
- ✅ Unity Server 玩家数上报 (`Assets/Scripts/WorldSnapshot/WorldServerReporter.cs`)
- ✅ 客户端动态连接 (`Assets/Scripts/AppFlow/BootFlowManager.cs`)

### 2. 部署脚本
- ✅ `deploy/setup-server.sh` - 一键部署 Backend
- ✅ `deploy/health-check.sh` - 健康检查
- ✅ `deploy/backup.sh` - 数据备份
- ✅ `deploy/README.md` - 脚本使用说明

### 3. 文档
- ✅ `README.md` - 项目主文档（已更新）
- ✅ `QUICK_START.md` - 30 分钟快速部署
- ✅ `DEPLOY_MULTI_WORLD.md` - 完整部署指南
- ✅ `MULTI_WORLD_ARCHITECTURE.md` - 架构详解
- ✅ `ARCHITECTURE_MIGRATION_SUMMARY.md` - 迁移总结
- ✅ `DEPLOYMENT_CHECKLIST_MULTI_WORLD.md` - 部署检查清单

### 4. 测试工具
- ✅ `Backend/test_world_manager.py` - World 管理测试脚本

---

## 🚀 开始部署

### 方式 A: 快速部署（推荐新手）

按照 [QUICK_START.md](QUICK_START.md) 操作，30 分钟完成。

```bash
# 1. 上传部署脚本
scp deploy/setup-server.sh your-username@your-server-ip:/tmp/

# 2. 运行部署
ssh your-username@your-server-ip
cd /tmp
chmod +x setup-server.sh
./setup-server.sh
```

### 方式 B: 详细部署（推荐运维）

按照 [DEPLOY_MULTI_WORLD.md](DEPLOY_MULTI_WORLD.md) 操作，了解每个步骤。

### 方式 C: 检查清单（推荐团队）

使用 [DEPLOYMENT_CHECKLIST_MULTI_WORLD.md](DEPLOYMENT_CHECKLIST_MULTI_WORLD.md)，逐项检查。

---

## 📋 部署流程概览

```
1. 上传项目代码到服务器
   ↓
2. 运行 deploy/setup-server.sh
   ├─ 安装 PostgreSQL
   ├─ 配置 Python Backend
   ├─ 创建 World 进程管理器
   └─ 配置防火墙
   ↓
3. 在 Unity 中构建 Linux Server
   ├─ 切换到 Dedicated Server 平台
   ├─ 配置 config.json
   └─ 构建到 Builds/LinuxServer
   ↓
4. 上传 Unity Server 到服务器
   ├─ scp 上传构建文件
   └─ chmod +x 设置权限
   ↓
5. 测试部署
   ├─ python test_world_manager.py
   └─ 验证 World 启动/停止
   ↓
6. 构建客户端并测试
   ├─ 切换回 Windows 平台
   ├─ 配置 config.json
   ├─ 构建客户端
   └─ 测试连接
   ↓
✅ 部署完成！
```

---

## 🔑 关键配置

### Backend 环境变量 (.env)

自动生成，位于 `Backend/.env`:

```bash
DATABASE_URL=postgresql://morphis_user:password@localhost:5432/morphis_db
HOST=0.0.0.0
PORT=8000
UNITY_SERVER_PATH=/home/your-username/Morphis/MorphisServer/Morphis.x86_64
SERVER_PUBLIC_IP=<自动检测>
API_BASE_URL=http://localhost:8000
```

### Unity Server 配置 (config.json)

需要手动创建在 `Assets/StreamingAssets/config.json`:

```json
{
  "ApiBaseUrl": "http://localhost:8000",
  "ServerListenAddress": "0.0.0.0",
  "ServerPort": 7777
}
```

**重要**: 
- `ServerListenAddress` 必须是 `0.0.0.0`（监听所有网卡）
- `ServerPort` 会被环境变量 `WORLD_PORT` 动态覆盖

### 客户端配置 (config.json)

项目根目录 `config.json`:

```json
{
  "ApiBaseUrl": "http://your-server-ip:8000"
}
```

**注意**: 客户端不再需要配置服务器地址和端口，这些信息会从 `/workspaces/join` API 动态获取。

---

## 🎯 核心特性

### 多 World 动态调度
- 每个 World 独立进程
- 自动启动/停止
- 动态端口分配（7777-7826）
- 空闲 5 分钟自动关闭
- 玩家数量实时监控

### 工作流程
```
用户登录 → 选择 World → Backend 检查状态
                              ↓
                         未运行？启动新进程
                              ↓
                    返回 IP:Port 给客户端
                              ↓
                    客户端连接到指定服务器
                              ↓
                    Unity Server 定期上报玩家数
                              ↓
                    无人 5 分钟后自动关闭
```

---

## 📊 系统要求

### 服务器
- Ubuntu 20.04+
- 4 核 8GB 内存（推荐）
- 50GB 磁盘空间
- 公网 IP

### 本地开发
- Unity 2021.3+
- Python 3.10+
- PostgreSQL 12+

---

## 🛠️ 管理命令

### Backend 管理
```bash
sudo systemctl status morphis-backend
sudo systemctl restart morphis-backend
sudo journalctl -u morphis-backend -f
```

### World 管理
```bash
# 列出所有 World
curl http://localhost:8000/worlds/manage/list

# 查看 World 状态
curl http://localhost:8000/worlds/manage/status/<world-id>

# 启动 World
curl -X POST http://localhost:8000/worlds/manage/start \
  -H "Content-Type: application/json" \
  -d '{"world_id":"<world-id>"}'

# 停止 World
curl -X POST http://localhost:8000/worlds/manage/stop \
  -H "Content-Type: application/json" \
  -d '{"world_id":"<world-id>"}'
```

### 日志查看
```bash
# Backend 日志
sudo journalctl -u morphis-backend -f

# World 日志
tail -f /var/log/morphis-worlds/<world-id>.log

# 查看所有 World 日志
ls -la /var/log/morphis-worlds/
```

---

## 🐛 故障排查

### Backend 无法启动
```bash
sudo journalctl -u morphis-backend -n 100
psql -U morphis_user -d morphis_db -h localhost
cd Backend && source venv/bin/activate && python server.py
```

### World 无法启动
```bash
tail -f /var/log/morphis-worlds/<world-id>.log
chmod +x /path/to/Morphis.x86_64
cd /path/to/MorphisServer && ./Morphis.x86_64 --mode=server --worldId=test -batchmode -nographics
```

### 客户端无法连接
1. 检查云服务商安全组（开放 7777-7826 端口）
2. 检查防火墙: `sudo ufw status`
3. 测试端口: `telnet your-server-ip 7777`

详细故障排查请查看 [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

---

## 📚 文档导航

### 快速开始
- 🚀 [QUICK_START.md](QUICK_START.md) - 30 分钟快速部署

### 详细指南
- 📖 [DEPLOY_MULTI_WORLD.md](DEPLOY_MULTI_WORLD.md) - 完整部署流程
- ✅ [DEPLOYMENT_CHECKLIST_MULTI_WORLD.md](DEPLOYMENT_CHECKLIST_MULTI_WORLD.md) - 部署检查清单

### 架构文档
- 🏗️ [MULTI_WORLD_ARCHITECTURE.md](MULTI_WORLD_ARCHITECTURE.md) - 架构详解
- 📝 [ARCHITECTURE_MIGRATION_SUMMARY.md](ARCHITECTURE_MIGRATION_SUMMARY.md) - 迁移总结

### 运维文档
- 🛠️ [deploy/README.md](deploy/README.md) - 脚本使用说明
- 🔧 [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - 故障排查
- ⚙️ [CONFIG_SETUP.md](CONFIG_SETUP.md) - 配置说明

---

## ✨ 新架构优势

### vs 旧架构（单实例）

| 特性 | 旧架构 | 新架构 |
|------|--------|--------|
| World 隔离 | ❌ 所有玩家共享 | ✅ 每个 World 独立进程 |
| 资源管理 | ❌ 手动启动/停止 | ✅ 自动启动/停止 |
| 端口分配 | ❌ 固定端口 | ✅ 动态分配 |
| 空闲清理 | ❌ 需要手动管理 | ✅ 自动清理 |
| 扩展性 | ❌ 单实例限制 | ✅ 最多 50 个 World |
| 监控 | ❌ 无状态监控 | ✅ 实时状态监控 |

---

## 🎓 学习路径

### 第一天：快速部署
1. 阅读 `QUICK_START.md`
2. 完成首次部署
3. 测试基本功能

### 第二天：深入理解
1. 阅读 `DEPLOY_MULTI_WORLD.md`
2. 理解 `MULTI_WORLD_ARCHITECTURE.md`
3. 学习管理命令

### 第三天：运维优化
1. 配置监控和备份
2. 优化性能
3. 学习故障排查

---

## 📞 获取帮助

如有问题，请查看：
1. 快速开始: `QUICK_START.md`
2. 完整指南: `DEPLOY_MULTI_WORLD.md`
3. 架构文档: `MULTI_WORLD_ARCHITECTURE.md`
4. 故障排查: `TROUBLESHOOTING.md`
5. 测试脚本: `Backend/test_world_manager.py`

---

## 🎊 准备好了吗？

所有准备工作已完成，现在可以开始部署了！

**推荐流程**:
1. 先阅读 `QUICK_START.md` 了解整体流程
2. 准备好服务器和配置信息
3. 按照步骤执行部署
4. 遇到问题查看 `TROUBLESHOOTING.md`

---

**祝部署顺利！** 🚀

如果部署成功，别忘了：
- ⭐ Star 项目仓库
- 📝 分享你的部署经验
- 🐛 报告遇到的问题
- 💡 提出改进建议
