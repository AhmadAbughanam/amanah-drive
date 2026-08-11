# API Reference

This is a scannable reference for the public ASP.NET Core API endpoints implemented so far. The internal AI service contract is documented separately in [AI Service Contract](ai-service-contract.md).

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/health` | No | Service health probe. |
| POST | `/auth/register` | Bootstrap token header | Create the single admin account during bootstrap. |
| POST | `/auth/login` | No | Exchange admin credentials for a JWT access token and refresh cookie. |
| POST | `/auth/refresh` | Refresh cookie | Rotate the refresh token and issue a new access token. |
| POST | `/auth/logout` | Refresh cookie | Revoke the current refresh token and clear the refresh cookie. |
| GET | `/drive/folders` | Bearer JWT | List folders and files for a parent folder with `page` and `pageSize`. |
| POST | `/drive/folders` | Bearer JWT | Create a folder. |
| PATCH | `/drive/folders/{folderId}/rename` | Bearer JWT | Rename a folder. |
| DELETE | `/drive/folders/{folderId}` | Bearer JWT | Delete a folder, descendants, metadata, and stored files. |
| POST | `/drive/files/upload` | Bearer JWT | Upload a PDF, Markdown, or plain text file and create a processing job. |
| GET | `/drive/files/{fileId}/download` | Bearer JWT | Download a stored file. |
| PATCH | `/drive/files/{fileId}/rename` | Bearer JWT | Rename a file. |
| PATCH | `/drive/files/{fileId}/move` | Bearer JWT | Move a file to another folder or the root. |
| DELETE | `/drive/files/{fileId}` | Bearer JWT | Delete file metadata, stored bytes, processing job, and chunks. |
| GET | `/search` | Bearer JWT | Semantic search over processed chunks with `query` and optional `topK`. |
| POST | `/chat` | Bearer JWT | Retrieve relevant chunks, ask the AI service for a grounded answer, and persist the exchange. |
| GET | `/chat/{conversationId}` | Bearer JWT | Return conversation message history with `page` and `pageSize`. |
