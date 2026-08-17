import logging
from typing import Any, List

import httpx
from tenacity import before_sleep_log, retry, retry_if_exception, stop_after_attempt, wait_exponential_jitter

from app.config import HF_CHAT_COMPLETIONS_URL, HF_REQUEST_TIMEOUT_SECONDS, get_hf_api_token, get_hf_model
from app.schemas import RagAnswerRequest, RagAnswerResponse, RagCitation

logger = logging.getLogger(__name__)

HF_RETRY_ATTEMPTS = 3
HF_RETRY_INITIAL_SECONDS = 0.5
HF_RETRY_MAX_SECONDS = 4.0
HF_RETRY_JITTER_SECONDS = 0.5


class HuggingFaceConfigurationError(RuntimeError):
    pass


class HuggingFaceUpstreamError(RuntimeError):
    def __init__(self, message: str, *, transient: bool = False):
        super().__init__(message)
        self.transient = transient


class HuggingFaceTimeoutError(RuntimeError):
    pass


def build_grounded_prompt(payload: RagAnswerRequest) -> str:
    history_lines = []
    for message in payload.history:
        history_lines.append(f"{message.role}: {message.content}")

    chunk_lines = []
    for chunk in payload.chunks:
        chunk_lines.append(
            f"[{chunk.reference}] File: {chunk.fileName}\n{chunk.text}"
        )

    history_text = "\n".join(history_lines) if history_lines else "No prior conversation."
    chunks_text = "\n\n".join(chunk_lines) if chunk_lines else "No retrieved chunks were provided."

    return (
        "You are Amanah Drive's grounded document assistant. Answer only from the retrieved chunks. "
        "If the chunks do not contain the answer, say you do not know from the provided documents. "
        "Cite relevant chunk references in square brackets when possible.\n\n"
        f"Recent conversation:\n{history_text}\n\n"
        f"Retrieved chunks:\n{chunks_text}\n\n"
        f"Question: {payload.question}\n\n"
        "Answer:"
    )


def generate_grounded_answer(payload: RagAnswerRequest, app_state: Any) -> RagAnswerResponse:
    if not payload.question.strip():
        raise ValueError("question must not be empty")

    generator_override = getattr(app_state, "rag_answer_generator", None)
    if generator_override is not None:
        return generator_override(payload)

    model = get_hf_model()
    answer = call_hugging_face(build_grounded_prompt(payload), model)
    return RagAnswerResponse(answer=answer, model=model, citations=create_citations(payload))


def _is_transient_hugging_face_error(exception: BaseException) -> bool:
    return isinstance(exception, HuggingFaceTimeoutError) or (
        isinstance(exception, HuggingFaceUpstreamError) and exception.transient
    )


@retry(
    retry=retry_if_exception(_is_transient_hugging_face_error),
    stop=stop_after_attempt(HF_RETRY_ATTEMPTS),
    wait=wait_exponential_jitter(
        initial=HF_RETRY_INITIAL_SECONDS,
        max=HF_RETRY_MAX_SECONDS,
        jitter=HF_RETRY_JITTER_SECONDS,
    ),
    before_sleep=before_sleep_log(logger, logging.WARNING),
    reraise=True,
)
def call_hugging_face(prompt: str, model: str) -> str:
    token = get_hf_api_token()
    if not token:
        raise HuggingFaceConfigurationError("HF_API_TOKEN is not configured")

    request_body = {
        "model": model,
        "stream": False,
        "max_tokens": 512,
        "messages": [
            {
                "role": "user",
                "content": prompt,
            }
        ],
    }

    try:
        response = httpx.post(
            HF_CHAT_COMPLETIONS_URL,
            headers={
                "Authorization": f"Bearer {token}",
                "Content-Type": "application/json",
            },
            json=request_body,
            timeout=HF_REQUEST_TIMEOUT_SECONDS,
        )
    except httpx.TimeoutException as exc:
        raise HuggingFaceTimeoutError("Hugging Face request timed out") from exc
    except httpx.RequestError as exc:
        raise HuggingFaceUpstreamError("Hugging Face request failed", transient=True) from exc

    if response.status_code >= 400:
        transient = response.status_code >= 500 or response.status_code in (408, 429)
        raise HuggingFaceUpstreamError(
            f"Hugging Face returned {response.status_code}: {response.text}",
            transient=transient,
        )

    try:
        body = response.json()
        choices = body["choices"]
        message = choices[0]["message"]
        content = message["content"]
    except (KeyError, IndexError, TypeError, ValueError) as exc:
        raise HuggingFaceUpstreamError("Hugging Face returned an invalid response") from exc

    if not isinstance(content, str) or not content.strip():
        raise HuggingFaceUpstreamError("Hugging Face returned an empty response")

    return content.strip()


def create_citations(payload: RagAnswerRequest) -> List[RagCitation]:
    return [
        RagCitation(
            reference=chunk.reference,
            fileName=chunk.fileName,
            snippet=create_snippet(chunk.text),
        )
        for chunk in payload.chunks
    ]


def create_snippet(text: str, max_length: int = 300) -> str:
    normalized = " ".join(text.split())
    if len(normalized) <= max_length:
        return normalized

    return normalized[: max_length - 3].rstrip() + "..."
