"""
Image to 3D Service
===================
使用可配置的 3D 后端将图片转换为 3D 模型
"""

import asyncio

from . import trellis_client, tencent_hunyuan_3d
from .api_config import ThreeDGenerationConfig

async def generate(image_data: bytes, format: str = "glb") -> bytes:
    """
    将图片转换为 3D 模型
    
    Args:
        image_data: 输入图片的 bytes 数据
        format: 输出格式 (glb)
    
    Returns:
        3D 模型的 bytes 数据 (GLB 格式)
    """
    provider = ThreeDGenerationConfig.get_provider()
    print(f"[Image23D] Provider: {provider}, size: {len(image_data)} bytes")
    
    try:
        if provider == "tencent_hunyuan":
            return await tencent_hunyuan_3d.generate_image_to_3d(image_data=image_data, format=format)

        if provider != "trellis":
            raise RuntimeError(f"不支持的 3D provider: {provider}")

        model_data = await trellis_client.generate_3d(
            endpoint="/trellis-image-to-3d",
            files={"file": ("input.png", image_data, "image/png")}
        )
        return model_data
        
    except Exception as e:
        raise Exception(f"Image-to-3D failed ({provider}): {e}")

# ========== 同步接口（供直接调用）==========

def generate_sync(image_data: bytes, format: str = "glb") -> bytes:
    """同步版本的生成接口"""
    return asyncio.run(generate(image_data, format))
