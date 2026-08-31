import logging
from dataclasses import dataclass
from typing import Optional

import httpx
from tenacity import before_sleep_log, retry, retry_if_exception, stop_after_attempt, wait_exponential_jitter

from app.config import HF_CHAT_COMPLETIONS_URL, HF_REQUEST_TIMEOUT_SECONDS, get_hf_api_token, get_hf_model
from app.schemas import AgentCompletionRequest, AgentCompletionResponse, AgentMessage, AgentToolCall, AgentToolCallFunction, ModelUsage
from app.services.rag import HuggingFaceConfigurationError, HuggingFaceTimeoutError, HuggingFaceUpstreamError

logger = logging.getLogger(__name__)


@dataclass(frozen=True)
class AgentHuggingFaceResult:
    message: AgentMessage
    input_tokens: Optional[int]
    output_tokens: Optional[int]


def _is_transient_error(exception: BaseException) -> bool:
    return isinstance(exception, HuggingFaceTimeoutError) or (
        isinstance(exception, HuggingFaceUpstreamError) and exception.transient
    )


def complete_agent(payload: AgentCompletionRequest) -> AgentCompletionResponse:
    if not payload.messages:
        raise ValueError("messages must not be empty")

    model = get_hf_model()
    result = call_hugging_face_with_tools(payload, model)
    return AgentCompletionResponse(
        message=result.message,
        model=model,
        usage=ModelUsage(provider="huggingface", inputTokens=result.input_tokens, outputTokens=result.output_tokens),
    )


@retry(
    retry=retry_if_exception(_is_transient_error),
    stop=stop_after_attempt(3),
    wait=wait_exponential_jitter(initial=0.5, max=4.0, jitter=0.5),
    before_sleep=before_sleep_log(logger, logging.WARNING),
    reraise=True,
)
def call_hugging_face_with_tools(payload: AgentCompletionRequest, model: str) -> AgentHuggingFaceResult:
    token = get_hf_api_token()
    if not token:
        raise HuggingFaceConfigurationError("HF_API_TOKEN is not configured")

    request_body = {
        "model": model,
        "stream": False,
        "max_tokens": 512,
        "tool_choice": "auto",
        "tools": [tool.model_dump() for tool in payload.tools],
        "messages": [
            {
                **{"role": message.role},
                **({"content": message.content} if message.content is not None else {}),
                **({"tool_call_id": message.toolCallId} if message.toolCallId is not None else {}),
                **({"tool_calls": [call.model_dump() for call in message.toolCalls]} if message.toolCalls else {}),
            }
            for message in payload.messages
        ],
    }

    try:
        response = httpx.post(
            HF_CHAT_COMPLETIONS_URL,
            headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json"},
            json=request_body,
            timeout=HF_REQUEST_TIMEOUT_SECONDS,
        )
    except httpx.TimeoutException as exc:
        raise HuggingFaceTimeoutError("Hugging Face request timed out") from exc
    except httpx.RequestError as exc:
        raise HuggingFaceUpstreamError("Hugging Face request failed", transient=True) from exc

    if response.status_code >= 400:
        raise HuggingFaceUpstreamError(
            f"Hugging Face returned {response.status_code}: {response.text}",
            transient=response.status_code >= 500 or response.status_code in (408, 429),
        )

    try:
        body = response.json()
        message = body["choices"][0]["message"]
        tool_calls = [
            AgentToolCall(
                id=call["id"],
                type=call.get("type", "function"),
                function=AgentToolCallFunction(
                    name=call["function"]["name"],
                    arguments=call["function"]["arguments"],
                ),
            )
            for call in message.get("tool_calls") or []
        ]
        content = message.get("content")
        if content is not None and not isinstance(content, str):
            raise TypeError("content must be a string or null")
        if not content and not tool_calls:
            raise ValueError("response had neither content nor tool calls")
        usage = body.get("usage") or {}
    except (KeyError, IndexError, TypeError, ValueError) as exc:
        raise HuggingFaceUpstreamError("Hugging Face returned an invalid response") from exc

    return AgentHuggingFaceResult(
        message=AgentMessage(role="assistant", content=content, toolCalls=tool_calls),
        input_tokens=usage.get("prompt_tokens") if isinstance(usage.get("prompt_tokens"), int) else None,
        output_tokens=usage.get("completion_tokens") if isinstance(usage.get("completion_tokens"), int) else None,
    )
