"""
Text to 3D Service
==================
使用可配置的 3D 后端将文本转换为 3D 模型
"""

import asyncio

from . import trellis_client, tencent_hunyuan_3d
from .api_config import ThreeDGenerationConfig

async def generate(prompt: str, format: str = "glb") -> bytes:
    """
    根据文本生成 3D 模型

    Args:
        prompt: 文本提示词
        format: 输出格式 (glb)
    
    Returns:
        3D 模型的 bytes 数据 (GLB 格式)
    """
    provider = ThreeDGenerationConfig.get_provider()
    print(f"[Text23D] Provider: {provider}, prompt: {prompt}")
    
    try:
        if provider == "tencent_hunyuan":
            return await tencent_hunyuan_3d.generate_text_to_3d(prompt=prompt, format=format)

        if provider != "trellis":
            raise RuntimeError(f"不支持的 3D provider: {provider}")

        model_data = await trellis_client.generate_3d(
            endpoint="/trellis-text-to-3d",
            data={"prompt": prompt}
        )
        print("[Text23D-Trellis] Completed successfully!")
        return model_data
        
    except Exception as e:
        raise Exception(f"Text-to-3D failed ({provider}): {e}")


# ========== 同步接口（供直接调用）==========

def generate_sync(prompt: str, format: str = "glb") -> bytes:
    """同步版本的生成接口"""
    return asyncio.run(generate(prompt, format))
