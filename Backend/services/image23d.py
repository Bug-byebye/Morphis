"""
Image to 3D Service
===================
使用本地 Trellis 服务将图片转换为 3D 模型
"""

import os
import asyncio
from typing import Optional, Dict, Any

from . import trellis_client
from .api_config import DoubaoSeed3DConfig # Keep for mock mode check if needed, or remove if fully switching

async def generate(image_data: bytes, format: str = "glb") -> bytes:
    """
    将图片转换为 3D 模型 (Using Trellis)
    
    Args:
        image_data: 输入图片的 bytes 数据
        format: 输出格式 (glb)
    
    Returns:
        3D 模型的 bytes 数据 (GLB 格式)
    """
    # Mock Mode Check (Optional: keep existing check if user wants to toggle)
    if os.getenv("SEED3D_MOCK_MODE", "false").lower() == "true":
         # Fallback to existing mock logic if needed, referencing old code
         # For now, let's assume we want to use the real Trellis unless explicitly mocked
         pass

    print(f"[Image23D-Trellis] Processing image, size: {len(image_data)} bytes")
    
    try:
        # Trellis image-to-3d endpoint expects 'file' parameter
        # Name of the file doesn't strictly matter for the server logic usually, using 'input.png'
        files = {"file": ("input.png", image_data, "image/png")}
        
        model_data = await trellis_client.generate_3d(
            endpoint="/trellis-image-to-3d",
            files=files
        )
        
        return model_data
        
    except Exception as e:
        raise Exception(f"Trellis Image-to-3D failed: {e}")

# ========== 同步接口（供直接调用）==========

def generate_sync(image_data: bytes, format: str = "glb") -> bytes:
    """同步版本的生成接口"""
    return asyncio.run(generate(image_data, format))
