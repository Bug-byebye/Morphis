"""
生成测试用 GLB 立方体文件
运行此脚本生成 test_cube.glb
"""
import trimesh
import numpy as np

# 创建一个简单的立方体
mesh = trimesh.creation.box(extents=[1.0, 1.0, 1.0])

# 设置颜色（蓝色）
mesh.visual.vertex_colors = [100, 150, 255, 255]

# 导出为 GLB 格式
mesh.export("test_cube.glb", file_type="glb")

print("✅ Created test_cube.glb")
