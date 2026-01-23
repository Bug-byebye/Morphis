"""
Image to 3D Service
===================
使用豆包 Seed3D (火山引擎) API 将图片转换为 3D 模型
"""

import os
import asyncio
import base64
import time
import httpx
from typing import Optional, Dict, Any
from pathlib import Path

from .api_config import DoubaoSeed3DConfig, BaseConfig


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
    # Mock 模式：直接返回本地模型
    if DoubaoSeed3DConfig.MOCK_MODE:
        return await load_mock_model("image_input", format)
    
    api_key = os.getenv('ARK_API_KEY')
    if not api_key:
        raise ValueError("缺少 ARK_API_KEY 环境变量")
    
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
    
    try:
        from volcenginesdkarkruntime import Ark
    except ImportError:
        raise ImportError('请安装 SDK: pip install "volcengine-python-sdk[ark]"')
    
    # 初始化 Ark 客户端
    client = Ark(
        base_url=DoubaoSeed3DConfig.BASE_URL,
        api_key=api_key
    )
    
    # 构建请求参数
    subdivision_level = DoubaoSeed3DConfig.SUBDIVISION_LEVEL
    file_format = format or DoubaoSeed3DConfig.FILE_FORMAT
    
    print(f"[Image23D] Creating 3D task: model={DoubaoSeed3DConfig.MODEL_ID}, subdivision={subdivision_level}, format={file_format}")
    
    # 1. 创建3D生成任务
    create_result = client.content_generation.tasks.create(
        model=DoubaoSeed3DConfig.MODEL_ID,
        content=[
            {
                "type": "text",
                "text": f"--subdivisionlevel {subdivision_level} --fileformat {file_format}"
            },
            {
                "type": "image_url",
                "image_url": {
                    "url": image_url
                }
            }
        ]
    )
    
    task_id = create_result.id
    print(f"[Image23D] Task created, id: {task_id}")
    
    # 2. 轮询查询任务状态
    start_time = time.time()
    while True:
        elapsed = time.time() - start_time
        if elapsed > DoubaoSeed3DConfig.MAX_POLL_TIME:
            raise TimeoutError(f"3D生成超时，已等待 {elapsed:.0f} 秒")
        
        get_result = client.content_generation.tasks.get(task_id=task_id)
        status = get_result.status
        
        if status == "succeeded":
            print(f"[Image23D] Task succeeded after {elapsed:.0f}s")
            break
        elif status == "failed":
            error_msg = getattr(get_result, 'error', 'Unknown error')
            raise Exception(f"3D生成失败: {error_msg}")
        else:
            print(f"[Image23D] Status: {status}, waiting {DoubaoSeed3DConfig.POLL_INTERVAL}s... ({elapsed:.0f}s elapsed)")
            await asyncio.sleep(DoubaoSeed3DConfig.POLL_INTERVAL)
    
    # 3. 从结果中提取模型 URL 并下载
    model_url = extract_model_url(get_result, file_format)
    if not model_url:
        raise Exception(f"No model URL in response: {get_result}")
    
    print(f"[Image23D] Downloading model from: {model_url}")
    model_data = await download_model(model_url)
    
    print(f"[Image23D] Model downloaded, size: {len(model_data)} bytes")
    
    # 自动缓存模型
    if DoubaoSeed3DConfig.CACHE_MODELS:
        saved_path = save_model_to_cache(model_data, "image_input", file_format, source="image23d")
        print(f"[Image23D] Model cached to: {saved_path}")
    
    return model_data


def extract_model_url(result, format: str = "glb") -> Optional[str]:
    """从任务结果中提取模型 URL"""
    # 尝试从 content 获取
    if hasattr(result, 'content') and result.content:
        for item in result.content:
            if hasattr(item, 'type') and item.type == 'file_url':
                if hasattr(item, 'file_url') and hasattr(item.file_url, 'url'):
                    return item.file_url.url
            # 也检查直接的 url 字段
            if hasattr(item, 'url'):
                return item.url
    
    # 尝试从 data 获取
    if hasattr(result, 'data') and result.data:
        data = result.data
        if isinstance(data, dict):
            # 检查各种可能的键名
            for key in ['model_url', 'modelUrl', 'url', 'download_url', 'file_url']:
                if key in data:
                    return data[key]
            # 检查嵌套的 urls
            if 'urls' in data:
                urls = data['urls']
                if isinstance(urls, dict):
                    return urls.get(format) or urls.get('glb') or next(iter(urls.values()), None)
                elif isinstance(urls, list) and urls:
                    return urls[0]
    
    return None


async def download_model(url: str) -> bytes:
    """下载模型文件"""
    async with httpx.AsyncClient(timeout=120.0) as client:
        response = await client.get(url)
        response.raise_for_status()
        return response.content


async def load_mock_model(source: str, format: str) -> bytes:
    """加载 Mock 模型（用于测试）"""
    mock_path = DoubaoSeed3DConfig.DEFAULT_MOCK_MODEL
    
    if mock_path.exists():
        print(f"[Image23D] Mock mode: loading from {mock_path}")
        return mock_path.read_bytes()
    
    # 如果默认 mock 模型不存在，尝试从缓存目录加载
    cache_dir = DoubaoSeed3DConfig.CACHE_DIR
    if cache_dir.exists():
        for f in cache_dir.glob(f"*.{format}"):
            print(f"[Image23D] Mock mode: loading from cache {f}")
            return f.read_bytes()
    
    raise FileNotFoundError(f"Mock模式需要模型文件，请将 .glb 文件放在 {mock_path} 或 {cache_dir}")


def save_model_to_cache(model_data: bytes, prompt: str, format: str, source: str = "image23d") -> Path:
    """保存模型到缓存目录"""
    DoubaoSeed3DConfig.ensure_cache_dir()
    
    # 生成文件名
    import hashlib
    hash_suffix = hashlib.md5(prompt.encode()).hexdigest()[:8]
    timestamp = int(time.time())
    filename = f"{source}_{timestamp}_{hash_suffix}.{format}"
    
    filepath = DoubaoSeed3DConfig.CACHE_DIR / filename
    filepath.write_bytes(model_data)
    
    return filepath


# ========== 同步接口（供直接调用）==========

def generate_sync(image_data: bytes, format: str = "glb") -> bytes:
    """同步版本的生成接口"""
    return asyncio.run(generate(image_data, format))
