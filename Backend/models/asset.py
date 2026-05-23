"""
用户资产（GLB 等二进制模型）数据模型。

资产以 SHA256 命名落地到 Backend/storage/assets/<sha256>.<ext>，数据库只存元数据。
共享世界（多成员）中放置的物体所引用的 asset_id 必须在此表中存在；
单机世界（仅本地存储）则不强制上传，客户端按需通过该表请求资产文件。
"""
from sqlalchemy import Column, Integer, String, DateTime, BigInteger, ForeignKey
from sqlalchemy.sql import func
from database import Base


class Asset(Base):
    __tablename__ = "assets"

    # 内容寻址：sha256 既是文件名也是主键，天然去重
    sha256 = Column(String(64), primary_key=True, comment="文件内容 SHA256 十六进制")
    owner_user_id = Column(
        Integer,
        ForeignKey("users.id", ondelete="SET NULL"),
        nullable=True,
        index=True,
        comment="首次上传者（可空，便于未来引入公共资产库）",
    )
    filename = Column(String(255), nullable=False, default="", comment="原始文件名（仅用于展示）")
    media_type = Column(String(64), nullable=False, default="model/gltf-binary")
    size_bytes = Column(BigInteger, nullable=False, default=0)
    created_at = Column(
        DateTime(timezone=True),
        server_default=func.now(),
        nullable=False,
    )

    def __repr__(self):
        return f"<Asset(sha256='{self.sha256[:8]}...', size={self.size_bytes})>"
