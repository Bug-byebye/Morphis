import os
import asyncio
import httpx
import hashlib
from datetime import datetime
from typing import Optional, Dict, Any
from pathlib import Path

from .api_config import TencentHunyuan3DConfig, BaseConfig


async def generate(prompt: str, format: str = "glb") -> bytes:
    """
    根据文本生成 3D 模型
    
    Args:
        prompt: 文本提示词
        format: 输出格式 ("glb", "obj", "fbx")
    
    Returns:
        3D 模型的 bytes 数据 (GLB 格式)
    """
    # Mock 模式：直接返回本地模型
    if TencentHunyuan3DConfig.MOCK_MODE:
        return await load_mock_model(prompt, format)
    
    api_key = TencentHunyuan3DConfig.API_KEY
    if not api_key:
        raise ValueError("缺少 TENCENT_3D_API_KEY 环境变量")
    
    print(f"[Text23D] Generating 3D model for: {prompt}")
    
    # 1. 提交生成任务
    job_id = await submit_task(prompt=prompt, api_key=api_key)
    print(f"[Text23D] Task submitted, JobId: {job_id}")
    
    # 2. 轮询等待结果
    result = await poll_task(job_id, api_key)
    
    # 3. 从结果中提取模型 URL
    model_url = extract_model_url(result, format)
    if not model_url:
        raise Exception(f"No model URL in response: {result}")
    
    print(f"[Text23D] Downloading model from: {model_url}")
    model_data = await download_model(model_url)
    
    print(f"[Text23D] Model downloaded, size: {len(model_data)} bytes")
    
    # 自动缓存模型
    if TencentHunyuan3DConfig.CACHE_MODELS:
        saved_path = save_model_to_cache(model_data, prompt, format, source="text23d")
        print(f"[Text23D] Model cached to: {saved_path}")
    
    return model_data


async def load_mock_model(prompt: str, format: str) -> bytes:
    """加载 Mock 模型（本地缓存）"""
    # 首先尝试找最近缓存的模型
    cache_dir = TencentHunyuan3DConfig.CACHE_DIR
    if cache_dir.exists():
        # 找到最新的 .glb 文件
        glb_files = list(cache_dir.glob(f"*.{format}"))
        if glb_files:
            latest_file = max(glb_files, key=lambda f: f.stat().st_mtime)
            print(f"[Text23D] MOCK MODE: Loading cached model: {latest_file}")
            return latest_file.read_bytes()
    
    # 使用默认 Mock 模型
    mock_model_path = TencentHunyuan3DConfig.DEFAULT_MOCK_MODEL
    if mock_model_path.exists():
        print(f"[Text23D] MOCK MODE: Loading default model: {mock_model_path}")
        return mock_model_path.read_bytes()
    
    raise FileNotFoundError(f"No mock model found. Please set HUNYUAN3D_MOCK_MODEL or cache a model first.")


def save_model_to_cache(model_data: bytes, prompt: str, format: str, source: str = "unknown") -> Path:
    """保存模型到缓存目录"""
    cache_dir = TencentHunyuan3DConfig.ensure_cache_dir()
    
    # 生成文件名：时间戳 + prompt摘要
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    prompt_hash = hashlib.md5(prompt.encode()).hexdigest()[:8]
    safe_prompt = "".join(c if c.isalnum() else "_" for c in prompt[:30])
    filename = f"{source}_{timestamp}_{safe_prompt}_{prompt_hash}.{format}"
    
    file_path = cache_dir / filename
    file_path.write_bytes(model_data)
    
    return file_path


async def submit_task(prompt: str = None, image_url: str = None, api_key: str = None) -> str:
    """
    提交生成任务
    
    Args:
        prompt: 文本提示词 (text23d)
        image_url: 图片 URL 或 base64 (image23d)
        api_key: API Key
    
    Returns:
        JobId
    """
    headers = {
        "Authorization": api_key,
        "Content-Type": "application/json"
    }
    
    # 构建请求体
    data = {}
    if prompt:
        data["Prompt"] = prompt
    if image_url:
        data["ImageUrl"] = {"Url": image_url}
    
    async with httpx.AsyncClient(timeout=60) as client:
        response = await client.post(
            TencentHunyuan3DConfig.SUBMIT_URL,
            headers=headers,
            json=data
        )
        
        if response.status_code != 200:
            raise Exception(f"Submit failed: {response.status_code} - {response.text}")
        
        result = response.json()
        print(f"[Hunyuan3D] Submit response: {result}")
        
        # 获取 JobId
        job_id = result.get("JobId") or result.get("Response", {}).get("JobId")
        if not job_id:
            raise Exception(f"No JobId in response: {result}")
        
        return job_id


async def poll_task(job_id: str, api_key: str) -> Dict[str, Any]:
    """
    轮询查询任务状态
    
    Args:
        job_id: 任务 ID
        api_key: API Key
    
    Returns:
        包含模型 URL 的结果
    """
    headers = {
        "Authorization": api_key,
        "Content-Type": "application/json"
    }
    
    elapsed = 0
    poll_interval = TencentHunyuan3DConfig.POLL_INTERVAL
    max_time = TencentHunyuan3DConfig.MAX_POLL_TIME
    
    async with httpx.AsyncClient(timeout=30) as client:
        while elapsed < max_time:
            response = await client.post(
                TencentHunyuan3DConfig.QUERY_URL,
                headers=headers,
                json={"JobId": job_id}
            )
            
            if response.status_code != 200:
                raise Exception(f"Query failed: {response.status_code} - {response.text}")
            
            result = response.json()
            print(f"[Hunyuan3D] Query response: {result}")
            
            # 检查状态
            status = result.get("Status") or result.get("Response", {}).get("Status")
            
            if status in ["SUCCEEDED", "SUCCESS", "DONE"]:
                print(f"[Hunyuan3D] Task completed with status: {status}")
                return result.get("Response", result)
            elif status in ["FAILED", "FAIL"]:
                error = result.get("ErrorMessage") or result.get("Response", {}).get("ErrorMessage", "Unknown error")
                raise Exception(f"Task failed: {error}")
            elif status in ["PENDING", "RUNNING", "PROCESSING", "SUBMITTED"]:
                print(f"[Hunyuan3D] Status: {status}, waiting {poll_interval}s...")
                await asyncio.sleep(poll_interval)
                elapsed += poll_interval
            else:
                # 未知状态，继续等待
                print(f"[Hunyuan3D] Unknown status: {status}, waiting...")
                await asyncio.sleep(poll_interval)
                elapsed += poll_interval
    
    raise TimeoutError(f"Task timed out after {max_time} seconds")


async def download_model(url: str) -> bytes:
    """下载模型文件"""
    async with httpx.AsyncClient(timeout=120) as client:
        response = await client.get(url)
        response.raise_for_status()
        return response.content


def extract_model_url(result: Dict[str, Any], format: str = "glb") -> Optional[str]:
    """
    从 API 响应中提取模型 URL
    
    Hunyuan3D API 返回格式:
    {
        "Status": "DONE",
        "ResultFile3Ds": [
            {"Type": "OBJ", "Url": "...", "PreviewImageUrl": "..."},
            {"Type": "GLB", "Url": "...", "PreviewImageUrl": "..."}
        ]
    }
    
    Args:
        result: API 响应结果
        format: 期望的格式 ("glb", "obj")
    
    Returns:
        模型下载 URL
    """
    # 尝试从 ResultFile3Ds 数组中提取
    result_files = result.get("ResultFile3Ds", [])
    if result_files:
        # 优先查找请求的格式
        target_type = format.upper()
        for file_info in result_files:
            if file_info.get("Type", "").upper() == target_type:
                url = file_info.get("Url")
                if url:
                    print(f"[Hunyuan3D] Found {target_type} model URL")
                    return url
        
        # 如果没找到指定格式，优先返回 GLB，其次 OBJ
        for preferred_type in ["GLB", "OBJ"]:
            for file_info in result_files:
                if file_info.get("Type", "").upper() == preferred_type:
                    url = file_info.get("Url")
                    if url:
                        print(f"[Hunyuan3D] Using {preferred_type} model URL (fallback)")
                        return url
        
        # 最后返回第一个可用的 URL
        if result_files and result_files[0].get("Url"):
            print(f"[Hunyuan3D] Using first available model URL")
            return result_files[0]["Url"]
    
    # 兼容旧格式：直接从顶层字段获取
    model_url = result.get("GlbUrl") or result.get("ModelUrl") or result.get("Url")
    if model_url:
        print(f"[Hunyuan3D] Using legacy model URL format")
        return model_url
    
    return None


# ========== 同步接口（供直接调用）==========

def generate_sync(prompt: str, format: str = "glb") -> bytes:
    """同步版本的生成接口"""
    return asyncio.run(generate(prompt, format))
