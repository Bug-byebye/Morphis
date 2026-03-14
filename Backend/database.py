"""
数据库连接配置
使用 SQLAlchemy 2.0（同步模式）
"""
from sqlalchemy import create_engine
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker
from config import get_database_url

# 从 deploy/server-config.json 获取数据库 URL
DATABASE_URL = get_database_url()

# 创建数据库引擎
engine = create_engine(
    DATABASE_URL,
    pool_pre_ping=True,  # 连接前检查连接是否有效
    pool_size=10,
    max_overflow=20,
    echo=False  # 设置为 True 可以打印 SQL 语句（调试用）
)

# 创建会话工厂
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

# 声明基类
Base = declarative_base()


def get_db():
    """
    获取数据库会话（依赖注入）
    """
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()


def check_connection():
    """
    尝试连接数据库；失败则抛错，用于启动时阻止静默启动。
    """
    from sqlalchemy import text
    with engine.connect() as conn:
        conn.execute(text("SELECT 1"))
    print("[Database] Connection check OK")


def init_db():
    """
    初始化数据库：先校验连接，再创建所有表。连接失败则抛错，不静默启动。
    """
    check_connection()
    from models import user, world, world_snapshot  # 注册到 Base.metadata
    Base.metadata.create_all(bind=engine)
    print("[Database] Tables created successfully")
