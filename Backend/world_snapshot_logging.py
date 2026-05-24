"""
场景快照专用日志：在 uvicorn 下保证控制台可见（与 services.world_manager 同类配置）。
"""
from __future__ import annotations

import json
import logging
import sys
from typing import Any, Dict

_CONFIGURED = False
LOGGER_NAME = "morphis.world_snapshot"


def ensure_world_snapshot_logging() -> logging.Logger:
    """注册独立 handler，避免 uvicorn 覆盖 root 后应用日志不可见。"""
    global _CONFIGURED
    log = logging.getLogger(LOGGER_NAME)
    if _CONFIGURED:
        return log

    log.setLevel(logging.INFO)
    if not log.handlers:
        handler = logging.StreamHandler(sys.stderr)
        handler.setFormatter(
            logging.Formatter("%(asctime)s [%(levelname)s] %(name)s: %(message)s")
        )
        log.addHandler(handler)
    log.propagate = False
    _CONFIGURED = True
    return log


def log_snapshot_struct(event: str, body: Dict[str, Any]) -> None:
    """打印场景快照结构体摘要 + 完整 JSON。"""
    log = ensure_world_snapshot_logging()
    world_id = body.get("world_id")
    version = body.get("version")
    objects = body.get("objects") or []
    summary = (
        f"[场景快照] {event} | world_id={world_id} version={version} objects={len(objects)}"
    )
    payload = json.dumps(body, ensure_ascii=False, indent=2)

    # 单行摘要：与 uvicorn access log 同流，最容易在控制台看到
    logging.getLogger("uvicorn.error").info(summary)
    log.info("%s\n%s", summary, payload)


def log_snapshot_message(message: str) -> None:
    log = ensure_world_snapshot_logging()
    logging.getLogger("uvicorn.error").info(message)
    log.info(message)
