# 开始部署

## 当前状态
✅ 代码已在服务器：`/root/Morphis`

## 立即开始

```bash
cd /root/Morphis/deploy
chmod +x setup-server.sh
./setup-server.sh
```

## 配置提示

脚本会询问：

1. **数据库密码**：直接回车使用默认 `morphis123`
2. **部署目录**：直接回车，脚本会自动检测 `/root/Morphis`

## 脚本会自动完成

- ✅ 安装系统依赖
- ✅ 配置 PostgreSQL 数据库
- ✅ 创建 Python 虚拟环境
- ✅ 安装 Python 依赖
- ✅ 初始化数据库表
- ✅ 配置环境变量
- ✅ 创建 systemd 服务
- ✅ 启动 Backend 服务

## 预计时间
约 5-10 分钟

## 完成后
查看 `QUICK_START.md` 进行测试
