FROM python:3.11-slim

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

RUN groupadd --gid 1000 appuser \
    && useradd --uid 1000 --gid appuser --create-home --shell /usr/sbin/nologin appuser

COPY app ./app
RUN chown -R appuser:appuser /app

EXPOSE 8000

# Uses Python directly rather than curl/wget so no extra package needs to be
# installed in this slim image just for the health check.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8000/health', timeout=3)" || exit 1

USER appuser
CMD ["fastapi", "run", "app/main.py", "--host", "0.0.0.0", "--port", "8000"]
