from bisect import bisect_right
import re
from typing import List, Match, Optional, Sequence

from app.schemas import ChunkDto


_PARAGRAPH_BREAK_RE = re.compile(r"(?:\r?\n[ \t]*){2,}")
_SENTENCE_END_RE = re.compile(r"[.!?]+(?:[\"'’”)\]}»]+)?(?=\s|$)")
_WORD_BREAK_RE = re.compile(r"\s+")
_TOKEN_BEFORE_PERIOD_RE = re.compile(r"([A-Za-z][A-Za-z.]*)$")
_COMMON_ABBREVIATIONS = {
    "dr",
    "e.g",
    "etc",
    "fig",
    "i.e",
    "jr",
    "mr",
    "mrs",
    "ms",
    "no",
    "prof",
    "sr",
    "st",
    "vs",
}


def create_chunks(text: str, chunk_size: int, overlap: int) -> List[ChunkDto]:
    if chunk_size <= 0:
        raise ValueError("chunkSize must be greater than zero")
    if overlap < 0:
        raise ValueError("overlap must be zero or greater")
    if overlap >= chunk_size:
        raise ValueError("overlap must be smaller than chunkSize")

    paragraph_boundaries = [match.end() for match in _PARAGRAPH_BREAK_RE.finditer(text)]
    sentence_boundaries = _find_sentence_boundaries(text)
    word_boundaries = _find_word_boundaries(text)
    overlap_boundaries = sorted(set(paragraph_boundaries + sentence_boundaries))

    chunks: List[ChunkDto] = []
    start = 0
    index = 0
    text_length = len(text)

    while start < text_length:
        end = _find_chunk_end(
            text,
            start,
            chunk_size,
            paragraph_boundaries,
            sentence_boundaries,
            word_boundaries,
        )
        chunk_text = text[start:end]
        chunks.append(ChunkDto(index=index, text=chunk_text, start_offset=start, end_offset=end))

        if end == text_length:
            break

        start = _find_overlap_start(
            text,
            start,
            end,
            overlap,
            overlap_boundaries,
            word_boundaries,
        )
        index += 1

    return chunks


def _find_sentence_boundaries(text: str) -> List[int]:
    boundaries: List[int] = []

    for match in _SENTENCE_END_RE.finditer(text):
        if _is_abbreviation_or_initial(text, match):
            continue

        boundary = match.end()
        while boundary < len(text) and text[boundary].isspace():
            boundary += 1
        boundaries.append(boundary)

    return sorted(set(boundaries))


def _is_abbreviation_or_initial(text: str, match: Match[str]) -> bool:
    punctuation = re.match(r"[.!?]+", match.group())
    if punctuation is None or punctuation.group() != ".":
        return False

    token_match = _TOKEN_BEFORE_PERIOD_RE.search(text[: match.start()])
    if token_match is None:
        return False

    token = token_match.group(1)
    return token.lower() in _COMMON_ABBREVIATIONS or (len(token) == 1 and token.isupper())


def _find_word_boundaries(text: str) -> List[int]:
    boundaries = {len(text)} if text else set()
    for match in _WORD_BREAK_RE.finditer(text):
        boundaries.add(match.start())
        boundaries.add(match.end())
    return sorted(boundaries)


def _find_chunk_end(
    text: str,
    start: int,
    chunk_size: int,
    paragraph_boundaries: Sequence[int],
    sentence_boundaries: Sequence[int],
    word_boundaries: Sequence[int],
) -> int:
    target = min(start + chunk_size, len(text))
    if target == len(text):
        return target

    for boundaries in (paragraph_boundaries, sentence_boundaries):
        boundary = _last_boundary(boundaries, start, target)
        if boundary is not None:
            return boundary

    if _is_word_boundary(text, target):
        return target

    boundary = _last_boundary(word_boundaries, start, target)
    if boundary is not None:
        return boundary

    boundary = _first_boundary(word_boundaries, target)
    return boundary if boundary is not None else len(text)


def _find_overlap_start(
    text: str,
    current_start: int,
    end: int,
    overlap: int,
    sentence_boundaries: Sequence[int],
    word_boundaries: Sequence[int],
) -> int:
    if overlap == 0:
        return end

    target = max(current_start + 1, end - overlap)
    boundary = _last_boundary(sentence_boundaries, current_start, target)
    if boundary is not None:
        return boundary

    if target < end and _is_word_boundary(text, target):
        return target

    boundary = _last_boundary(word_boundaries, current_start, target)
    if boundary is not None:
        return boundary

    boundary = _first_boundary(word_boundaries, target)
    if boundary is not None and boundary < end:
        return boundary

    return end


def _last_boundary(boundaries: Sequence[int], start: int, target: int) -> Optional[int]:
    index = bisect_right(boundaries, target) - 1
    if index >= 0 and boundaries[index] > start:
        return boundaries[index]
    return None


def _first_boundary(boundaries: Sequence[int], target: int) -> Optional[int]:
    index = bisect_right(boundaries, target)
    return boundaries[index] if index < len(boundaries) else None


def _is_word_boundary(text: str, position: int) -> bool:
    if position <= 0 or position >= len(text):
        return position == len(text)
    return text[position - 1].isspace() or text[position].isspace()
