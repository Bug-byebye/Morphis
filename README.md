# Morphis - Unity AI 生成管线

一个类似 ComfyUI 的可视化节点编辑器，用于在 Unity 中构建 AI 生成管线。

![Unity](https://img.shields.io/badge/Unity-6000.0+-black?logo=unity)
![Python](https://img.shields.io/badge/Python-3.10+-blue?logo=python)

## 功能特点

- 🎨 **可视化节点编辑器** - 拖拽式节点工作流
- 🔗 **节点连接** - 贝塞尔曲线连接
- ✨ **文本转 3D** - 从文字生成 3D 模型
- 📦 **GLB 加载** - 使用 glTFast 运行时加载模型
- 💕 **浪漫主题** - 粉紫色配色方案

## 节点类型

| 节点 | 说明 |
|------|------|
| Text Input | 输入文本提示词 |
| Image Input | 加载图片（开发中）|
| Text to Image | 文字生成图片（开发中）|
| Image to Image | 图片转换（开发中）|
| Image to 3D | 图片转 3D（开发中）|
| Text to 3D | 文字生成 3D 模型 |
| Preview | 预览生成结果 |

## 快速开始

### 环境要求

- Unity 6000.0.38f1 或更高版本
- Python 3.10+
- Git

### 1. 克隆项目

```bash
git clone git@github.com:Bug-byebye/Morphis.git
cd Morphis
```

### 2. 在 Unity 中打开

1. 打开 **Unity Hub**
2. 点击 **Add** → 选择克隆的文件夹
3. 等待 Unity 导入资源（首次打开可能需要几分钟）
4. 打开 `Assets/Scenes/` 下的 `Playground` 场景

### 3. 启动 Python 后端

```bash
cd Backend
pip install fastapi uvicorn trimesh numpy
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

## 开源协议

MIT License
