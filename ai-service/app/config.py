import os
from typing import Optional, Set

MODEL_NAME = "sentence-transformers/all-MiniLM-L6-v2"
EMBEDDING_DIMENSION = 384
HF_CHAT_COMPLETIONS_URL = "https://router.huggingface.co/v1/chat/completions"
HF_DEFAULT_MODEL = "openai/gpt-oss-20b"
HF_REQUEST_TIMEOUT_SECONDS = 30.0
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
