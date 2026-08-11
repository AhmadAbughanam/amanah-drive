from fastapi import APIRouter, Depends, HTTPException, Request

from app.auth import require_service_token
from app.schemas import EmbedRequest, EmbedResponse
from app.services.embedding import embed_texts

router = APIRouter(dependencies=[Depends(require_service_token)])


@router.post("/embed")
def embed(payload: EmbedRequest, request: Request) -> EmbedResponse:
    try:
        return embed_texts(payload.texts, request.app.state)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail="texts must not be empty") from exc
