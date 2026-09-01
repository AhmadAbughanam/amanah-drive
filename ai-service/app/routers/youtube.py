from fastapi import APIRouter, Depends, HTTPException

from app.auth import require_service_token
from app.schemas import YouTubeTranscriptRequest
from app.services.youtube import YouTubeTranscriptError, fetch_transcript

router = APIRouter(dependencies=[Depends(require_service_token)])


@router.post("/youtube/transcript")
async def transcript(request: YouTubeTranscriptRequest) -> dict[str, object]:
    try:
        text = fetch_transcript(request.sourceUrl)
    except YouTubeTranscriptError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc

    return {"text": text, "characterCount": len(text)}
