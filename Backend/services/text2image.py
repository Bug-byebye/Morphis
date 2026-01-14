"""
Text to Image Service
=====================
将文本提示词转换为图片

在这里填入你的 API 代码（如 Stable Diffusion, DALL-E 等）
"""

from typing import Optional
import base64


async def generate(prompt: str, negative_prompt: str = "", width: int = 512, height: int = 512) -> bytes:
    """
    生成图片
    
    Args:
        prompt: 正向提示词
        negative_prompt: 负向提示词
        width: 图片宽度
        height: 图片高度
    
    Returns:
        图片的 bytes 数据 (PNG 格式)
    
    TODO: 在这里实现你的 API 调用
    例如:
    - Stable Diffusion API
    - DALL-E API
    - Replicate API
    - 本地 ComfyUI
    """
    
    # ========== 示例：返回占位图片 ==========
    # 创建一个简单的 1x1 像素 PNG 作为占位符
    # 实际使用时请替换为真实的 API 调用
    
    placeholder_png = create_placeholder_image(width, height, prompt)
    return placeholder_png


def create_placeholder_image(width: int, height: int, text: str = "") -> bytes:
    """
    创建占位图片
    如果你安装了 Pillow，可以生成带文字的占位图
    """
    try:
        from PIL import Image, ImageDraw, ImageFont
        import io
        
        # 创建渐变背景
        img = Image.new('RGB', (width, height), color=(100, 100, 150))
        draw = ImageDraw.Draw(img)
        
        # 添加文字
        message = f"Placeholder\n{text[:30]}..."
        draw.text((width//4, height//2), message, fill=(255, 255, 255))
        
        # 保存为 PNG
        buffer = io.BytesIO()
        img.save(buffer, format='PNG')
        return buffer.getvalue()
        
    except ImportError:
        # 如果没有 Pillow，返回最小 PNG
        return _minimal_png()


def _minimal_png() -> bytes:
    """返回最小有效 PNG (1x1 灰色像素)"""
    return base64.b64decode(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='
    )


# ========== API 实现示例 ==========

# --- Stability AI (Stable Diffusion) ---
# async def generate_stability(prompt: str, api_key: str) -> bytes:
#     import httpx
#     async with httpx.AsyncClient() as client:
#         response = await client.post(
#             "https://api.stability.ai/v1/generation/stable-diffusion-xl-1024-v1-0/text-to-image",
#             headers={"Authorization": f"Bearer {api_key}"},
#             json={"text_prompts": [{"text": prompt}], "cfg_scale": 7, "steps": 30}
#         )
#         return base64.b64decode(response.json()["artifacts"][0]["base64"])

# --- OpenAI DALL-E ---
# async def generate_dalle(prompt: str, api_key: str) -> bytes:
#     import openai
#     openai.api_key = api_key
#     response = await openai.Image.acreate(prompt=prompt, n=1, size="1024x1024")
#     image_url = response["data"][0]["url"]
#     async with httpx.AsyncClient() as client:
#         img_response = await client.get(image_url)
#         return img_response.content

# --- Replicate ---
# async def generate_replicate(prompt: str, api_token: str) -> bytes:
#     import replicate
#     output = replicate.run(
#         "stability-ai/sdxl:...",
#         input={"prompt": prompt}
#     )
#     return download_image(output[0])
