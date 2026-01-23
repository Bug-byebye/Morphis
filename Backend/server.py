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

from dotenv import load_dotenv
load_dotenv()  # 加载 .env 文件

from fastapi import FastAPI, Response, File, UploadFile, Form, Header
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Optional, Dict, List
import base64
import secrets

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

class AuthLoginRequest(BaseModel):
    username: str
    password: str

class AuthRegisterRequest(BaseModel):
    username: str
    password: str

class AuthResponse(BaseModel):
    token: str
    username: str

class WorkspaceDto(BaseModel):
    id: str
    name: str
    members: List[str]

class WorkspaceListResponse(BaseModel):
    items: List[WorkspaceDto]


# ========== 简易内存存储（占位实现） ==========

# 默认账号（你要求的 111111/111111）
_users: Dict[str, str] = {
    "111111": "111111",
    "test": "test",
    "demo": "demo",
}

# token -> username
_tokens: Dict[str, str] = {}

# username -> workspaces（先做伪数据；后续可接数据库）
_workspaces_by_user: Dict[str, List[WorkspaceDto]] = {
    "111111": [
        WorkspaceDto(id="ws-love-001", name="Couple Space 001", members=["111111", "partner"]),
        WorkspaceDto(id="ws-cozy-002", name="Cozy Home 002", members=["111111"]),
    ],
    "test": [
        WorkspaceDto(id="ws-test-001", name="Test Workspace", members=["test"]),
    ],
    "demo": [
        WorkspaceDto(id="ws-demo-001", name="Demo Space", members=["demo", "partner"]),
    ],
}

def _require_user(authorization: Optional[str]) -> str:
    """
    解析 Authorization: Bearer <token> 并返回 username
    """
    if not authorization:
        raise ValueError("Missing Authorization header")
    if not authorization.startswith("Bearer "):
        raise ValueError("Invalid Authorization scheme")
    token = authorization[len("Bearer "):].strip()
    username = _tokens.get(token)
    if not username:
        raise ValueError("Invalid token")
    return username


# ========== API 端点 ==========

@app.post("/auth/login", response_model=AuthResponse)
async def auth_login(request: AuthLoginRequest):
    """
    登录（占位实现，后续替换为真实鉴权/数据库）
    """
    pwd = _users.get(request.username)
    if pwd is None or pwd != request.password:
        return Response(content="Invalid username or password", status_code=401)
    token = secrets.token_urlsafe(24)
    _tokens[token] = request.username
    return AuthResponse(token=token, username=request.username)


@app.post("/auth/register", response_model=AuthResponse)
async def auth_register(request: AuthRegisterRequest):
    """
    注册（占位实现）
    """
    if request.username in _users:
        return Response(content="Username already exists", status_code=409)
    if not request.username or not request.password:
        return Response(content="Username/password required", status_code=400)
    _users[request.username] = request.password
    # 给新用户一个默认空间（也可以为空）
    _workspaces_by_user.setdefault(
        request.username,
        [WorkspaceDto(id=f"ws-{request.username}-001", name="我的第一个空间", members=[request.username])]
    )
    token = secrets.token_urlsafe(24)
    _tokens[token] = request.username
    return AuthResponse(token=token, username=request.username)


@app.get("/workspaces", response_model=WorkspaceListResponse)
async def list_workspaces(authorization: Optional[str] = Header(default=None)):
    """
    获取当前账号的 workspace 列表（占位实现）
    Header:
      Authorization: Bearer <token>
    """
    try:
        username = _require_user(authorization)
    except ValueError as e:
        return Response(content=str(e), status_code=401)
    return WorkspaceListResponse(items=_workspaces_by_user.get(username, []))


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


@app.post("/text2image/urls")
async def api_text2image_urls(request: Text2ImageRequest):
    """
    文字生成图片 - 返回 URL 列表
    
    Request Body:
        prompt: 正向提示词
        negative_prompt: 负向提示词 (可选)
        width: 图片宽度
        height: 图片高度
    
    Returns:
        JSON 响应，包含图片 URL 列表
    """
    print(f"[Text2Image/URLs] Prompt: {request.prompt}")
    
    try:
        size = f"{request.width}x{request.height}"
        result = await text2image.generate_with_urls(
            prompt=request.prompt,
            size=size
        )
        return result
    except Exception as e:
        return {"status": "error", "error": str(e)}


@app.get("/text2image/task/{task_id}")
async def api_text2image_task_status(task_id: str):
    """
    查询文生图任务状态
    
    Args:
        task_id: 任务 ID
    
    Returns:
        任务状态和图片 URL
    """
    from services.text2image import TextToImageService
    return TextToImageService.get_task_status(task_id)


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
        import traceback
        traceback.print_exc()
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
            "POST /auth/login": "登录（占位实现）",
            "POST /auth/register": "注册（占位实现）",
            "GET /workspaces": "获取 workspace 列表（占位实现，Bearer token）",
            "POST /text2image": "文字生成图片 (返回 PNG)",
            "POST /text2image/urls": "文字生成图片 (返回 URL 列表)",
            "GET /text2image/task/{task_id}": "查询文生图任务状态",
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
    print("  POST /text2image       - 文字生成图片 (返回 PNG)")
    print("  POST /text2image/urls  - 文字生成图片 (返回 URL)")
    print("  GET  /text2image/task  - 查询任务状态")
    print("  POST /image2image      - 图片转换")
    print("  POST /image23d         - 图片转 3D")
    print("  POST /text23d          - 文字生成 3D")
    print("=" * 50)
    uvicorn.run(app, host="0.0.0.0", port=8000)
