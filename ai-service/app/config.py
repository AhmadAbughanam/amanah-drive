import os
from typing import Optional, Set

MODEL_NAME = "sentence-transformers/all-MiniLM-L6-v2"
EMBEDDING_DIMENSION = 384
SUPPORTED_CONTENT_TYPES: Set[str] = {
    "application/pdf",
    "text/markdown",
    "text/plain",
}


def get_service_token() -> Optional[str]:
    return os.environ.get("AI_SERVICE_TOKEN")
