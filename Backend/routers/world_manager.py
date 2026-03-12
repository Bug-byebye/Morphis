"""
World 进程管理 API 路由
"""
from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session
from typing import Dict, List
from pydantic import BaseModel
from database import get_db
from services.world_manager import get_world_manager

router = APIRouter(prefix="/worlds/manage", tags=["world-management"])


class StartWorldRequest(BaseModel):
    world_id: str


class StartWorldResponse(BaseModel):
    status: str
    port: int = None
    message: str


class StopWorldRequest(BaseModel):
    world_id: str
    force: bool = False


class WorldStatusResponse(BaseModel):
    status: str
    world_id: str = None
    world_status: str = None
    port: int = None
    player_count: int = None
    process_id: int = None
    last_active: str = None
    message: str = None


class WorldListItem(BaseModel):
    id: str
    name: str
    status: str
    port: int = None
    player_count: int
    process_id: int = None


class WorldListResponse(BaseModel):
    worlds: List[WorldListItem]


class PlayerCountUpdate(BaseModel):
    world_id: str
    count: int


@router.post("/start", response_model=StartWorldResponse)
async def start_world(
    request: StartWorldRequest,
    db: Session = Depends(get_db)
):
    """
    启动 World 进程
    
    如果 World 已在运行，返回现有端口
    如果未运行，分配端口并启动新进程
    """
    manager = get_world_manager()
    result = manager.start_world(db, request.world_id)
    
    if result["status"] == "error":
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=result["message"]
        )
    
    return StartWorldResponse(
        status=result["status"],
        port=result.get("port"),
        message=result["message"]
    )


@router.post("/stop")
async def stop_world(
    request: StopWorldRequest,
    db: Session = Depends(get_db)
):
    """
    停止 World 进程
    
    force=True 时强制终止（SIGKILL）
    """
    manager = get_world_manager()
    result = manager.stop_world(db, request.world_id, request.force)
    
    if result["status"] == "error":
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=result["message"]
        )
    
    return result


@router.get("/status/{world_id}", response_model=WorldStatusResponse)
async def get_world_status(
    world_id: str,
    db: Session = Depends(get_db)
):
    """
    获取 World 状态
    
    返回：运行状态、端口、玩家数量、进程 ID 等
    """
    manager = get_world_manager()
    result = manager.get_world_status(db, world_id)
    
    if result["status"] == "error":
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=result["message"]
        )
    
    return WorldStatusResponse(**result)


@router.post("/player-count")
async def update_player_count(
    request: PlayerCountUpdate,
    db: Session = Depends(get_db)
):
    """
    更新 World 玩家数量（由 Unity Server 调用）
    """
    manager = get_world_manager()
    manager.update_player_count(db, request.world_id, request.count)
    return {"status": "ok"}


@router.get("/list", response_model=WorldListResponse)
async def list_worlds(db: Session = Depends(get_db)):
    """
    列出所有 World 及其状态
    """
    manager = get_world_manager()
    worlds = manager.list_all_worlds(db)
    
    items = [WorldListItem(**w) for w in worlds]
    return WorldListResponse(worlds=items)
