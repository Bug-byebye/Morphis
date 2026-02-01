# Backend 数据库现状审计报告

## 【一】现状审计结论

### 1. 数据库连接

| 项目 | 现状 | 结论 |
|------|------|------|
| 是否使用 PostgreSQL | ✅ 是。`database.py` 使用 SQLAlchemy + `psycopg2-binary`（见 requirements.txt） | 已满足 |
| DATABASE_URL 来源 | ✅ 来自 `os.getenv("DATABASE_URL", "postgresql://postgres:postgres@localhost:5432/morphis")` | 已从环境变量读取，但**有默认 localhost**，生产需显式配置 |
| 是否支持远程云数据库 | ✅ 支持。只要在环境变量中设置远程 `DATABASE_URL` 即可 | 已满足 |
| 连接失败时是否静默启动 | ⚠️ **存在风险**。`server.py` 中 `init_db()` 被**整体注释**，且原先被包在 `try/except` 中仅打印 Warning 后继续启动 | **需修复**：连接失败应抛错并阻止服务启动 |

### 2. ORM / 表定义

| 表/模块 | 是否存在 | 主键 | 外键 | 时间戳 | 版本字段 | 备注 |
|--------|----------|------|------|--------|----------|------|
| **User（用户）** | ❌ 不存在 | - | - | - | - | 当前认证为内存字典 `_users` / `_tokens` / `_workspaces_by_user` |
| **World / Scene（世界/场景）** | ❌ 不存在 | - | - | - | - | Workspace 仅为内存中的 `WorkspaceDto`，未落库 |
| **WorldSnapshot（世界快照）** | ✅ 存在 | ✅ `world_id` (String) | ❌ `owner_id` 为 String，无 FK | ✅ `created_at` / `updated_at` | ✅ `version` (Integer) | 结构基本合理；缺与 World/User 的正式外键关系 |

**已存在但可用的部分**

- `database.py`：引擎、SessionLocal、`get_db`、`init_db`（逻辑正确，仅未在启动时强制调用）。
- `models/world_snapshot.py`：主键、版本、时间戳、JSON 快照字段齐全。
- `crud/world_snapshot.py`、`schemas/world_snapshot.py`、`routers/world.py`：世界快照的 CRUD 与 API 链路完整。

**已存在但不安全 / 不完整的部分**

- 启动时未调用 `init_db()`，且历史上用 try/except 吞掉异常，存在“数据库未就绪却静默启动”的风险。
- 无 User / World 表，无法做真正的用户身份与场景元数据持久化；世界快照与“世界”“用户”无外键约束。

**完全缺失的关键模块**

- User 表及与认证的对接（当前为内存占位）。
- World/Scene 表（可被多人进入的“世界”元数据）。
- 启动阶段的**连接校验**（连接失败应直接抛错并退出）。
- `.env.example` 中未体现 `DATABASE_URL` 等数据库配置说明。

---

## 【二】数据库结构设计（MVP 补齐）

在**不破坏已有 WorldSnapshot 逻辑**的前提下，做以下最小补齐与规范：

### 1️⃣ User（用户表）

- `id`：主键（UUID 或自增），当前采用 **Integer 自增**，便于与 FK 配合。
- `username`：唯一，用于登录与展示。
- `created_at`：创建时间（带时区）。
- 可选：`password_hash` 预留（nullable），便于后续接入真实鉴权。

### 2️⃣ World / Scene（世界表）

- `id`：主键，与当前 API 的 `world_id` 一致（String），便于与 WorldSnapshot 一一对应。
- `name`：世界名称。
- `owner_user_id`：外键 → User.id（可空，便于迁移与占位数据）。
- `created_at` / `updated_at`：时间戳。

### 3️⃣ WorldSnapshot（世界快照）

- **评估**：现有字段（world_id、version、snapshot、created_at、updated_at、owner_id）已满足“版本 + 时间戳 + 快照内容”的 MVP 需求。
- **补齐**：
  - 为 `world_id` 增加**外键 → World.id**，保证“快照必属于某一世界”。
  - 首次保存某 `world_id` 的快照时，在 CRUD/路由层**先确保对应 World 行存在**（get_or_create），避免破坏现有“按 world_id 直接存快照”的 API 行为。

不做过度设计：暂不增加复杂权限、审计日志表；预留字段与 FK 即可支撑后续 Mirror 与联机演进。

---

## 【三】启动行为与配置（目标）

- **启动时**：先尝试连接远程 PostgreSQL；若连接失败则**明确抛错并阻止服务启动**（不使用 try/except 静默吞掉）。
- **表创建**：使用 SQLAlchemy `Base.metadata.create_all(bind=engine)`，首次部署到云库即可自动建表。
- **配置**：所有数据库连接信息来自环境变量，不硬编码账号、密码、host；`.env.example` 中补充 `DATABASE_URL` 说明。

---

## 【四】修改文件与目的

| 文件 | 修改目的 |
|------|----------|
| `database.py` | 新增 `check_connection()`（`SELECT 1`），`init_db()` 开头先调用；连接失败即抛错。导入 `user`/`world`/`world_snapshot` 以注册表到 `Base.metadata`。 |
| `models/user.py` | **新增**。User 表：id（自增）、username（唯一）、created_at。 |
| `models/world.py` | **新增**。World 表：id（与 world_id 一致）、name、owner_user_id（FK → User）、created_at/updated_at。 |
| `models/world_snapshot.py` | 为 `world_id` 增加 `ForeignKey("worlds.id", ondelete="CASCADE")`，保证快照必属于某一世界。 |
| `models/__init__.py` | 导出 `User`、`World`、`WorldSnapshot`。 |
| `crud/world.py` | **新增**。`get_or_create_world(db, world_id, name, owner_user_id)`，供保存快照前确保 World 存在。 |
| `crud/__init__.py` | 导出 `get_or_create_world`。 |
| `routers/world.py` | 在 `create_or_update_world_snapshot` 前调用 `get_or_create_world(db, world_id, ...)`。 |
| `server.py` | 启用 `from routers import world` 与 `from database import init_db`；增加 `@app.on_event("startup")` 调用 `init_db()`（不 try/except，失败即阻止启动）；`app.include_router(world.router)`。 |
| `.env.example` | 增加 `DATABASE_URL` 说明及生产/联机提示。 |
| `AUDIT_DATABASE.md` | **新增**。本审计报告与结论清单。 |

---

## 【五】最终结论清单

实施上述修改后，Backend 满足：

| 项 | 状态 |
|----|------|
| 成功连接远程云 PostgreSQL | ✅ 启动时 `check_connection()` 执行 `SELECT 1`，失败即抛错并阻止服务启动 |
| 自动创建数据库表 | ✅ `init_db()` 中 `Base.metadata.create_all(bind=engine)`，首次部署即可建表 |
| 支持「用户 + 世界 + 世界快照」最小联机需求 | ✅ User / World 表已建；WorldSnapshot 有 version、world_id 外键、时间戳；保存快照前自动 get_or_create World |

**需要你手动完成的：**

- 在项目根目录（或 Backend 目录）创建 `.env`，设置 `DATABASE_URL`（例如远程云 PostgreSQL）。不设置则使用默认 `postgresql://postgres:postgres@localhost:5432/morphis`（仅适合本地开发）。
- 若使用远程库，确保网络可达、账号密码正确；连接失败时服务将直接报错退出。

**下一阶段（Mirror Server）才需要做的：**

- 将 `/auth/login`、`/auth/register`、`/workspaces` 从内存占位改为读写 User / World 表（注册写 User，登录校验可仍占位或接密码哈希；workspaces 从 World 表按 owner_user_id 查）。
- 可选：为 World 表增加成员/权限字段；为 User 表增加 password_hash 与鉴权逻辑。
- Mirror 服务端作为“服务器权威”调用本 Backend 的 API 或直连同一数据库读写 User / World / WorldSnapshot。

---

## 【六】与 Unity + Mirror 的对应关系

- 当前数据库用途：**用户身份**、**世界/场景元数据**、**世界持久化（非实时同步）**。
- Mirror Server 后续可作为“服务器权威”读写 User / World / WorldSnapshot；本次仅补齐表结构与启动行为，不实现 Mirror 端逻辑。
