from fastapi import APIRouter, Depends, HTTPException

from app.auth import require_service_token
from app.schemas import ChunkRequest, ChunkResponse
from app.services.chunking import create_chunks

router = APIRouter(dependencies=[Depends(require_service_token)])


@router.post("/chunk")
def chunk(request: ChunkRequest) -> ChunkResponse:
    try:
        chunks = create_chunks(request.text, request.chunkSize, request.overlap)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail="overlap must be smaller than chunkSize") from exc

    return ChunkResponse(chunks=chunks)
