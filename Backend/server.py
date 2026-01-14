"""
AI Generation Pipeline - Backend Server
========================================
FastAPI 服务：为 Unity 节点编辑器提供各种 AI 生成 API

端点列表:
- POST /text2image     - 文字生成图片
- POST /image2image    - 图片转换
- POST /image23d       - 图片转 3D
- POST /text23d        - 文字生成 3D
- GET  /health         - 健康检查
"""

from fastapi import FastAPI, Response, File, UploadFile, Form
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Optional
import base64

# 导入服务模块
from services import text2image, image2image, image23d, text23d

app = FastAPI(title="AI Generation Pipeline Server")

# CORS 中间件 - 允许 Unity 跨域请求
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# ========== 请求模型 ==========

class Text2ImageRequest(BaseModel):
    prompt: str
    negative_prompt: str = ""
    width: int = 512
    height: int = 512


class Text23DRequest(BaseModel):
    prompt: str
    format: str = "glb"


# ========== API 端点 ==========

@app.post("/text2image")
async def api_text2image(request: Text2ImageRequest):
    """
    文字生成图片
    
    Request Body:
        prompt: 正向提示词
        negative_prompt: 负向提示词
        width: 图片宽度
        height: 图片高度
    
    Returns:
        PNG 图片数据
    """
    print(f"[Text2Image] Prompt: {request.prompt}")
    
    try:
        image_data = await text2image.generate(
            prompt=request.prompt,
            negative_prompt=request.negative_prompt,
            width=request.width,
            height=request.height
        )
        return Response(content=image_data, media_type="image/png")
    except Exception as e:
        return Response(content=str(e), status_code=500)


@app.post("/image2image")
async def api_image2image(
    image: UploadFile = File(...),
    prompt: str = Form(...),
    strength: float = Form(0.75),
    negative_prompt: str = Form("")
):
    """
    图片转换
    
    Form Data:
        image: 输入图片文件
        prompt: 提示词
        strength: 变化强度 (0.0-1.0)
        negative_prompt: 负向提示词
    
    Returns:
        PNG 图片数据
    """
    print(f"[Image2Image] Prompt: {prompt}, Strength: {strength}")
    
    try:
        image_data = await image.read()
        result = await image2image.generate(
            image_data=image_data,
            prompt=prompt,
            strength=strength,
            negative_prompt=negative_prompt
        )
        return Response(content=result, media_type="image/png")
    except Exception as e:
        return Response(content=str(e), status_code=500)


@app.post("/image23d")
async def api_image23d(
    image: UploadFile = File(...),
    format: str = Form("glb")
):
    """
    图片转 3D 模型
    
    Form Data:
        image: 输入图片文件
        format: 输出格式 (glb/obj/fbx)
    
    Returns:
        GLB 模型数据
    """
    print(f"[Image23D] Processing image, format: {format}")
    
    try:
        image_data = await image.read()
        model_data = await image23d.generate(
            image_data=image_data,
            format=format
        )
        return Response(content=model_data, media_type="model/gltf-binary")
    except Exception as e:
        return Response(content=str(e), status_code=500)


@app.post("/text23d")
async def api_text23d(request: Text23DRequest):
    """
    文字生成 3D 模型
    
    Request Body:
        prompt: 文本提示词
        format: 输出格式 (glb/obj/fbx)
    
    Returns:
        GLB 模型数据
    """
    print(f"[Text23D] Prompt: {request.prompt}")
    
    try:
        model_data = await text23d.generate(
            prompt=request.prompt,
            format=request.format
        )
        return Response(content=model_data, media_type="model/gltf-binary")
    except Exception as e:
        return Response(content=str(e), status_code=500)


# ========== 兼容旧端点 ==========

@app.post("/generate")
async def generate_legacy(request: Text23DRequest):
    """旧版 Text23D 端点 (兼容性)"""
    return await api_text23d(request)


# ========== 工具端点 ==========

@app.get("/health")
async def health():
    """健康检查"""
    return {"status": "ok", "services": ["text2image", "image2image", "image23d", "text23d"]}


@app.get("/")
async def root():
    """根路径 - 服务信息"""
    return {
        "name": "AI Generation Pipeline",
        "version": "1.0.0",
        "endpoints": {
            "POST /text2image": "文字生成图片",
            "POST /image2image": "图片转换",
            "POST /image23d": "图片转 3D",
            "POST /text23d": "文字生成 3D",
            "GET /health": "健康检查"
        }
    }


if __name__ == "__main__":
    import uvicorn
    print("=" * 50)
    print("AI Generation Pipeline Server")
    print("=" * 50)
    print("Endpoints:")
    print("  POST /text2image  - 文字生成图片")
    print("  POST /image2image - 图片转换")
    print("  POST /image23d    - 图片转 3D")
    print("  POST /text23d     - 文字生成 3D")
    print("=" * 50)
    uvicorn.run(app, host="0.0.0.0", port=8000)
