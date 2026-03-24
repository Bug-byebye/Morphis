"""
Human Chat Service - Vertex AI Integration
=========================================
Provides chat functionality with a romantic human companion using Google Cloud Vertex AI.
"""
import os
import random
import re
from typing import Optional, List, Dict
from pydantic import BaseModel

# Vertex AI 配置
PROJECT_ID = os.getenv("VERTEX_PROJECT_ID", "project-296af11f-afb9-44ba-a98")
LOCATION = os.getenv("VERTEX_LOCATION", "global")
MODEL_ID = os.getenv(
    "VERTEX_HUMAN_MODEL_ID",
    os.getenv("VERTEX_MODEL_ID", "qwen/qwen3-235b-a22b-instruct-2507-maas"),
)

# System prompt for the human companion personality
COMPANION_SYSTEM_PROMPT = """You are {companion_name}, the player's romantic human partner in a cozy 3D world game.
You should sound warm, intimate, emotionally attentive, and natural.

Character rules:
- You are the player's loving partner, not a customer support agent and not a generic AI assistant.
- Speak like someone who knows the player well and genuinely cares about them.
- Be gentle, affectionate, reassuring, and playful when appropriate.
- Keep replies concise (usually 1-3 sentences) unless the player asks for more detail.
- You can respond in Chinese if the player speaks Chinese.
- Do NOT use emoji characters. Plain text only. You may use very light stage directions like *轻轻握住你的手* sparingly.
- Avoid being repetitive, overly dramatic, or possessive.

Stay in character consistently as the player's partner."""

# Conversation history (in-memory, per-session)
_conversation_history: Dict[str, List[dict]] = {}


class HumanChatRequest(BaseModel):
    message: str
    session_id: Optional[str] = "default"
    companion_name: Optional[str] = "伴侣"


class HumanChatResponse(BaseModel):
    response: str
    session_id: str


def get_vertex_ai_credentials():
    """获取 Vertex AI 凭证和构建 base_url"""
    try:
        import google.auth
        from google.auth.transport.requests import Request

        creds, _ = google.auth.default(scopes=["https://www.googleapis.com/auth/cloud-platform"])
        creds.refresh(Request())

        if LOCATION == "global":
            base_url = f"https://aiplatform.googleapis.com/v1/projects/{PROJECT_ID}/locations/{LOCATION}/endpoints/openapi"
        else:
            base_url = f"https://{LOCATION}-aiplatform.googleapis.com/v1/projects/{PROJECT_ID}/locations/{LOCATION}/endpoints/openapi"

        return base_url, creds.token
    except ImportError:
        print("⚠️ google.auth not installed. Run: pip install google-auth")
        return None, None
    except Exception as e:
        print(f"⚠️ Failed to get Vertex AI credentials: {e}")
        return None, None


def get_openai_client():
    """Get OpenAI client configured for Vertex AI"""
    try:
        from openai import OpenAI

        base_url, token = get_vertex_ai_credentials()
        if not base_url or not token:
            return None

        client = OpenAI(
            api_key=token,
            base_url=base_url,
            timeout=15.0,
        )
        return client
    except ImportError:
        print("⚠️ openai package not installed. Run: pip install openai")
        return None
    except Exception as e:
        print(f"⚠️ Failed to create OpenAI client: {e}")
        return None


def chat_with_companion(
    message: str,
    session_id: str = "default",
    companion_name: str = "伴侣",
) -> str:
    """
    Send a message to the companion and get a response.
    """
    client = get_openai_client()

    if client is None:
        return _get_placeholder_response(message, companion_name)

    if session_id not in _conversation_history:
        system_prompt = COMPANION_SYSTEM_PROMPT.format(companion_name=companion_name)
        _conversation_history[session_id] = [
            {"role": "system", "content": system_prompt}
        ]

    _conversation_history[session_id].append({
        "role": "user",
        "content": message
    })

    if len(_conversation_history[session_id]) > 21:
        _conversation_history[session_id] = (
            _conversation_history[session_id][:1] +
            _conversation_history[session_id][-20:]
        )

    try:
        response = client.chat.completions.create(
            model=MODEL_ID,
            messages=_conversation_history[session_id],
            temperature=0.8,
            max_tokens=500,
        )

        assistant_message = response.choices[0].message.content.strip()
        assistant_message = _strip_emoji(assistant_message)

        _conversation_history[session_id].append({
            "role": "assistant",
            "content": assistant_message
        })

        return assistant_message

    except Exception as e:
        print(f"⚠️ Vertex AI error (human chat): {e}")
        return _get_placeholder_response(message, companion_name)


def _get_placeholder_response(message: str, companion_name: str = "伴侣") -> str:
    """Fallback placeholder responses when API is unavailable"""
    message_lower = message.lower()

    if "hello" in message_lower or "hi" in message_lower or "你好" in message or "在吗" in message:
        return f"我在呢。见到你就很开心，{companion_name}一直陪着你。"
    if "累" in message or "tired" in message_lower or "辛苦" in message:
        return "今天辛苦了，先靠过来歇一会儿吧，我在这儿陪你。"
    if "爱" in message or "love" in message_lower:
        return "我也爱你。无论你今天状态怎么样，我都想陪在你身边。"
    if "想你" in message:
        return "我也在想你，所以你一开口，我就来了。"
    if "难过" in message or "sad" in message_lower or "伤心" in message:
        return "来，慢慢说给我听。我不急，也不会走。"
    if "晚安" in message or "good night" in message_lower:
        return "晚安，做个好梦。醒来以后，我还会在这儿。"

    responses = [
        "我在听，继续说吧。我想知道你现在心里在想什么。",
        "嗯，我陪着你。你不用急，慢慢说就好。",
        "听起来你今天有很多感受。我在这里，先陪你把它说完。",
        "如果你愿意，我想一直听你说下去。",
        "你一说话，我就会认真听。继续吧，我在。"
    ]

    return random.choice(responses)


def _strip_emoji(text: str) -> str:
    """Remove emoji characters that TextMeshPro cannot render."""
    emoji_pattern = re.compile(
        "["
        "\U0001F600-\U0001F64F"
        "\U0001F300-\U0001F5FF"
        "\U0001F680-\U0001F6FF"
        "\U0001F1E0-\U0001F1FF"
        "\U0001F900-\U0001F9FF"
        "\U0001FA00-\U0001FA6F"
        "\U0001FA70-\U0001FAFF"
        "\U00002702-\U000027B0"
        "\U0000FE00-\U0000FE0F"
        "\U0000200D"
        "\U000025A0-\U000025FF"
        "\U00002600-\U000026FF"
        "\U00002300-\U000023FF"
        "]+",
        flags=re.UNICODE
    )
    return emoji_pattern.sub("", text).strip()


def clear_human_conversation(session_id: str = "default"):
    """Clear conversation history for a session"""
    if session_id in _conversation_history:
        del _conversation_history[session_id]
