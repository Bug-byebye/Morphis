"""
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

from fastapi import FastAPI, Response, File, UploadFile, Form, Header, Depends
from fastapi.middleware.cors import CORSMiddleware
from sqlalchemy.orm import Session
from pydantic import BaseModel
from typing import Optional, Dict, List
import base64
import secrets

# 导入服务模块
from services import text2image, image2image, image23d, text23d
from services.dog_chat import ChatRequest, ChatResponse, chat_with_dog, clear_conversation
from services.human_chat import (
    HumanChatRequest,
    HumanChatResponse,
    chat_with_companion,
    clear_human_conversation,
)

# 导入世界快照路由和数据库初始化
from routers import world
from database import init_db, get_db
from crud import (
    get_user_by_username,
    create_user,
    get_workspaces_for_user,
    get_or_create_world,
    create_workspace_with_coowners,
    are_friends,
    get_friendship_rows_for_user,
    get_incoming_friend_request_rows,
    get_outgoing_friend_request_rows,
    get_pending_friend_request_between,
    get_pending_friend_request_for_receiver,
    create_friend_request,
    accept_friend_request,
    decline_friend_request,
)
from models.world import WorldMember

app = FastAPI(title="AI Generation Pipeline Server")


@app.on_event("startup")
def startup_db():
    """启动时连接数据库并建表；连接失败则抛错并阻止服务启动（不静默失败）"""
    init_db()


# 注册世界快照路由
app.include_router(world.router)

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


class CreateWorkspaceRequest(BaseModel):
    name: Optional[str] = None
    co_owner_usernames: Optional[List[str]] = None


class CreateWorkspaceResponse(BaseModel):
    id: str
    name: str


# ========== 会话：token -> username（内存，重启后需重新登录） ==========

_tokens: Dict[str, str] = {}


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
async def auth_login(request: AuthLoginRequest, db: Session = Depends(get_db)):
    """
    登录（数据库校验：按用户名查 User，密码明文匹配）
    """
    user = get_user_by_username(db, request.username)
    if user is None or user.password != request.password:
        return Response(content="Invalid username or password", status_code=401)
    token = secrets.token_urlsafe(24)
    _tokens[token] = request.username
    return AuthResponse(token=token, username=request.username)


@app.post("/auth/register", response_model=AuthResponse)
async def auth_register(request: AuthRegisterRequest, db: Session = Depends(get_db)):
    """
    注册（写入 User 表；可选创建默认 World 并加入 WorldMember）
    """
    if not request.username or not request.password:
        return Response(content="Username/password required", status_code=400)
    if get_user_by_username(db, request.username) is not None:
        return Response(content="Username already exists", status_code=409)
    user = create_user(db, username=request.username, password=request.password, email=None)
    # 给新用户一个默认世界并设为 owner + member
    default_world_id = f"ws-{request.username}-001"
    get_or_create_world(db, world_id=default_world_id, name="My First Space", owner_user_id=user.id)
    member = WorldMember(world_id=default_world_id, user_id=user.id)
    db.add(member)
    db.commit()
    token = secrets.token_urlsafe(24)
    _tokens[token] = request.username
    return AuthResponse(token=token, username=request.username)


@app.post("/workspaces/create", response_model=CreateWorkspaceResponse)
async def create_workspace(
    request: CreateWorkspaceRequest,
    authorization: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    """
    Create a new workspace (space). Current user becomes owner.
    Optionally add co-owners by their usernames.
    """
    try:
        username = _require_user(authorization)
    except ValueError as e:
        return Response(content=str(e), status_code=401)
    user = get_user_by_username(db, username)
    if user is None:
        return Response(content="User not found", status_code=401)

    name = (request.name or "").strip() or "My Space"
    co_usernames = [u.strip() for u in (request.co_owner_usernames or []) if u and u.strip()]
    try:
        world = create_workspace_with_coowners(
            db=db,
            owner_user_id=user.id,
            name=name,
            co_owner_usernames=co_usernames,
        )
        return CreateWorkspaceResponse(id=world.id, name=world.name)
    except Exception as e:
        return Response(content=str(e), status_code=400)


@app.get("/workspaces", response_model=WorkspaceListResponse)
async def list_workspaces(
    authorization: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    """
    获取当前账号的 workspace 列表（从 World + WorldMember 查）
    Header: Authorization: Bearer <token>
    """
    try:
        username = _require_user(authorization)
    except ValueError as e:
        return Response(content=str(e), status_code=401)
    user = get_user_by_username(db, username)
    if user is None:
        return Response(content="User not found", status_code=401)
    rows = get_workspaces_for_user(db, user.id)
    items = [WorkspaceDto(id=r["id"], name=r["name"], members=r["members"]) for r in rows]
    return WorkspaceListResponse(items=items)


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

@app.post("/chat", response_model=ChatResponse)
async def api_dog_chat(request: ChatRequest):
    """
    Dog companion chat endpoint.
    
    Request Body:
        message: User's message
        session_id: Optional session ID for conversation history
        dog_name: Optional dog name (default: Buddy)
    
    Returns:
        Dog's response
    """
    import asyncio
    print(f"[DogChat] Message: {request.message[:50]}...")
    
    try:
        # Run blocking LLM call in a thread pool to avoid freezing the event loop
        loop = asyncio.get_event_loop()
        response = await asyncio.wait_for(
            loop.run_in_executor(
                None,
                chat_with_dog,
                request.message,
                request.session_id or "default",
                request.dog_name or "Buddy"
            ),
            timeout=20.0  # 20 second overall timeout
        )
        print(f"[DogChat] Response: {response[:50]}...")
        return ChatResponse(
            response=response,
            session_id=request.session_id or "default"
        )
    except asyncio.TimeoutError:
        print("[DogChat] Error: LLM request timed out")
        return ChatResponse(
            response="*yawns* Woof... I got distracted by a squirrel! Can you say that again?",
            session_id=request.session_id or "default"
        )
    except Exception as e:
        print(f"[DogChat] Error: {e}")
        return ChatResponse(
            response="*whimpers* Woof... something went wrong...",
            session_id=request.session_id or "default"
        )


@app.post("/chat/clear")
async def api_clear_chat(session_id: str = "default"):
    """
    Clear conversation history for a session.
    """
    clear_conversation(session_id)
    return {"status": "ok", "message": "Conversation cleared"}


@app.post("/human-chat", response_model=HumanChatResponse)
async def api_human_chat(request: HumanChatRequest):
    """
    Human companion chat endpoint.

    Request Body:
        message: User's message
        session_id: Optional session ID for conversation history
        companion_name: Optional companion name

    Returns:
        Companion's response
    """
    print(f"[HumanChat] Message: {request.message[:50]}...")

    try:
        response = chat_with_companion(
            message=request.message,
            session_id=request.session_id or "default",
            companion_name=request.companion_name or "伴侣"
        )
        return HumanChatResponse(
            response=response,
            session_id=request.session_id or "default"
        )
    except Exception as e:
        print(f"[HumanChat] Error: {e}")
        return HumanChatResponse(
            response="我在这里，只是刚刚有点走神了。你再和我说一次，好吗？",
            session_id=request.session_id or "default"
        )


@app.post("/human-chat/clear")
async def api_clear_human_chat(session_id: str = "default"):
    """
    Clear human chat conversation history for a session.
    """
    clear_human_conversation(session_id)
    return {"status": "ok", "message": "Human conversation cleared"}


@app.get("/health")
async def health():
    """健康检查"""
    return {
        "status": "ok",
        "services": [
            "text2image",
            "image2image",
            "image23d",
            "text23d",
            "dog_chat",
            "human_chat",
        ],
    }


@app.get("/")
async def root():
    """根路径 - 服务信息"""
    return {
        "name": "AI Generation Pipeline",
        "version": "1.0.0",
        "endpoints": {
            "POST /auth/login": "登录（数据库校验）",
            "POST /auth/register": "注册（写入 User + 默认 World）",
            "GET /workspaces": "List workspaces (World + WorldMember)",
            "POST /workspaces/create": "Create workspace with optional co-owners",
            "POST /workspaces/join": "Join workspace and get server address",
            "POST /world/{world_id}": "创建或更新世界快照",
            "GET /world/{world_id}": "获取世界快照",
            "POST /chat": "狗狗伙伴聊天",
            "POST /human-chat": "玩家伴侣聊天",
            "POST /text2image": "文字生成图片 (返回 PNG)",
            "POST /text2image/urls": "文字生成图片 (返回 URL 列表)",
            "GET /text2image/task/{task_id}": "查询文生图任务状态",
            "POST /image2image": "图片转换",
            "POST /image23d": "图片转 3D",
            "POST /text23d": "文字生成 3D",
            "GET /health": "健康检查"
        }
    }


if False and __name__ == "__main__":
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
"""
AI Generation Pipeline - Backend Server
FastAPI 服务：为 Unity 节点编辑器提供各种 AI 生成 API

端点列表:
- POST /text2image     - 文字生成图片
- POST /image2image    - 图片转换
- POST /image23d       - 图片转 3D
- POST /text23d        - 文字生成 3D
- GET  /health         - 健康检查
"""

from fastapi import FastAPI, Response, File, UploadFile, Form, Header, Depends
from fastapi.middleware.cors import CORSMiddleware
from sqlalchemy.orm import Session
from pydantic import BaseModel
from typing import Optional, Dict, List
import base64
import secrets
from urllib.parse import urlparse

# 导入服务模块
from services import text2image, image2image, image23d, text23d
from services.dog_chat import ChatRequest, ChatResponse, chat_with_dog, clear_conversation
from services.human_chat import (
    HumanChatRequest,
    HumanChatResponse,
    chat_with_companion,
    clear_human_conversation,
)

# 导入世界快照路由和数据库初始化
from routers import world, world_manager
from database import init_db, get_db
from config import get_api_base_url, get_server_listen_address, get_server_port
from crud import (
    get_user_by_username,
    create_user,
    get_workspaces_for_user,
    get_or_create_world,
    create_workspace_with_coowners,
    are_friends,
    get_friendship_rows_for_user,
    get_incoming_friend_request_rows,
    get_outgoing_friend_request_rows,
    get_pending_friend_request_between,
    get_pending_friend_request_for_receiver,
    create_friend_request,
    accept_friend_request,
    decline_friend_request,
)
from models.world import WorldMember

app = FastAPI(title="AI Generation Pipeline Server")


@app.on_event("startup")
def startup_db():
    """启动时连接数据库并建表；连接失败则抛错并阻止服务启动（不静默失败）"""
    init_db()


# 注册世界快照路由
app.include_router(world.router)
# 注册世界进程管理路由
app.include_router(world_manager.router)

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
    status: str = "stopped"  # World 运行状态
    port: Optional[int] = None  # 连接端口
    player_count: int = 0  # 当前玩家数

class WorkspaceListResponse(BaseModel):
    items: List[WorkspaceDto]


class CreateWorkspaceRequest(BaseModel):
    name: Optional[str] = None
    co_owner_usernames: Optional[List[str]] = None


class CreateWorkspaceResponse(BaseModel):
    id: str
    name: str


class JoinWorldRequest(BaseModel):
    world_id: str


class JoinWorldResponse(BaseModel):
    status: str
    world_id: str
    server_address: str
    server_port: int
    message: str = ""


class FriendDto(BaseModel):
    username: str


class FriendRequestDto(BaseModel):
    id: int
    sender_username: str
    receiver_username: str
    status: str
    created_at: Optional[str] = None


class FriendsStateResponse(BaseModel):
    friends: List[FriendDto]
    incoming_requests: List[FriendRequestDto]
    outgoing_requests: List[FriendRequestDto]


class SendFriendRequestPayload(BaseModel):
    target_username: str


class FriendActionResponse(BaseModel):
    status: str
    message: str


# ========== 会话：token -> username（内存，重启后需重新登录） ==========

_tokens: Dict[str, str] = {}


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
async def auth_login(request: AuthLoginRequest, db: Session = Depends(get_db)):
    """
    登录（数据库校验：按用户名查 User，密码明文匹配）
    """
    user = get_user_by_username(db, request.username)
    if user is None or user.password != request.password:
        return Response(content="Invalid username or password", status_code=401)
    token = secrets.token_urlsafe(24)
    _tokens[token] = request.username
    return AuthResponse(token=token, username=request.username)


@app.post("/auth/register", response_model=AuthResponse)
async def auth_register(request: AuthRegisterRequest, db: Session = Depends(get_db)):
    """
    注册（写入 User 表；可选创建默认 World 并加入 WorldMember）
    """
    if not request.username or not request.password:
        return Response(content="Username/password required", status_code=400)
    if get_user_by_username(db, request.username) is not None:
        return Response(content="Username already exists", status_code=409)
    user = create_user(db, username=request.username, password=request.password, email=None)
    # 给新用户一个默认世界并设为 owner + member
    default_world_id = f"ws-{request.username}-001"
    get_or_create_world(db, world_id=default_world_id, name="My First Space", owner_user_id=user.id)
    member = WorldMember(world_id=default_world_id, user_id=user.id)
    db.add(member)
    db.commit()
    token = secrets.token_urlsafe(24)
    _tokens[token] = request.username
    return AuthResponse(token=token, username=request.username)


@app.post("/workspaces/create", response_model=CreateWorkspaceResponse)
async def create_workspace(
    request: CreateWorkspaceRequest,
    authorization: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    """
    Create a new workspace (space). Current user becomes owner.
    Optionally add co-owners by their usernames.
    """
    try:
        username = _require_user(authorization)
    except ValueError as e:
        return Response(content=str(e), status_code=401)
    user = get_user_by_username(db, username)
    if user is None:
        return Response(content="User not found", status_code=401)

    name = (request.name or "").strip() or "My Space"
    co_usernames = [u.strip() for u in (request.co_owner_usernames or []) if u and u.strip()]
    try:
        world = create_workspace_with_coowners(
            db=db,
            owner_user_id=user.id,
            name=name,
            co_owner_usernames=co_usernames,
        )
        return CreateWorkspaceResponse(id=world.id, name=world.name)
    except Exception as e:
        import traceback
        error_detail = f"{str(e)}\n{traceback.format_exc()}"
        print(f"[CreateWorkspace] Error: {error_detail}")
        return Response(content=str(e), status_code=400)


@app.post("/workspaces/join", response_model=JoinWorldResponse)
async def join_world(
    request: JoinWorldRequest,
    authorization: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    """
    请求加入 World
    
    流程：
    1. 验证用户权限（是否为 World 成员）
    2. 检查 World 是否运行
    3. 未运行则启动 World
    4. 返回连接信息（IP + Port）
    """
    try:
        username = _require_user(authorization)
    except ValueError as e:
        return Response(content=str(e), status_code=401)
    
    user = get_user_by_username(db, username)
    if user is None:
        return Response(content="User not found", status_code=401)
    
    # 验证用户是否有权限访问该 World
    from models.world import World, WorldMember
    world = db.query(World).filter(World.id == request.world_id).first()
    if not world:
        return Response(content="World not found", status_code=404)
    
    # 检查是否为 owner 或 member
    is_owner = world.owner_user_id == user.id
    is_member = db.query(WorldMember).filter(
        WorldMember.world_id == request.world_id,
        WorldMember.user_id == user.id
    ).first() is not None
    
    if not (is_owner or is_member):
        return Response(content="Access denied", status_code=403)
    
    # 启动或获取 World
    from services.world_manager import get_world_manager
    manager = get_world_manager()
    result = manager.start_world(db, request.world_id)
    
    if result["status"] == "error":
        return Response(content=result["message"], status_code=500)
    
    # 获取服务器地址（从 deploy/server-config.json 的 ApiBaseUrl 解析）
    parsed = urlparse(get_api_base_url())
    server_ip = parsed.hostname or "127.0.0.1"
    
    return JoinWorldResponse(
        status="ok",
        world_id=request.world_id,
        server_address=server_ip,
        server_port=result["port"],
        message=f"World ready on {server_ip}:{result['port']}"
    )


@app.get("/workspaces", response_model=WorkspaceListResponse)
async def list_workspaces(
    authorization: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    """
    获取当前账号的 workspace 列表（从 World + WorldMember 查）
    Header: Authorization: Bearer <token>
    返回包含 World 运行状态和连接信息
    """
    try:
        username = _require_user(authorization)
    except ValueError as e:
        return Response(content=str(e), status_code=401)
    user = get_user_by_username(db, username)
    if user is None:
        return Response(content="User not found", status_code=401)
    rows = get_workspaces_for_user(db, user.id)
    
    # 获取每个 World 的运行状态
    from models.world import World
    items = []
    for r in rows:
        world = db.query(World).filter(World.id == r["id"]).first()
        items.append(WorkspaceDto(
            id=r["id"],
            name=r["name"],
            members=r["members"],
            status=world.status.value if world else "stopped",
            port=world.port if world else None,
            player_count=world.player_count if world else 0
        ))
    
    return WorkspaceListResponse(items=items)


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

@app.post("/chat", response_model=ChatResponse)
async def api_dog_chat(request: ChatRequest):
    """
    Dog companion chat endpoint.
    
    Request Body:
        message: User's message
        session_id: Optional session ID for conversation history
        dog_name: Optional dog name (default: Buddy)
    
    Returns:
        Dog's response
    """
    print(f"[DogChat] Message: {request.message[:50]}...")
    
    try:
        response = chat_with_dog(
            message=request.message,
            session_id=request.session_id or "default",
            dog_name=request.dog_name or "Buddy"
        )
        return ChatResponse(
            response=response,
            session_id=request.session_id or "default"
        )
    except Exception as e:
        print(f"[DogChat] Error: {e}")
        return ChatResponse(
            response="*whimpers* Woof... something went wrong...",
            session_id=request.session_id or "default"
        )


@app.post("/chat/clear")
async def api_clear_chat(session_id: str = "default"):
    """
    Clear conversation history for a session.
    """
    clear_conversation(session_id)
    return {"status": "ok", "message": "Conversation cleared"}


@app.post("/human-chat", response_model=HumanChatResponse)
async def api_human_chat(request: HumanChatRequest):
    """
    Human companion chat endpoint.

    Request Body:
        message: User's message
        session_id: Optional session ID for conversation history
        companion_name: Optional companion name

    Returns:
        Companion's response
    """
    print(f"[HumanChat] Message: {request.message[:50]}...")

    try:
        response = chat_with_companion(
            message=request.message,
            session_id=request.session_id or "default",
            companion_name=request.companion_name or "伴侣"
        )
        return HumanChatResponse(
            response=response,
            session_id=request.session_id or "default"
        )
    except Exception as e:
        print(f"[HumanChat] Error: {e}")
        return HumanChatResponse(
            response="我在这里，只是刚刚有点走神了。你再和我说一次，好吗？",
            session_id=request.session_id or "default"
        )


@app.post("/human-chat/clear")
async def api_clear_human_chat(session_id: str = "default"):
    """
    Clear human chat conversation history for a session.
    """
    clear_human_conversation(session_id)
    return {"status": "ok", "message": "Human conversation cleared"}


@app.get("/friends", response_model=FriendsStateResponse)
async def list_friends(
    authorization: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    try:
        username = _require_user(authorization)
    except ValueError as e:
        return Response(content=str(e), status_code=401)

    user = get_user_by_username(db, username)
    if user is None:
        return Response(content="User not found", status_code=401)

    friend_rows = get_friendship_rows_for_user(db, user.id)
    incoming_rows = get_incoming_friend_request_rows(db, user.id)
    outgoing_rows = get_outgoing_friend_request_rows(db, user.id)

    return FriendsStateResponse(
        friends=[
            FriendDto(username=friend_user.username)
            for _, friend_user in friend_rows
        ],
        incoming_requests=[
            FriendRequestDto(
                id=friend_request.id,
                sender_username=sender_user.username,
                receiver_username=username,
                status=friend_request.status.value,
                created_at=friend_request.created_at.isoformat() if friend_request.created_at else None,
            )
            for friend_request, sender_user in incoming_rows
        ],
        outgoing_requests=[
            FriendRequestDto(
                id=friend_request.id,
                sender_username=username,
                receiver_username=receiver_user.username,
                status=friend_request.status.value,
                created_at=friend_request.created_at.isoformat() if friend_request.created_at else None,
            )
            for friend_request, receiver_user in outgoing_rows
        ],
    )


@app.post("/friends/requests", response_model=FriendActionResponse)
async def send_friend_request(
    request: SendFriendRequestPayload,
    authorization: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    try:
        username = _require_user(authorization)
    except ValueError as e:
        return Response(content=str(e), status_code=401)

    sender = get_user_by_username(db, username)
    if sender is None:
        return Response(content="User not found", status_code=401)

    target_username = (request.target_username or "").strip()
    if not target_username:
        return Response(content="target_username required", status_code=400)
    if target_username == username:
        return Response(content="Cannot add yourself as a friend", status_code=400)

    receiver = get_user_by_username(db, target_username)
    if receiver is None:
        return Response(content="Target user not found", status_code=404)

    if are_friends(db, sender.id, receiver.id):
        return Response(content="You are already friends", status_code=409)

    existing_pending = get_pending_friend_request_between(db, sender.id, receiver.id)
    if existing_pending is not None:
        if existing_pending.sender_user_id == receiver.id:
            return Response(content="This user already sent you a friend request", status_code=409)
        return Response(content="Friend request already sent", status_code=409)

    create_friend_request(db, sender.id, receiver.id)
    return FriendActionResponse(
        status="ok",
        message=f"Friend request sent to {receiver.username}",
    )


@app.post("/friends/requests/{request_id}/accept", response_model=FriendActionResponse)
async def accept_friend_request_api(
    request_id: int,
    authorization: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    try:
        username = _require_user(authorization)
    except ValueError as e:
        return Response(content=str(e), status_code=401)

    user = get_user_by_username(db, username)
    if user is None:
        return Response(content="User not found", status_code=401)

    friend_request = get_pending_friend_request_for_receiver(db, request_id, user.id)
    if friend_request is None:
        return Response(content="Friend request not found", status_code=404)

    accept_friend_request(db, friend_request)
    return FriendActionResponse(status="ok", message="Friend request accepted")


@app.post("/friends/requests/{request_id}/decline", response_model=FriendActionResponse)
async def decline_friend_request_api(
    request_id: int,
    authorization: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    try:
        username = _require_user(authorization)
    except ValueError as e:
        return Response(content=str(e), status_code=401)

    user = get_user_by_username(db, username)
    if user is None:
        return Response(content="User not found", status_code=401)

    friend_request = get_pending_friend_request_for_receiver(db, request_id, user.id)
    if friend_request is None:
        return Response(content="Friend request not found", status_code=404)

    decline_friend_request(db, friend_request)
    return FriendActionResponse(status="ok", message="Friend request declined")


@app.get("/health")
async def health():
    """健康检查"""
    return {
        "status": "ok",
        "services": [
            "text2image",
            "image2image",
            "image23d",
            "text23d",
            "dog_chat",
            "human_chat",
        ],
    }


@app.get("/")
async def root():
    """根路径 - 服务信息"""
    return {
        "name": "AI Generation Pipeline",
        "version": "1.0.0",
        "endpoints": {
            "POST /auth/login": "登录（数据库校验）",
            "POST /auth/register": "注册（写入 User + 默认 World）",
            "GET /workspaces": "List workspaces (World + WorldMember)",
            "POST /workspaces/create": "Create workspace with optional co-owners",
            "POST /world/{world_id}": "创建或更新世界快照",
            "GET /world/{world_id}": "获取世界快照",
            "POST /chat": "狗狗伙伴聊天",
            "POST /human-chat": "玩家伴侣聊天",
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
    uvicorn.run(app, host=get_server_listen_address(), port=get_server_port())
