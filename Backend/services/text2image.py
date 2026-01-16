"""
Text to Image Service
=====================
使用 ARK OpenAI 兼容 API 将文本提示词转换为图片
"""

import os
import uuid
import base64
import httpx
from typing import Optional, Dict, Any, List
from urllib.parse import urlparse, parse_qs, urlencode, urlunparse
from openai import OpenAI

from .api_config import BaseConfig, ArkImageGenConfig


# 任务缓存 - 用于异步任务状态查询
TASK_CACHE: Dict[str, Dict[str, Any]] = {}


def remove_watermark_from_url(url: str) -> str:
    """
    尝试从 TOS URL 中移除水印参数
    火山引擎在 URL 中通过 x-tos-process 参数添加水印
    """
    try:
        parsed = urlparse(url)
        query_params = parse_qs(parsed.query)
        
        # 移除水印相关参数
        if 'x-tos-process' in query_params:
            del query_params['x-tos-process']
        
        # 重建查询字符串（parse_qs 返回的是 list，需要取第一个值）
        new_query = urlencode({k: v[0] for k, v in query_params.items()})
        
        # 重建 URL
        new_url = urlunparse((
            parsed.scheme,
            parsed.netloc,
            parsed.path,
            parsed.params,
            new_query,
            parsed.fragment
        ))
        
        print(f"[TEXT2IMG] Removed watermark param from URL")
        return new_url
    except Exception as e:
        print(f"[TEXT2IMG] Failed to remove watermark: {e}")
        return url


class TextToImageService:
    """文生图服务类"""
    
    @staticmethod
    def generate(prompt: str, **kwargs) -> Dict[str, Any]:
        """
        同步生成图片（返回图片 URL）
        使用官方 volcengine SDK
        
        Args:
            prompt: 提示词
            **kwargs: 额外参数
                - model_id: 模型 ID
                - size: 图片尺寸
        
        Returns:
            包含状态和图片 URL 的字典
        """
        BaseConfig.ensure_output_dir()
        api_key = os.getenv('ARK_API_KEY')
        if not api_key:
            return {'status': 'error', 'error': '缺少 ARK_API_KEY 环境变量'}

        try:
            from volcenginesdkarkruntime import Ark
            
            client = Ark(
                base_url=ArkImageGenConfig.BASE_URL,
                api_key=api_key
            )
        except ImportError:
            return {'status': 'error', 'error': '请安装 SDK: pip install "volcengine-python-sdk[ark]"'}
        
        model_id = kwargs.get('model_id') or ArkImageGenConfig.MODEL_T2I
        size = kwargs.get('size') or ArkImageGenConfig.SIZE
        local_task_id = str(uuid.uuid4())[:8]
        
        try:
            print(f"[TEXT2IMG] request model={model_id} size={size} prompt_len={len(prompt or '')} watermark=False")
            
            resp = client.images.generate(
                prompt=prompt,
                model=model_id,
                response_format="url",
                size=size,
                watermark=False
            )
            
            urls: List[str] = []
            data = getattr(resp, 'data', [])
            for item in data:
                u = getattr(item, 'url', None) or (item.get('url') if isinstance(item, dict) else None)
                if u:
                    urls.append(u)
            
            print(f"[TEXT2IMG] response urls={urls}")
            task_id = str(uuid.uuid4())[:8]
            TASK_CACHE[task_id] = {'status': 'completed', 'imageUrls': urls}
            
            return {
                'status': 'success', 
                'task_id': task_id, 
                'local_task_id': local_task_id, 
                'message': '已完成',
                'imageUrls': urls,
                'metadata': {
                    'prompt': prompt, 
                    'type': 'text_to_image', 
                    'api_used': 'ark_openai', 
                    'size': size
                }
            }
        except Exception as exc:
            print(f"[TEXT2IMG] error: {exc}")
            return {'status': 'error', 'error': str(exc)}

    @staticmethod
    def get_task_status(task_id: str, api_key: str = None, openai_client=None) -> Dict[str, Any]:
        """查询任务状态"""
        if task_id in TASK_CACHE:
            return {
                'status': 'success', 
                'data': TASK_CACHE[task_id], 
                'metadata': {'task_id': task_id, 'api_used': 'ark_openai', 'action': 'get_task_status'}
            }
        return {
            'status': 'success', 
            'data': {'status': 'running', 'imageUrls': []}, 
            'metadata': {'task_id': task_id}
        }


# ========== FastAPI 兼容的异步接口 ==========

async def generate(prompt: str, negative_prompt: str = "", width: int = 512, height: int = 512) -> bytes:
    """
    异步生成图片（FastAPI 端点使用）
    
    Args:
        prompt: 正向提示词
        negative_prompt: 负向提示词 (ARK API 可能不支持，保留兼容性)
        width: 图片宽度
        height: 图片高度
    
    Returns:
        图片的 bytes 数据 (PNG 格式)
    """
    # 根据宽高设置 size 参数
    size = f"{width}x{height}"
    
    # 调用同步服务
    result = TextToImageService.generate(prompt, size=size)
    
    if result['status'] == 'error':
        raise Exception(result.get('error', 'Unknown error'))
    
    # 获取图片 URL 并下载
    urls = result.get('imageUrls', [])
    if not urls:
        raise Exception("No image URLs returned")
    
    # 下载第一张图片
    async with httpx.AsyncClient() as client:
        response = await client.get(urls[0], timeout=60.0)
        response.raise_for_status()
        return response.content


async def generate_with_urls(prompt: str, **kwargs) -> Dict[str, Any]:
    """
    异步生成图片，返回 URL 列表（不下载）
    
    Args:
        prompt: 提示词
        **kwargs: 传递给 TextToImageService.generate 的额外参数
    
    Returns:
        包含图片 URL 的响应字典
    """
    return TextToImageService.generate(prompt, **kwargs)


# ========== 占位图片（备用） ==========

def create_placeholder_image(width: int, height: int, text: str = "") -> bytes:
    """创建占位图片（当 API 不可用时使用）"""
    try:
        from PIL import Image, ImageDraw
        import io
        
        img = Image.new('RGB', (width, height), color=(100, 100, 150))
        draw = ImageDraw.Draw(img)
        message = f"Placeholder\n{text[:30]}..."
        draw.text((width//4, height//2), message, fill=(255, 255, 255))
        
        buffer = io.BytesIO()
        img.save(buffer, format='PNG')
        return buffer.getvalue()
        
    except ImportError:
        return _minimal_png()


def _minimal_png() -> bytes:
    """返回最小有效 PNG (1x1 灰色像素)"""
    return base64.b64decode(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='
    )
#     import replicate
#     output = replicate.run(
#         "stability-ai/sdxl:...",
#         input={"prompt": prompt}
#     )
#     return download_image(output[0])
