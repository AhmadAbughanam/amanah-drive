"use client";

import type { ChangeEvent, FormEvent } from "react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { apiFetch, apiJson, errorMessage } from "@/lib/api";
import type { ChatCitation, ChatResponse, FileItem, Folder, FolderContents, SearchResponse, SearchResult } from "@/lib/types";
import { useAuth } from "../auth-provider";

type Breadcrumb = { id: string | null; name: string };
type AppView = "files" | "knowledge";
type ChatEntry = {
  id: string;
  role: "user" | "assistant";
  content: string;
  citations?: ChatCitation[];
};

const panelClass = "rounded-[28px] border border-black/10 bg-[#f8f7f2] shadow-[0_24px_80px_rgba(0,0,0,0.16)]";
const labelClass = "text-[11px] font-semibold uppercase tracking-[0.22em] text-black/60";
const inputClass = "w-full rounded-none border-0 border-b border-black/25 bg-transparent px-0 py-3 text-sm text-black outline-none placeholder:text-black/35 focus:border-black";
const primaryButtonClass = "rounded-full bg-black px-5 py-2.5 text-xs font-semibold uppercase tracking-[0.18em] text-[#f8f7f2] transition hover:bg-black/80 disabled:cursor-not-allowed disabled:bg-black/35";
const secondaryButtonClass = "rounded-full border border-black/20 px-4 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-black transition hover:border-black disabled:cursor-not-allowed disabled:opacity-40";

export default function DrivePage() {
  const router = useRouter();
  const { status, ensureSession, signOut } = useAuth();
  const [activeView, setActiveView] = useState<AppView>("files");
  const [contents, setContents] = useState<FolderContents | null>(null);
  const [breadcrumbs, setBreadcrumbs] = useState<Breadcrumb[]>([{ id: null, name: "Root" }]);
  const [currentFolderId, setCurrentFolderId] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [newFolderName, setNewFolderName] = useState("");
  const [isLoading, setLoading] = useState(true);
  const [isWorking, setWorking] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [moveTargets, setMoveTargets] = useState<Record<string, string>>({});
  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState<SearchResult[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [hasSearched, setHasSearched] = useState(false);
  const [chatQuestion, setChatQuestion] = useState("");
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [chatEntries, setChatEntries] = useState<ChatEntry[]>([]);
  const [chatLoading, setChatLoading] = useState(false);
  const [chatError, setChatError] = useState<string | null>(null);

  const folderQuery = useMemo(() => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (currentFolderId) {
      params.set("parentFolderId", currentFolderId);
    }
    return params.toString();
  }, [currentFolderId, page, pageSize]);

  const loadContents = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await apiJson<FolderContents>(`/drive/folders?${folderQuery}`);
      setContents(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load drive contents.");
    } finally {
      setLoading(false);
    }
  }, [folderQuery]);

  useEffect(() => {
    let cancelled = false;
    ensureSession().then((ok) => {
      if (!ok && !cancelled) {
        router.push("/login");
      }
    });
    return () => {
      cancelled = true;
    };
  }, [ensureSession, router]);

  useEffect(() => {
    if (status === "authenticated") {
      void loadContents();
    }
  }, [loadContents, status]);

  function enterFolder(folder: Folder) {
    setCurrentFolderId(folder.id);
    setBreadcrumbs((items) => [...items, { id: folder.id, name: folder.name }]);
    setPage(1);
  }

  function goToBreadcrumb(index: number) {
    const next = breadcrumbs[index];
    setBreadcrumbs(breadcrumbs.slice(0, index + 1));
    setCurrentFolderId(next.id);
    setPage(1);
  }

  async function runAction(action: () => Promise<void>) {
    setWorking(true);
    setError(null);
    try {
      await action();
      await loadContents();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Action failed.");
    } finally {
      setWorking(false);
    }
  }

  async function createFolder() {
    const name = newFolderName.trim();
    if (!name) {
      return;
    }

    await runAction(async () => {
      await apiJson<Folder>("/drive/folders", {
        method: "POST",
        body: JSON.stringify({ name, parentFolderId: currentFolderId }),
      });
      setNewFolderName("");
    });
  }

  async function renameFolder(folder: Folder) {
    const name = window.prompt("Rename folder", folder.name)?.trim();
    if (!name || name === folder.name) {
      return;
    }

    await runAction(async () => {
      await apiJson<Folder>(`/drive/folders/${folder.id}/rename`, {
        method: "PATCH",
        body: JSON.stringify({ name }),
      });
    });
  }

  async function deleteFolder(folder: Folder) {
    if (!window.confirm(`Delete folder "${folder.name}" and all of its contents?`)) {
      return;
    }

    await runAction(async () => {
      const response = await apiFetch(`/drive/folders/${folder.id}`, { method: "DELETE" });
      if (!response.ok) {
        throw new Error(await errorMessage(response));
      }
    });
  }

  async function uploadFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) {
      return;
    }

    await runAction(async () => {
      const form = new FormData();
      form.append("file", file);
      if (currentFolderId) {
        form.append("folderId", currentFolderId);
      }
      const response = await apiFetch("/drive/files/upload", {
        method: "POST",
        body: form,
      });
      if (!response.ok) {
        throw new Error(await errorMessage(response));
      }
    });
  }

  async function renameFile(file: FileItem) {
    const name = window.prompt("Rename file", file.originalFileName)?.trim();
    if (!name || name === file.originalFileName) {
      return;
    }

    await runAction(async () => {
      await apiJson<FileItem>(`/drive/files/${file.id}/rename`, {
        method: "PATCH",
        body: JSON.stringify({ name }),
      });
    });
  }

  async function moveFile(file: FileItem) {
    const target = moveTargets[file.id] ?? "";
    await runAction(async () => {
      await apiJson<FileItem>(`/drive/files/${file.id}/move`, {
        method: "PATCH",
        body: JSON.stringify({ folderId: target || null }),
      });
      setMoveTargets((items) => ({ ...items, [file.id]: "" }));
    });
  }

  async function deleteFile(file: FileItem) {
    if (!window.confirm(`Delete file "${file.originalFileName}"?`)) {
      return;
    }

    await runAction(async () => {
      const response = await apiFetch(`/drive/files/${file.id}`, { method: "DELETE" });
      if (!response.ok) {
        throw new Error(await errorMessage(response));
      }
    });
  }

  async function downloadSource(fileId: string | null | undefined, fileName: string, onFailure: (message: string | null) => void) {
    if (!fileId) {
      onFailure("This citation is not linked to a downloadable file.");
      return;
    }

    onFailure(null);
    try {
      const response = await apiFetch(`/drive/files/${fileId}/download`);
      if (!response.ok) {
        throw new Error(await errorMessage(response));
      }

      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = fileName || "source-file";
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      onFailure(err instanceof Error ? err.message : "Download failed.");
    }
  }

  async function downloadFile(file: FileItem) {
    await downloadSource(file.id, file.originalFileName, setError);
  }

  async function runSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const query = searchQuery.trim();
    setHasSearched(true);
    setSearchError(null);
    if (!query) {
      setSearchResults([]);
      setSearchError("Enter a search query before searching.");
      return;
    }

    setSearchLoading(true);
    try {
      const params = new URLSearchParams({ query, topK: "8" });
      const data = await apiJson<SearchResponse>(`/search?${params}`);
      setSearchResults(data.results);
    } catch (err) {
      setSearchResults([]);
      setSearchError(err instanceof Error ? err.message : "Search failed.");
    } finally {
      setSearchLoading(false);
    }
  }

  async function askQuestion(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const question = chatQuestion.trim();
    setChatError(null);
    if (!question) {
      setChatError("Enter a question before sending.");
      return;
    }

    const userEntry: ChatEntry = { id: crypto.randomUUID(), role: "user", content: question };
    setChatEntries((items) => [...items, userEntry]);
    setChatQuestion("");
    setChatLoading(true);

    try {
      const payload = conversationId ? { question, conversationId } : { question };
      const response = await apiJson<ChatResponse>("/chat", {
        method: "POST",
        body: JSON.stringify(payload),
      });
      setConversationId(response.conversationId);
      setChatEntries((items) => [
        ...items,
        {
          id: crypto.randomUUID(),
          role: "assistant",
          content: response.answer,
          citations: response.citations,
        },
      ]);
    } catch (err) {
      setChatError(err instanceof Error ? err.message : "Chat failed. Try again in a moment.");
    } finally {
      setChatLoading(false);
    }
  }

  function startNewConversation() {
    setConversationId(null);
    setChatEntries([]);
    setChatQuestion("");
    setChatError(null);
  }

  if (status === "checking" || (status === "anonymous" && !contents)) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-[#080808] px-6 text-[#f8f7f2]">
        <p className={labelClass}>Checking session...</p>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-[#080808] px-4 py-4 text-black sm:px-6 sm:py-6">
      <div className={`${panelClass} mx-auto min-h-[calc(100vh-2rem)] max-w-7xl overflow-hidden sm:min-h-[calc(100vh-3rem)]`}>
        <header className="border-b border-black/10 px-6 py-6 md:px-10">
          <div className="flex flex-wrap items-center justify-between gap-5">
            <div>
              <p className={labelClass}>Amanah Drive</p>
              <h1 className="mt-2 font-serif text-3xl font-normal tracking-normal text-black">File management</h1>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <nav className="flex rounded-full border border-black/15 p-1" aria-label="Drive sections">
                <button
                  className={`rounded-full px-4 py-2 text-xs font-semibold uppercase tracking-[0.16em] transition ${activeView === "files" ? "bg-black text-[#f8f7f2]" : "text-black/55 hover:text-black"}`}
                  type="button"
                  onClick={() => setActiveView("files")}
                >
                  Files
                </button>
                <button
                  className={`rounded-full px-4 py-2 text-xs font-semibold uppercase tracking-[0.16em] transition ${activeView === "knowledge" ? "bg-black text-[#f8f7f2]" : "text-black/55 hover:text-black"}`}
                  type="button"
                  onClick={() => setActiveView("knowledge")}
                >
                  Search & Chat
                </button>
              </nav>
              <button className={secondaryButtonClass} onClick={signOut} type="button">
                Logout
              </button>
            </div>
          </div>
        </header>

        {activeView === "files" ? (
          <FilesView
            breadcrumbs={breadcrumbs}
            contents={contents}
            currentPage={page}
            error={error}
            isLoading={isLoading}
            isWorking={isWorking}
            moveTargets={moveTargets}
            newFolderName={newFolderName}
            pageSize={pageSize}
            onCreateFolder={createFolder}
            onDeleteFile={deleteFile}
            onDeleteFolder={deleteFolder}
            onDownloadFile={downloadFile}
            onEnterFolder={enterFolder}
            onGoToBreadcrumb={goToBreadcrumb}
            onMoveFile={moveFile}
            onMoveTargetChange={(fileId, value) => setMoveTargets((items) => ({ ...items, [fileId]: value }))}
            onPageChange={setPage}
            onRenameFile={renameFile}
            onRenameFolder={renameFolder}
            onUploadFile={uploadFile}
            onNewFolderNameChange={setNewFolderName}
          />
        ) : (
          <KnowledgeView
            chatEntries={chatEntries}
            chatError={chatError}
            chatLoading={chatLoading}
            chatQuestion={chatQuestion}
            conversationId={conversationId}
            hasSearched={hasSearched}
            searchError={searchError}
            searchLoading={searchLoading}
            searchQuery={searchQuery}
            searchResults={searchResults}
            onAskQuestion={askQuestion}
            onChatQuestionChange={setChatQuestion}
            onDownloadCitation={(citation) => downloadSource(citation.fileId, citation.fileName, setChatError)}
            onDownloadSearchResult={(result) => downloadSource(result.fileId, result.fileName, setSearchError)}
            onNewConversation={startNewConversation}
            onSearch={runSearch}
            onSearchQueryChange={setSearchQuery}
          />
        )}
      </div>
    </main>
  );
}

function FilesView({
  breadcrumbs,
  contents,
  currentPage,
  error,
  isLoading,
  isWorking,
  moveTargets,
  newFolderName,
  pageSize,
  onCreateFolder,
  onDeleteFile,
  onDeleteFolder,
  onDownloadFile,
  onEnterFolder,
  onGoToBreadcrumb,
  onMoveFile,
  onMoveTargetChange,
  onNewFolderNameChange,
  onPageChange,
  onRenameFile,
  onRenameFolder,
  onUploadFile,
}: {
  breadcrumbs: Breadcrumb[];
  contents: FolderContents | null;
  currentPage: number;
  error: string | null;
  isLoading: boolean;
  isWorking: boolean;
  moveTargets: Record<string, string>;
  newFolderName: string;
  pageSize: number;
  onCreateFolder: () => Promise<void>;
  onDeleteFile: (file: FileItem) => Promise<void>;
  onDeleteFolder: (folder: Folder) => Promise<void>;
  onDownloadFile: (file: FileItem) => Promise<void>;
  onEnterFolder: (folder: Folder) => void;
  onGoToBreadcrumb: (index: number) => void;
  onMoveFile: (file: FileItem) => Promise<void>;
  onMoveTargetChange: (fileId: string, value: string) => void;
  onNewFolderNameChange: (value: string) => void;
  onPageChange: (updater: (value: number) => number) => void;
  onRenameFile: (file: FileItem) => Promise<void>;
  onRenameFolder: (folder: Folder) => Promise<void>;
  onUploadFile: (event: ChangeEvent<HTMLInputElement>) => Promise<void>;
}) {
  return (
    <section className="px-6 py-7 md:px-10 md:py-10">
      <div className="mb-6 flex flex-wrap items-center gap-2 text-sm">
        {breadcrumbs.map((item, index) => (
          <button
            key={`${item.id ?? "root"}-${index}`}
            className="border-b border-transparent px-1 py-1 text-black/65 transition hover:border-black hover:text-black"
            onClick={() => onGoToBreadcrumb(index)}
            type="button"
          >
            {item.name}
          </button>
        ))}
      </div>

      {error ? (
        <div className="mb-6 border border-red-900/25 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
          {error}
        </div>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-[340px_1fr]">
        <aside className="space-y-6">
          <div className="border border-black/10 bg-white/55 p-5">
            <h2 className="font-serif text-2xl font-normal">Create folder</h2>
            <div className="mt-5 flex gap-3">
              <input
                aria-label="Folder name"
                className={inputClass}
                value={newFolderName}
                onChange={(event) => onNewFolderNameChange(event.target.value)}
                placeholder="Folder name"
              />
              <button className={primaryButtonClass} onClick={onCreateFolder} disabled={isWorking} type="button">
                Add
              </button>
            </div>
          </div>

          <div className="border border-black/10 bg-white/55 p-5">
            <h2 className="font-serif text-2xl font-normal">Upload file</h2>
            <p className="mt-3 text-sm leading-6 text-black/60">Supported: PDF, Markdown, and plain text.</p>
            <input
              className="mt-5 block w-full text-sm text-black/70 file:mr-3 file:rounded-full file:border-0 file:bg-black file:px-4 file:py-2 file:text-xs file:font-semibold file:uppercase file:tracking-[0.16em] file:text-[#f8f7f2] hover:file:bg-black/80"
              type="file"
              accept=".pdf,.md,.txt,application/pdf,text/markdown,text/plain"
              onChange={onUploadFile}
              disabled={isWorking}
            />
          </div>
        </aside>

        <section className="border border-black/10 bg-white/55">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-black/10 px-5 py-4">
            <p className={labelClass}>Contents</p>
            <div className="flex items-center gap-3 text-sm text-black/60">
              <button className={secondaryButtonClass} disabled={currentPage <= 1 || isLoading} onClick={() => onPageChange((value) => Math.max(1, value - 1))} type="button">
                Previous
              </button>
              <span>Page {contents?.page ?? currentPage}</span>
              <button
                className={secondaryButtonClass}
                disabled={isLoading || ((contents?.folders.length ?? 0) + (contents?.files.length ?? 0) < pageSize)}
                onClick={() => onPageChange((value) => value + 1)}
                type="button"
              >
                Next
              </button>
            </div>
          </div>

          {isLoading ? (
            <div className="p-8 text-sm text-black/60">Loading drive contents...</div>
          ) : (
            <div className="divide-y divide-black/10">
              {contents?.folders.map((folder) => (
                <div key={folder.id} className="grid gap-3 px-5 py-4 md:grid-cols-[1fr_auto] md:items-center">
                  <button className="text-left font-serif text-2xl font-normal text-black transition hover:text-black/65" onClick={() => onEnterFolder(folder)} type="button">
                    {folder.name}
                  </button>
                  <div className="flex flex-wrap gap-2">
                    <button className={secondaryButtonClass} onClick={() => onRenameFolder(folder)} type="button">
                      Rename
                    </button>
                    <button className="rounded-full border border-red-900/25 px-4 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-red-800 transition hover:bg-red-50" onClick={() => onDeleteFolder(folder)} type="button">
                      Delete
                    </button>
                  </div>
                </div>
              ))}

              {contents?.files.map((file) => (
                <div key={file.id} className="grid gap-4 px-5 py-4 xl:grid-cols-[1fr_auto] xl:items-center">
                  <div>
                    <p className="font-serif text-2xl font-normal text-black">{file.originalFileName}</p>
                    <p className="mt-1 text-xs uppercase tracking-[0.14em] text-black/45">
                      {file.contentType} / {formatBytes(file.sizeBytes)}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <button className={secondaryButtonClass} onClick={() => onDownloadFile(file)} type="button">
                      Download
                    </button>
                    <button className={secondaryButtonClass} onClick={() => onRenameFile(file)} type="button">
                      Rename
                    </button>
                    <select
                      aria-label={`Move ${file.originalFileName} to folder`}
                      className="rounded-full border border-black/20 bg-transparent px-3 py-2 text-xs font-semibold uppercase tracking-[0.12em] text-black"
                      value={moveTargets[file.id] ?? ""}
                      onChange={(event) => onMoveTargetChange(file.id, event.target.value)}
                    >
                      <option value="">Root</option>
                      {contents.folders.map((folder) => (
                        <option key={folder.id} value={folder.id}>
                          {folder.name}
                        </option>
                      ))}
                    </select>
                    <button className={secondaryButtonClass} onClick={() => onMoveFile(file)} type="button">
                      Move
                    </button>
                    <button className="rounded-full border border-red-900/25 px-4 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-red-800 transition hover:bg-red-50" onClick={() => onDeleteFile(file)} type="button">
                      Delete
                    </button>
                  </div>
                </div>
              ))}

              {contents && contents.folders.length === 0 && contents.files.length === 0 ? (
                <div className="p-8 text-sm text-black/60">This folder is empty.</div>
              ) : null}
            </div>
          )}
        </section>
      </div>
    </section>
  );
}

function KnowledgeView({
  chatEntries,
  chatError,
  chatLoading,
  chatQuestion,
  conversationId,
  hasSearched,
  searchError,
  searchLoading,
  searchQuery,
  searchResults,
  onAskQuestion,
  onChatQuestionChange,
  onDownloadCitation,
  onDownloadSearchResult,
  onNewConversation,
  onSearch,
  onSearchQueryChange,
}: {
  chatEntries: ChatEntry[];
  chatError: string | null;
  chatLoading: boolean;
  chatQuestion: string;
  conversationId: string | null;
  hasSearched: boolean;
  searchError: string | null;
  searchLoading: boolean;
  searchQuery: string;
  searchResults: SearchResult[];
  onAskQuestion: (event: FormEvent<HTMLFormElement>) => Promise<void>;
  onChatQuestionChange: (value: string) => void;
  onDownloadCitation: (citation: ChatCitation) => void;
  onDownloadSearchResult: (result: SearchResult) => void;
  onNewConversation: () => void;
  onSearch: (event: FormEvent<HTMLFormElement>) => Promise<void>;
  onSearchQueryChange: (value: string) => void;
}) {
  return (
    <section className="grid gap-6 px-6 py-7 md:px-10 md:py-10 xl:grid-cols-[minmax(0,0.92fr)_minmax(0,1.08fr)]">
      <div className="border border-black/10 bg-white/55">
        <div className="border-b border-black/10 px-5 py-5">
          <p className={labelClass}>Semantic search</p>
          <h2 className="mt-2 font-serif text-3xl font-normal">Find document passages</h2>
        </div>
        <form className="border-b border-black/10 px-5 py-5" onSubmit={onSearch}>
          <label className={labelClass} htmlFor="search-documents">
            Search documents
          </label>
          <div className="mt-3 flex flex-col gap-3 sm:flex-row">
            <input
              id="search-documents"
              className={inputClass}
              value={searchQuery}
              onChange={(event) => onSearchQueryChange(event.target.value)}
              placeholder="Ask in natural language"
            />
            <button className={primaryButtonClass} disabled={searchLoading} type="submit">
              {searchLoading ? "Searching" : "Search"}
            </button>
          </div>
        </form>

        <div className="min-h-[340px] divide-y divide-black/10">
          {searchError ? (
            <div className="m-5 border border-red-900/25 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
              {searchError}
            </div>
          ) : null}
          {searchLoading ? <p className="p-5 text-sm text-black/60">Searching processed document chunks...</p> : null}
          {!searchLoading && hasSearched && !searchError && searchResults.length === 0 ? <p className="p-5 text-sm text-black/60">No matching document sections found.</p> : null}
          {!searchLoading && !hasSearched ? <p className="p-5 text-sm text-black/60">Search processed PDFs, Markdown, and text files by meaning, not just filenames.</p> : null}
          {searchResults.map((result) => (
            <article key={result.chunkId} className="px-5 py-5">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="font-serif text-2xl font-normal">{result.fileName}</p>
                  <p className="mt-1 text-xs uppercase tracking-[0.14em] text-black/45">
                    Chunk {result.chunkIndex} / Score {formatScore(result.score)}
                  </p>
                </div>
                <button className={secondaryButtonClass} onClick={() => onDownloadSearchResult(result)} type="button">
                  Download
                </button>
              </div>
              <p className="mt-4 text-sm leading-6 text-black/65">{result.snippet}</p>
            </article>
          ))}
        </div>
      </div>

      <div className="border border-black/10 bg-white/55">
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-black/10 px-5 py-5">
          <div>
            <p className={labelClass}>AI chat</p>
            <h2 className="mt-2 font-serif text-3xl font-normal">Ask your drive</h2>
            {conversationId ? <p className="mt-2 text-xs uppercase tracking-[0.12em] text-black/40">Conversation active</p> : null}
          </div>
          <button className={secondaryButtonClass} onClick={onNewConversation} type="button">
            New conversation
          </button>
        </div>

        <div className="min-h-[360px] space-y-4 border-b border-black/10 px-5 py-5">
          {chatEntries.length === 0 ? <p className="text-sm leading-6 text-black/60">Ask a question after your files finish processing. Answers stay grounded in retrieved chunks and include source citations when available.</p> : null}
          {chatEntries.map((entry) => (
            <article key={entry.id} className={entry.role === "user" ? "ml-auto max-w-[88%] border border-black bg-black px-4 py-3 text-[#f8f7f2]" : "max-w-[92%] border border-black/10 bg-[#f8f7f2] px-4 py-4"}>
              <p className="text-[11px] font-semibold uppercase tracking-[0.18em] opacity-65">{entry.role === "user" ? "You" : "Amanah Drive"}</p>
              <p className="mt-2 whitespace-pre-wrap text-sm leading-6">{entry.content}</p>
              {entry.citations && entry.citations.length > 0 ? (
                <div className="mt-4 space-y-3 border-t border-black/10 pt-4">
                  <p className={labelClass}>Citations</p>
                  {entry.citations.map((citation) => (
                    <div key={`${citation.chunkId}-${citation.fileId ?? "none"}`} className="border border-black/10 bg-white/60 p-3">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <p className="text-sm font-semibold">{citation.fileName}</p>
                        <button className={secondaryButtonClass} onClick={() => onDownloadCitation(citation)} type="button">
                          Download
                        </button>
                      </div>
                      <p className="mt-2 text-sm leading-6 text-black/60">{citation.snippet}</p>
                    </div>
                  ))}
                </div>
              ) : null}
            </article>
          ))}
          {chatLoading ? <p className="text-sm text-black/60">Waiting for a grounded answer...</p> : null}
          {chatError ? (
            <div className="border border-red-900/25 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
              {chatError}
            </div>
          ) : null}
        </div>

        <form className="px-5 py-5" onSubmit={onAskQuestion}>
          <label className={labelClass} htmlFor="chat-question">
            Ask a question
          </label>
          <textarea
            id="chat-question"
            className={`${inputClass} min-h-28 resize-y`}
            value={chatQuestion}
            onChange={(event) => onChatQuestionChange(event.target.value)}
            placeholder="What does my document say about..."
          />
          <div className="mt-4 flex justify-end">
            <button className={primaryButtonClass} disabled={chatLoading} type="submit">
              {chatLoading ? "Thinking" : "Send"}
            </button>
          </div>
        </form>
      </div>
    </section>
  );
}

function formatBytes(bytes: number) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function formatScore(score: number) {
  return score.toFixed(3);
}
