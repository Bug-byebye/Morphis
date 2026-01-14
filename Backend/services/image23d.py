"""
Image to 3D Service
===================
将图片转换为 3D 模型

在这里填入你的 API 代码（如 TripoSR, InstantMesh, Meshy 等）
"""

from typing import Optional
from pathlib import Path


async def generate(image_data: bytes, format: str = "glb") -> bytes:
    """
    将图片转换为 3D 模型
    
    Args:
        image_data: 输入图片的 bytes 数据
        format: 输出格式 ("glb", "obj", "fbx")
    
    Returns:
        3D 模型的 bytes 数据 (GLB 格式)
    
    TODO: 在这里实现你的 API 调用
    例如:
    - TripoSR
    - InstantMesh
    - Meshy.ai
    - Stability AI (Stable 3D)
    """
    
    # ========== 示例：返回测试立方体 ==========
    # 实际使用时请替换为真实的 API 调用
    
    test_glb = Path(__file__).parent.parent / "test_cube.glb"
    if test_glb.exists():
        return test_glb.read_bytes()
    
    raise FileNotFoundError("test_cube.glb not found")


# ========== API 实现示例 ==========

# --- TripoSR (Stability AI) ---
# async def generate_triposr(image_data: bytes, api_key: str) -> bytes:
#     import httpx
#     
#     async with httpx.AsyncClient(timeout=120) as client:
#         # 创建任务
#         response = await client.post(
#             "https://api.stability.ai/v1/3d/triposr",
#             headers={"Authorization": f"Bearer {api_key}"},
#             files={"image": image_data}
#         )
#         task_id = response.json()["id"]
#         
#         # 轮询结果
#         while True:
#             status = await client.get(f"https://api.stability.ai/v1/3d/triposr/{task_id}")
#             if status.json()["status"] == "completed":
#                 return await client.get(status.json()["output_url"]).content
#             await asyncio.sleep(2)

# --- Meshy.ai ---
# async def generate_meshy(image_data: bytes, api_key: str) -> bytes:
#     import httpx
#     
#     async with httpx.AsyncClient() as client:
#         # 上传图片并创建任务
#         response = await client.post(
#             "https://api.meshy.ai/v1/image-to-3d",
#             headers={"Authorization": f"Bearer {api_key}"},
#             files={"image": image_data}
#         )
#         # 等待完成并下载
#         ...

# --- 本地 InstantMesh ---
# async def generate_instantmesh(image_data: bytes, server_url: str = "http://localhost:7860") -> bytes:
#     # 调用本地 Gradio 服务
#     pass
