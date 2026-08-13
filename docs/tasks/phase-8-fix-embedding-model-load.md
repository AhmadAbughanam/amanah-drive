# Fix — AI Service Embedding Model Fails to Load

## Bug (found during live smoke-testing, not caught by any existing test)

Uploading a real file and calling `GET /search` against the running stack (Docker Compose, real Hugging Face token) fails with a 502 from the API. The API logs show `/extract` and `/chunk` succeeding, but `/embed` returns `500` from the AI service. The AI service logs show the real cause:

```
File ".../sentence_transformers/SentenceTransformer.py", line 367, in __init__
    self.to(device)
...
NotImplementedError: Cannot copy out of meta tensor; no data! Please use torch.nn.Module.to_empty() instead of torch.nn.Module.to() when moving module from meta to a different device.
```

This happens the first time `get_embedding_model()` (`ai-service/app/services/embedding.py`) actually loads `sentence-transformers/all-MiniLM-L6-v2` for real — every existing `ai-service` test stubs this via `app.state.embedding_model`, so no test has ever exercised the real model-loading path. That's how this shipped without being caught across Phases 3, 4, 5, and 7.

Root cause: `ai-service/requirements.txt` pins `sentence-transformers==5.1.0` but does not pin `torch` or `transformers`, and does **not** include `accelerate` at all. The installed versions resolved to `torch==2.13.0` / `transformers==4.57.6` with no `accelerate` present. Recent `transformers` versions default to meta-device/`low_cpu_mem_usage` model initialization on `from_pretrained`, which needs `accelerate` installed to properly materialize real weights onto the target device — without it, the model gets stuck as an empty meta tensor and fails exactly as seen above.

## Fix

1. Add `accelerate` to `ai-service/requirements.txt` with a version compatible with the currently-resolved `torch`/`transformers`/`sentence-transformers` versions.
2. Pin `torch` and `transformers` explicitly (don't leave them fully transitive) to versions known to work together with `sentence-transformers==5.1.0` and `accelerate` — pick current stable, compatible versions and record what you tested.
3. Rebuild the `ai-service` image and confirm the fix against a **real** model load, not the stub: call `POST /embed` directly against the running container (with the real service token) with a couple of sample texts and confirm you get back real 384-dimension float vectors, not an error.
4. Add a genuine (non-stubbed) smoke-level test or startup check that would have caught this — use your judgment on the right shape for this given the model download cost (e.g. a test explicitly marked/skippable to run separately from the fast stubbed suite, or a Docker healthcheck that does one real embed call on container start). The goal is that a future dependency bump can't silently break real model loading again while every existing test still passes on the stub.
5. Re-run the full existing `ai-service` test suite (`pytest -q`) to confirm nothing else broke.

## Verification

Don't consider this done until you've actually exercised the real path end to end: rebuild the container, hit `/embed` for real (not through the stub), and confirm a real embedding vector comes back. Building successfully and the stubbed test suite passing is not sufficient evidence — that's exactly what already happened across four prior phases while this bug shipped.

## Constraints

- Only touch `ai-service/` (requirements, and whatever test/healthcheck addition you choose).
- Don't touch `api/` or `web/` unless the fix genuinely requires it (it shouldn't).
- Commits are fine for completed scope per `docs/AI_RULES.md`, don't push to `main`.
- Report per `docs/AI_RULES.md`'s completion report format, and explicitly include the real `/embed` request/response you used to verify the fix (not just "tests passed").
