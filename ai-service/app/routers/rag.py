from fastapi import APIRouter, Depends, HTTPException, Request

from app.auth import require_service_token
from app.schemas import RagAnswerRequest, RagAnswerResponse
from app.services.rag import (
    HuggingFaceConfigurationError,
    HuggingFaceTimeoutError,
    HuggingFaceUpstreamError,
    generate_grounded_answer,
)

router = APIRouter(dependencies=[Depends(require_service_token)])


@router.post("/rag/answer")
def answer(payload: RagAnswerRequest, request: Request) -> RagAnswerResponse:
    try:
        return generate_grounded_answer(payload, request.app.state)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail="question must not be empty") from exc
    except HuggingFaceConfigurationError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    except HuggingFaceTimeoutError as exc:
        raise HTTPException(status_code=504, detail=str(exc)) from exc
    except HuggingFaceUpstreamError as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
