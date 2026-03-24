"""
好友关系与好友请求模型
"""
import enum

from sqlalchemy import Column, Integer, DateTime, ForeignKey, UniqueConstraint, Enum as SQLEnum
from sqlalchemy.sql import func

from database import Base


class FriendRequestStatus(str, enum.Enum):
    PENDING = "pending"
    ACCEPTED = "accepted"
    DECLINED = "declined"


class Friendship(Base):
    """
    有向好友关系；接受请求后会写入双向两条记录，便于直接按 user_id 查询好友列表。
    """
    __tablename__ = "friendships"
    __table_args__ = (
        UniqueConstraint("user_id", "friend_user_id", name="uq_friendships_user_friend"),
    )

    id = Column(Integer, primary_key=True, autoincrement=True, comment="主键")
    user_id = Column(
        Integer,
        ForeignKey("users.id", ondelete="CASCADE"),
        nullable=False,
        index=True,
        comment="用户ID"
    )
    friend_user_id = Column(
        Integer,
        ForeignKey("users.id", ondelete="CASCADE"),
        nullable=False,
        index=True,
        comment="好友用户ID"
    )
    created_at = Column(
        DateTime(timezone=True),
        server_default=func.now(),
        nullable=False,
        comment="成为好友时间"
    )

    def __repr__(self):
        return f"<Friendship(user_id={self.user_id}, friend_user_id={self.friend_user_id})>"


class FriendRequest(Base):
    """
    好友请求；允许保留历史状态，客户端默认只消费 pending。
    """
    __tablename__ = "friend_requests"

    id = Column(Integer, primary_key=True, autoincrement=True, comment="主键")
    sender_user_id = Column(
        Integer,
        ForeignKey("users.id", ondelete="CASCADE"),
        nullable=False,
        index=True,
        comment="发送者用户ID"
    )
    receiver_user_id = Column(
        Integer,
        ForeignKey("users.id", ondelete="CASCADE"),
        nullable=False,
        index=True,
        comment="接收者用户ID"
    )
    status = Column(
        SQLEnum(
            FriendRequestStatus,
            native_enum=True,
            create_constraint=False,
            name="friendrequeststatus",
            values_callable=lambda x: [e.value for e in x],
        ),
        nullable=False,
        default=FriendRequestStatus.PENDING,
        comment="请求状态"
    )
    created_at = Column(
        DateTime(timezone=True),
        server_default=func.now(),
        nullable=False,
        comment="创建时间"
    )
    responded_at = Column(
        DateTime(timezone=True),
        nullable=True,
        comment="处理时间"
    )

    def __repr__(self):
        return (
            f"<FriendRequest(id={self.id}, sender_user_id={self.sender_user_id}, "
            f"receiver_user_id={self.receiver_user_id}, status='{self.status}')>"
        )
