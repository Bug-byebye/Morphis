"""
用户数据模型（MVP：支持联机身份与后续扩展）
"""
from sqlalchemy import Column, Integer, String, DateTime
from sqlalchemy.sql import func
from database import Base


class User(Base):
    """
    用户表：支持多人联机的身份与后续鉴权扩展
    """
    __tablename__ = "users"

    id = Column(Integer, primary_key=True, autoincrement=True, comment="主键")
    username = Column(String(64), unique=True, nullable=False, index=True, comment="用户名（唯一）")
    password = Column(String(128), nullable=False, default="", comment="密码（当前仅做字符串匹配，不加哈希）")
    email = Column(String(256), nullable=True, index=True, comment="邮箱（暂未使用）")
    created_at = Column(
        DateTime(timezone=True),
        server_default=func.now(),
        nullable=False,
        comment="创建时间"
    )

    def __repr__(self):
        return f"<User(id={self.id}, username='{self.username}')>"
