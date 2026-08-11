from typing import Annotated, Dict

from fastapi import APIRouter, Depends, File, HTTPException, UploadFile

from app.auth import require_service_token
from app.services.extraction import TextExtractionError, UnsupportedContentTypeError, extract_text

router = APIRouter(dependencies=[Depends(require_service_token)])


@router.post("/extract")
async def extract(file: Annotated[UploadFile, File()]) -> Dict[str, object]:
    data = await file.read()

    try:
        content_type, text = extract_text(data, file.content_type)
    except UnsupportedContentTypeError as exc:
        raise HTTPException(status_code=400, detail="Unsupported content type") from exc
    except TextExtractionError as exc:
        raise HTTPException(status_code=400, detail="Unable to extract text") from exc

    return {
        "text": text,
        "contentType": content_type,
        "characterCount": len(text),
    }
