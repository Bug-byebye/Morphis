
import httpx
import os
import time
from urllib.parse import urljoin
from typing import Dict, Any, Optional

from .api_config import TrellisConfig

async def generate_3d(endpoint: str, data: Dict[str, Any] = None, files: Dict[str, Any] = None) -> bytes:
    """
    通用函数：调用 Trellis 服务生成 3D 模型
    
    Args:
        endpoint: API 端点 (e.g., "/trellis-text-to-3d")
        data: 表单数据
        files: 上传文件
        
    Returns:
        GLB 文件内容 (bytes)
    """
    url = urljoin(TrellisConfig.BASE_URL, endpoint)
    print(f"[Trellis] Calling {url}...")
    
    async with httpx.AsyncClient(timeout=TrellisConfig.TIMEOUT) as client:
        # 1. 发送生成请求
        if files:
            response = await client.post(url, data=data, files=files)
        else:
            response = await client.post(url, data=data)
            
        response.raise_for_status()
        result = response.json()
        
        job_id = result.get("job_id")
        if not job_id:
            raise ValueError(f"No job_id returned from Trellis server: {result}")
            
        print(f"[Trellis] Job finished! ID: {job_id}")
        
        # 2. 获取 GLB 下载链接
        # The server returns paths like {"glb": "/download/uuid/filename.glb"}
        glb_path = result.get("glb")
        if not glb_path:
            raise ValueError(f"No GLB path in response: {result}")
            
        download_url = urljoin(TrellisConfig.BASE_URL, glb_path)
        print(f"[Trellis] Downloading GLB from {download_url}...")
        
        # 3. 下载文件
        glb_response = await client.get(download_url)
        glb_response.raise_for_status()
        
        return glb_response.content
