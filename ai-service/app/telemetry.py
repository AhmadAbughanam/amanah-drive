import os

from fastapi import FastAPI
from opentelemetry import trace
from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.httpx import HTTPXClientInstrumentor
from opentelemetry.sdk.resources import Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor

DEFAULT_SERVICE_NAME = "amanah-drive-ai-service"


def configure_tracing(app: FastAPI) -> None:
    if not _tracing_enabled():
        return

    resource = Resource.create(
        {"service.name": os.environ.get("OTEL_SERVICE_NAME", DEFAULT_SERVICE_NAME)}
    )
    provider = TracerProvider(resource=resource)
    provider.add_span_processor(BatchSpanProcessor(OTLPSpanExporter()))
    trace.set_tracer_provider(provider)

    FastAPIInstrumentor.instrument_app(
        app,
        tracer_provider=provider,
        excluded_urls="/health.*",
    )
    HTTPXClientInstrumentor().instrument(tracer_provider=provider)
    app.state.tracer_provider = provider


def _tracing_enabled() -> bool:
    return os.environ.get("OTEL_TRACING_ENABLED", "false").lower() in {
        "1",
        "true",
        "yes",
    }
