import secrets
from typing import Annotated, Optional

from fastapi import Header, HTTPException

from app.config import get_service_token


def require_service_token(
    x_service_token: Annotated[Optional[str], Header(alias="X-Service-Token")] = None,
) -> None:
    expected_token = get_service_token()
    if not expected_token:
        raise HTTPException(status_code=503, detail="AI service token is not configured")

    if x_service_token is None or not secrets.compare_digest(x_service_token, expected_token):
        raise HTTPException(status_code=401, detail="Invalid service token")
