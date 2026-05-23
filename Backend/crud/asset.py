"""
资产 CRUD + 文件存储辅助。
存储约定：Backend/storage/assets/<sha256>.<ext>（按内容寻址，天然去重）。
"""
from __future__ import annotations

import hashlib
from pathlib import Path
from typing import Optional, Tuple

from sqlalchemy.orm import Session

from models.asset import Asset

# Backend/storage/assets/
_STORAGE_ROOT = Path(__file__).resolve().parent.parent / "storage" / "assets"


def _ensure_storage_dir() -> Path:
    _STORAGE_ROOT.mkdir(parents=True, exist_ok=True)
    return _STORAGE_ROOT


def compute_sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def asset_file_path(sha256: str, ext: str = "glb") -> Path:
    safe_ext = (ext or "glb").lstrip(".").lower()
    return _ensure_storage_dir() / f"{sha256}.{safe_ext}"


def get_asset(db: Session, sha256: str) -> Optional[Asset]:
    return db.query(Asset).filter(Asset.sha256 == sha256).first()


def save_asset(
    db: Session,
    data: bytes,
    filename: str,
    media_type: str = "model/gltf-binary",
    owner_user_id: Optional[int] = None,
) -> Tuple[Asset, bool]:
    """
    幂等上传：根据内容计算 sha256，若已存在直接返回 (asset, False)；
    否则写盘并入库 (asset, True)。
    """
    sha = compute_sha256(data)
    existing = get_asset(db, sha)
    if existing is not None:
        # 已经入库，写盘做一次自愈，防止文件丢失
        path = asset_file_path(sha, _ext_from_filename(filename))
        if not path.exists():
            path.write_bytes(data)
        return existing, False

    path = asset_file_path(sha, _ext_from_filename(filename))
    path.write_bytes(data)

    asset = Asset(
        sha256=sha,
        owner_user_id=owner_user_id,
        filename=filename or f"{sha}.glb",
        media_type=media_type or "model/gltf-binary",
        size_bytes=len(data),
    )
    db.add(asset)
    db.commit()
    db.refresh(asset)
    return asset, True


def read_asset_bytes(sha256: str, filename: str = "") -> Optional[bytes]:
    path = asset_file_path(sha256, _ext_from_filename(filename))
    if not path.exists():
        # 兜底：扫一下同名 sha 的所有后缀
        for candidate in _ensure_storage_dir().glob(f"{sha256}.*"):
            return candidate.read_bytes()
        return None
    return path.read_bytes()


def _ext_from_filename(filename: Optional[str]) -> str:
    if not filename or "." not in filename:
        return "glb"
    return filename.rsplit(".", 1)[-1].lower() or "glb"
