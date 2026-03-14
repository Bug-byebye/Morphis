# 部署文档总览

## 📚 文档结构

本项目提供了完整的云服务器部署文档和自动化脚本，帮助你快速将 Morphis 部署到生产环境。

---

## 🚀 快速开始

### 新手推荐路径

1. **阅读**: `QUICK_START_DEPLOYMENT.md` （5 分钟快速上手）
2. **执行**: 按照快速指南部署
3. **验证**: 使用检查清单确认部署成功

### 详细部署路径

1. **准备**: `DEPLOYMENT_CHECKLIST.md` （部署前检查）
2. **部署**: `DEPLOYMENT_GUIDE.md` （详细步骤）
3. **构建**: `CLIENT_BUILD_GUIDE.md` （客户端构建）
4. **配置**: `CONFIG_SETUP.md` （配置文件说明）

---

## 📖 文档说明

### 核心文档

| 文档 | 用途 | 适合人群 |
|------|------|----------|
| `QUICK_START_DEPLOYMENT.md` | 5 分钟快速部署 | 想快速上手的开发者 |
| `DEPLOYMENT_GUIDE.md` | 完整部署指南 | 需要详细了解的运维人员 |
| `DEPLOYMENT_CHECKLIST.md` | 部署检查清单 | 确保部署完整性 |
| `CLIENT_BUILD_GUIDE.md` | 客户端构建指南 | 需要打包客户端的开发者 |
| `CONFIG_SETUP.md` | 配置文件详解 | 需要理解配置系统的人员 |

### 脚本文档

| 文档 | 用途 |
|------|------|
| `deploy/README.md` | 部署脚本使用说明 |

---

## 🛠️ 自动化脚本

### 部署脚本

| 脚本 | 用途 | 使用场景 |
|------|------|----------|
| `deploy/setup-server.sh` | Python Backend 自动部署 | 首次部署 Backend |
| `deploy/setup-unity-server.sh` | Unity Server 自动部署 | 首次部署 Unity Server |

### 运维脚本

| 脚本 | 用途 | 使用场景 |
|------|------|----------|
| `deploy/health-check.sh` | 服务健康检查 | 定时检查服务状态 |
| `deploy/backup.sh` | 数据备份 | 定时备份数据库和配置 |

### 配置模板

| 文件 | 用途 |
|------|------|
| `config.json.example` | 配置文件模板 |
| `deploy/client-config-production.json` | 生产环境客户端配置模板 |

---

## 🎯 部署流程图

```
┌─────────────────────────────────────────────────────────┐
│                    部署准备                              │
│  - 购买云服务器（Ubuntu 20.04+）                        │
│  - 获取 SSH 访问权限                                     │
│  - 准备域名（可选）                                      │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│              第一步：部署 Python Backend                 │
│  1. 上传 setup-server.sh                                │
│  2. 运行脚本（自动安装依赖、配置数据库、创建服务）      │
│  3. 验证：curl http://localhost:8000/health             │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│            第二步：构建 Unity Server                     │
│  1. Unity: File > Build Settings                        │
│  2. 选择 Dedicated Server + Linux                       │
│  3. 配置 config.json                                    │
│  4. 构建到 Builds/LinuxServer                           │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│            第三步：部署 Unity Server                     │
│  1. 上传构建文件到服务器                                 │
│  2. 运行 setup-unity-server.sh                          │
│  3. 验证：sudo netstat -tulpn | grep 7777               │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│              第四步：配置客户端                          │
│  1. 创建 config.json（填入服务器地址）                  │
│  2. 构建 Windows 客户端                                  │
│  3. 测试连接                                             │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│              第五步：配置监控和备份                      │
│  1. 设置健康检查（health-check.sh）                     │
│  2. 设置自动备份（backup.sh）                           │
│  3. 配置 SSL（可选）                                     │
└─────────────────────────────────────────────────────────┘
```

---

## 🔧 技术栈

### 服务器端
- **操作系统**: Ubuntu 20.04 LTS+
- **Python Backend**: FastAPI + Uvicorn
- **数据库**: PostgreSQL
- **Unity Server**: Linux Dedicated Server Build
- **Web 服务器**: Nginx（可选，用于反向代理）

### 客户端
- **平台**: Windows 64-bit
- **引擎**: Unity 6000.0+
- **网络**: Mirror Networking

---

## 📋 部署检查清单

### 部署前
- [ ] 服务器已购买并可 SSH 访问
- [ ] Unity 项目可本地运行
- [ ] Python Backend 可本地运行
- [ ] 已阅读相关文档

### 部署中
- [ ] Backend 服务已启动
- [ ] Unity Server 服务已启动
- [ ] 防火墙已配置
- [ ] 端口可从外网访问

### 部署后
- [ ] 客户端可连接服务器
- [ ] 可以登录和创建空间
- [ ] 世界数据可保存和加载
- [ ] 多客户端可互相看到
- [ ] 监控和备份已配置

---

## 🚨 常见问题

### Q1: 部署需要多长时间？
A: 
- 快速部署：约 10-15 分钟
- 完整部署（含 SSL、监控）：约 30-60 分钟

### Q2: 服务器配置要求？
A: 
- 最低：2 核 4GB RAM，20GB 存储
- 推荐：4 核 8GB RAM，50GB 存储

### Q3: 是否需要域名？
A: 
- 不是必须的，可以直接使用 IP
- 但推荐使用域名，便于配置 SSL 和管理

### Q4: 如何更新服务器？
A: 
1. 构建新版本
2. 上传到服务器
3. 重启服务：`sudo systemctl restart morphis-server`

### Q5: 如何备份数据？
A: 
- 自动备份：使用 `deploy/backup.sh` 脚本
- 手动备份：`sudo -u postgres pg_dump morphis_db > backup.sql`

### Q6: 客户端无法连接怎么办？
A: 
1. 检查服务器防火墙：`sudo ufw status`
2. 检查云服务商安全组
3. 测试端口：`telnet server-ip 7777`
4. 查看服务器日志：`sudo journalctl -u morphis-server -f`

---

## 🔐 安全建议

### 必须做
1. ✅ 使用强密码（数据库、SSH）
2. ✅ 配置防火墙（只开放必要端口）
3. ✅ 定期备份数据
4. ✅ 定期更新系统

### 推荐做
1. 🔒 配置 SSL 证书（HTTPS）
2. 🔒 禁用 root SSH 登录
3. 🔒 使用 SSH 密钥认证
4. 🔒 安装 fail2ban 防暴力破解
5. 🔒 配置日志监控

---

## 📊 监控指标

### 服务健康
- Backend API 响应时间
- Unity Server 在线状态
- 数据库连接状态

### 资源使用
- CPU 使用率
- 内存使用率
- 磁盘空间
- 网络流量

### 业务指标
- 在线玩家数
- 世界数据保存频率
- API 调用次数

---

## 🆘 获取帮助

### 查看日志
```bash
# Backend 日志
sudo journalctl -u morphis-backend -f

# Unity Server 日志
sudo journalctl -u morphis-server -f
tail -f /var/log/morphis-server.log

# 系统日志
sudo journalctl -xe
```

### 服务管理
```bash
# 查看状态
sudo systemctl status morphis-backend
sudo systemctl status morphis-server

# 重启服务
sudo systemctl restart morphis-backend
sudo systemctl restart morphis-server

# 查看端口
sudo netstat -tulpn | grep -E '7777|8000'
```

### 资源监控
```bash
htop              # CPU/内存
df -h             # 磁盘
free -h           # 内存详情
sudo iotop        # 磁盘 I/O
sudo nethogs      # 网络流量
```

---

## 🎓 学习路径

### 初学者
1. 阅读 `QUICK_START_DEPLOYMENT.md`
2. 跟随步骤完成首次部署
3. 使用 `DEPLOYMENT_CHECKLIST.md` 验证

### 进阶用户
1. 阅读 `DEPLOYMENT_GUIDE.md` 了解细节
2. 学习 `CONFIG_SETUP.md` 理解配置系统
3. 自定义部署脚本

### 运维人员
1. 掌握所有文档内容
2. 配置监控和告警系统
3. 优化性能和安全性
4. 制定灾难恢复计划

---

## 📝 更新日志

### 2026-02-25
- ✅ 创建完整部署文档体系
- ✅ 提供自动化部署脚本
- ✅ 修复 AppSession 硬编码问题
- ✅ 创建配置文件系统
- ✅ 添加健康检查和备份脚本

---

## 🔗 相关链接

- Unity Mirror 文档: https://mirror-networking.com/
- FastAPI 文档: https://fastapi.tiangolo.com/
- PostgreSQL 文档: https://www.postgresql.org/docs/
- Ubuntu Server 指南: https://ubuntu.com/server/docs

---

## 📞 支持

如有问题，请：
1. 查看相关文档
2. 检查日志文件
3. 参考故障排查章节
4. 提交 Issue 到项目仓库

---

**祝部署顺利！🚀**
