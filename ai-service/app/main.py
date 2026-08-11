import os
import secrets
from io import BytesIO
from typing import Annotated, Dict, List, Optional

from fastapi import Depends, FastAPI, File, Header, HTTPException, UploadFile
from pydantic import BaseModel, Field
from pypdf import PdfReader

app = FastAPI(title="Amanah Drive AI Service")

MODEL_NAME = "sentence-transformers/all-MiniLM-L6-v2"
EMBEDDING_DIMENSION = 384
SUPPORTED_CONTENT_TYPES = {
    "application/pdf",
    "text/markdown",
    "text/plain",
}


class ChunkRequest(BaseModel):
    text: str
    chunkSize: int = Field(default=1000, gt=0)
    overlap: int = Field(default=200, ge=0)


class ChunkDto(BaseModel):
    index: int
    text: str
    start_offset: int = Field(alias="startOffset")
    end_offset: int = Field(alias="endOffset")

    model_config = {"populate_by_name": True}


class ChunkResponse(BaseModel):
    chunks: List[ChunkDto]


class EmbedRequest(BaseModel):
    texts: List[str]


class EmbedResponse(BaseModel):
    model: str
    dimension: int
    embeddings: List[List[float]]


def require_service_token(
    x_service_token: Annotated[Optional[str], Header(alias="X-Service-Token")] = None,
) -> None:
    expected_token = os.environ.get("AI_SERVICE_TOKEN")
    if not expected_token:
        raise HTTPException(status_code=503, detail="AI service token is not configured")

    if x_service_token is None or not secrets.compare_digest(x_service_token, expected_token):
        raise HTTPException(status_code=401, detail="Invalid service token")


def get_embedding_model():
    model_override = getattr(app.state, "embedding_model", None)
    if model_override is not None:
        return model_override

    from sentence_transformers import SentenceTransformer

    model = SentenceTransformer(MODEL_NAME)
    app.state.embedding_model = model
    return model


@app.get("/health")
def health() -> Dict[str, str]:
    return {"status": "ok"}


@app.post("/extract", dependencies=[Depends(require_service_token)])
async def extract(file: Annotated[UploadFile, File()]) -> Dict[str, object]:
    content_type = normalize_content_type(file.content_type)
    if content_type not in SUPPORTED_CONTENT_TYPES:
        raise HTTPException(status_code=400, detail="Unsupported content type")

    data = await file.read()
    try:
        if content_type == "application/pdf":
            text = extract_pdf_text(data)
        else:
            text = data.decode("utf-8")
    except Exception as exc:
        raise HTTPException(status_code=400, detail="Unable to extract text") from exc

    return {
        "text": text,
        "contentType": content_type,
        "characterCount": len(text),
    }


@app.post("/chunk", dependencies=[Depends(require_service_token)])
def chunk(request: ChunkRequest) -> ChunkResponse:
    if request.overlap >= request.chunkSize:
        raise HTTPException(status_code=400, detail="overlap must be smaller than chunkSize")

    chunks: List[ChunkDto] = []
    start = 0
    index = 0
    text_length = len(request.text)
    step = request.chunkSize - request.overlap

    while start < text_length:
        end = min(start + request.chunkSize, text_length)
        chunk_text = request.text[start:end]
        chunks.append(ChunkDto(index=index, text=chunk_text, start_offset=start, end_offset=end))

        if end == text_length:
            break

        start += step
        index += 1

    return ChunkResponse(chunks=chunks)


@app.post("/embed", dependencies=[Depends(require_service_token)])
def embed(request: EmbedRequest) -> EmbedResponse:
    if len(request.texts) == 0:
        raise HTTPException(status_code=400, detail="texts must not be empty")

    model = get_embedding_model()
    embeddings = model.encode(request.texts, convert_to_numpy=True).tolist()

    for embedding in embeddings:
        if len(embedding) != EMBEDDING_DIMENSION:
            raise HTTPException(status_code=500, detail="Embedding model returned an unexpected dimension")

    return EmbedResponse(model=MODEL_NAME, dimension=EMBEDDING_DIMENSION, embeddings=embeddings)


def normalize_content_type(content_type: Optional[str]) -> str:
    return (content_type or "").split(";", 1)[0].strip().lower()


def extract_pdf_text(data: bytes) -> str:
    reader = PdfReader(BytesIO(data))
    pages = [page.extract_text() or "" for page in reader.pages]
    return "\n".join(page_text for page_text in pages if page_text)
