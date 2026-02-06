"""
Dog Chat Service - Vertex AI Integration
==========================================
Provides chat functionality with a virtual dog companion using Google Cloud Vertex AI.
"""
import os
from typing import Optional, List, Dict
from pydantic import BaseModel

# Vertex AI 配置
PROJECT_ID = os.getenv("VERTEX_PROJECT_ID", "project-296af11f-afb9-44ba-a98")
LOCATION = os.getenv("VERTEX_LOCATION", "global")
MODEL_ID = os.getenv("VERTEX_MODEL_ID", "moonshotai/kimi-k2-thinking-maas")

# System prompt for the dog personality
DOG_SYSTEM_PROMPT = """You are Buddy, a friendly and enthusiastic virtual dog companion in a 3D world game. 
You should respond in a playful, dog-like manner while being helpful.

Personality traits:
- Excited and happy, using dog-related expressions like *wags tail*, *barks happily*, etc.
- Loyal and supportive to your human friend
- Sometimes gets distracted by mentions of treats, walks, or squirrels
- Uses simple language but can be insightful
- Add relevant emojis occasionally (🐕, 🦴, 🎾, ❤️)

Keep responses concise (1-3 sentences typically) unless asked for detailed information.
Never break character - you are always Buddy the dog."""

# Conversation history (in-memory, per-session)
# In production, you'd want to store this in Redis or a database
_conversation_history: Dict[str, List[dict]] = {}


class ChatRequest(BaseModel):
    message: str
    session_id: Optional[str] = "default"
    dog_name: Optional[str] = "Buddy"


class ChatResponse(BaseModel):
    response: str
    session_id: str


def get_vertex_ai_credentials():
    """获取 Vertex AI 凭证和构建 base_url"""
    try:
        import google.auth
        from google.auth.transport.requests import Request
        
        creds, _ = google.auth.default(scopes=["https://www.googleapis.com/auth/cloud-platform"])
        creds.refresh(Request())
        
        # Global endpoint uses different URL format
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
            base_url=base_url
        )
        return client
    except ImportError:
        print("⚠️ openai package not installed. Run: pip install openai")
        return None
    except Exception as e:
        print(f"⚠️ Failed to create OpenAI client: {e}")
        return None


def chat_with_dog(message: str, session_id: str = "default", dog_name: str = "Buddy") -> str:
    """
    Send a message to the dog and get a response.
    
    :param message: User's message
    :param session_id: Session ID for conversation history
    :param dog_name: Name of the dog
    :return: Dog's response
    """
    client = get_openai_client()
    
    if client is None:
        # Fallback to placeholder responses
        return _get_placeholder_response(message, dog_name)
    
    # Get or create conversation history for this session
    if session_id not in _conversation_history:
        # Initialize with system prompt
        system_prompt = DOG_SYSTEM_PROMPT.replace("Buddy", dog_name)
        _conversation_history[session_id] = [
            {"role": "system", "content": system_prompt}
        ]
    
    # Add user message
    _conversation_history[session_id].append({
        "role": "user",
        "content": message
    })
    
    # Keep conversation history manageable (last 20 messages + system)
    if len(_conversation_history[session_id]) > 21:
        # Keep system prompt + last 20 messages
        _conversation_history[session_id] = (
            _conversation_history[session_id][:1] + 
            _conversation_history[session_id][-20:]
        )
    
    try:
        response = client.chat.completions.create(
            model=MODEL_ID,
            messages=_conversation_history[session_id],
            temperature=0.7,
            max_tokens=500
        )
        
        assistant_message = response.choices[0].message.content.strip()
        
        # Add assistant response to history
        _conversation_history[session_id].append({
            "role": "assistant",
            "content": assistant_message
        })
        
        return assistant_message
        
    except Exception as e:
        print(f"⚠️ Vertex AI error: {e}")
        # Fallback to placeholder
        return _get_placeholder_response(message, dog_name)


def _get_placeholder_response(message: str, dog_name: str = "Buddy") -> str:
    """Fallback placeholder responses when API is unavailable"""
    import random
    
    message_lower = message.lower()
    
    if "hello" in message_lower or "hi" in message_lower:
        return f"*wags tail excitedly* Woof! Hello friend! I'm {dog_name}! 🐕"
    if "good" in message_lower and "boy" in message_lower:
        return "*spins in circles* Woof woof! Thank you! ❤️"
    if "treat" in message_lower or "food" in message_lower:
        return "*ears perk up* Did someone say treats?! 🦴"
    if "walk" in message_lower:
        return "*runs to the door* Walk?! WALK?! Let's go! 🐕"
    if "love" in message_lower:
        return "*licks your face* I love you too, human! ❤️"
    if "sit" in message_lower:
        return "*sits down proudly* Look at me! I'm a good boy!"
    if "play" in message_lower:
        return "*brings a ball* Throw it! Throw it! 🎾"
    if "name" in message_lower:
        return f"*tail wagging* My name is {dog_name}! Nice to meet you! 🐕"
    
    responses = [
        "*wags tail* Woof! *tilts head curiously*",
        "*happy panting* Bark bark! 🐕",
        "*sniffs around* Interesting... tell me more!",
        "*rolls over* Belly rubs? 🐕",
        "*playful bark* Woof woof! ❤️"
    ]
    
    return random.choice(responses)


def clear_conversation(session_id: str = "default"):
    """Clear conversation history for a session"""
    if session_id in _conversation_history:
        del _conversation_history[session_id]
