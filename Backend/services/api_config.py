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


class TencentHunyuan3DConfig:
    """腾讯云混元生 3D 极速版 API 配置"""
    HOST = os.getenv("TENCENT_AI3D_HOST", "ai3d.tencentcloudapi.com")
    ENDPOINT = f"https://{HOST}"
    SERVICE = "ai3d"
    VERSION = os.getenv("TENCENT_AI3D_VERSION", "2025-05-13")
    REGION = os.getenv("TENCENT_AI3D_REGION", "ap-guangzhou")

    SECRET_ID = os.getenv("TENCENT_SECRET_ID") or os.getenv("TENCENTCLOUD_SECRET_ID", "")
    SECRET_KEY = os.getenv("TENCENT_SECRET_KEY") or os.getenv("TENCENTCLOUD_SECRET_KEY", "")

    # 兼容用户手头可能已有的 OpenAI 风格 key；官方 ai3d 接口实际不会用它做签名。
    API_KEY = os.getenv("TENCENT_HUNYUAN_API_KEY", "")

    RESULT_FORMAT = os.getenv("TENCENT_AI3D_RESULT_FORMAT", "GLB").upper()
    ENABLE_PBR = os.getenv("TENCENT_AI3D_ENABLE_PBR", "false").lower() == "true"
    ENABLE_GEOMETRY = os.getenv("TENCENT_AI3D_ENABLE_GEOMETRY", "false").lower() == "true"

    POLL_INTERVAL = int(os.getenv("TENCENT_AI3D_POLL_INTERVAL", "5"))
    MAX_POLL_TIME = int(os.getenv("TENCENT_AI3D_MAX_POLL_TIME", "600"))

    @classmethod
    def has_credentials(cls) -> bool:
        return bool(cls.SECRET_ID and cls.SECRET_KEY)

    @classmethod
    def validate_credentials(cls):
        if cls.has_credentials():
            return
        if cls.API_KEY:
            raise RuntimeError(
                "检测到 TENCENT_HUNYUAN_API_KEY，但腾讯云 ai3d 官方接口使用 "
                "TC3-HMAC-SHA256 签名，需要 SecretId/SecretKey。"
            )
        raise RuntimeError(
            "缺少腾讯云混元 3D 凭证，请设置 TENCENT_SECRET_ID 和 TENCENT_SECRET_KEY。"
        )


class ThreeDGenerationConfig:
    """统一的 3D 生成提供方选择"""
    PROVIDER = os.getenv("THREED_PROVIDER", "auto").strip().lower()

    @classmethod
    def get_provider(cls) -> str:
        if cls.PROVIDER in ("", "auto"):
            if TencentHunyuan3DConfig.has_credentials():
                return "tencent_hunyuan"
            return "trellis"
        return cls.PROVIDER


class TrellisConfig:
    """Trellis 3D Generation Server Configuration"""
    BASE_URL = os.getenv("TRELLIS_SERVER_URL", "http://localhost:8001")
    TIMEOUT = int(os.getenv("TRELLIS_TIMEOUT", "600"))
