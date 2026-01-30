# 世界快照系统 (World Snapshot System)

## 概述

世界快照系统用于将 Unity 场景中的"可放置物体"序列化为结构化数据，支持本地保存/加载（JSON）和通过 HTTP 与后端交互。

## 核心特性

- ✅ 只处理"静态世界数据"（不涉及 Player 状态、实时同步）
- ✅ 不依赖 Mirror/NetworkIdentity
- ✅ 支持本地 JSON 存储
- ✅ 支持 HTTP 服务器存储（GET/POST）
- ✅ 自动版本管理
- ✅ 模块化设计，易于扩展

## 快速开始

### 1. 设置 PrefabRegistry

创建一个 PrefabRegistry ScriptableObject：

1. 在 Project 窗口中右键 → `Create → Morphis → WorldSnapshot → PrefabRegistry`
2. 在 Inspector 中配置 `prefab_id` 到 Prefab 的映射

### 2. 标记可序列化的物体

为需要保存的 GameObject 添加 `WorldObject` 组件：

```csharp
var worldObj = gameObject.AddComponent<WorldObject>();
worldObj.PrefabId = "my_prefab_id"; // 必须与 PrefabRegistry 中的 ID 匹配
```

### 3. 使用 WorldSnapshotManager

在场景中添加 `WorldSnapshotManager` 组件：

```csharp
// 保存到本地
WorldSnapshotManager.Instance.SaveWorldLocal("my_world_id");

// 从本地加载
WorldSnapshotManager.Instance.LoadWorldFromLocal("my_world_id");

// 保存到服务器
WorldSnapshotManager.Instance.SaveWorldServer("my_world_id", 
    onSuccess: () => Debug.Log("保存成功"),
    onError: (error) => Debug.LogError($"保存失败: {error}"));

// 从服务器加载
WorldSnapshotManager.Instance.LoadWorldFromServer("my_world_id",
    onSuccess: () => Debug.Log("加载成功"),
    onError: (error) => Debug.LogError($"加载失败: {error}"));
```

## 核心模块

### WorldObjectData / WorldSnapshot

数据结构：
- `WorldObjectData`: 单个物体的数据（position, rotation, scale, prefab_id, object_id）
- `WorldSnapshot`: 世界快照（world_id, version, objects 列表）

### WorldObject

标记组件，必须挂载在需要序列化的 GameObject 上：
- `PrefabId`: Prefab 标识符
- `ObjectId`: 对象唯一 ID（自动生成）

### PrefabRegistry

Prefab 注册表，管理 `prefab_id` → `Prefab` 的映射：
- 支持 ScriptableObject 配置
- 支持运行时注册

### WorldSnapshotBuilder

构建世界快照：
- 扫描场景中所有 `WorldObject`
- 构建 `WorldSnapshot` 对象

### WorldSnapshotApplier

应用世界快照：
- 清空现有世界物体
- 根据 `prefab_id` 从 PrefabRegistry 实例化
- 恢复 Transform 数据

### LocalWorldStorage

本地存储（JSON 文件）：
- `SaveToLocal(snapshot)`: 保存到 `Application.persistentDataPath/WorldSnapshots/`
- `LoadFromLocal(worldId)`: 从本地加载

### HttpWorldService

HTTP 服务：
- `POST /world/{world_id}`: 保存快照
- `GET /world/{world_id}`: 加载快照
- 自动使用 `AppSession.Token` 进行认证（如果已登录）

## 使用示例

### 示例脚本

`WorldSnapshotExample.cs` 提供了完整的按键测试示例：
- F5: 保存到本地
- F6: 从本地加载
- F7: 保存到服务器
- F8: 从服务器加载
- F9: 清空世界

### 完整工作流

```csharp
// 1. 设置 PrefabRegistry
var registry = Resources.Load<PrefabRegistry>("PrefabRegistry");
WorldSnapshotManager.Instance.SetPrefabRegistry(registry);

// 2. 运行时注册 Prefab（可选）
WorldSnapshotManager.Instance.RegisterPrefab("cube", cubePrefab);

// 3. 保存世界
WorldSnapshotManager.Instance.SaveWorldLocal("my_world");

// 4. 清空并重新加载
WorldSnapshotManager.Instance.ClearWorld();
WorldSnapshotManager.Instance.LoadWorldFromLocal("my_world");
```

## 后端 API 规范

### POST /world/{world_id}

请求体（JSON）：
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

### GET /world/{world_id}

响应（JSON）：与 POST 请求体格式相同

## 注意事项

1. **Prefab ID 约定**：所有需要保存的 Prefab 必须在 PrefabRegistry 中注册
2. **WorldObject 组件**：所有需要序列化的物体必须挂载 `WorldObject` 组件
3. **版本管理**：每次保存会自动递增版本号
4. **错误处理**：HTTP 调用可能失败，请始终提供 `onError` 回调
5. **未来扩展**：系统设计支持未来被 Mirror Server 直接复用

## 文件结构

```
Assets/Scripts/WorldSnapshot/
├── WorldSnapshotData.cs          # 数据结构
├── WorldObject.cs                # WorldObject 组件
├── PrefabRegistry.cs             # Prefab 注册表
├── WorldSnapshotBuilder.cs        # 快照构建器
├── WorldSnapshotApplier.cs        # 快照应用器
├── LocalWorldStorage.cs           # 本地存储
├── HttpWorldService.cs            # HTTP 服务
├── WorldSnapshotManager.cs        # 管理器（统一 API）
├── WorldSnapshotExample.cs        # 使用示例
└── README.md                      # 本文档
```
