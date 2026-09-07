import logging
from dataclasses import dataclass
from typing import Optional

import httpx
from tenacity import before_sleep_log, retry, retry_if_exception, stop_after_attempt, wait_exponential_jitter

from app.config import (
    GEMINI_CHAT_COMPLETIONS_URL,
    GEMINI_DEFAULT_MODEL,
    GROQ_CHAT_COMPLETIONS_URL,
    GROQ_PRIMARY_MODEL,
    GROQ_SECONDARY_MODEL,
    HF_CHAT_COMPLETIONS_URL,
    HF_REQUEST_TIMEOUT_SECONDS,
    OPENROUTER_CHAT_COMPLETIONS_URL,
    get_gemini_api_key,
    get_groq_api_key,
    get_hf_api_token,
    get_hf_model,
    get_openrouter_api_key,
    get_openrouter_model,
)
from app.schemas import AgentCompletionRequest, AgentCompletionResponse, AgentMessage, AgentToolCall, AgentToolCallFunction, ModelUsage
from app.services.rag import HuggingFaceConfigurationError, HuggingFaceTimeoutError, HuggingFaceUpstreamError

logger = logging.getLogger(__name__)


@dataclass(frozen=True)
class ProviderConfig:
    name: str
    endpoint: str
    api_key: Optional[str]
    model: str


@dataclass(frozen=True)
class AgentCompletionResult:
    message: AgentMessage
    provider: str
    model: str
    input_tokens: Optional[int]
    output_tokens: Optional[int]


def _is_transient_error(exception: BaseException) -> bool:
    return isinstance(exception, HuggingFaceTimeoutError) or (
        isinstance(exception, HuggingFaceUpstreamError) and exception.transient
    )


def complete_agent(payload: AgentCompletionRequest) -> AgentCompletionResponse:
    if not payload.messages:
        raise ValueError("messages must not be empty")

    result = call_agent_with_fallbacks(payload)
    return AgentCompletionResponse(
        message=result.message,
        model=result.model,
        usage=ModelUsage(provider=result.provider, inputTokens=result.input_tokens, outputTokens=result.output_tokens),
    )


def get_agent_providers() -> list[ProviderConfig]:
    groq_api_key = get_groq_api_key()
    return [
        ProviderConfig("gemini", GEMINI_CHAT_COMPLETIONS_URL, get_gemini_api_key(), GEMINI_DEFAULT_MODEL),
        ProviderConfig("groq", GROQ_CHAT_COMPLETIONS_URL, groq_api_key, GROQ_PRIMARY_MODEL),
        ProviderConfig("groq", GROQ_CHAT_COMPLETIONS_URL, groq_api_key, GROQ_SECONDARY_MODEL),
        ProviderConfig(
            "openrouter",
            OPENROUTER_CHAT_COMPLETIONS_URL,
            get_openrouter_api_key(),
            get_openrouter_model(),
        ),
        ProviderConfig("huggingface", HF_CHAT_COMPLETIONS_URL, get_hf_api_token(), get_hf_model()),
    ]


def call_agent_with_fallbacks(payload: AgentCompletionRequest) -> AgentCompletionResult:
    failures: list[str] = []
    configured_provider_found = False

    for provider in get_agent_providers():
        if not provider.api_key:
            logger.info("Skipping unconfigured agent completion tier %s/%s", provider.name, provider.model)
            continue

        configured_provider_found = True
        try:
            return call_openai_compatible_with_tools(payload, provider)
        except (HuggingFaceTimeoutError, HuggingFaceUpstreamError) as exc:
            failures.append(f"{provider.name}/{provider.model}: {exc}")
            logger.warning("Agent completion tier %s/%s failed; trying the next tier", provider.name, provider.model)

    if not configured_provider_found:
        raise HuggingFaceConfigurationError("No agent completion provider API keys are configured")

    raise HuggingFaceUpstreamError(
        "All configured agent completion providers failed: " + "; ".join(failures),
        transient=False,
    )


@retry(
    retry=retry_if_exception(_is_transient_error),
    stop=stop_after_attempt(2),
    wait=wait_exponential_jitter(initial=0.5, max=4.0, jitter=0.5),
    before_sleep=before_sleep_log(logger, logging.WARNING),
    reraise=True,
)
def call_openai_compatible_with_tools(
    payload: AgentCompletionRequest,
    provider: ProviderConfig,
) -> AgentCompletionResult:
    if not provider.api_key:
        raise HuggingFaceConfigurationError(f"{provider.name} API key is not configured")

    request_body = {
        "model": provider.model,
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
            provider.endpoint,
            headers={"Authorization": f"Bearer {provider.api_key}", "Content-Type": "application/json"},
            json=request_body,
            timeout=HF_REQUEST_TIMEOUT_SECONDS,
        )
    except httpx.TimeoutException as exc:
        raise HuggingFaceTimeoutError(f"{provider.name} request timed out") from exc
    except httpx.RequestError as exc:
        raise HuggingFaceUpstreamError(f"{provider.name} request failed", transient=True) from exc

    if response.status_code >= 400:
        raise HuggingFaceUpstreamError(
            f"{provider.name} returned {response.status_code}: {response.text}",
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
        response_model = body.get("model")
        if not isinstance(response_model, str) or not response_model:
            response_model = provider.model
    except (KeyError, IndexError, TypeError, ValueError) as exc:
        raise HuggingFaceUpstreamError(f"{provider.name} returned an invalid response") from exc

    return AgentCompletionResult(
        message=AgentMessage(role="assistant", content=content, toolCalls=tool_calls),
        provider=provider.name,
        model=response_model,
        input_tokens=usage.get("prompt_tokens") if isinstance(usage.get("prompt_tokens"), int) else None,
        output_tokens=usage.get("completion_tokens") if isinstance(usage.get("completion_tokens"), int) else None,
    )


def call_hugging_face_with_tools(payload: AgentCompletionRequest, model: str) -> AgentCompletionResult:
    token = get_hf_api_token()
    if not token:
        raise HuggingFaceConfigurationError("HF_API_TOKEN is not configured")
    return call_openai_compatible_with_tools(
        payload,
        ProviderConfig("huggingface", HF_CHAT_COMPLETIONS_URL, token, model),
    )
