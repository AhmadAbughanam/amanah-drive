# AI Service Restructure

## Why

`ai-service/app/main.py` currently holds everything — FastAPI app setup, service-token auth dependency, all Pydantic request/response models, PDF/text extraction logic, chunking logic, and embedding logic — in one 148-line file. The API side (`api/src/AmanahDrive.Api/`) is already split by concern (`Auth/`, `Data/`, `Endpoints/`, `Models/`, `Options/`, `Storage/`). The AI service should follow the same principle: `main.py` should only wire the app together, not contain business logic.

This is a structure-only refactor. Do not change any request/response shapes, status codes, or behavior — `docs/ai-service-contract.md` is unchanged and every existing test must still pass without modification to what they assert (imports may need to change).

## Target layout

```
ai-service/
  app/
    __init__.py
    main.py            # FastAPI() instance, includes routers, nothing else
    config.py          # env-driven settings (AI_SERVICE_TOKEN, model name, dimension, supported content types)
    auth.py            # require_service_token dependency
    schemas.py         # all Pydantic models (ChunkRequest, ChunkDto, ChunkResponse, EmbedRequest, EmbedResponse)
    services/
      __init__.py
      extraction.py    # normalize_content_type, extract_pdf_text, extract() logic
      chunking.py       # chunk() logic
      embedding.py       # get_embedding_model(), embed() logic
    routers/
      __init__.py
      health.py         # GET /health
      extract.py         # POST /extract
      chunk.py           # POST /chunk
      embed.py            # POST /embed
  tests/
    ...                  # update imports only; test behavior/assertions unchanged
```

Use judgment on exact file boundaries if a slightly different split reads more naturally in idiomatic FastAPI style (e.g. one `routers/processing.py` instead of three router files is fine) — the goal is separation of concerns, not a rigid file count. Keep route handler functions thin: parse/validate input, call a service function, shape the response.

## Constraints

- No behavior changes. `pytest -q` must still pass (currently 6 tests) with equivalent assertions.
- Don't touch `api/` or `web/`.
- Don't change `docs/ai-service-contract.md`.
- Commits are fine for the completed refactor per `docs/AI_RULES.md`, don't push to `main`.
- Report per `docs/AI_RULES.md`'s completion report format: what changed, files changed, key decisions, anything incomplete, remaining risks.
