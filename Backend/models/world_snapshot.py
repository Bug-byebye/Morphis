"""
世界快照数据模型
"""
from sqlalchemy import Column, String, Integer, DateTime, JSON
from sqlalchemy.sql import func
from database import Base


class WorldSnapshot(Base):
    """
    世界快照数据表
    """
    __tablename__ = "world_snapshots"

    # 主键
    world_id = Column(String, primary_key=True, index=True, comment="世界ID")

    # 预留字段
    owner_id = Column(String, nullable=True, index=True, comment="所有者ID（预留）")

    # 版本号
    version = Column(Integer, nullable=False, default=1, comment="版本号")

    # 快照数据（JSONB）
    snapshot = Column(JSON, nullable=False, comment="完整世界数据（JSONB）")

    # 时间戳
    created_at = Column(
        DateTime(timezone=True),
        server_default=func.now(),
        nullable=False,
        comment="创建时间"
    )
    updated_at = Column(
        DateTime(timezone=True),
        server_default=func.now(),
        onupdate=func.now(),
        nullable=False,
        comment="更新时间"
    )

    def __repr__(self):
        return f"<WorldSnapshot(world_id='{self.world_id}', version={self.version})>"
