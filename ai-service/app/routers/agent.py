from fastapi import APIRouter, Depends, HTTPException

from app.auth import require_service_token
from app.schemas import AgentCompletionRequest, AgentCompletionResponse
from app.services.agent import complete_agent
from app.services.rag import HuggingFaceConfigurationError, HuggingFaceTimeoutError, HuggingFaceUpstreamError

router = APIRouter(dependencies=[Depends(require_service_token)])


@router.post("/agent/complete")
def complete(payload: AgentCompletionRequest) -> AgentCompletionResponse:
    try:
        return complete_agent(payload)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except HuggingFaceConfigurationError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    except HuggingFaceTimeoutError as exc:
        raise HTTPException(status_code=504, detail=str(exc)) from exc
    except HuggingFaceUpstreamError as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
