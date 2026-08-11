from typing import Any, List

from fastapi import HTTPException

from app.config import EMBEDDING_DIMENSION, MODEL_NAME
from app.schemas import EmbedResponse


def embed_texts(texts: List[str], app_state: Any) -> EmbedResponse:
    if len(texts) == 0:
        raise ValueError("texts must not be empty")

    model = get_embedding_model(app_state)
    embeddings = model.encode(texts, convert_to_numpy=True).tolist()

    for embedding in embeddings:
        if len(embedding) != EMBEDDING_DIMENSION:
            raise HTTPException(status_code=500, detail="Embedding model returned an unexpected dimension")

    return EmbedResponse(model=MODEL_NAME, dimension=EMBEDDING_DIMENSION, embeddings=embeddings)


def get_embedding_model(app_state: Any):
    model_override = getattr(app_state, "embedding_model", None)
    if model_override is not None:
        return model_override

    from sentence_transformers import SentenceTransformer

    model = SentenceTransformer(MODEL_NAME)
    app_state.embedding_model = model
    return model
