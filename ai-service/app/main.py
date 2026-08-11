from fastapi import FastAPI

from app.routers import chunk, embed, extract, health


def create_app() -> FastAPI:
    app = FastAPI(title="Amanah Drive AI Service")
    app.include_router(health.router)
    app.include_router(extract.router)
    app.include_router(chunk.router)
    app.include_router(embed.router)
    return app


app = create_app()
