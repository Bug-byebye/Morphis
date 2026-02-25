"""
World 进程管理器：负责动态启动/停止 Unity Server 进程
"""
import os
import subprocess
import signal
import time
import psutil
from typing import Optional, Dict, List
from datetime import datetime, timedelta
from sqlalchemy.orm import Session
from models.world import World, WorldStatus
from database import SessionLocal
import threading
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class WorldProcessManager:
    """
    World 进程管理器（单例）
    - 动态启动/停止 Unity Server 进程
    - 端口分配
    - 进程健康检查
    - 自动清理空闲 World
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
        self.base_port = 7777
        self.max_worlds = 50
        self.idle_timeout_minutes = 5  # 空闲 5 分钟后关闭
        self.server_executable = os.getenv("UNITY_SERVER_PATH", "/home/morphis/MorphisServer/Morphis.x86_64")
        self.backend_url = os.getenv("API_BASE_URL", "http://localhost:8000")
        
        # 启动后台清理线程
        self._cleanup_thread = threading.Thread(target=self._cleanup_loop, daemon=True)
        self._cleanup_thread.start()
        
        logger.info(f"[WorldManager] Initialized. Server: {self.server_executable}")
    
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
        
        # 检查服务器可执行文件
        if not os.path.exists(self.server_executable):
            return {
                "status": "error",
                "message": f"Unity Server executable not found: {self.server_executable}"
            }
        
        # 分配端口
        port = self._get_available_port(db)
        if port is None:
            return {"status": "error", "message": "No available ports"}
        
        # 更新状态为启动中
        world.status = WorldStatus.STARTING
        world.port = port
        db.commit()
        
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
            log_dir = "/var/log/morphis-worlds"
            os.makedirs(log_dir, exist_ok=True)
            log_file = os.path.join(log_dir, f"{world_id}.log")
            
            with open(log_file, "a") as log:
                process = subprocess.Popen(
                    cmd,
                    env=env,
                    stdout=log,
                    stderr=subprocess.STDOUT,
                    start_new_session=True  # 独立进程组
                )
            
            # 等待进程启动（最多 10 秒）
            for _ in range(10):
                time.sleep(1)
                if psutil.pid_exists(process.pid):
                    break
            else:
                raise Exception("Process failed to start")
            
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
