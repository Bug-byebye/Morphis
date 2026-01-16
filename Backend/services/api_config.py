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
    MODEL_I2I = os.getenv("ARK_IMAGE_EDIT_MODEL_ID", "doubao-seedream-4-0-250828")
    
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


class TencentHunyuan3DConfig:
    """腾讯混元生3D API 配置"""
    BASE_URL = "https://api.ai3d.cloud.tencent.com"
    SUBMIT_URL = f"{BASE_URL}/v1/ai3d/submit"
    QUERY_URL = f"{BASE_URL}/v1/ai3d/query"
    
    # API Key
    API_KEY = os.getenv("TENCENT_3D_API_KEY", "")
    
    # 轮询配置
    POLL_INTERVAL = 5  # 秒
    MAX_POLL_TIME = 300  # 最大等待时间（秒）
    
    # Mock 模式 - 使用本地缓存模型，不调用真实 API
    MOCK_MODE = os.getenv("HUNYUAN3D_MOCK_MODE", "false").lower() == "true"
    
    # 缓存模型 - 生成成功后自动保存到本地
    CACHE_MODELS = os.getenv("HUNYUAN3D_CACHE_MODELS", "true").lower() == "true"
    
    # 缓存目录
    CACHE_DIR = Path(os.getenv("HUNYUAN3D_CACHE_DIR", "./output/models"))
    
    # Mock 模式下使用的默认模型
    DEFAULT_MOCK_MODEL = Path(os.getenv("HUNYUAN3D_MOCK_MODEL", "./test_cube.glb"))
    
    @classmethod
    def ensure_cache_dir(cls):
        """确保缓存目录存在"""
        cls.CACHE_DIR.mkdir(parents=True, exist_ok=True)
        return cls.CACHE_DIR
