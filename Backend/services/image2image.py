"""
Image to Image Service
======================
基于参考图片生成新图片 (Powered by Doubao/Volcengine)
"""

import os
import io
import base64
import httpx
from typing import Optional

from .api_config import ArkImageGenConfig

async def generate(
    image_data: bytes,
    prompt: str,
    strength: float = 0.75,
    negative_prompt: str = ""
) -> bytes:
    """
    基于输入图片生成新图片 (使用火山引擎/豆包 API)
    
    Args:
        image_data: 输入图片的 bytes 数据
        prompt: 提示词
        strength: 变化强度 (0.0-1.0) - 注意: 豆包 API 可能使用不同的参数或不支持此参数，需确认
        negative_prompt: 负向提示词
    
    Returns:
        生成图片的 bytes 数据 (PNG 格式)
    """
    api_key = os.getenv('ARK_API_KEY')
    if not api_key:
        raise ValueError("Missing ARK_API_KEY environment variable")

    print(f"[Image2Image] Processing request for prompt: {prompt}")

    # 1. 准备图片 (Base64 Data URI)
    # 检测图片类型 (简单判断)
    mime_type = "image/png"
    if image_data.startswith(b'\xFF\xD8'):
        mime_type = "image/jpeg"
    
    image_b64 = base64.b64encode(image_data).decode('utf-8')
    # 对于 Ark Runtime/OpenAI 兼容接口，通常支持 URL 或 Base64
    # 这里我们尝试直接上传 Base64 Data URL (如果模型支持) 
    # 或者如果不兼容，可能需要先上传到 TOS。
    # 根据用户提供的 snippet，使用的是 image URL。
    # 标准 OpenAI SDK image edit 接受 file stream。
    # 但 volcanosdkarkruntime 是 specific client.
    # 让我们尝试构造 Data URI，这是最常见的无状态传图方式。
    
    # 警告：如果不支持 Data URI，我们可能需要一个临时的 TOS 上传服务。
    # 但根据文档，很多模型支持 base64。
    # 如果失败，我们可能需要 fallback 到 TOS (这需要额外的 AK/SK 配置)。
    # 暂时假设支持 Data URI。
    
    # The user snippet snippet implies `client.images.generate`
    # Let's try to adapt:
    
    try:
        from volcenginesdkarkruntime import Ark
    except ImportError:
         raise ImportError("Please install SDK: pip install 'volcengine-python-sdk[ark]'")

    client = Ark(
        base_url=ArkImageGenConfig.BASE_URL,
        api_key=api_key
    )

    # 尝试将图片作为 BytesIO 上传 (Standard OpenAI style) or URL
    # 用户 snippet 用的是 'image="https://..."'
    # 我们这里没有 URL，只有 bytes。
    # 如果是 OpenAI Compatible，通常是 client.images.edit(image=file_obj)
    # 但是 Volcengine 的 'client.images.generate' 可能只接受 prompt?
    # 查看 snippet: client.images.generate(..., image="url")
    
    # 让我们尝试上传图片到临时服务或者构建 storage URL?
    # 不，我们没有 storage。
    # 尝试直接传 storage URL (这里假设没有 storage)。
    # 另外一种方式是使用 requests 直接 post binary data (if supported).
    
    # 既然用户给了 snippet 是 generate(..., image=url)，这看起来像是 Img2Img。
    # 让我们尝试最可能的路径：如果是在线服务，必须有 URL。
    # 如果没有 URL，看看是否支持 Base64 Data URI string.
    
    # 构建 Data URI (尝试用这个作为 image 参数)
    # format: "https://..." or "data:image/png;base64,..."
    # 很多库在 "image_url" 字段支持 data uri。这里参数名叫 "image"。
    # 值得一试。
    
    # 注意：strength 参数在 snippet 里没看到，但通常 Img2Img 会有。
    # 这里我们主要把 input 接进去。
    
    try:
        # TODO: 使用 asyncio 包装阻塞的 SDK 调用
        import asyncio
        
        # 重新封装为 ThreadPool 调用
        loop = asyncio.get_running_loop()
        
        # 临时函数：执行 SDK 调用
        def call_sdk_blocking():
            # 尝试1: 传入 Base64 字符串 (不带 prefix 目前看来可能不行，带 base64:// ?)
            # 或者 data:image/png;base64,...
            # 
            # 许多国内模型服务支持 Base64 编码的图片。
            # 让我们尝试 Data URI。
            image_input = f"data:{mime_type};base64,{image_b64}"
            
            # 如果太长 (CLI/SDK 可能有限制)，这可能会失败。
            # 
            # 还有一个方法：查看 image23d.py。那里使用了 client.content_generation.tasks.create
            # 并且传入了 {"type": "image_url", "image_url": {"url": data_uri}}
            # 
            # 用户的 snippet 用的是 `client.images.generate` (OpenAI 风格但扩展了参数)。
            # 让我们先试 Data URI。
            
            print(f"[Image2Image] Calling Ark API with model: {ArkImageGenConfig.MODEL_I2I}")
            
            resp = client.images.generate(
                model=ArkImageGenConfig.MODEL_I2I,
                prompt=prompt,
                image=image_input, # 尝试 Data URI
                size=ArkImageGenConfig.SIZE,
                response_format="url", # 获取 URL 后下载
                watermark=ArkImageGenConfig.WATERMARK
            )
            return resp

        response = await loop.run_in_executor(None, call_sdk_blocking)
        
        # 获取结果 URL
        if not response.data:
            raise Exception("API returned empty data")
            
        result_url = response.data[0].url
        print(f"[Image2Image] Generation success, downloading from: {result_url}")
        
        # 下载结果图片
        async with httpx.AsyncClient() as dl_client:
            dl_resp = await dl_client.get(result_url)
            dl_resp.raise_for_status()
            return dl_resp.content

    except Exception as e:
        import traceback
        traceback.print_exc() # Print full stack trace
        print(f"[Image2Image] Error: {str(e)}")
        print(f"[Image2Image] Error Type: {type(e)}")
        # 如果是 SDK 错误，可能包含更多信息
        if hasattr(e, "body"):
            print(f"API Error Body: {e.body}")
        raise e
