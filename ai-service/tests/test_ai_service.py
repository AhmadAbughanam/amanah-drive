import os

import httpx
import pytest
from fastapi.testclient import TestClient
from tenacity import wait_none

from app.config import EMBEDDING_DIMENSION, HF_DEFAULT_MODEL
from app.main import app
from app.schemas import RagAnswerResponse, RagCitation
from app.services.rag import HuggingFaceUpstreamError, build_grounded_prompt, call_hugging_face

TOKEN = "tests-only-service-token"


class FakeEmbedding:
    def encode(self, texts, convert_to_numpy=True):
        return FakeEmbeddingArray([[float(index)] * EMBEDDING_DIMENSION for index, _ in enumerate(texts)])


class FakeEmbeddingArray:
    def __init__(self, values):
        self._values = values

    def tolist(self):
        return self._values


def client(rag_answer_generator=None) -> TestClient:
    os.environ["AI_SERVICE_TOKEN"] = TOKEN
    app.state.embedding_model = FakeEmbedding()
    if rag_answer_generator is None:
        if hasattr(app.state, "rag_answer_generator"):
            delattr(app.state, "rag_answer_generator")
    else:
        app.state.rag_answer_generator = rag_answer_generator

    return TestClient(app)


def headers() -> dict[str, str]:
    return {"X-Service-Token": TOKEN}


def test_extract_plain_text():
    response = client().post(
        "/extract",
        headers=headers(),
        files={"file": ("note.txt", b"hello world", "text/plain")},
    )

    assert response.status_code == 200
    assert response.json()["text"] == "hello world"
    assert response.json()["contentType"] == "text/plain"


def test_extract_markdown():
    response = client().post(
        "/extract",
        headers=headers(),
        files={"file": ("note.md", b"# Title\n\nBody", "text/markdown")},
    )

    assert response.status_code == 200
    assert "# Title" in response.json()["text"]


def test_extract_pdf():
    response = client().post(
        "/extract",
        headers=headers(),
        files={"file": ("note.pdf", pdf_bytes(), "application/pdf")},
    )

    assert response.status_code == 200
    assert "Hello PDF" in response.json()["text"]


def test_extract_rejects_missing_service_token():
    response = client().post(
        "/extract",
        files={"file": ("note.txt", b"hello", "text/plain")},
    )

    assert response.status_code == 401


def test_chunk_fixed_size_with_overlap():
    response = client().post(
        "/chunk",
        headers=headers(),
        json={"text": "abcdefghij", "chunkSize": 4, "overlap": 1},
    )

    assert response.status_code == 200
    assert response.json()["chunks"] == [
        {"index": 0, "text": "abcd", "startOffset": 0, "endOffset": 4},
        {"index": 1, "text": "defg", "startOffset": 3, "endOffset": 7},
        {"index": 2, "text": "ghij", "startOffset": 6, "endOffset": 10},
    ]


def test_embed_returns_expected_shape():
    response = client().post(
        "/embed",
        headers=headers(),
        json={"texts": ["first", "second"]},
    )

    assert response.status_code == 200
    body = response.json()
    assert body["dimension"] == EMBEDDING_DIMENSION
    assert len(body["embeddings"]) == 2
    assert len(body["embeddings"][0]) == EMBEDDING_DIMENSION


@pytest.mark.real_model
@pytest.mark.skipif(os.environ.get("RUN_REAL_EMBED_SMOKE") != "1", reason="set RUN_REAL_EMBED_SMOKE=1 to load the real embedding model")
def test_embed_real_model_smoke():
    os.environ["AI_SERVICE_TOKEN"] = TOKEN
    if hasattr(app.state, "embedding_model"):
        delattr(app.state, "embedding_model")

    try:
        response = TestClient(app).post(
            "/embed",
            headers=headers(),
            json={"texts": ["real model smoke test", "second embedding"]},
        )

        assert response.status_code == 200
        body = response.json()
        assert body["model"] == "sentence-transformers/all-MiniLM-L6-v2"
        assert body["dimension"] == EMBEDDING_DIMENSION
        assert len(body["embeddings"]) == 2
        assert len(body["embeddings"][0]) == EMBEDDING_DIMENSION
        assert all(isinstance(value, float) for value in body["embeddings"][0][:8])
    finally:
        if hasattr(app.state, "embedding_model"):
            delattr(app.state, "embedding_model")


def test_rag_prompt_includes_question_and_chunks():
    prompt = build_grounded_prompt(rag_request())

    assert "What is the renewal rule?" in prompt
    assert "[chunk-1] File: lease.pdf" in prompt
    assert "The lease renews yearly." in prompt
    assert "[chunk-2] File: policy.md" in prompt
    assert "Approval is required." in prompt


def test_rag_answer_with_stubbed_llm_returns_expected_shape():
    def generator(payload):
        return RagAnswerResponse(
            answer="The lease renews yearly. [chunk-1]",
            model=HF_DEFAULT_MODEL,
            citations=[
                RagCitation(reference=payload.chunks[0].reference, fileName=payload.chunks[0].fileName, snippet=payload.chunks[0].text)
            ],
        )

    response = client(generator).post(
        "/rag/answer",
        headers=headers(),
        json=rag_request_json(),
    )

    assert response.status_code == 200
    body = response.json()
    assert body["answer"] == "The lease renews yearly. [chunk-1]"
    assert body["model"] == HF_DEFAULT_MODEL
    assert body["citations"] == [
        {"reference": "chunk-1", "fileName": "lease.pdf", "snippet": "The lease renews yearly."}
    ]


def test_rag_answer_with_upstream_failure_returns_clean_error():
    def generator(_payload):
        raise HuggingFaceUpstreamError("Hugging Face returned 503: overloaded")

    response = client(generator).post(
        "/rag/answer",
        headers=headers(),
        json=rag_request_json(),
    )

    assert response.status_code == 502
    assert response.json()["detail"] == "Hugging Face returned 503: overloaded"


def test_hugging_face_call_retries_transient_failures_then_succeeds(monkeypatch):
    monkeypatch.setenv("HF_API_TOKEN", "tests-only-hf-token")
    attempts = 0

    def fake_post(*_args, **_kwargs):
        nonlocal attempts
        attempts += 1
        if attempts <= 2:
            request = httpx.Request("POST", "https://huggingface.test/v1/chat/completions")
            raise httpx.ConnectError("temporary connection failure", request=request)
        return httpx.Response(
            200,
            json={"choices": [{"message": {"content": "Recovered answer"}}]},
        )

    monkeypatch.setattr("app.services.rag.httpx.post", fake_post)

    answer = call_hugging_face.retry_with(wait=wait_none())("grounded prompt", "test-model")

    assert answer == "Recovered answer"
    assert attempts == 3


def test_hugging_face_call_does_not_retry_non_transient_client_error(monkeypatch):
    monkeypatch.setenv("HF_API_TOKEN", "tests-only-hf-token")
    attempts = 0

    def fake_post(*_args, **_kwargs):
        nonlocal attempts
        attempts += 1
        return httpx.Response(400, text="invalid request")

    monkeypatch.setattr("app.services.rag.httpx.post", fake_post)

    with pytest.raises(HuggingFaceUpstreamError, match="returned 400"):
        call_hugging_face.retry_with(wait=wait_none())("grounded prompt", "test-model")

    assert attempts == 1


def rag_request_json() -> dict:
    return {
        "question": "What is the renewal rule?",
        "chunks": [
            {"reference": "chunk-1", "fileName": "lease.pdf", "text": "The lease renews yearly."},
            {"reference": "chunk-2", "fileName": "policy.md", "text": "Approval is required."},
        ],
        "history": [
            {"role": "user", "content": "What document is this?"},
            {"role": "assistant", "content": "It is a lease."},
        ],
    }


def rag_request():
    from app.schemas import RagAnswerRequest

    return RagAnswerRequest(**rag_request_json())


def pdf_bytes() -> bytes:
    return b"""%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 4 0 R >> >> /MediaBox [0 0 612 792] /Contents 5 0 R >>
endobj
4 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj
5 0 obj
<< /Length 44 >>
stream
BT /F1 24 Tf 100 700 Td (Hello PDF) Tj ET
endstream
endobj
xref
0 6
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000241 00000 n
0000000311 00000 n
trailer
<< /Root 1 0 R /Size 6 >>
startxref
405
%%EOF
"""
