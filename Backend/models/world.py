"""
世界/场景元数据模型（可被多人进入的世界）
"""
from sqlalchemy import Column, String, Integer, DateTime, ForeignKey, Enum as SQLEnum
from sqlalchemy.sql import func
from database import Base
import enum


class WorldStatus(str, enum.Enum):
    """World 进程状态"""
    STOPPED = "stopped"      # 未运行
    STARTING = "starting"    # 启动中
    RUNNING = "running"      # 运行中
    STOPPING = "stopping"    # 停止中
    ERROR = "error"          # 错误状态


class World(Base):
    """
    世界表：描述一个可被多人进入的世界/场景
    """
    __tablename__ = "worlds"

    id = Column(String(64), primary_key=True, index=True, comment="世界ID（与 API world_id 一致）")
    name = Column(String(256), nullable=False, default="", comment="世界名称")
    owner_user_id = Column(
        Integer,
        ForeignKey("users.id", ondelete="SET NULL"),
        nullable=True,
        index=True,
        comment="所有者用户ID"
    )
    
    # 进程管理字段
    status = Column(
        SQLEnum(WorldStatus),
        nullable=False,
        default=WorldStatus.STOPPED,
        comment="World 进程状态"
    )
    process_id = Column(Integer, nullable=True, comment="进程 PID")
    port = Column(Integer, nullable=True, comment="监听端口")
    player_count = Column(Integer, nullable=False, default=0, comment="当前玩家数量")
    last_active_at = Column(
        DateTime(timezone=True),
        nullable=True,
        comment="最后活跃时间"
    )
    
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
        return f"<World(id='{self.id}', name='{self.name}', status='{self.status}', port={self.port})>"


class WorldMember(Base):
    """
    世界成员表：场景与用户多对多，表示某场景共属于哪些成员（A 和 B 等）
    """
    __tablename__ = "world_members"

    world_id = Column(
        String(64),
        ForeignKey("worlds.id", ondelete="CASCADE"),
        primary_key=True,
        comment="世界ID"
    )
    user_id = Column(
        Integer,
        ForeignKey("users.id", ondelete="CASCADE"),
        primary_key=True,
        comment="用户ID"
    )
    created_at = Column(
        DateTime(timezone=True),
        server_default=func.now(),
        nullable=False,
        comment="加入时间"
    )

    def __repr__(self):
        return f"<WorldMember(world_id='{self.world_id}', user_id={self.user_id})>"
