from io import BytesIO
from typing import Optional, Tuple

from pypdf import PdfReader

from app.config import SUPPORTED_CONTENT_TYPES


class UnsupportedContentTypeError(ValueError):
    pass


class TextExtractionError(ValueError):
    pass


def extract_text(data: bytes, content_type: Optional[str]) -> Tuple[str, str]:
    normalized_content_type = normalize_content_type(content_type)
    if normalized_content_type not in SUPPORTED_CONTENT_TYPES:
        raise UnsupportedContentTypeError("Unsupported content type")

    try:
        if normalized_content_type == "application/pdf":
            text = extract_pdf_text(data)
        else:
            text = data.decode("utf-8")
    except Exception as exc:
        raise TextExtractionError("Unable to extract text") from exc

    return normalized_content_type, text


def normalize_content_type(content_type: Optional[str]) -> str:
    return (content_type or "").split(";", 1)[0].strip().lower()


def extract_pdf_text(data: bytes) -> str:
    reader = PdfReader(BytesIO(data))
    pages = [page.extract_text() or "" for page in reader.pages]
    return "\n".join(page_text for page_text in pages if page_text)
