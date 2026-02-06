"""
世界快照 API 路由
"""
import json
from fastapi import APIRouter, Depends, HTTPException, status, Response
from sqlalchemy.orm import Session
from typing import Dict, Any
from database import get_db
from crud.world_snapshot import (
    get_world_snapshot,
    create_or_update_world_snapshot,
)
from crud.world import get_or_create_world
from schemas.world_snapshot import (
    WorldSnapshotPayload,
    WorldSnapshotResponse,
    WorldSnapshotSimpleResponse,
)

router = APIRouter(prefix="/world", tags=["world"])


@router.post(
    "/{world_id}",
    response_model=WorldSnapshotSimpleResponse,
    status_code=status.HTTP_200_OK,
    summary="创建或更新世界快照",
    description="""
    创建或更新世界快照：
    - 如果 world_id 不存在 → 创建新记录，version=1
    - 如果存在 → 覆盖 snapshot，version 自动 +1
    
    请求体应包含完整的世界快照数据（world_id, version, objects）
    """
)
async def create_or_update_world(
    world_id: str,
    payload: WorldSnapshotPayload,
    db: Session = Depends(get_db)
):
    """
    POST /world/{world_id}
    
    创建或更新世界快照
    """
    try:
        # 确保 World 行存在（满足 world_id 外键）
        get_or_create_world(db=db, world_id=world_id, name=world_id, owner_user_id=None)
        # 将整个 payload 转换为字典作为 snapshot 存储
        snapshot_data = payload.model_dump()
        # 调用 CRUD 操作
        result = create_or_update_world_snapshot(
            db=db,
            world_id=world_id,
            snapshot_data=snapshot_data,
            owner_id=None  # 预留字段，暂时为 None
        )
        
        return WorldSnapshotSimpleResponse(
            world_id=result.world_id,
            version=result.version
        )
    
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Internal server error: {str(e)}"
        )


@router.get(
    "/{world_id}",
    status_code=status.HTTP_200_OK,
    summary="获取世界快照",
    description="根据 world_id 获取最新版本的世界快照。返回体为 Unity 可直接解析的 { world_id, version, objects } 结构。"
)
async def get_world(
    world_id: str,
    db: Session = Depends(get_db)
):
    """
    GET /world/{world_id}
    
    获取世界快照。返回存储的 snapshot 内容（与 Unity WorldSnapshot 结构一致）。
    """
    row = get_world_snapshot(db=db, world_id=world_id)
    
    if not row:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"World '{world_id}' not found"
        )
    # 返回与 Unity 一致的顶层结构：world_id, version, objects
    body = dict(row.snapshot) if isinstance(row.snapshot, dict) else {}
    body.setdefault("world_id", row.world_id)
    body.setdefault("version", row.version)
    body.setdefault("objects", [])
    return Response(content=json.dumps(body), media_type="application/json")
