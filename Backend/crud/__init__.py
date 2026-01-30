"""
CRUD 操作模块
"""
from .world_snapshot import (
    get_world_snapshot,
    create_or_update_world_snapshot,
)

__all__ = [
    "get_world_snapshot",
    "create_or_update_world_snapshot",
]
