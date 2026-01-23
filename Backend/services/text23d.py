"""
Text to 3D Service
==================
使用豆包 Seed3D (火山引擎) API 将文本转换为 3D 模型
注意：Seed3D 主要支持 Image-to-3D，Text-to-3D 需要先生成图片再转3D
"""

import os
import asyncio
from typing import Optional, Dict, Any

from .api_config import DoubaoSeed3DConfig
from . import text2image, image23d


async def generate(prompt: str, format: str = "glb") -> bytes:
    """
    根据文本生成 3D 模型 (Chained Pipeline)
    流程: Text -> Image -> 3D
    
    Args:
        prompt: 文本提示词
        format: 输出格式 ("glb", "obj", "fbx")
    
    Returns:
        3D 模型的 bytes 数据 (GLB 格式)
    """
    # Mock 模式：直接返回本地模型
    if DoubaoSeed3DConfig.MOCK_MODE:
        return await image23d.load_mock_model("text23d", format)
    
    print(f"[Text23D] Pipeline started for: {prompt}")
    
    # Step 1: Text to Image
    print(f"[Text23D] Step 1: Generating intermediate image...")
    try:
        # 使用 1024x1024 以获得更好的 3D 生成细节
        image_data = await text2image.generate(
            prompt=prompt, 
            width=1024, 
            height=1024
        )
        print(f"[Text23D] Intermediate image generated, size: {len(image_data)} bytes")
    except Exception as e:
        raise Exception(f"Text-to-Image step failed: {e}")

    # Step 2: Image to 3D
    print(f"[Text23D] Step 2: Generating 3D model from image...")
    try:
        # 直接复用 image23d 的完整逻辑（含 polling 和 download）
        model_data = await image23d.generate(
            image_data=image_data, 
            format=format
        )
        print(f"[Text23D] Pipeline completed successfully!")
        return model_data
    except Exception as e:
        raise Exception(f"Image-to-3D step failed: {e}")


# ========== 同步接口（供直接调用）==========

def generate_sync(prompt: str, format: str = "glb") -> bytes:
    """同步版本的生成接口"""
    return asyncio.run(generate(prompt, format))
