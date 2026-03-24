"""
好友关系 CRUD
"""
from datetime import datetime, timezone
from typing import Optional

from sqlalchemy import and_, or_
from sqlalchemy.orm import Session

from models.friend import FriendRequest, FriendRequestStatus, Friendship
from models.user import User


def are_friends(db: Session, user_id: int, friend_user_id: int) -> bool:
    return db.query(Friendship.id).filter(
        Friendship.user_id == user_id,
        Friendship.friend_user_id == friend_user_id,
    ).first() is not None


def get_friendship_rows_for_user(db: Session, user_id: int):
    return db.query(Friendship, User).join(
        User, User.id == Friendship.friend_user_id
    ).filter(
        Friendship.user_id == user_id
    ).order_by(
        User.username.asc()
    ).all()


def get_incoming_friend_request_rows(db: Session, user_id: int):
    return db.query(FriendRequest, User).join(
        User, User.id == FriendRequest.sender_user_id
    ).filter(
        FriendRequest.receiver_user_id == user_id,
        FriendRequest.status == FriendRequestStatus.PENDING,
    ).order_by(
        FriendRequest.created_at.desc()
    ).all()


def get_outgoing_friend_request_rows(db: Session, user_id: int):
    return db.query(FriendRequest, User).join(
        User, User.id == FriendRequest.receiver_user_id
    ).filter(
        FriendRequest.sender_user_id == user_id,
        FriendRequest.status == FriendRequestStatus.PENDING,
    ).order_by(
        FriendRequest.created_at.desc()
    ).all()


def get_pending_friend_request_between(db: Session, user_a_id: int, user_b_id: int) -> Optional[FriendRequest]:
    return db.query(FriendRequest).filter(
        FriendRequest.status == FriendRequestStatus.PENDING,
        or_(
            and_(
                FriendRequest.sender_user_id == user_a_id,
                FriendRequest.receiver_user_id == user_b_id,
            ),
            and_(
                FriendRequest.sender_user_id == user_b_id,
                FriendRequest.receiver_user_id == user_a_id,
            ),
        ),
    ).order_by(FriendRequest.created_at.desc()).first()


def get_pending_friend_request_for_receiver(db: Session, request_id: int, receiver_user_id: int) -> Optional[FriendRequest]:
    return db.query(FriendRequest).filter(
        FriendRequest.id == request_id,
        FriendRequest.receiver_user_id == receiver_user_id,
        FriendRequest.status == FriendRequestStatus.PENDING,
    ).first()


def create_friend_request(db: Session, sender_user_id: int, receiver_user_id: int) -> FriendRequest:
    friend_request = FriendRequest(
        sender_user_id=sender_user_id,
        receiver_user_id=receiver_user_id,
        status=FriendRequestStatus.PENDING,
    )
    db.add(friend_request)
    db.commit()
    db.refresh(friend_request)
    return friend_request


def accept_friend_request(db: Session, friend_request: FriendRequest) -> FriendRequest:
    friend_request.status = FriendRequestStatus.ACCEPTED
    friend_request.responded_at = datetime.now(timezone.utc)
    _ensure_friendship(db, friend_request.sender_user_id, friend_request.receiver_user_id)
    _ensure_friendship(db, friend_request.receiver_user_id, friend_request.sender_user_id)
    db.commit()
    db.refresh(friend_request)
    return friend_request


def decline_friend_request(db: Session, friend_request: FriendRequest) -> FriendRequest:
    friend_request.status = FriendRequestStatus.DECLINED
    friend_request.responded_at = datetime.now(timezone.utc)
    db.commit()
    db.refresh(friend_request)
    return friend_request


def _ensure_friendship(db: Session, user_id: int, friend_user_id: int) -> None:
    if user_id == friend_user_id:
        return
    if are_friends(db, user_id, friend_user_id):
        return
    db.add(Friendship(user_id=user_id, friend_user_id=friend_user_id))
