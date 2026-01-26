### Morphis 多人联机改造总体方案（基于 Mirror）

> 角色：Unity 网络游戏技术负责人  
> 背景：当前项目已具备第三人称移动、物体放置与拖拽、留言交互、登录 & Workspace 选择（BootFlow + FastAPI），计划从单机拓展为多人协作搭建游戏，采用 Mirror。

---

## 1. 整体架构设计

### 1.1 使用 Mirror 时的整体系统架构（文字版）

- **Unity 客户端（多台）**
  - 负责：玩家本地输入采集（键盘、鼠标）、UI（登录、Workspace 选择、节点编辑器、模型库 UI）、本地表现（动画、摄像机、VFX）、本地预测（可选）。
  - 使用 Mirror 的 `NetworkClient`，通过 `NetworkManager` 连接到 Unity 服务器实例。
  - 只在本地操作“纯表现层”逻辑；所有会影响共享世界状态的操作通过 Mirror 的 Command / RPC / NetworkVariable 同步。

- **Unity 服务器（基于 Mirror 的 Headless 实例）**
  - 可和客户端同一工程构建，运行在无图形界面的 Headless 模式。
  - 挂载 `NetworkManager`、自定义 `NetworkManager` 派生类（如 `MorphisNetworkManager`）、`NetworkRoomManager`（可选）。
  - 负责：
    - 玩家连接、断线、重连管理。
    - **权威拥有的世界状态**：玩家 Transform、可放置物体（位置/旋转/缩放/自定义属性）、留言内容、场景中的运行时生成对象。
    - 处理所有“会改变共享世界”的逻辑：放置/移动/删除建筑、修改留言、世界存档调度等。
  - 仅对外开放 Unity Transport 端口，不直接暴露给公网玩家（可放在内网或受控的云环境中）。

- **后端服务（Python / FastAPI，当前 `Backend/server.py`）**
  - 已实现：`/auth/login`、`/auth/register`、`/workspaces`，并有内存态的用户与 workspace 管理。
  - 未来扩展为：
    - 用户账号系统（含 Token 鉴权、密码、找回/重置 等）。
    - Workspace = “世界/房间”的元数据管理（ID、名称、成员、当前服务器地址/端口、世界存档位置）。
    - 世界存档的元数据接口（例如 `/worlds/{id}`，返回存档路径、最后更新时间等）。
    - 服务器发现与分配（简单版：某个 Workspace 固定绑定一台 Unity Server；复杂版：按负载动态分配）。
  - 与 Unity 服务器之间可以不直接通讯，**通过客户端间接协作**：
    - 客户端 → 后端：登录、选 Workspace、获取该 Workspace 对应的 Unity 服务器地址/端口 & WorldId。
    - 客户端 → Mirror 服务器：携带从后端拿到的 token / WorldId 连接。

### 1.2 职责边界

- **客户端**
  - UI：BootFlow 登录 & Workspace 列表、模型库 UI（`ModelLibraryUI`）、节点编辑器（`SimpleNodeEditor` 等）、交互提示（`ObjectInteractionManager`）。
  - 本地输入 & 视角控制：
    - `ThirdPersonController` / `PlayerController` 只在本地 player 上启用。
  - 只在本地执行：
    - 摄像机旋转、角色动画播放（基于同步到本地的状态）、UI 打开/关闭。
  - 不直接修改世界权威状态，只发起请求（按 Mirror 模式：Command -> 服务器）。

- **Unity 服务器（Mirror）**
  - 有权修改：
    - 所有带 `NetworkIdentity` / `NetworkBehaviour` 的对象的状态。
    - 玩家出生点与重生。
    - 放置物体的最终位置 / 旋转 / 缩放 / UUID / 自定义属性（如 comment）。
  - 负责世界存档的“序列化与反序列化”（可通过调用本地文件 / 对象存储 SDK）。
  - 执行游戏规则：例如物体数量限制、区域权限、碰撞规则、后续若有经济/进度等。

- **后端（FastAPI）**
  - **不参与帧级游戏逻辑**，只做“会话级 / 元信息级”操作：
    - 登录 / 注册（已实现）。
    - 用户与 Workspace 关系管理（已实现占位）。
    - 为每个 Workspace 记录：
      - 当前绑定的 Unity 服务器地址/端口。
      - 存档 ID 或路径。
    - 未来可扩展：好友、房间浏览、购买 DLC、计费等。

### 1.3 必须在服务器权威下执行的功能

- **玩家 Transform 与移动**：
  - `ThirdPersonController` 等只在 `isLocalPlayer` 上读输入，但真正的位移写入应由服务器控制（典型做法：Server Authoritative Movement）。
  - 可采用“客户端发意图 / 输入，服务器计算结果并同步”的模式，减少外挂与不同步风险。

- **世界中所有可放置 / 拖拽物体**（目前通过 `PlaceableObjectMover` + `ObjectInteractionManager` 实现）：
  - 物体的创建（Instantiate）、销毁（Destroy）。
  - 物体坐标、旋转、缩放。
  - 物体的逻辑属性（例如 `InteractableObject.comment`、自发光状态）。

- **世界存档的读写**：
  - 读：通常在 **服务器启动某个 Workspace 时** 加载。
  - 写：应由服务器在安全时间点（如定时、玩家离开、手动保存时）统一写入。

- **与多人协作相关的校验**：
  - 防止无效/恶意请求（比如客户端请求把物体移动到极远位置、频繁刷物体等）。
  - 后续如有“权限”（谁能改谁的物体）、“资源限制”等也应在服务器执行。

---

## 2. 现有代码改造方案

### 2.1 当前单机逻辑中，可以基本保持不变的部分

- **BootFlow & AppFlow（`BootFlowManager`, `AppSession`, `ModelLibraryBootstrap`）**
  - 登录 → 选 Workspace → 加载 `MainScene` 的流程可以保留。
  - 只需在“进入 MainScene 时”多做一步：根据 Workspace 信息连接到对应的 Mirror 服务器。
  - `AppSession` 继续作为客户端的会话状态容器（Token、WorkspaceId、WorkspaceName）。

- **UI 与编辑器相关逻辑**
  - 节点编辑器：`SimpleNodeEditor`、`PipelineGraph`、各类 Node（`Text23DNode`、`Text2ImageNode` 等）
    - 这些更多偏工具/生成流程，暂时与多人协作弱关联，可先保持单机逻辑；未来再考虑协作编辑。
  - 模型库 UI（`ModelLibraryUI`）：展示 & 选择模型的 UI 可以保持本地，只有“生成/放置实例”的那一步需要改成发请求给服务器。
  - `ObjectInteractionManager` 中的 **UI 创建逻辑**、鼠标射线检测、悬浮 Tooltip 位置更新都可以保留为“本地表现层”。

- **与网络无关的工具脚本 / 静态数据**
  - 材质 Fix、Editor 扩展、NodeEditor 的大部分 UI 逻辑。

### 2.2 必须重构为网络版本的逻辑

#### 2.2.1 Player 相关（`ThirdPersonController` / `PlayerController` 等）

- 新增 `NetworkPlayer` 或改造为 `NetworkBehaviour`：
  - 每个玩家 GameObject 应挂载：
    - `NetworkIdentity`（Mirror 自动加在 `NetworkBehaviour` 上）。
    - 输入 + 控制逻辑只在 `isLocalPlayer` 上启用。
  - 在服务器创建/销毁玩家对象，给每个客户端分配一个 Player 实例（使用 `NetworkManager` 的 `OnServerAddPlayer`）。

- 典型改造：
  - 将当前 `ThirdPersonController` 保留为“本地控制类”，但挂在 **Player prefab** 上，只在 `isLocalPlayer` 时启用。
  - 新增一个 `NetworkPlayerController`（继承 `NetworkBehaviour`），负责：
    - 采集本地输入并通过 `CmdMove` 发送给服务器。
    - 服务器应用运动逻辑并同步位置给所有客户端（通过 `NetworkTransform` 或自定义同步）。

#### 2.2.2 物体放置与交互（`PlaceableObjectMover`, `ObjectInteractionManager`, `InteractableObject`）

- **放置 / 拖拽物体的权威**：
  - 现在的 `PlaceableObjectMover`、`ObjectInteractionManager` 直接在本机通过射线 + `transform.position` 改变位置，这在多人环境下会产生冲突。
  - 改造方向：
    - `PlaceableObjectMover` 仅在本地决定“目标位置”，然后通过 `Command` 发给服务器。
    - 服务器在对应的 `NetworkPlaceableObject` 上真正更新 `transform.position`，并通过 `NetworkTransform` 同步。

- **留言 / 光晕**：
  - `InteractableObject.comment`、`UpdateGlow()` 必须在服务器上更新，并通过 SyncVar/ClientRpc 通知各客户端。
    - `comment` 用 `[SyncVar(hook = nameof(OnCommentChanged))]`。
    - 当 comment 改变时，在 `OnCommentChanged` 里调用 `UpdateGlow()`（该方法可以仍为本地执行，只作用于当前实例的材质表现）。
  - `ObjectInteractionManager`：
    - 保留用于 UI 与射线检测。
    - 但 `OnSaveComment` / `OnDeleteComment` 不再直接改 `currentTarget.comment`，而是调用 `NetworkInteractableObject.CmdSetComment` / `CmdClearComment`。

#### 2.2.3 世界管理（运行时生成/销毁的物体）

- 需要一个“世界管理器”：
  - 如 `WorldManager : NetworkBehaviour`（仅服务器上有权执行）。
  - 负责：
    - 在服务器启动该 Workspace 时，根据存档数据创建初始场景物体。
    - 接收客户端请求（“放置某种模型”、“删除某个物体”、“移动某个物体到位置 X”）。
    - 分配每个物体的 `NetworkIdentity` 与逻辑 ID（如 GUID）。

### 2.3 Player / 建筑物 / 世界管理改造建议

- **Player**
  - 新建网络 Player prefab（例如 `NetworkPlayer`）：
    - 组件结构示例：
      - `NetworkIdentity` + `NetworkTransform`
      - `CharacterController`
      - `ThirdPersonController`（保持原控制逻辑，仅在 `isLocalPlayer` 时启用）
      - `NetworkPlayerController`（NetworkBehaviour，处理输入转发与权限控制）

- **可放置物体（建筑/道具）**
  - 所有可放置的 prefab 增加：
    - `NetworkIdentity`
    - 自定义 `NetworkPlaceableObject : NetworkBehaviour`，负责：
      - 维护物体的逻辑 ID（GUID）、类型 ID（哪种模型）、初始参数。
      - 提供服务器端修改位置信息的方法。
  - 将当前 `PlaceableObjectMover` 中直接改 `transform.position` 的逻辑迁移到：
    - 客户端：计算目标位置，调用 `CmdRequestMoveObject(objectId, targetPosition)`。
    - 服务器：验证 & 更新物体 Transform，再同步。

- **世界管理脚本**
  - 创建：`WorldManager : NetworkBehaviour` / `WorldStateManager`：
    - 在 `OnStartServer` 中从存档加载世界数据，实例化物体并注册到字典 `Dictionary<string, NetworkPlaceableObject>`。
    - 对外提供供 Command 调用的接口（内部只服务器执行）。

### 2.4 “单机 → Mirror”的典型改造模式示例

#### 2.4.1 Player 控制改造示例（伪代码）

**原始（简化）**：`ThirdPersonController.Update()`

```csharp
// 单机：直接从输入计算并写 CharacterController.Move(...)
private void Update()
{
    JumpAndGravity();
    GroundedCheck();
    Move(); // 内部直接操作 _controller.Move(...)
}
```

**改造：增加 NetworkPlayerController**

```csharp
using Mirror;

public class NetworkPlayerController : NetworkBehaviour
{
    private ThirdPersonController _localController;

    private void Awake()
    {
        _localController = GetComponent<ThirdPersonController>();
    }

    public override void OnStartAuthority()
    {
        // 只有本地玩家启用输入
        _localController.enabled = true;
    }

    private void Update()
    {
        if (!hasAuthority) return;

        // 本地收集输入（可直接使用 StarterAssetsInputs）
        Vector2 move = _localController.GetInput(); // 假设封装了输入
        CmdMove(move);
    }

    [Command]
    private void CmdMove(Vector2 move)
    {
        // 在服务器上应用移动（可直接重用 ThirdPersonController 或提取公共方法）
        ServerApplyMovement(move);
    }

    [Server]
    private void ServerApplyMovement(Vector2 move)
    {
        // 这里仍可以使用 CharacterController，只是运行在服务器
        // 或者只在服务器更新位置，然后 NetworkTransform 同步到客户端
    }
}
```

> 实际工程中，可以阶段性妥协：**早期允许客户端直接驱动自身位置，只做位置同步（弱权威）**，待联机稳定后再引入“完全服务器权威移动”。

#### 2.4.2 InteractableObject → NetworkInteractableObject

```csharp
using Mirror;

public class NetworkInteractableObject : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnCommentChanged))]
    public string comment;

    [Server]
    public void SetCommentServer(string newComment)
    {
        comment = newComment;
    }

    [Server]
    public void ClearCommentServer()
    {
        comment = string.Empty;
    }

    void OnCommentChanged(string oldValue, string newValue)
    {
        // 本地表现：调用已有 InteractableObject.UpdateGlow()
        var interactable = GetComponent<InteractableObject>();
        if (interactable != null)
        {
            interactable.comment = newValue;
            interactable.UpdateGlow();
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdSetComment(string newComment)
    {
        // TODO: 可以在这里做权限校验（比如只有拥有者可以改）
        SetCommentServer(newComment);
    }

    [Command(requiresAuthority = false)]
    public void CmdClearComment()
    {
        ClearCommentServer();
    }
}
```

`ObjectInteractionManager.OnSaveComment()` 中改为：

```csharp
void OnSaveComment()
{
    if (currentTarget != null)
    {
        var net = currentTarget.GetComponent<NetworkInteractableObject>();
        if (net != null)
        {
            net.CmdSetComment(commentInput.text);
        }
    }
    CloseDialog();
}
```

---

## 3. 世界数据与存档设计

### 3.1 场景抽象为可序列化“世界数据”

考虑当前玩法：“合作搭建 + 对物体留言”，我们可以把世界抽象为：

- `WorldData`
  - `worldId`：对应 Workspace / Room 的唯一 ID。
  - `version`：存档版本号，便于将来迁移。
  - `placedObjects`：场景中所有放置物体的列表。
  - `environmentSettings`（可选）：天气、时间、天空盒等。

- `PlacedObjectData`
  - `id`：物体唯一 ID（GUID）。
  - `prefabId`：指向资源表中的一个 prefab（例如 `house_small_01`）。
  - `position`：`Vector3`（序列化为 `{x,y,z}`）。
  - `rotation`：`Quaternion` 或 `Vector3 euler`。
  - `scale`：`Vector3`。
  - `comment`：字符串（对接 `InteractableObject.comment`）。
  - 未来可加：`ownerUserId`、`createdAt`、`customData`。

### 3.2 世界数据结构示例（字段级）

```csharp
[Serializable]
public class WorldData
{
    public string worldId;
    public int version;
    public List<PlacedObjectData> placedObjects = new();
}

[Serializable]
public class PlacedObjectData
{
    public string id;
    public string prefabId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public string comment;
}
```

可以使用 Unity 的 `JsonUtility` 或 `Newtonsoft.Json` 在服务器侧序列化/反序列化。

### 3.3 世界加载 / 保存流程

- **加载流程（服务器侧）**
  - 触发时机：
    - Unity 服务器启动某个 Workspace 对应的世界时（`WorldManager.OnStartServer`）。
  - 步骤：
    1. 通过 WorkspaceId → 向后端查询存档元数据（或根据约定路径直接访问存档）。
    2. 读取 `WorldData` JSON。
    3. 遍历 `placedObjects`：
       - 根据 `prefabId` 从服务器资源表（`Dictionary<string, GameObject>`）找到 prefab。
       - 在服务器 Instantiate 对象，给它挂 `NetworkPlaceableObject` + `NetworkInteractableObject` 等。
       - 设置 Transform / comment / 其它属性。
    4. Mirror 会自动把这些对象同步到后来加入的客户端。

- **保存流程（服务器侧）**
  - 触发时机（推荐多种组合）：
    - 定时（例如每 60 秒增量保存 / 全量保存）。
    - 玩家显式点击“保存世界”按钮 → 客户端发 Command 到服务器。
    - 所有玩家离开该 Workspace 时，服务器进行一次最终保存再卸载。
  - 步骤：
    1. 遍历服务器上的所有 `NetworkPlaceableObject`：
       - 读取它的 prefabId、Transform、comment 等。
    2. 写入一个 `WorldData` 实例。
    3. 序列化为 JSON。
    4. 写入文件 / 对象存储。

### 3.4 世界数据应该存在哪里

- **早期迭代阶段**（本地开发 / 小规模测试）：
  - 存在 **Unity 服务器的本地磁盘**：如 `./WorldSaves/{worldId}.json`。
  - 优点：实现简单，和当前 FastAPI 内存态用户系统风格一致。
  - 使用 Python 后端时，只记录“世界存在/路径”元数据即可，不必介入实际文件 IO。

- **准备上线阶段**：
  - 建议将世界存档迁移到 **对象存储 / 挂载磁盘 / 数据库**：
    - 对象存储：如 AWS S3 / 阿里云 OSS / 七牛（通过 SDK 上传 `WorldData` JSON）。
    - 或者使用 数据库（PostgreSQL, MongoDB 等）作为持久化层。
  - 方案：
    - Unity 服务器负责生成 `WorldData` JSON，然后调用后端 API（FastAPI 提供）上传该 JSON。
    - 后端持久化到对象存储 / DB，并记录版本号与最后更新时间。

> 建议：**第 1 轮联机原型不引入复杂的存档后端**，先使用 Unity 服务器本地文件，等联机稳定、玩法成型后再统一迁移。

---

## 4. 用户系统与后端（Python 可行性）

### 4.1 是否推荐引入独立后端（Python / FastAPI）

- 当前项目已经有比较完整的 `Backend/server.py`，并运行在 FastAPI 上，支持登录、注册、workspace 列表。
- 对于一个 **计划长期运营** 的小团队项目，非常推荐继续沿用 Python/ FastAPI 作为：
  - 用户 & Session 中心。
  - Workspace / World 元信息中心。
  - 后续统计与运营扩展（埋点、成就、付费等）的统一出口。

Python + FastAPI 对小团队而言：
- 易于快速开发与迭代。
- 当前代码已示范了 CORS、pydantic 模型、简单内存状态的实现。
- 可以逐步引入数据库 ORM（SQLAlchemy / Tortoise ORM 等），不会立即增加 Unity 端复杂度。

### 4.2 后端应负责的功能

- **核心（现在就有价值）**
  - 登录 / 注册 / Token 签发（已经有占位逻辑，可替换为 JWT + 数据库）。
  - 工作区（Workspace / World）的创建、查询、成员管理。
  - 某个 Workspace 当前绑定的 Unity 服务器实例信息（host, port）。

- **中期扩展**
  - 用户资料（昵称、头像、语言）。
  - 好友系统 / 邀请制进入同一 Workspace。
  - 世界浏览 / 推荐：公开/私有 worlds 列表。

- **长期扩展**
  - 商业化相关：付费道具、订阅、DLC。
  - 日志 / 运营后台。

### 4.3 Unity 客户端与后端交互流程（登录 → 选世界 → 进游戏）

基于 `BootFlowManager` 当前逻辑，增加“获得服务器地址 & WorldId”的步骤：

1. **登录**
   - 客户端：`BootFlowManager` 调 `POST /auth/login`。
   - 后端：验证成功 → 返回 `{token, username}`。
   - 客户端：保存到 `AppSession`。

2. **获取 Workspace 列表**
   - 客户端：`GET /workspaces` 带 `Authorization: Bearer <token>`。
   - 后端：根据用户名返回该用户的 Workspace 列表（当前已实现占位）。

3. **选中某个 Workspace**
   - 客户端：在 UI 中点击，设置 `AppSession.WorkspaceId/Name`。

4. **查询 Unity 服务器信息（可在第 2 步一起返回）**
   - 方式 A（推荐）：`GET /worlds/{workspaceId}` 或在 `/workspaces` 响应中携带 `serverHost, serverPort, worldId`。
   - 客户端：记录该 Workspace 对应的 `serverHost, serverPort, worldId`。

5. **加载 Unity 主场景 + 连接 Mirror 服务器**
   - `BootFlowManager.EnterMainScene()` 加载 `MainScene`。
   - 在 `MainScene` 中：
     - 有一个 `MorphisNetworkManager`，初始时读取 `AppSession` 中的服务器地址与 WorldId。
     - 调用 `NetworkManager.StartClient()` 连接服务器。
     - 连接成功后，客户端通过 Mirror 的 `Command` 把 `AppSession.Token`、`WorkspaceId`、`WorldId` 发送给服务器。
     - 服务器可简单验证这些信息（例如 WS id 是否存在），并将玩家加入对应房间。

### 4.4 Mirror 与后端之间的边界与协作方式

- **边界原则**
  - 后端不需要“感知到每一帧的游戏状态变化”。
  - Mirror 服务器只需在以下场景与后端交互：
    - 连入验证（可选）：接到客户端 token 后，向后端调用一次 `/auth/validate`。
    - 世界存档上传 / 下载（后期）：通过后端提供的 REST API 读写存档。

- **协作方式建议**
  - **早期阶段**：
    - 客户端只跟后端交互，Mirror 服务器“相信客户端送来的 token & WorldId”，不做更多验证。
  - **中期 & 上线阶段**：
    - Mirror 服务器启动时，主动向后端注册自己（`POST /servers/register`），说明自己可以承载哪些 world。
    - 客户端进入世界时，Mirror 服务器会向后端验证 token & workspaceId（`/auth/validate`，`/workspaces/{id}`）。
    - 世界存档由 Mirror 服务器生成 JSON，通过后端 API 上传（统一加鉴权与存储逻辑）。

---

## 5. 云服务器与部署路线

### 5.1 项目早期是否需要云服务器

- 早期（团队内部开发 + 小规模朋友测试）：
  - 不一定需要真正的云服务器，可以：
    - 本地开一台 Headless Unity Server + 内网穿透（如 frp/ngrok）提供演示。
    - 或者让所有人通过 VPN / 局域网访问。
  - 这样可以在没有部署负担的情况下快速迭代联机逻辑。

### 5.2 什么时候必须引入云服务器

- 当出现以下任一情况时，基本可以认为要上云：
  - 需要稳定对外公网接入（测试玩家分布在不同城市）。
  - 需要长时间运行的“常驻世界服务器”（比如空间全天在线）。
  - 需要日志、监控、崩溃恢复。
  - 需要与其他云服务（如对象存储、数据库）打通。

### 5.3 Unity Headless Server 的角色与部署方式

- **角色**
  - 运行 Mirror 服务器，维护世界状态与多人协作。
  - 不负责 UI 显示，只负责逻辑和同步。

- **部署方式**
  - 使用 Unity 构建：
    - Build Target：Windows / Linux Server。
    - 勾选 Headless 模式（取决于 Unity 版本与 Build 选项）。
  - 部署在：
    - 早期：单台云主机（如 2-4 核，8GB 内存即可）。
    - 系统：推荐 Linux（成本低、镜像成熟）。
  - 启动参数：
    - 指定端口、worldId、配置路径（可通过命令行参数或 env 变量）。

### 5.4 最小可行的服务器部署方案

- **方案 A：一台机器 + 1 个 Unity Server + 1 个 FastAPI 后端**
  - 机器：云主机（例如 2C4G/8G）。
  - 进程：
    - `uvicorn Backend.server:app`（FastAPI）。
    - `MorphisServer.x86_64`（Unity Headless，内含 Mirror Server）。
  - 网络：
    - 对外开放 HTTP 端口（如 8000）给客户端登录。
    - Mirror 服务器端口（如 7777）也对外开放。
  - 路线：
    - 客户端登录 → `/workspaces` → 后端返回 `serverHost=your-cloud-ip, serverPort=7777`。
    - 客户端连接 Mirror 服务器 → 进入世界。

- **方案 B：一台机器，多个 Workspace 共用一个 Unity 进程**
  - Mirror 中使用 Room / 多场景；但小团队前期不必追求多房间复用，先按「一台服务器 = 一组 Workspace」即可。

---

## 6. 分阶段实施计划（6–8 阶段）

> 原则：**先打通最小可玩的联机合作搭建**，再逐步提升安全性、易用性和规模，不要一开始就做分布式和复杂后端。

### 阶段 1：学习 & 环境准备（1 周）

- **目标**
  - 团队熟悉 Mirror 的基本概念和使用方法，并能跑起来官方示例。
- **主要改动**
  - 在当前 Unity 项目中引入 Mirror 包（通过 UPM / Git URL）。
  - 创建一个最简单的 `NetworkManager` 场景，完成本地两客户端连接。
- **验证标准**
  - 在一台 PC 上开两个 Unity 客户端（或一个 Editor + 一个 Build），实现玩家 Capsule 在联机场景中相互可见。

### 阶段 2：基础 Player 联机（1–2 周）

- **目标**
  - 将 `ThirdPersonController`/`PlayerController` 改造为可在 Mirror 下多人同步（即“我能看见别人的角色在走动”）。
- **主要改动**
  - 新建 `NetworkPlayer` prefab，集成：
    - `NetworkIdentity` + `NetworkTransform`。
    - `ThirdPersonController`（只在 `isLocalPlayer` 上启用）。
  - 使用简单的 Mirror 客户端权限模式（客户端驱动自身位置，服务器广播）。
- **验证标准**
  - 两名玩家进入同一场景，可以互相看到角色移动（即使暂时有一点不同步/抖动也可以）。
- **可推迟的工作**
  - 完全服务器权威的移动与作弊防护。

### 阶段 3：物体可视同步（不含交互）（1 周）

- **目标**
  - 所有场景中已有的放置物体在各客户端之间位置一致（静态同步）。
- **主要改动**
  - 将需要同步的运行时物体 prefab 增加 `NetworkIdentity`。
  - 新建 `NetworkPlaceableObject`，用于将 Transform 状态通过 Mirror 同步。
  - 在 MainScene 中，对已经放置的关键物体 prefab 做初步网络化处理（无需玩家操作）。
- **验证标准**
  - 服务器上调整物体位置（例如调试命令），所有客户端看到位置变化。
- **可推迟的工作**
  - 物体拖拽和创建/删除逻辑。

### 阶段 4：物体放置与拖拽联机化（2 周）

- **目标**
  - 玩家可以在多人环境中共同放置 / 拖拽物体，并在各端保持一致。
- **主要改动**
  - 将 `PlaceableObjectMover` 改造为客户端请求 → 服务器更新 → Mirror 同步的模式：
    - 客户端计算目标位置，发送 `CmdRequestMove(objectId, targetPos)`。
    - 服务器校验后，更新 `NetworkPlaceableObject` 的 Transform。
  - 为“新放置的物体”增加网络化流程（服务器负责 `Instantiate` 并 `NetworkServer.Spawn`）。
- **验证标准**
  - 玩家 A 拖拽物体，玩家 B 实时看到物体移动。
  - 多个玩家同时拖拽同一物体时，仍以服务器结果为准（即哪一边生效由服务器决定）。
- **可推迟的工作**
  - 复杂的冲突解决策略（锁定物体、排队机制等）。

### 阶段 5：留言与交互联机化（1 周）

- **目标**
  - `InteractableObject.comment` 与光晕效果在多人间一致。
- **主要改动**
  - 新建 `NetworkInteractableObject`，用 SyncVar/Lobby 模式同步 comment。
  - 将 `ObjectInteractionManager` 的 `OnSaveComment` / `OnDeleteComment` 改为调用 Command。
  - 在 comment 修改时服务器广播变更，所有玩家看到相同的 Tooltip / 光晕状态。
- **验证标准**
  - 玩家 A 在某物体上写留言，玩家 B 悬停即可看到相同内容，光晕状态一致。

### 阶段 6：世界存档（服务器本地文件）（1–2 周）

- **目标**
  - 服务器可以在重启后恢复上一次的建筑状态。
- **主要改动**
  - 实现 `WorldData` / `PlacedObjectData` 序列化结构。
  - 新建 `WorldManager`（服务器）：
    - `OnStartServer` 时从 `WorldSaves/{worldId}.json` 加载并实例化物体。
    - 定时或手动触发保存，将当前世界对象转为 `WorldData` 并写入 JSON。
  - 在后端（Python）中为 Workspace 记录 worldId，Unity 服务器通过启动参数获知 worldId。
- **验证标准**
  - 在服务器上构建多个物体，退出服务器进程后重启，物体布局能被完全恢复。
- **可推迟的工作**
  - 对象存储/数据库、增量存档、版本迁移等。

### 阶段 7：简易部署 & 小规模外网测试（1–2 周）

- **目标**
  - 在一台云主机上跑起 FastAPI + Headless Unity Server，允许外网少量玩家同时进入同一世界。
- **主要改动**
  - 将 FastAPI 服务部署到云端（使用 uvicorn + systemd / Docker 皆可）。
  - 将 Unity Headless Server build 上传到云主机并运行，开放端口。
  - 更改客户端配置：`AppSession.BaseUrl` 指向云后端域名。
- **验证标准**
  - 位于不同网络环境的玩家可以：
    - 登录 / 注册。
    - 选择同一个 Workspace。
    - 一起进入世界移动、放置物体、加留言。

### 阶段 8：打磨与扩展（持续进行）

- **目标**
  - 在稳定联机基础上，逐渐处理延迟、卡顿、掉线恢复、权限、安全等问题。
- **可选工作**
  - 移动预测与插值、延迟补偿。
  - 更细致的权限控制（谁能修改谁的物体、Kick/封禁等）。
  - 分离多个 Unity 服务器实例（不同 Workspace 或分区）。

---

## 7. 风险点与常见坑

### 7.1 新手团队使用 Mirror 时常见坑

- **在客户端直接修改状态，而不是通过服务器**：
  - 例如在 `Update()` 里直接改 `transform.position`，但其实该对象是服务器权威的 NetworkObject，导致状态被服务器回滚。
  - 解决：区分好“本地表现”和“权威状态”，对权威状态只在服务器改。

- **忘记在 prefab 上配置 `NetworkIdentity` / 没有通过 `NetworkServer.Spawn` 创建对象**：
  - 导致客户端看不到运行时生成的物体。

- **在非服务器环境调用 Server Only 方法**：
  - 比如在客户端调用 `[Server]` 方法，引发运行时警告或逻辑不生效。
  - 需要使用 `isServer` / `isClient` / `isLocalPlayer` 判断。

- **场景切换与网络对象生命周期管理混乱**：
  - 没有规划好“哪个场景是网络场景”、“哪个场景只在本地运行”。
  - 当前项目已经有 BootScene → MainScene 的流程，应清晰区分：
    - BootScene：纯本地（不启用 Mirror）。
    - MainScene：网络场景（由 Mirror 管理对象）。

### 7.2 结合当前项目结构的特有风险

- **BootFlow 与 MainScene 的职责边界**
  - 当前 `BootFlowManager` 使用 `DontDestroyOnLoad` 持久化挂载，且会动态创建 Canvas & EventSystem。
  - 进入 MainScene 后如果不彻底清理，可能会与网络 UI / EventSystem 冲突。
  - 必须确保：在 MainScene 中，BootFlow 不再持有任何 UI/输入控制权，只保留 `AppSession` 和必要的网络配置。

- **已有输入系统（StarterAssets + 新 InputSystem）与 Mirror 的组合**
  - 需要规范：哪些脚本在 `isLocalPlayer` 时启用输入，哪些脚本只做表现。
  - 否则多个客户端可能同时驱动同一玩家对象。

- **节点编辑器与玩法逻辑耦合**
  - 当前 NodeEditor 更偏“生产工具”，若贸然把它联机化，会极大增加复杂度。
  - 建议：短期完全按单机工具对待，联机只同步“结果”（生成好的 3D 模型，作为普通放置物体）。

### 7.3 明确“现在不应该做的事情”

- **不应该一开始就做多区服 / 动态扩容 / 分布式匹配**：
  - 小团队 + 2–3 名程序员，初期目标应是“稳定的一台服务器 + 少量用户”。

- **不应该立即引入复杂的数据库与微服务架构**：
  - 目前 `Backend/server.py` 内存态逻辑足够支撑早期玩法验证。
  - 先把世界数据结构、联机安全性打实，再引入数据库。

- **不应该现在就尝试“多人协作编辑 NodeEditor 流程图”**：
  - 这属于高级协作编辑问题（CRDT/OT），比“多人协作搭建建筑”复杂一个量级。
  - 建议 NodeEditor 保持本地编辑，生成的结果（3D 模型）作为普通物体交由服务器管理即可。

- **不应该过早优化所有网络性能细节**：
  - 如一开始就做各类插值/预测/压缩等，会分散注意力。
  - 正确顺序：先保证逻辑正确、状态一致，再做延迟体验优化。

---

## 结语

以上方案紧贴当前项目结构（BootFlow + Python 后端 + StarterAssets Player + 物体放置与留言），以 **Mirror 为核心网络层**，建议按“先实现联机合作搭建 + 简单存档，再慢慢引入后端与部署复杂度” 的路径推进。  
如果团队在实施过程中遇到具体脚本改造问题（例如某个 MonoBehaviour 如何改为 NetworkBehaviour），可以以本方案中的改造模式为模板，逐步替换与拆分。 

