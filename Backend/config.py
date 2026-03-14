"""
服务端配置加载：统一从 deploy/server-config.json 读取。
"""
from __future__ import annotations

import json
from functools import lru_cache
from pathlib import Path
from typing import Any, Dict
from urllib.parse import urlparse
from urllib.parse import quote_plus


def _resolve_config_path() -> Path:
    backend_dir = Path(__file__).resolve().parent
    return backend_dir.parent / "deploy" / "server-config.json"


@lru_cache(maxsize=1)
def _load_raw_config() -> Dict[str, Any]:
    config_path = _resolve_config_path()
    if not config_path.exists():
        raise FileNotFoundError(f"Config file not found: {config_path}")

    with config_path.open("r", encoding="utf-8") as f:
        return json.load(f)


def get_server_config() -> Dict[str, Any]:
    """返回完整配置字典。"""
    return _load_raw_config()


def get_api_base_url() -> str:
    return str(_load_raw_config().get("ApiBaseUrl", "http://localhost:8000"))


def get_server_listen_address() -> str:
    return str(_load_raw_config().get("ServerListenAddress", "0.0.0.0"))


def get_server_port() -> int:
    api_base_url = get_api_base_url()
    parsed = urlparse(api_base_url)
    if parsed.port:
        return int(parsed.port)
    return 443 if parsed.scheme == "https" else 8000


def get_unity_server_config() -> Dict[str, Any]:
    return dict(_load_raw_config().get("UnityServer", {}))


def get_database_url() -> str:
    db = _load_raw_config().get("Database", {})
    host = db.get("Host", "localhost")
    port = int(db.get("Port", 5432))
    name = db.get("Name", "morphis")
    user = db.get("User", "postgres")
    password = db.get("Password", "postgres")

    user_enc = quote_plus(str(user))
    password_enc = quote_plus(str(password))
    return f"postgresql://{user_enc}:{password_enc}@{host}:{port}/{name}"
