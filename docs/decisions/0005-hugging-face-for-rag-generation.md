# ADR 0005 — Hugging Face Inference API for RAG Generation

## Status

Accepted

## Context

Grounded chat answers need an LLM call. The realistic options were a major hosted provider (OpenAI, Anthropic), a self-hosted local model (via Ollama or similar), or the Hugging Face Inference API. This choice affects ongoing cost, latency, operational complexity, and how easy the system is for someone else to actually run.

## Decision

Use the Hugging Face Inference API, calling its OpenAI-compatible chat completions endpoint (`https://router.huggingface.co/v1/chat/completions`) with an instruction-tuned model. The default model was chosen empirically rather than assumed: the originally planned model (`HuggingFaceH4/zephyr-7b-beta`) was not available through the configured Hugging Face Inference Providers during implementation, so the default was switched to `openai/gpt-oss-20b` after verification. The model is overridable via the `HF_MODEL` environment variable, and the token via `HF_API_TOKEN` - never hardcoded, always excluded from version control.

## Consequences

- Generation is isolated behind one provider boundary, which keeps the rest of the system independent of Hugging Face-specific request handling.
- Model availability on Hugging Face's free tier is not fully within this project's control — it's worth periodically confirming the configured default model is still served, since providers change their supported-model lists.
- The generation call is isolated behind `ai-service`'s `/rag/answer` endpoint and a `call_hugging_face` function with distinct exception types for configuration errors, timeouts, and upstream failures (`HuggingFaceConfigurationError`, `HuggingFaceTimeoutError`, `HuggingFaceUpstreamError`), each mapped to a distinct HTTP status (503/504/502) rather than a generic failure. Swapping providers later means changing this one function and its error handling, not anything in the API or frontend.
