from typing import List

from app.schemas import ChunkDto


def create_chunks(text: str, chunk_size: int, overlap: int) -> List[ChunkDto]:
    if overlap >= chunk_size:
        raise ValueError("overlap must be smaller than chunkSize")

    chunks: List[ChunkDto] = []
    start = 0
    index = 0
    text_length = len(text)
    step = chunk_size - overlap

    while start < text_length:
        end = min(start + chunk_size, text_length)
        chunk_text = text[start:end]
        chunks.append(ChunkDto(index=index, text=chunk_text, start_offset=start, end_offset=end))

        if end == text_length:
            break

        start += step
        index += 1

    return chunks
