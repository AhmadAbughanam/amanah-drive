import os

from fastapi.testclient import TestClient

from app.main import EMBEDDING_DIMENSION, app

TOKEN = "tests-only-service-token"


class FakeEmbedding:
    def encode(self, texts, convert_to_numpy=True):
        return FakeEmbeddingArray([[float(index)] * EMBEDDING_DIMENSION for index, _ in enumerate(texts)])


class FakeEmbeddingArray:
    def __init__(self, values):
        self._values = values

    def tolist(self):
        return self._values


def client() -> TestClient:
    os.environ["AI_SERVICE_TOKEN"] = TOKEN
    app.state.embedding_model = FakeEmbedding()
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
