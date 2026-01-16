"""
Image to 3D Service
===================
使用腾讯混元生3D API 将图片转换为 3D 模型
"""

import os
import asyncio
import base64
import httpx
from typing import Optional, Dict, Any
from pathlib import Path

from .api_config import TencentHunyuan3DConfig, BaseConfig
from .text23d import submit_task, poll_task, download_model, extract_model_url, load_mock_model, save_model_to_cache


async def generate(image_data: bytes, format: str = "glb") -> bytes:
    """
    将图片转换为 3D 模型
    
    Args:
        image_data: 输入图片的 bytes 数据
        format: 输出格式 ("glb", "obj", "fbx")
    
    Returns:
        3D 模型的 bytes 数据 (GLB 格式)
    """
    # Mock 模式：直接返回本地模型
    if TencentHunyuan3DConfig.MOCK_MODE:
        return await load_mock_model("image_input", format)
    
    api_key = TencentHunyuan3DConfig.API_KEY
    if not api_key:
        raise ValueError("缺少 TENCENT_3D_API_KEY 环境变量")
    
    print(f"[Image23D] Processing image, size: {len(image_data)} bytes")
    
    # 将图片转为 base64
    image_base64 = base64.b64encode(image_data).decode('utf-8')
    
    # 检测图片类型
    if image_data[:8] == b'\x89PNG\r\n\x1a\n':
        mime_type = "image/png"
    elif image_data[:2] == b'\xff\xd8':
        mime_type = "image/jpeg"
    else:
        mime_type = "image/jpeg"  # 默认
    
    # 构建 data URI 格式
    image_url = f"data:{mime_type};base64,{image_base64}"
    
    print(f"[Image23D] Image converted to base64, mime: {mime_type}")
    
    # 1. 提交生成任务
    job_id = await submit_task(image_url=image_url, api_key=api_key)
    print(f"[Image23D] Task submitted, JobId: {job_id}")
    
    # 2. 轮询等待结果
    result = await poll_task(job_id, api_key)
    
    # 3. 从结果中提取模型 URL
    model_url = extract_model_url(result, format)
    if not model_url:
        raise Exception(f"No model URL in response: {result}")
    
    print(f"[Image23D] Downloading model from: {model_url}")
    model_data = await download_model(model_url)
    
    print(f"[Image23D] Model downloaded, size: {len(model_data)} bytes")
    
    # 自动缓存模型
    if TencentHunyuan3DConfig.CACHE_MODELS:
        saved_path = save_model_to_cache(model_data, "image_input", format, source="image23d")
        print(f"[Image23D] Model cached to: {saved_path}")
    
    return model_data


# ========== 同步接口（供直接调用）==========

def generate_sync(image_data: bytes, format: str = "glb") -> bytes:
    """同步版本的生成接口"""
    return asyncio.run(generate(image_data, format))
