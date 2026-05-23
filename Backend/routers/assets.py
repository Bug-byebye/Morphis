"""
资产（GLB）上传/下载/查询 API。

约定：内容寻址，asset_id == sha256（十六进制，64 字符）。
- POST /assets/upload  multipart：file=<GLB bytes>  → 返回 {asset_id, size, created}
- GET  /assets/{asset_id}                          → 返回 GLB 字节（model/gltf-binary）
- HEAD /assets/{asset_id}                          → 仅判断存在，客户端用于上传前去重
"""
from __future__ import annotations

from typing import Optional

from fastapi import APIRouter, Depends, File, Header, HTTPException, Response, UploadFile, status
from sqlalchemy.orm import Session

from crud.asset import get_asset, read_asset_bytes, save_asset
from crud.user import get_user_by_username
from database import get_db

router = APIRouter(prefix="/assets", tags=["assets"])


def _resolve_owner_id(authorization: Optional[str], db: Session) -> Optional[int]:
    """从 Bearer token 解析 owner user_id；解析失败返回 None（允许匿名上传）。"""
    if not authorization or not authorization.startswith("Bearer "):
        return None
    try:
        from server import _tokens  # 复用现有的内存 token 表
    except Exception:
        return None
    token = authorization[len("Bearer "):].strip()
    username = _tokens.get(token)
    if not username:
        return None
    user = get_user_by_username(db, username)
    return user.id if user else None


@router.post("/upload")
async def upload_asset(
    file: UploadFile = File(...),
    authorization: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    try:
        data = await file.read()
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Failed to read upload: {e}")
    if not data:
        raise HTTPException(status_code=400, detail="Empty upload body")

    owner_id = _resolve_owner_id(authorization, db)
    asset, created = save_asset(
        db=db,
        data=data,
        filename=file.filename or "asset.glb",
        media_type=file.content_type or "model/gltf-binary",
        owner_user_id=owner_id,
    )
    return {
        "asset_id": asset.sha256,
        "size": asset.size_bytes,
        "filename": asset.filename,
        "created": created,
    }


@router.get("/{asset_id}")
async def download_asset(asset_id: str, db: Session = Depends(get_db)):
    if len(asset_id) != 64:
        raise HTTPException(status_code=400, detail="Invalid asset_id (expect sha256 hex)")
    asset = get_asset(db, asset_id)
    if asset is None:
        raise HTTPException(status_code=404, detail="Asset not found")
    data = read_asset_bytes(asset_id, asset.filename)
    if data is None:
        raise HTTPException(status_code=410, detail="Asset row exists but file is gone")
    return Response(content=data, media_type=asset.media_type or "model/gltf-binary")


@router.head("/{asset_id}")
async def head_asset(asset_id: str, db: Session = Depends(get_db)):
    if len(asset_id) != 64:
        return Response(status_code=400)
    asset = get_asset(db, asset_id)
    if asset is None:
        return Response(status_code=404)
    return Response(status_code=200, headers={"Content-Length": str(asset.size_bytes)})
