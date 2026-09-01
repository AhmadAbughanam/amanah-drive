import re
from urllib.parse import parse_qs, urlparse

from youtube_transcript_api import (
    NoTranscriptFound,
    TranscriptsDisabled,
    VideoUnavailable,
    YouTubeRequestFailed,
    YouTubeTranscriptApi,
)


class YouTubeTranscriptError(ValueError):
    """A user-safe reason that the requested caption track cannot be read."""


_VIDEO_ID = re.compile(r"^[A-Za-z0-9_-]{11}$")


def extract_video_id(source_url: str) -> str:
    parsed = urlparse(source_url.strip())
    host = parsed.hostname.lower() if parsed.hostname else ""
    video_id = None

    if host in {"youtu.be", "www.youtu.be"}:
        video_id = parsed.path.strip("/").split("/", 1)[0]
    elif host in {"youtube.com", "www.youtube.com", "m.youtube.com"}:
        parts = parsed.path.strip("/").split("/")
        if parsed.path == "/watch":
            video_id = parse_qs(parsed.query).get("v", [None])[0]
        elif len(parts) >= 2 and parts[0] in {"shorts", "embed", "live"}:
            video_id = parts[1]

    if not video_id or not _VIDEO_ID.fullmatch(video_id):
        raise YouTubeTranscriptError("Enter a valid YouTube video URL.")
    return video_id


def fetch_transcript(source_url: str) -> str:
    """Fetches YouTube caption text only; this library never downloads media bytes."""
    video_id = extract_video_id(source_url)
    try:
        transcripts = list(YouTubeTranscriptApi().list(video_id))
        selected = next((item for item in transcripts if not item.is_generated), None)
        selected = selected or next((item for item in transcripts if item.is_generated), None)
        if selected is None:
            raise YouTubeTranscriptError("No captions are available for this video.")
        text = "\n".join(snippet.text.strip() for snippet in selected.fetch() if snippet.text.strip()).strip()
        if not text:
            raise YouTubeTranscriptError("No readable captions are available for this video.")
        return text
    except YouTubeTranscriptError:
        raise
    except TranscriptsDisabled as exc:
        raise YouTubeTranscriptError("YouTube captions are disabled for this video.") from exc
    except VideoUnavailable as exc:
        raise YouTubeTranscriptError("YouTube video is private or unavailable.") from exc
    except NoTranscriptFound as exc:
        raise YouTubeTranscriptError("No captions are available for this video.") from exc
    except YouTubeRequestFailed as exc:
        raise YouTubeTranscriptError(f"YouTube captions could not be fetched: {exc}") from exc
    except Exception as exc:
        raise YouTubeTranscriptError(f"YouTube captions could not be fetched: {exc}") from exc
