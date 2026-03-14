"""
用户 CRUD：登录、注册与按用户名查询
"""
from sqlalchemy.orm import Session
from typing import Optional
from models.user import User


def get_user_by_username(db: Session, username: str) -> Optional[User]:
    """按用户名查询用户，不存在返回 None"""
    return db.query(User).filter(User.username == username).first()


def get_user_by_id(db: Session, user_id: int) -> Optional[User]:
    """按 id 查询用户"""
    return db.query(User).filter(User.id == user_id).first()


def create_user(
    db: Session,
    username: str,
    password: str,
    email: Optional[str] = None,
) -> User:
    """创建用户（注册用），明文密码存储（当前仅做字符串匹配）"""
    user = User(username=username, password=password, email=email)
    db.add(user)
    db.commit()
    db.refresh(user)
    return user
