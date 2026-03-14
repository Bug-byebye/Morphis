# Morphis - Unity 多人在线游戏系统

基于 Unity + Mirror 的多人在线游戏系统，支持多 World 动态调度架构。

![Unity](https://img.shields.io/badge/Unity-2021.3+-black?logo=unity)
![Python](https://img.shields.io/badge/Python-3.10+-blue?logo=python)
![Mirror](https://img.shields.io/badge/Mirror-Networking-green)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Database-blue)

## 🌟 核心特性

### 多 World 动态调度架构
- ✅ **独立进程**: 每个 World 运行在独立的 Unity Server 进程中
- ✅ **自动管理**: Backend 自动启动/停止 World 进程
- ✅ **动态端口**: 自动分配端口（7777-7826，最多 50 个 World）
- ✅ **智能清理**: 空闲 5 分钟自动关闭 World，节省资源
- ✅ **实时监控**: 玩家数量实时上报，状态可视化

### 游戏功能
- 🎮 **多人联机**: 基于 Mirror 的实时同步
- 🌍 **多 World 系统**: 用户可创建和管理多个独立空间
- 👥 **协作编辑**: 支持多用户共享 World
- 💾 **持久化**: World 状态自动保存到数据库
- 🔐 **用户系统**: 完整的登录/注册/权限管理

## 🏗️ 系统架构

```
┌─────────────────────────────────────────────────────┐
│  Backend (Python FastAPI)                           │
│  - World 元数据管理 (PostgreSQL)                     │
│  - World 进程调度器 (WorldProcessManager)            │
│  - 玩家路由 (world_id → server_ip:port)             │
│  - 自动清理空闲 World (5 分钟无人自动关闭)           │
└──────────────┬──────────────────────────────────────┘
               │
    ┌──────────┴──────────┬──────────┬──────────┐
    │                     │          │          │
┌───▼────────┐  ┌────▼────────┐  ┌──▼─────────┐  ┌──▼─────────┐
│ World A    │  │ World B     │  │ World C    │  │ World D    │
│ Process 1  │  │ Process 2   │  │ Process 3  │  │ (stopped)  │
│ Port: 7777 │  │ Port: 7778  │  │ Port: 7779 │  │            │
│ 2 players  │  │ 1 player    │  │ 0 players  │  │            │
└────────────┘  └─────────────┘  └────────────┘  └────────────┘
```

## 🚀 快速开始

### 环境要求

- Unity 2021.3+
- Python 3.10+
- PostgreSQL 12+
- Ubuntu 20.04+ (服务器)

### 本地开发

1. **克隆项目**
   ```bash
   git clone <your-repo-url>
   cd Morphis
   ```

2. **配置数据库**
   ```bash
   # 安装 PostgreSQL
   sudo apt install postgresql postgresql-contrib
   
   # 创建数据库
   sudo -u postgres psql
   CREATE DATABASE morphis_db;
   CREATE USER morphis_user WITH PASSWORD 'your-password';
   GRANT ALL PRIVILEGES ON DATABASE morphis_db TO morphis_user;
   ```

3. **启动 Backend**
   ```bash
   cd Backend
   python3 -m venv venv
   source venv/bin/activate
   pip install -r requirements.txt
   
   # 配置 .env
   cp .env.example .env
   nano .env  # 编辑数据库连接
   
   # 启动服务
   python server.py
   ```

4. **配置 Unity**
   
   编辑项目根目录 `config.json`:
   ```json
   {
     "ApiBaseUrl": "http://127.0.0.1:8000"
   }
   ```
   
   **注意**: 新架构中，客户端只需要配置 Backend API 地址。服务器地址和端口会动态获取。

5. **在 Unity 中打开**
   - 打开 Unity Hub
   - 添加项目文件夹
   - 打开 `Assets/Scenes/BootScene` 场景
   - 点击 Play

### 服务器部署

详细部署指南请查看：

- **快速开始**: [QUICK_START.md](QUICK_START.md) - 30 分钟完成部署
- **完整指南**: [DEPLOY_MULTI_WORLD.md](DEPLOY_MULTI_WORLD.md) - 详细步骤说明
- **架构文档**: [MULTI_WORLD_ARCHITECTURE.md](MULTI_WORLD_ARCHITECTURE.md) - 架构详解

**一键部署**:
```bash
# 上传部署脚本
scp deploy/setup-server.sh your-username@your-server-ip:/tmp/

# SSH 连接并运行
ssh your-username@your-server-ip
cd /tmp
chmod +x setup-server.sh
./setup-server.sh
```

## 📖 文档

### 部署相关
- [快速开始](QUICK_START.md) - 30 分钟快速部署
- [多 World 部署指南](DEPLOY_MULTI_WORLD.md) - 完整部署流程
- [部署脚本说明](deploy/README.md) - 脚本使用文档

### 架构相关
- [多 World 架构](MULTI_WORLD_ARCHITECTURE.md) - 架构设计详解
- [架构迁移总结](ARCHITECTURE_MIGRATION_SUMMARY.md) - 从单实例到多 World
- [配置说明](CONFIG_SETUP.md) - 配置文件详解

### 开发相关
- [故障排查](TROUBLESHOOTING.md) - 常见问题解决
- [API 文档](Backend/server.py) - Backend API 说明

## 🎮 使用流程

### 客户端流程

1. **启动游戏** → 自动进入登录界面
2. **登录/注册** → 输入用户名密码
3. **选择 World** → 从列表中选择要进入的 World
4. **自动连接** → Backend 自动启动 World 并返回连接信息
5. **进入游戏** → 客户端连接到指定的 World 服务器

### World 生命周期

```
用户请求加入 World
    ↓
Backend 检查 World 状态
    ↓
未运行？启动新进程（分配端口）
    ↓
返回连接信息给客户端
    ↓
客户端连接到 World
    ↓
Unity Server 定期上报玩家数
    ↓
无人 5 分钟后自动关闭
```

## 🛠️ 技术栈

### 前端（Unity）
- Unity 2021.3+
- Mirror Networking
- TextMeshPro
- Input System

### 后端（Python）
- FastAPI
- SQLAlchemy 2.0
- PostgreSQL
- psutil (进程管理)

### 部署
- Ubuntu 20.04+
- systemd
- UFW 防火墙

## 📊 性能指标

### 资源消耗
- Backend: ~50MB 内存
- 每个 World 进程: 500MB-2GB 内存
- CPU: 每个 World 10-30%

### 推荐配置
- 2 核 4GB: 最多 2-3 个 World
- 4 核 8GB: 最多 5-8 个 World
- 8 核 16GB: 最多 10-15 个 World

## 🔧 管理命令

### Backend 管理
```bash
# 查看状态
sudo systemctl status morphis-backend

# 重启服务
sudo systemctl restart morphis-backend

# 查看日志
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

## 🐛 故障排查

### Backend 无法启动
```bash
sudo journalctl -u morphis-backend -n 100
psql -U morphis_user -d morphis_db -h localhost
```

### World 无法启动
```bash
tail -f /var/log/morphis-worlds/<world-id>.log
chmod +x /path/to/Morphis.x86_64
```

### 客户端无法连接
1. 检查云服务商安全组（开放 7777-7826 端口）
2. 检查防火墙: `sudo ufw status`
3. 测试端口: `telnet your-server-ip 7777`

详细故障排查请查看 [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

## 📝 开发计划

### 短期（1-2 周）
- [ ] World 预热机制
- [ ] 进程池
- [ ] 监控面板

### 中期（1-2 月）
- [ ] 跨服务器调度
- [ ] 负载均衡
- [ ] 自动扩缩容

### 长期（3-6 月）
- [ ] 多区域部署
- [ ] 热迁移
- [ ] CDN 加速

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

MIT License

## 📞 联系方式

如有问题，请查看：
- 部署文档: `DEPLOY_MULTI_WORLD.md`
- 架构文档: `MULTI_WORLD_ARCHITECTURE.md`
- 测试脚本: `Backend/test_world_manager.py`

---

**Morphis - 让多人游戏开发更简单** 🎮

```bash
cd Backend
python -m venv venv
.\venv\Scripts\Activate.ps1
pip install -r requirements.txt
python server.py
```

服务器将在 `http://localhost:8000` 运行

### 4. 运行项目

1. 在 Unity 中进入 **Play Mode**
2. 按 **Tab** 打开节点编辑器
3. **右键点击** 添加节点
4. 点击 **输出端口**（粉色）→ **输入端口**（蓝色）连接节点
5. 在 Text Input 节点输入提示词
6. 点击 **Execute** 执行管线

## 操作说明

| 按键/操作 | 功能 |
|-----------|------|
| Tab | 开关节点编辑器 |
| 右键 | 添加节点菜单 |
| 左键拖拽 | 移动节点 |
| 输出 → 输入端口 | 创建连接 |
| Execute 按钮 | 执行管线 |
| Clear 按钮 | 清空画布 |

## 项目结构

```
Morphis/
├── Assets/
│   └── Scripts/
│       └── NodeEditor/
│           ├── SimpleNodeEditor.cs    # 主编辑器
│           ├── PipelineNode.cs        # 节点基类
│           ├── PipelineGraph.cs       # 图管理器
│           └── Nodes/                 # 节点实现
├── Backend/
│   ├── server.py                      # FastAPI 后端
│   ├── generate_test_cube.py          # 测试模型生成器
│   └── test_cube.glb                  # 测试 GLB 文件
└── Packages/
```

## 依赖项

### Unity 包
- glTFast (com.unity.cloud.gltfast)
- TextMeshPro
- Input System

### Python 包
- FastAPI
- Uvicorn
- Trimesh
- NumPy

## 云服务器部署

### 快速部署（5 分钟）

详见 `QUICK_START_DEPLOYMENT.md`

```bash
# 1. 上传部署脚本到服务器
scp deploy/setup-server.sh user@your-server:/tmp/

# 2. SSH 连接并运行
ssh user@your-server
cd /tmp && chmod +x setup-server.sh && ./setup-server.sh

# 3. 构建 Unity Server（Linux Dedicated Server）
# 4. 上传并部署
scp -r Builds/LinuxServer/* user@your-server:/home/user/MorphisServer/
scp deploy/setup-unity-server.sh user@your-server:/tmp/
ssh user@your-server
cd /tmp && chmod +x setup-unity-server.sh && ./setup-unity-server.sh /home/user/MorphisServer
```

### 部署文档

- 📖 `DEPLOYMENT_SUMMARY.md` - 部署文档总览
- 🚀 `QUICK_START_DEPLOYMENT.md` - 5 分钟快速部署
- 📚 `DEPLOYMENT_GUIDE.md` - 完整部署指南
- ✅ `DEPLOYMENT_CHECKLIST.md` - 部署检查清单
- 🎮 `CLIENT_BUILD_GUIDE.md` - 客户端构建指南
- ⚙️ `CONFIG_SETUP.md` - 配置文件详解

### 自动化脚本

- `deploy/setup-server.sh` - Python Backend 自动部署
- `deploy/setup-unity-server.sh` - Unity Server 自动部署
- `deploy/health-check.sh` - 服务健康检查
- `deploy/backup.sh` - 数据自动备份

## 开源协议

MIT License
