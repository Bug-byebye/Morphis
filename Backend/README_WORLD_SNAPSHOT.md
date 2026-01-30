# 世界快照存储服务 (World Snapshot Storage Service)

## 概述

基于 FastAPI + PostgreSQL 的世界数据存储服务，为 Unity 客户端提供世界快照的持久化存储功能。

## 技术栈

- **FastAPI**: 现代、快速的 Web 框架
- **SQLAlchemy 2.0**: ORM 框架
- **PostgreSQL**: 关系型数据库（使用 JSONB 存储快照数据）
- **Pydantic v2**: 数据验证和序列化

## 项目结构

```
Backend/
├── database.py              # 数据库连接配置
├── models/                  # SQLAlchemy 数据模型
│   ├── __init__.py
│   └── world_snapshot.py   # 世界快照模型
├── schemas/                 # Pydantic 模式定义
│   ├── __init__.py
│   └── world_snapshot.py   # 世界快照 Schema
├── crud/                    # 数据库操作（CRUD）
│   ├── __init__.py
│   └── world_snapshot.py   # 世界快照 CRUD
├── routers/                 # API 路由
│   ├── __init__.py
│   └── world.py            # 世界快照路由
├── server.py                # FastAPI 应用入口
└── requirements.txt         # Python 依赖
```

## 数据库模型

### WorldSnapshot 表

| 字段 | 类型 | 说明 |
|------|------|------|
| world_id | String (PK) | 世界ID（主键） |
| owner_id | String (nullable) | 所有者ID（预留字段） |
| version | Integer | 版本号 |
| snapshot | JSONB | 完整世界数据（JSON） |
| created_at | DateTime | 创建时间 |
| updated_at | DateTime | 更新时间 |

## API 端点

### POST /world/{world_id}

创建或更新世界快照。

**行为：**
- 如果 `world_id` 不存在 → 创建新记录，`version=1`
- 如果 `world_id` 已存在 → 更新 `snapshot`，`version` 自动 +1

**请求体：**
```json
{
  "world_id": "my_world",
  "version": 1,
  "objects": [
    {
      "object_id": "uuid",
      "prefab_id": "cube",
      "pos_x": 0.0,
      "pos_y": 0.0,
      "pos_z": 0.0,
      "rot_x": 0.0,
      "rot_y": 0.0,
      "rot_z": 0.0,
      "rot_w": 1.0,
      "scale_x": 1.0,
      "scale_y": 1.0,
      "scale_z": 1.0
    }
  ]
}
```

**响应：**
```json
{
  "world_id": "my_world",
  "version": 2
}
```

### GET /world/{world_id}

获取世界快照（最新版本）。

**响应：**
```json
{
  "world_id": "my_world",
  "version": 2,
  "snapshot": {
    "world_id": "my_world",
    "version": 2,
    "objects": [...]
  },
  "owner_id": null,
  "created_at": "2026-01-28T10:00:00Z",
  "updated_at": "2026-01-28T10:05:00Z"
}
```

**错误响应：**
- `404 Not Found`: 世界不存在

## 环境配置

### 1. 安装依赖

```bash
cd Backend
pip install -r requirements.txt
```

### 2. 配置数据库

创建 `.env` 文件（或修改现有文件）：

```env
DATABASE_URL=postgresql://postgres:postgres@localhost:5432/morphis
```

**数据库 URL 格式：**
```
postgresql://[用户名]:[密码]@[主机]:[端口]/[数据库名]
```

### 3. 创建 PostgreSQL 数据库

```sql
CREATE DATABASE morphis;
```

或者使用命令行：

```bash
createdb morphis
```

## 启动服务

### 方式 1: 直接运行

```bash
cd Backend
python server.py
```

### 方式 2: 使用 uvicorn

```bash
cd Backend
uvicorn server:app --host 0.0.0.0 --port 8000 --reload
```

服务启动后，访问：
- API 文档: http://localhost:8000/docs
- 健康检查: http://localhost:8000/health

## 自动创建表

服务启动时会自动调用 `init_db()` 创建所有数据表。如果数据库连接失败，会打印警告信息但不会阻止服务启动。

## 测试 API

### 使用 curl

**创建/更新世界快照：**
```bash
curl -X POST "http://localhost:8000/world/test_world" \
  -H "Content-Type: application/json" \
  -d '{
    "world_id": "test_world",
    "version": 1,
    "objects": [
      {
        "object_id": "obj-001",
        "prefab_id": "cube",
        "pos_x": 0.0,
        "pos_y": 0.0,
        "pos_z": 0.0,
        "rot_x": 0.0,
        "rot_y": 0.0,
        "rot_z": 0.0,
        "rot_w": 1.0,
        "scale_x": 1.0,
        "scale_y": 1.0,
        "scale_z": 1.0
      }
    ]
  }'
```

**获取世界快照：**
```bash
curl "http://localhost:8000/world/test_world"
```

### 使用 Python requests

```python
import requests

# 创建/更新
response = requests.post(
    "http://localhost:8000/world/test_world",
    json={
        "world_id": "test_world",
        "version": 1,
        "objects": [...]
    }
)
print(response.json())

# 获取
response = requests.get("http://localhost:8000/world/test_world")
print(response.json())
```

## 错误处理

- **404 Not Found**: 世界不存在（GET 请求）
- **500 Internal Server Error**: 服务器内部错误（数据库连接失败、SQL 错误等）

## 设计原则

1. **数据模型集中**: 所有数据库操作集中在 `crud/` 目录
2. **API 层不直接写 SQL**: API 路由只调用 CRUD 函数
3. **配置通过环境变量**: 数据库连接字符串通过 `DATABASE_URL` 环境变量配置
4. **快照数据不拆表**: 完整的世界数据存储在单个 JSONB 字段中
5. **版本自动管理**: 每次更新自动递增版本号

## 注意事项

1. **PostgreSQL 版本**: 建议使用 PostgreSQL 12+（支持 JSONB）
2. **连接池**: 默认连接池大小为 10，最大溢出 20
3. **时区**: 时间戳使用 UTC 时区
4. **JSONB 索引**: 如需对 `snapshot` 字段进行查询，可以添加 GIN 索引

## 扩展建议

1. **认证授权**: 可以基于 `owner_id` 实现权限控制
2. **历史版本**: 可以创建历史版本表，保存所有版本
3. **索引优化**: 根据查询需求添加索引
4. **缓存**: 可以添加 Redis 缓存热点数据
5. **备份**: 定期备份 PostgreSQL 数据库

## 故障排查

### 数据库连接失败

1. 检查 PostgreSQL 是否运行：`pg_isready`
2. 检查 `DATABASE_URL` 环境变量是否正确
3. 检查数据库用户权限

### 表创建失败

1. 检查数据库用户是否有 CREATE TABLE 权限
2. 查看服务启动日志中的错误信息

### API 返回 500 错误

1. 查看服务日志
2. 检查数据库连接状态
3. 验证请求体格式是否正确
