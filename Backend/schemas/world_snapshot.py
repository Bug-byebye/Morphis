"""
世界快照 Pydantic 模式
"""
from pydantic import BaseModel, Field
from typing import Optional, Dict, Any, List
from datetime import datetime


class WorldObjectData(BaseModel):
    """单个世界物体数据"""
    object_id: str
    prefab_id: str
    pos_x: float
    pos_y: float
    pos_z: float
    rot_x: float
    rot_y: float
    rot_z: float
    rot_w: float
    scale_x: float
    scale_y: float
    scale_z: float


class WorldSnapshotPayload(BaseModel):
    """前端发送的世界快照数据（完整结构）"""
    world_id: str
    version: int
    objects: List[Dict[str, Any]] = Field(default_factory=list)


class WorldSnapshotCreate(BaseModel):
    """创建/更新世界快照的请求体"""
    snapshot: Dict[str, Any] = Field(..., description="完整世界数据（JSON）")
    owner_id: Optional[str] = Field(None, description="所有者ID（预留）")


class WorldSnapshotUpdate(BaseModel):
    """更新世界快照的请求体"""
    snapshot: Dict[str, Any] = Field(..., description="完整世界数据（JSON）")


class WorldSnapshotResponse(BaseModel):
    """世界快照响应"""
    world_id: str
    version: int
    snapshot: Dict[str, Any]
    owner_id: Optional[str] = None
    created_at: datetime
    updated_at: datetime

    class Config:
        from_attributes = True


class WorldSnapshotSimpleResponse(BaseModel):
    """简化的世界快照响应（仅返回 world_id 和 version）"""
    world_id: str
    version: int
