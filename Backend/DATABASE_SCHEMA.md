# 数据库表结构与命名规则

本文档说明当前 PostgreSQL 中各表的含义，以及**除 User 表外**各表字段的命名规则与含义。

---

## 一、表含义概览

| 表名 | 含义 |
|------|------|
| **users** | 用户表：登录身份，支持注册/登录与后续联机鉴权。 |
| **worlds** | 世界/空间表：一个“可被多人进入的空间”，对应 Unity 中的一个 workspace（如默认空间、新建空间），以 MainScene 为初始场景。 |
| **world_members** | 世界成员表：多对多关系，表示某空间共属于哪些用户（owner + 协作者）。 |
| **world_snapshots** | 世界快照表：每个空间当前的状态快照（场景内物体等），由 Unity 通过 GET/POST `/world/{world_id}` 读写。 |

---

## 二、除 User 外各表字段与命名规则

**统一命名规则**：所有字段使用 **snake_case**（小写 + 下划线）。外键字段以 `_id` 结尾；时间戳以 `_at` 结尾。

---

### 1. `worlds`（世界/空间表）

| 字段名 | 类型 | 含义 | 命名说明 |
|--------|------|------|----------|
| **id** | String(64), PK | 世界 ID，与 API 的 `world_id` 一致（如 `ws-{username}-001`、`ws-{uuid}`） | 主键，单词 id。 |
| **name** | String(256) | 世界名称（展示用） | 单词 name。 |
| **owner_user_id** | Integer, FK → users.id, nullable | 所有者用户 ID | 格式：`角色_user_id`，外键。 |
| **created_at** | DateTime(tz) | 创建时间 | 时间戳：`created_at`。 |
| **updated_at** | DateTime(tz) | 最后更新时间 | 时间戳：`updated_at`。 |

---

### 2. `world_members`（世界成员表）

| 字段名 | 类型 | 含义 | 命名说明 |
|--------|------|------|----------|
| **world_id** | String(64), PK, FK → worlds.id | 世界 ID | 外键：`关联表_id`。 |
| **user_id** | Integer, PK, FK → users.id | 用户 ID | 外键：`关联表_id`。 |
| **created_at** | DateTime(tz) | 加入时间 | 时间戳：`created_at`。 |

主键为复合主键 `(world_id, user_id)`。

---

### 3. `world_snapshots`（世界快照表）

| 字段名 | 类型 | 含义 | 命名说明 |
|--------|------|------|----------|
| **world_id** | String(64), PK, FK → worlds.id | 世界 ID，与 worlds.id 一一对应 | 外键：`关联表_id`。 |
| **owner_id** | String, nullable | 所有者 ID（预留，当前未用） | 预留字段，`_id` 结尾。 |
| **version** | Integer | 快照版本号，更新时递增 | 单词 version。 |
| **snapshot** | JSON/JSONB | 完整世界数据（如 world_id、version、objects 等） | 单词 snapshot，存整块 JSON。 |
| **created_at** | DateTime(tz) | 创建时间 | 时间戳：`created_at`。 |
| **updated_at** | DateTime(tz) | 最后更新时间 | 时间戳：`updated_at`。 |

---

## 三、命名规则小结（除 User 外）

- **表名**：复数、snake_case（如 `worlds`, `world_members`, `world_snapshots`）。
- **主键**：单表主键用 `id`；关联表用 `关联表_id`（如 `world_id`, `user_id`）。
- **外键**：`被引用表_id`（如 `owner_user_id` → users.id）。
- **时间戳**：`created_at`、`updated_at`（带时区）。
- **其他字段**：小写单词或 snake_case（如 `name`, `version`, `snapshot`）。
