"""
服务端配置加载：统一从 deploy/server-config.json 读取。
"""
from __future__ import annotations

import json
import os
import stat
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


def get_public_game_server_address() -> str:
    value = _load_raw_config().get("PublicGameServerAddress")
    if value:
        return str(value)

    parsed = urlparse(get_api_base_url())
    return str(parsed.hostname or "127.0.0.1")


def get_unity_server_config() -> Dict[str, Any]:
    return dict(_load_raw_config().get("UnityServer", {}))


def get_unity_server_executable() -> str:
    """Unity Server 可执行文件路径；UNITY_SERVER_PATH 环境变量优先。"""
    unity_cfg = get_unity_server_config()
    configured = os.getenv("UNITY_SERVER_PATH") or unity_cfg.get("ExecutablePath", "")
    if not configured:
        raise ValueError("Unity Server ExecutablePath is not configured")
    return str(configured)


def get_unity_server_root() -> Path:
    """Unity Server 部署根目录（含 Morphis_Data、UnityPlayer.so）。"""
    return Path(get_unity_server_executable()).resolve().parent


def _chmod_executable(path: Path) -> None:
    mode = path.stat().st_mode
    if mode & stat.S_IXUSR:
        return
    path.chmod(mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)


def ensure_unity_server_permissions() -> None:
    """
    上传部署后 MorphisServer 内二进制可能缺少 +x，启动 World 进程前补齐。
    覆盖主程序、UnityPlayer.so 及 Morphis_Data 下的 .so 插件。
    """
    root = get_unity_server_root()
    if not root.is_dir():
        return

    seen: set[Path] = set()
    candidates = [
        Path(get_unity_server_executable()),
        root / "UnityPlayer.so",
        *root.glob("*.so"),
        *root.glob("*.so.*"),
        *root.glob("*.x86_64"),
        *root.rglob("*.so"),
    ]
    for path in candidates:
        resolved = path.resolve()
        if resolved in seen or not path.is_file():
            continue
        seen.add(resolved)
        _chmod_executable(path)


def get_unity_server_log_directory() -> Path:
    """World 进程日志目录；相对路径基于 Morphis 项目根目录解析。"""
    unity_cfg = get_unity_server_config()
    backend_dir = Path(__file__).resolve().parent
    project_root = backend_dir.parent
    configured_log_dir = str(unity_cfg.get("LogDirectory", "")).strip()
    if configured_log_dir:
        log_dir = Path(configured_log_dir)
        if not log_dir.is_absolute():
            log_dir = project_root / log_dir
    else:
        log_dir = project_root / "logs" / "worlds"
    return log_dir


def build_unity_server_config_payload() -> Dict[str, Any]:
    """从 deploy/server-config.json 生成 Unity Server 所需的 config.json 内容。"""
    raw = _load_raw_config()
    unity_cfg = raw.get("UnityServer", {})
    return {
        "ApiBaseUrl": get_api_base_url(),
        "ServerListenAddress": get_server_listen_address(),
        "ServerPort": int(unity_cfg.get("BasePort", raw.get("ServerPort", 7777))),
        "DefaultWorldId": str(raw.get("DefaultWorldId", "default-world")),
    }


def ensure_unity_server_runtime_config() -> Path:
    """
    在 Unity Server 部署目录写入 config.json（不移动 MorphisServer 到 Morphis 仓库内）。
    Unity 会从 <MorphisServer>/config.json 读取配置。
    """
    config_path = get_unity_server_root() / "config.json"
    payload = build_unity_server_config_payload()
    new_content = json.dumps(payload, indent=2, ensure_ascii=False) + "\n"

    if config_path.exists():
        existing = config_path.read_text(encoding="utf-8")
        if existing.strip() == new_content.strip():
            return config_path

    config_path.write_text(new_content, encoding="utf-8")
    return config_path


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
