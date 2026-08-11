from fastapi import FastAPI

app = FastAPI(title="Amanah Drive AI Service")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}
