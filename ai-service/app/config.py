import os
from typing import Optional, Set

MODEL_NAME = "sentence-transformers/all-MiniLM-L6-v2"
EMBEDDING_DIMENSION = 384
HF_CHAT_COMPLETIONS_URL = "https://router.huggingface.co/v1/chat/completions"
HF_DEFAULT_MODEL = "openai/gpt-oss-20b"
HF_REQUEST_TIMEOUT_SECONDS = 30.0
GEMINI_CHAT_COMPLETIONS_URL = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"
GEMINI_DEFAULT_MODEL = "gemini-3.7-flash"
GROQ_CHAT_COMPLETIONS_URL = "https://api.groq.com/openai/v1/chat/completions"
GROQ_PRIMARY_MODEL = "openai/gpt-oss-120b"
GROQ_SECONDARY_MODEL = "qwen/qwen3.6-27b"
OPENROUTER_CHAT_COMPLETIONS_URL = "https://openrouter.ai/api/v1/chat/completions"
OPENROUTER_DEFAULT_MODEL = "nvidia/nemotron-3-ultra-550b-a55b:free"
SUPPORTED_CONTENT_TYPES: Set[str] = {
    "application/pdf",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "image/jpeg",
    "image/png",
    "text/csv",
    "text/markdown",
    "text/plain",
}


def get_service_token() -> Optional[str]:
    return os.environ.get("AI_SERVICE_TOKEN")


def get_hf_api_token() -> Optional[str]:
    return os.environ.get("HF_API_TOKEN")


def get_hf_model() -> str:
    return os.environ.get("HF_MODEL") or HF_DEFAULT_MODEL


def get_gemini_api_key() -> Optional[str]:
    return os.environ.get("GEMINI_API_KEY")


def get_groq_api_key() -> Optional[str]:
    return os.environ.get("GROQ_API_KEY")


def get_openrouter_api_key() -> Optional[str]:
    return os.environ.get("OPENROUTER_API_KEY")


def get_openrouter_model() -> str:
    return os.environ.get("OPENROUTER_MODEL") or OPENROUTER_DEFAULT_MODEL
