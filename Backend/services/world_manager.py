"""
World 进程管理器：负责动态启动/停止 Unity Server 进程
"""
import os
import subprocess
import time
import psutil
from pathlib import Path
from typing import Optional, Dict, List
from datetime import datetime, timedelta
from sqlalchemy.orm import Session
from models.world import World, WorldStatus
from database import SessionLocal
from config import (
    get_api_base_url,
    get_unity_server_executable,
    get_unity_server_root,
    get_unity_server_log_directory,
    get_unity_server_config,
    ensure_unity_server_runtime_config,
    ensure_unity_server_permissions,
)
import threading
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class WorldProcessManager:
    """
    World 进程管理器（单例）
    - 动态启动/停止 Unity Server 进程
    - MorphisServer 与 Morphis 仓库分离部署，通过配置指向独立目录
    """
    
    _instance = None
    _lock = threading.Lock()
    
    def __new__(cls):
        if cls._instance is None:
            with cls._lock:
                if cls._instance is None:
                    cls._instance = super().__new__(cls)
                    cls._instance._initialized = False
        return cls._instance
    
    def __init__(self):
        if self._initialized:
            return
        
        self._initialized = True
        unity_cfg = get_unity_server_config()
        self.base_port = int(unity_cfg.get("BasePort", 7777))
        self.max_worlds = int(unity_cfg.get("MaxWorlds", 50))
        self.idle_timeout_minutes = int(unity_cfg.get("IdleTimeoutMinutes", 5))
        self.server_executable = get_unity_server_executable()
        self.server_root = get_unity_server_root()
        self.log_dir = get_unity_server_log_directory()
        self.backend_url = get_api_base_url()

        try:
            ensure_unity_server_permissions()
            config_path = ensure_unity_server_runtime_config()
            logger.info(f"[WorldManager] Unity runtime config: {config_path}")
        except Exception as e:
            logger.warning(f"[WorldManager] Failed to prepare Unity config.json: {e}")
        
        # 启动后台清理线程
        self._cleanup_thread = threading.Thread(target=self._cleanup_loop, daemon=True)
        self._cleanup_thread.start()
        
        logger.info(
            "[WorldManager] Initialized. Server: %s, Root: %s, Logs: %s",
            self.server_executable,
            self.server_root,
            self.log_dir,
        )
    
    def _validate_server_installation(self) -> Optional[str]:
        ensure_unity_server_permissions()
        executable = Path(self.server_executable)
        if not executable.is_file():
            return f"Unity Server executable not found: {self.server_executable}"

        if not os.access(executable, os.X_OK):
            return f"Unity Server executable is not executable: {self.server_executable}"

        data_dir = self.server_root / "Morphis_Data"
        if not data_dir.is_dir():
            return f"Unity Server data directory not found: {data_dir}"

        player_lib = self.server_root / "UnityPlayer.so"
        if not player_lib.is_file():
            return f"UnityPlayer.so not found in server root: {player_lib}"

        return None

    def _read_log_tail(self, log_file: Path, max_lines: int = 20) -> str:
        if not log_file.exists():
            return ""
        try:
            lines = log_file.read_text(encoding="utf-8", errors="replace").splitlines()
            return "\n".join(lines[-max_lines:])
        except OSError:
            return ""

    def _get_available_port(self, db: Session) -> Optional[int]:
        """分配可用端口"""
        used_ports = set()
        for world in db.query(World).filter(World.port.isnot(None)).all():
            used_ports.add(world.port)
        
        for port in range(self.base_port, self.base_port + self.max_worlds):
            if port not in used_ports:
                return port
        return None
    
    def start_world(self, db: Session, world_id: str) -> Dict:
        """
        启动 World 进程
        返回: {"status": "ok/error", "port": int, "message": str}
        """
        world = db.query(World).filter(World.id == world_id).first()
        if not world:
            return {"status": "error", "message": f"World '{world_id}' not found"}
        
        # 检查是否已运行
        if world.status == WorldStatus.RUNNING:
            if world.process_id and psutil.pid_exists(world.process_id):
                return {
                    "status": "ok",
                    "port": world.port,
                    "message": "World already running"
                }
            else:
                # 进程已死，重置状态
                world.status = WorldStatus.STOPPED
                world.process_id = None
                db.commit()
        
        install_error = self._validate_server_installation()
        if install_error:
            return {"status": "error", "message": install_error}

        try:
            ensure_unity_server_runtime_config()
        except Exception as e:
            return {
                "status": "error",
                "message": f"Failed to prepare Unity Server config.json: {e}",
            }
        
        # 分配端口
        port = self._get_available_port(db)
        if port is None:
            return {"status": "error", "message": "No available ports"}
        
        # 更新状态为启动中
        world.status = WorldStatus.STARTING
        world.port = port
        db.commit()
        
        log_file = self.log_dir / f"{world_id}.log"
        try:
            # 启动 Unity Server 进程
            # 命令: ./Morphis.x86_64 --mode=server --worldId=xxx -batchmode -nographics
            cmd = [
                self.server_executable,
                "--mode=server",
                f"--worldId={world_id}",
                "-batchmode",
                "-nographics"
            ]
            
            # 设置环境变量（传递端口和 Backend URL）
            env = os.environ.copy()
            env["WORLD_PORT"] = str(port)
            env["API_BASE_URL"] = self.backend_url
            
            # 日志文件
            self.log_dir.mkdir(parents=True, exist_ok=True)
            
            with open(log_file, "a", encoding="utf-8") as log:
                log.write(
                    f"\n[{datetime.utcnow().isoformat()}] Starting world '{world_id}' "
                    f"cwd={self.server_root} port={port}\n"
                )
                process = subprocess.Popen(
                    cmd,
                    cwd=str(self.server_root),
                    env=env,
                    stdout=log,
                    stderr=subprocess.STDOUT,
                    start_new_session=True,
                )
            
            # 等待进程启动（最多 10 秒）
            for _ in range(10):
                time.sleep(1)
                if process.poll() is not None:
                    tail = self._read_log_tail(log_file)
                    raise RuntimeError(
                        f"Process exited early with code {process.returncode}. "
                        f"Log tail:\n{tail}"
                    )
                if psutil.pid_exists(process.pid):
                    break
            else:
                raise RuntimeError("Process failed to start within timeout")
            
            # 更新数据库
            world.status = WorldStatus.RUNNING
            world.process_id = process.pid
            world.last_active_at = datetime.utcnow()
            db.commit()
            
            logger.info(f"[WorldManager] Started world '{world_id}' on port {port}, PID {process.pid}")
            
            return {
                "status": "ok",
                "port": port,
                "pid": process.pid,
                "message": f"World started on port {port}"
            }
        
        except Exception as e:
            logger.error(f"[WorldManager] Failed to start world '{world_id}': {e}")
            world.status = WorldStatus.ERROR
            db.commit()
            return {"status": "error", "message": str(e)}
    
    def stop_world(self, db: Session, world_id: str, force: bool = False) -> Dict:
        """
        停止 World 进程
        force: 是否强制终止（SIGKILL）
        """
        world = db.query(World).filter(World.id == world_id).first()
        if not world:
            return {"status": "error", "message": f"World '{world_id}' not found"}
        
        if world.status == WorldStatus.STOPPED:
            return {"status": "ok", "message": "World already stopped"}
        
        if not world.process_id:
            world.status = WorldStatus.STOPPED
            db.commit()
            return {"status": "ok", "message": "No process to stop"}
        
        try:
            if not psutil.pid_exists(world.process_id):
                world.status = WorldStatus.STOPPED
                world.process_id = None
                world.port = None
                db.commit()
                return {"status": "ok", "message": "Process already dead"}
            
            # 更新状态
            world.status = WorldStatus.STOPPING
            db.commit()
            
            # 终止进程
            process = psutil.Process(world.process_id)
            if force:
                process.kill()  # SIGKILL
            else:
                process.terminate()  # SIGTERM
                # 等待最多 10 秒
                try:
                    process.wait(timeout=10)
                except psutil.TimeoutExpired:
                    process.kill()
            
            # 更新数据库
            world.status = WorldStatus.STOPPED
            world.process_id = None
            world.port = None
            world.player_count = 0
            db.commit()
            
            logger.info(f"[WorldManager] Stopped world '{world_id}'")
            
            return {"status": "ok", "message": "World stopped"}
        
        except Exception as e:
            logger.error(f"[WorldManager] Failed to stop world '{world_id}': {e}")
            return {"status": "error", "message": str(e)}
    
    def get_world_status(self, db: Session, world_id: str) -> Dict:
        """获取 World 状态"""
        world = db.query(World).filter(World.id == world_id).first()
        if not world:
            return {"status": "error", "message": "World not found"}
        
        # 检查进程是否真的在运行
        if world.status == WorldStatus.RUNNING and world.process_id:
            if not psutil.pid_exists(world.process_id):
                world.status = WorldStatus.STOPPED
                world.process_id = None
                world.port = None
                db.commit()
        
        return {
            "status": "ok",
            "world_id": world.id,
            "world_status": world.status.value,
            "port": world.port,
            "player_count": world.player_count,
            "process_id": world.process_id,
            "last_active": world.last_active_at.isoformat() if world.last_active_at else None
        }
    
    def update_player_count(self, db: Session, world_id: str, count: int):
        """更新玩家数量"""
        world = db.query(World).filter(World.id == world_id).first()
        if world:
            world.player_count = count
            world.last_active_at = datetime.utcnow()
            db.commit()
    
    def _cleanup_loop(self):
        """后台清理线程：关闭空闲 World"""
        while True:
            try:
                time.sleep(60)  # 每分钟检查一次
                self._cleanup_idle_worlds()
            except Exception as e:
                logger.error(f"[WorldManager] Cleanup error: {e}")
    
    def _cleanup_idle_worlds(self):
        """清理空闲 World"""
        db = SessionLocal()
        try:
            threshold = datetime.utcnow() - timedelta(minutes=self.idle_timeout_minutes)
            
            idle_worlds = db.query(World).filter(
                World.status == WorldStatus.RUNNING,
                World.player_count == 0,
                World.last_active_at < threshold
            ).all()
            
            for world in idle_worlds:
                logger.info(f"[WorldManager] Cleaning up idle world: {world.id}")
                self.stop_world(db, world.id)
        
        finally:
            db.close()
    
    def list_all_worlds(self, db: Session) -> List[Dict]:
        """列出所有 World 状态"""
        worlds = db.query(World).all()
        result = []
        for world in worlds:
            # 验证进程状态
            if world.status == WorldStatus.RUNNING and world.process_id:
                if not psutil.pid_exists(world.process_id):
                    world.status = WorldStatus.STOPPED
                    world.process_id = None
                    world.port = None
                    db.commit()
            
            result.append({
                "id": world.id,
                "name": world.name,
                "status": world.status.value,
                "port": world.port,
                "player_count": world.player_count,
                "process_id": world.process_id
            })
        
        return result


# 全局单例
_manager = None

def get_world_manager() -> WorldProcessManager:
    """获取 World 管理器单例"""
    global _manager
    if _manager is None:
        _manager = WorldProcessManager()
    return _manager
