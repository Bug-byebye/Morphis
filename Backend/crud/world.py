"""
世界/场景 CRUD：确保保存快照前 World 行存在；按用户返回 workspace 列表
"""
from sqlalchemy.orm import Session
from typing import Optional, List, Dict, Any
from models.world import World, WorldMember
from models.user import User


def get_or_create_world(
    db: Session,
    world_id: str,
    name: Optional[str] = None,
    owner_user_id: Optional[int] = None,
) -> World:
    """
    按 world_id 获取或创建 World 行（保存快照前调用，保证外键满足）
    """
    row = db.query(World).filter(World.id == world_id).first()
    if row:
        return row
    row = World(
        id=world_id,
        name=name or world_id,
        owner_user_id=owner_user_id,
    )
    db.add(row)
    db.commit()
    db.refresh(row)
    return row


def get_workspaces_for_user(db: Session, user_id: int) -> List[Dict[str, Any]]:
    """
    返回某用户可见的 workspace 列表（作为 owner 或 member）。
    每项为 { "id": world_id, "name": world.name, "members": [username, ...] }。
    """
    # 作为 owner 的世界
    owned = db.query(World).filter(World.owner_user_id == user_id).all()
    # 作为 member 的世界（不含已作为 owner 的）
    member_world_ids = (
        db.query(WorldMember.world_id)
        .filter(WorldMember.user_id == user_id)
        .distinct()
        .all()
    )
    member_world_ids_set = {w[0] for w in member_world_ids}
    world_ids_seen = {w.id for w in owned}
    for wid in member_world_ids_set:
        if wid not in world_ids_seen:
            w = db.query(World).filter(World.id == wid).first()
            if w:
                owned.append(w)
                world_ids_seen.add(wid)

    result: List[Dict[str, Any]] = []
    for w in owned:
        members: List[str] = []
        if w.owner_user_id:
            u = db.query(User).filter(User.id == w.owner_user_id).first()
            if u and u.username not in members:
                members.append(u.username)
        for m in db.query(WorldMember).filter(WorldMember.world_id == w.id).all():
            u = db.query(User).filter(User.id == m.user_id).first()
            if u and u.username not in members:
                members.append(u.username)
        result.append({"id": w.id, "name": w.name or w.id, "members": members})
    return result


def create_workspace_with_coowners(
    db: Session,
    owner_user_id: int,
    name: str,
    co_owner_usernames: List[str],
) -> World:
    """
    Create a new workspace (World) owned by the current user,
    and add co-owners by their usernames.
    Returns the created World.
    """
    import uuid
    world_id = f"ws-{uuid.uuid4().hex[:12]}"
    world = World(
        id=world_id,
        name=name or world_id,
        owner_user_id=owner_user_id,
    )
    db.add(world)
    # Add owner as member
    db.add(WorldMember(world_id=world_id, user_id=owner_user_id))
    # Add co-owners
    for username in co_owner_usernames:
        username = (username or "").strip()
        if not username:
            continue
        co_user = db.query(User).filter(User.username == username).first()
        if co_user and co_user.id != owner_user_id:
            db.add(WorldMember(world_id=world_id, user_id=co_user.id))
    db.commit()
    db.refresh(world)
    return world
