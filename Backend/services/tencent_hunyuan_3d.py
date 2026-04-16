"""
Tencent Hunyuan 3D Rapid Client
===============================
对接腾讯云混元生 3D 极速版：
- SubmitHunyuanTo3DRapidJob
- QueryHunyuanTo3DRapidJob
"""

import base64
import asyncio
import hashlib
import hmac
import json
import time
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

import httpx

from .api_config import BaseConfig, TencentHunyuan3DConfig


def _sha256_hex(content: str) -> str:
    return hashlib.sha256(content.encode("utf-8")).hexdigest()


def _sign(key: bytes, msg: str) -> bytes:
    return hmac.new(key, msg.encode("utf-8"), hashlib.sha256).digest()


def _build_authorization(action: str, payload: str, timestamp: int) -> str:
    config = TencentHunyuan3DConfig
    date = datetime.fromtimestamp(timestamp, tz=timezone.utc).strftime("%Y-%m-%d")

    canonical_headers = (
        "content-type:application/json; charset=utf-8\n"
        f"host:{config.HOST}\n"
        f"x-tc-action:{action.lower()}\n"
    )
    signed_headers = "content-type;host;x-tc-action"
    canonical_request = "\n".join([
        "POST",
        "/",
        "",
        canonical_headers,
        signed_headers,
        _sha256_hex(payload),
    ])

    credential_scope = f"{date}/{config.SERVICE}/tc3_request"
    string_to_sign = "\n".join([
        "TC3-HMAC-SHA256",
        str(timestamp),
        credential_scope,
        _sha256_hex(canonical_request),
    ])

    secret_date = _sign(("TC3" + config.SECRET_KEY).encode("utf-8"), date)
    secret_service = _sign(secret_date, config.SERVICE)
    secret_signing = _sign(secret_service, "tc3_request")
    signature = hmac.new(
        secret_signing,
        string_to_sign.encode("utf-8"),
        hashlib.sha256,
    ).hexdigest()

    return (
        "TC3-HMAC-SHA256 "
        f"Credential={config.SECRET_ID}/{credential_scope}, "
        f"SignedHeaders={signed_headers}, "
        f"Signature={signature}"
    )


async def _request(action: str, body: Dict[str, Any]) -> Dict[str, Any]:
    TencentHunyuan3DConfig.validate_credentials()

    payload = json.dumps(body, ensure_ascii=False, separators=(",", ":"))
    timestamp = int(time.time())
    headers = {
        "Authorization": _build_authorization(action, payload, timestamp),
        "Content-Type": "application/json; charset=utf-8",
        "Host": TencentHunyuan3DConfig.HOST,
        "X-TC-Action": action,
        "X-TC-Version": TencentHunyuan3DConfig.VERSION,
        "X-TC-Timestamp": str(timestamp),
        "X-TC-Region": TencentHunyuan3DConfig.REGION,
    }

    async with httpx.AsyncClient(timeout=BaseConfig.API_TIMEOUT) as client:
        response = await client.post(
            TencentHunyuan3DConfig.ENDPOINT,
            headers=headers,
            content=payload.encode("utf-8"),
        )
        response.raise_for_status()
        data = response.json()

    body = data.get("Response", {})
    api_error = body.get("Error")
    if api_error:
        code = api_error.get("Code", "TencentCloudApiError")
        message = api_error.get("Message", "Unknown Tencent Cloud API error")
        raise RuntimeError(f"{code}: {message}")

    return body


def _normalize_result_format(format_name: Optional[str]) -> str:
    result_format = (format_name or TencentHunyuan3DConfig.RESULT_FORMAT or "GLB").upper()
    if TencentHunyuan3DConfig.ENABLE_GEOMETRY and result_format == "OBJ":
        return "GLB"
    return result_format


async def submit_rapid_job(
    *,
    prompt: Optional[str] = None,
    image_base64: Optional[str] = None,
    image_url: Optional[str] = None,
    result_format: Optional[str] = None,
) -> str:
    if not prompt and not image_base64 and not image_url:
        raise ValueError("prompt、image_base64、image_url 至少需要一个")
    if prompt and (image_base64 or image_url):
        raise ValueError("Prompt 和 ImageBase64/ImageUrl 不能同时存在")

    payload: Dict[str, Any] = {
        "ResultFormat": _normalize_result_format(result_format),
        "EnablePBR": TencentHunyuan3DConfig.ENABLE_PBR,
        "EnableGeometry": TencentHunyuan3DConfig.ENABLE_GEOMETRY,
    }
    if prompt:
        payload["Prompt"] = prompt
    elif image_base64:
        payload["ImageBase64"] = image_base64
    else:
        payload["ImageUrl"] = image_url

    response = await _request("SubmitHunyuanTo3DRapidJob", payload)
    job_id = response.get("JobId")
    if not job_id:
        raise RuntimeError("腾讯云混元 3D 未返回 JobId")
    return job_id


async def query_rapid_job(job_id: str) -> Dict[str, Any]:
    return await _request("QueryHunyuanTo3DRapidJob", {"JobId": job_id})


def _pick_result_file(result_files: List[Dict[str, Any]], desired_format: str) -> Dict[str, Any]:
    desired = desired_format.upper()
    for item in result_files:
        if str(item.get("Type", "")).upper() == desired:
            return item
    for item in result_files:
        if str(item.get("Type", "")).upper() == "GLB":
            return item
    if result_files:
        return result_files[0]
    raise RuntimeError("腾讯云混元 3D 任务已完成，但没有返回可下载文件")


async def wait_for_result(job_id: str, result_format: Optional[str] = None) -> Dict[str, Any]:
    deadline = time.time() + TencentHunyuan3DConfig.MAX_POLL_TIME
    desired_format = _normalize_result_format(result_format)

    while time.time() < deadline:
        result = await query_rapid_job(job_id)
        status = str(result.get("Status", "")).upper()

        if status == "DONE":
            result_files = result.get("ResultFile3Ds") or []
            return _pick_result_file(result_files, desired_format)

        if status == "FAIL":
            error_code = result.get("ErrorCode") or "Hunyuan3DJobFailed"
            error_message = result.get("ErrorMessage") or "任务失败"
            raise RuntimeError(f"{error_code}: {error_message}")

        if status not in ("WAIT", "RUN"):
            raise RuntimeError(f"未知的腾讯云混元 3D 任务状态: {status or 'EMPTY'}")

        await asyncio.sleep(TencentHunyuan3DConfig.POLL_INTERVAL)

    raise TimeoutError("等待腾讯云混元 3D 任务结果超时")


async def download_result_file(url: str) -> bytes:
    async with httpx.AsyncClient(timeout=BaseConfig.API_TIMEOUT) as client:
        response = await client.get(url)
        response.raise_for_status()
        return response.content


async def generate_text_to_3d(prompt: str, format: str = "glb") -> bytes:
    job_id = await submit_rapid_job(prompt=prompt, result_format=format)
    result_file = await wait_for_result(job_id, result_format=format)
    return await download_result_file(result_file["Url"])


async def generate_image_to_3d(image_data: bytes, format: str = "glb") -> bytes:
    image_base64 = base64.b64encode(image_data).decode("utf-8")
    job_id = await submit_rapid_job(image_base64=image_base64, result_format=format)
    result_file = await wait_for_result(job_id, result_format=format)
    return await download_result_file(result_file["Url"])
