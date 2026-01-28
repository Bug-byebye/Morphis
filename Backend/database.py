"""
数据库连接配置
使用 SQLAlchemy 2.0（同步模式）
"""
import os
from sqlalchemy import create_engine
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker
from dotenv import load_dotenv

load_dotenv()

# 从环境变量获取数据库 URL
DATABASE_URL = os.getenv(
    "DATABASE_URL",
    "postgresql://postgres:postgres@localhost:5432/morphis"
)

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


def init_db():
    """
    初始化数据库（创建所有表）
    """
    from models import world_snapshot  # 导入模型以注册到 Base.metadata
    Base.metadata.create_all(bind=engine)
    print("[Database] Tables created successfully")
