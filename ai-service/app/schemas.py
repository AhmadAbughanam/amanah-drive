from typing import List, Optional

from pydantic import BaseModel, Field


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


class ModelUsage(BaseModel):
    provider: str
    inputTokens: Optional[int] = None
    outputTokens: Optional[int] = None


class EmbedResponse(BaseModel):
    model: str
    dimension: int
    embeddings: List[List[float]]
    usage: Optional[ModelUsage] = None


class RagChunk(BaseModel):
    fileName: str
    text: str


class RagHistoryMessage(BaseModel):
    role: str
    content: str


class RagAnswerRequest(BaseModel):
    question: str
    chunks: List[RagChunk] = Field(default_factory=list)
    history: List[RagHistoryMessage] = Field(default_factory=list)


class RagCitation(BaseModel):
    reference: str
    fileName: str
    snippet: str


class RagAnswerResponse(BaseModel):
    answer: str
    model: str
    citations: List[RagCitation]
    usage: Optional[ModelUsage] = None
