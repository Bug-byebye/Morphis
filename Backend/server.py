"""
Text-to-3D Pipeline - Mock Backend Server
==========================================
FastAPI 服务：接收 Prompt，返回测试用 GLB 文件
"""

from fastapi import FastAPI, Response
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from pathlib import Path

app = FastAPI(title="Text-to-3D Mock Server")

# CORS 中间件 - 允许 Unity 跨域请求
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


class GenerateRequest(BaseModel):
    prompt: str


@app.post("/generate")
async def generate(request: GenerateRequest):
    """
    接收 Prompt，返回测试用 GLB 文件
    实际项目中这里会调用 AI 模型生成 3D 内容
    """
    print(f"[Server] Received prompt: {request.prompt}")
    
    # 读取本地测试 GLB 文件
    glb_path = Path(__file__).parent / "test_cube.glb"
    
    if not glb_path.exists():
        return Response(
            content=f"Error: test_cube.glb not found at {glb_path}",
            status_code=404
        )
    
    glb_data = glb_path.read_bytes()
    print(f"[Server] Returning GLB file: {len(glb_data)} bytes")
    
    return Response(
        content=glb_data,
        media_type="model/gltf-binary"
    )


@app.get("/health")
async def health():
    """健康检查端点"""
    return {"status": "ok"}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
