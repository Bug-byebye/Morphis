"""
世界快照 CRUD 操作
所有数据库操作集中在这里
"""
from sqlalchemy.orm import Session
from typing import Optional, Dict, Any
from models.world_snapshot import WorldSnapshot


def get_world_snapshot(db: Session, world_id: str) -> Optional[WorldSnapshot]:
    """
    根据 world_id 获取世界快照（最新版本）
    
    Args:
        db: 数据库会话
        world_id: 世界ID
    
    Returns:
        WorldSnapshot 对象，如果不存在则返回 None
    """
    return db.query(WorldSnapshot).filter(WorldSnapshot.world_id == world_id).first()


def create_or_update_world_snapshot(
    db: Session,
    world_id: str,
    snapshot_data: Dict[str, Any],
    owner_id: Optional[str] = None
) -> WorldSnapshot:
    """
    创建或更新世界快照
    - 如果 world_id 不存在 → 创建新记录，version=1
    - 如果存在 → 更新 snapshot，version 自动 +1
    
    Args:
        db: 数据库会话
        world_id: 世界ID
        snapshot_data: 快照数据（完整 JSON）
        owner_id: 所有者ID（可选）
    
    Returns:
        创建或更新后的 WorldSnapshot 对象
    """
    # 查询现有记录
    existing = db.query(WorldSnapshot).filter(WorldSnapshot.world_id == world_id).first()
    
    if existing:
        # 更新现有记录
        existing.snapshot = snapshot_data
        existing.version += 1
        if owner_id is not None:
            existing.owner_id = owner_id
        db.commit()
        db.refresh(existing)
        return existing
    else:
        # 创建新记录
        new_snapshot = WorldSnapshot(
            world_id=world_id,
            owner_id=owner_id,
            version=1,
            snapshot=snapshot_data
        )
        db.add(new_snapshot)
        db.commit()
        db.refresh(new_snapshot)
        return new_snapshot
