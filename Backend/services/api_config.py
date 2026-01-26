"""
API Configuration
=================
API 配置和基础设置
"""

import os
from pathlib import Path


class BaseConfig:
    """基础配置"""
    OUTPUT_DIR = Path(os.getenv("OUTPUT_DIR", "./output"))
    MODEL_DIR = Path(os.getenv("MODEL_DIR", "./assets/models"))
    API_TIMEOUT = int(os.getenv("API_TIMEOUT", "300"))
    DEBUG_MODE = os.getenv("DEBUG_MODE", "false").lower() == "true"
    
    @classmethod
    def ensure_output_dir(cls):
        """确保输出目录存在"""
        cls.OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        return cls.OUTPUT_DIR


class ArkImageGenConfig:
    """ARK 图像生成 API 配置"""
    BASE_URL = "https://ark.cn-beijing.volces.com/api/v3"
    
    # 模型 ID - 从环境变量读取
    MODEL_T2I = os.getenv("ARK_IMAGE_MODEL_ID", "doubao-seedream-4-0-250828")
    MODEL_I2I = os.getenv("ARK_IMAGE_EDIT_MODEL_ID", "doubao-seedream-4-5-251128")
    
    # 默认参数
    DEFAULT_NUM_IMAGES = int(os.getenv("DEFAULT_NUM_IMAGES", "1"))
    DEFAULT_IMAGE_SIZE = os.getenv("DEFAULT_IMAGE_SIZE", "1:1")
    SIZE = "1024x1024"  # API 使用的尺寸格式
    RESPONSE_FORMAT = "url"
    SEED = -1
    SEQ_IMAGE_GENERATION = False
    SEQ_OPTIONS = {}
    STREAM = False
    GUIDANCE_SCALE = 7.5
    WATERMARK = False
    OPTIMIZE_PROMPT_OPTIONS = {}


class DoubaoSeed3DConfig:
    """豆包 Seed3D (火山引擎) API 配置"""
    BASE_URL = "https://ark.cn-beijing.volces.com/api/v3"
    
    # 模型 ID
    MODEL_ID = os.getenv("ARK_3D_MODEL_ID", "doubao-seed3d-1-0-250928")
    
    # 轮询配置
    POLL_INTERVAL = 5  # 秒 - Seed3D 生成较慢，但用短轮询保持连接活跃
    MAX_POLL_TIME = 600  # 最大等待时间（秒）
    
    # 默认参数
    SUBDIVISION_LEVEL = os.getenv("SEED3D_SUBDIVISION_LEVEL", "medium")  # low, medium, high
    FILE_FORMAT = os.getenv("SEED3D_FILE_FORMAT", "glb")  # glb, obj, fbx
    
    # Mock 模式 - 使用本地缓存模型，不调用真实 API
    MOCK_MODE = os.getenv("SEED3D_MOCK_MODE", "true").lower() == "true"
    
    # 缓存模型 - 生成成功后自动保存到本地
    CACHE_MODELS = os.getenv("SEED3D_CACHE_MODELS", "true").lower() == "true"
    
    # 缓存目录
    CACHE_DIR = Path(os.getenv("SEED3D_CACHE_DIR", "./output/models"))
    
    # Mock 模式下使用的默认模型
    DEFAULT_MOCK_MODEL = Path(os.getenv("SEED3D_MOCK_MODEL", "./test_cube.glb"))
    
    @classmethod
    def ensure_cache_dir(cls):
        """确保缓存目录存在"""
        cls.CACHE_DIR.mkdir(parents=True, exist_ok=True)
        return cls.CACHE_DIR


class TrellisConfig:
    """Trellis 3D Generation Server Configuration"""
    BASE_URL = os.getenv("TRELLIS_SERVER_URL", "http://localhost:8001")
    TIMEOUT = int(os.getenv("TRELLIS_TIMEOUT", "600"))
