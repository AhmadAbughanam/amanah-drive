# Amanah Drive

> **A secure AI-powered personal knowledge drive built with modern backend engineering practices.**

Amanah Drive is a self-hosted intelligent document management system designed for a **single user**. It combines secure file storage, semantic search, and Retrieval-Augmented Generation (RAG) to transform a personal document collection into a searchable knowledge base.

This project is intentionally focused on demonstrating **software engineering quality** rather than implementing every possible feature. Every architectural decision prioritizes clean design, security, maintainability, and scalability.

---

# Project Goals

The primary goal of Amanah Drive is to showcase professional backend engineering skills through a real-world application.

The project focuses on:

* Secure authentication and authorization
* Clean software architecture
* Modern API design
* AI integration
* Background processing
* Containerized deployment
* Database design
* Security best practices
* Scalable system design

Although V1 is built for a single user, the architecture is designed so that cloud storage, multiple users, and additional services can be added later with minimal changes.

---

# Core Features (V1)

## Secure Authentication

* Single administrator account
* Argon2id password hashing
* JWT authentication
* Refresh token rotation
* HTTP-only secure cookies
* Rate limiting
* Account lockout after repeated failed logins
* Session management
* CSRF protection where applicable

---

## Personal Drive

* Folder management
* File upload
* File download
* Rename
* Move
* Delete
* File metadata
* File previews (future)
* Local filesystem storage

---

## AI Knowledge Engine

Automatically process uploaded documents.

Pipeline:

Upload

↓

Text Extraction

↓

Chunking

↓

Embedding Generation

↓

Vector Storage

↓

Semantic Search

↓

AI Chat

Supported initially:

* PDF
* Markdown
* Plain Text

Additional document formats will be added later.

---

## Semantic Search

Instead of searching filenames:

> contract_final_v3.pdf

Users can search naturally:

> "employment agreement"

The system retrieves the most relevant document sections using vector similarity search.

---

## AI Chat

Chat with your documents using Retrieval-Augmented Generation (RAG).

Features:

* Context-aware answers
* Source citations
* Conversation history
* Retrieval from vector database
* Grounded responses

---

## Background Processing

Document processing runs asynchronously.

Upload

↓

Processing Job Created

↓

Worker

↓

Extract Text

↓

Generate Embeddings

↓

Completed

This keeps uploads responsive while supporting larger files.

---

# Technology Stack

## Frontend

* Next.js
* TypeScript
* Tailwind CSS

---

## Backend

* ASP.NET Core
* REST API
* JWT Authentication

---

## AI Service

* Python
* FastAPI
* LangChain
* Sentence Transformers
* RAG Pipeline

---

## Database

PostgreSQL

Extensions:

* pgvector

Stores:

* File metadata
* Processing jobs
* Embeddings
* Chat history
* Sessions

---

## Storage

### V1

Local filesystem on VPS.

A storage abstraction layer is implemented from the beginning so the storage backend can be replaced without affecting business logic.

Future storage providers:

* Cloudflare R2
* Amazon S3
* MinIO

---

## Infrastructure

* Docker
* Docker Compose
* Nginx
* GitHub Actions (CI)

---

# Project Architecture

```text
                   Next.js Frontend
                          │
                          ▼
                ASP.NET Core REST API
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        ▼                 ▼                 ▼
 Authentication      File Service      AI Service
        │                 │                 │
        ▼                 ▼                 ▼
 PostgreSQL      Local Filesystem      FastAPI
        │                                   │
        └───────────────┬───────────────────┘
                        ▼
                  pgvector Database
```

---

# Security

Security is a first-class design goal.

Implemented practices include:

* Argon2id password hashing
* JWT authentication
* Refresh token rotation
* HTTP-only cookies
* Secure cookie configuration
* Rate limiting
* Request validation
* MIME type validation
* File size limits
* Path traversal prevention
* Structured logging
* Environment-based configuration
* Secret management
* Secure error handling

---

# Engineering Principles

This project emphasizes engineering quality over feature quantity.

Key principles:

* Clean Architecture
* Dependency Injection
* Repository Pattern
* Storage Abstraction
* Separation of Concerns
* SOLID Principles
* Structured Logging
* Configuration via Environment Variables
* RESTful API Design
* Background Workers
* Modular Services

---

# Roadmap

## Phase 1 — Foundation

* Project setup
* Docker environment
* PostgreSQL
* Configuration system
* Logging
* Authentication

---

## Phase 2 — Secure Drive

* Folder management
* Upload
* Download
* Rename
* Delete
* Filesystem storage

---

## Phase 3 — AI Processing

* PDF extraction
* Markdown support
* Chunk generation
* Embeddings
* Vector indexing

---

## Phase 4 — Search & Chat

* Semantic search
* AI chat
* Source citations
* Conversation history

---

## Phase 5 — Production Polish

* Testing
* CI/CD
* Documentation
* Performance optimization
* Security review

---

# Future Improvements

The architecture is intentionally designed to support future expansion.

Potential future features include:

* Cloudflare R2 storage
* Multi-user support
* File sharing
* Role-based access control
* OCR
* Additional document formats
* Image understanding
* Local LLM support
* End-to-end encryption
* Mobile application
* Real-time synchronization

---

# Why This Project?

Amanah Drive is more than a file manager.

It demonstrates the ability to design and build a production-inspired software system using modern backend engineering practices, AI integration, secure authentication, asynchronous processing, containerized deployment, and scalable architecture.

The objective is to build a system that is small enough to complete independently while reflecting the design principles and implementation quality expected in professional software engineering.
