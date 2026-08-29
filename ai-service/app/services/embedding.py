from typing import Any, List

from fastapi import HTTPException

from app.config import EMBEDDING_DIMENSION, MODEL_NAME
from app.schemas import EmbedResponse, ModelUsage


def embed_texts(texts: List[str], app_state: Any) -> EmbedResponse:
    if len(texts) == 0:
        raise ValueError("texts must not be empty")

    model = get_embedding_model(app_state)
    embeddings = model.encode(texts, convert_to_numpy=True).tolist()

    for embedding in embeddings:
        if len(embedding) != EMBEDDING_DIMENSION:
            raise HTTPException(status_code=500, detail="Embedding model returned an unexpected dimension")

    return EmbedResponse(
        model=MODEL_NAME,
        dimension=EMBEDDING_DIMENSION,
        embeddings=embeddings,
        usage=ModelUsage(provider="local", inputTokens=count_input_tokens(model, texts), outputTokens=0),
    )


def count_input_tokens(model: Any, texts: List[str]):
    tokenizer = getattr(model, "tokenizer", None)
    if tokenizer is None:
        return None

    encoded = tokenizer(texts, padding=True, truncation=True, return_attention_mask=True)
    attention_mask = encoded.get("attention_mask")
    if attention_mask is None:
        return None

    if hasattr(attention_mask, "sum"):
        total = attention_mask.sum()
        return int(total.item() if hasattr(total, "item") else total)

    return sum(sum(int(value) for value in row) for row in attention_mask)


def get_embedding_model(app_state: Any):
    model_override = getattr(app_state, "embedding_model", None)
    if model_override is not None:
        return model_override

    from sentence_transformers import SentenceTransformer

    model = SentenceTransformer(MODEL_NAME)
    app_state.embedding_model = model
    return model
