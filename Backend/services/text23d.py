"""
Text to 3D Service
==================
使用本地 Trellis 服务将文本转换为 3D 模型
"""

import os
import asyncio
from typing import Optional, Dict, Any

from . import trellis_client
from .api_config import DoubaoSeed3DConfig # Keep for mock Check

async def generate(prompt: str, format: str = "glb") -> bytes:
    """
    根据文本生成 3D 模型 (Using Trellis Direct API)
    
    Args:
        prompt: 文本提示词
        format: 输出格式 (glb)
    
    Returns:
        3D 模型的 bytes 数据 (GLB 格式)
    """
    # Mock Mode Check
    if os.getenv("SEED3D_MOCK_MODE", "false").lower() == "true":
        from . import image23d # Lazy import to avoid circular dependency if any
        # This assumes image23d has helper for loading mock models, 
        # but since I removed it from image23d, let's just minimal mock implementation here or skip.
        # Given the user wants "real logic", I will skip complex mock fallback here unless requested.
        pass

    print(f"[Text23D-Trellis] Pipeline started for: {prompt}")
    
    try:
        data = {"prompt": prompt}
        
        model_data = await trellis_client.generate_3d(
            endpoint="/trellis-text-to-3d",
            data=data
        )
        
        print(f"[Text23D-Trellis] Completed successfully!")
        return model_data
        
    except Exception as e:
        raise Exception(f"Trellis Text-to-3D failed: {e}")


# ========== 同步接口（供直接调用）==========

def generate_sync(prompt: str, format: str = "glb") -> bytes:
    """同步版本的生成接口"""
    return asyncio.run(generate(prompt, format))
