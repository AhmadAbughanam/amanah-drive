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


class AgentToolFunction(BaseModel):
    name: str
    description: str
    parameters: dict


class AgentToolDefinition(BaseModel):
    type: str = "function"
    function: AgentToolFunction


class AgentToolCallFunction(BaseModel):
    name: str
    arguments: str


class AgentToolCall(BaseModel):
    id: str
    type: str = "function"
    function: AgentToolCallFunction


class AgentMessage(BaseModel):
    role: str
    content: Optional[str] = None
    toolCallId: Optional[str] = None
    # Optional, not just defaulted: the .NET API explicitly sends `null` for messages with
    # no tool calls (system/user/plain-text-assistant messages), rather than omitting the
    # field. A plain `List[...] = Field(default_factory=list)` only applies its default when
    # the field is *missing* - Pydantic v2 still rejects an explicit `null` against a
    # non-Optional list type ("Input should be a valid list"). Downstream code already treats
    # None the same as an empty list (`if message.toolCalls else {}`), so this is safe.
    toolCalls: Optional[List[AgentToolCall]] = None


class AgentCompletionRequest(BaseModel):
    messages: List[AgentMessage]
    tools: List[AgentToolDefinition]


class AgentCompletionResponse(BaseModel):
    message: AgentMessage
    model: str
    usage: Optional[ModelUsage] = None
