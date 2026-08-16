"use client";

import type { ChangeEvent, FormEvent } from "react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { apiFetch, apiJson, errorMessage } from "@/lib/api";
import type { AdminLogResponse, ChatCitation, ChatResponse, FileItem, Folder, FolderContents, SearchResponse, SearchResult } from "@/lib/types";
import { useAuth } from "../auth-provider";

type Breadcrumb = { id: string | null; name: string };
type AppView = "files" | "knowledge" | "logs";
type LogFilters = { level: string; search: string };
type ChatEntry = {
  id: string;
  role: "user" | "assistant";
  content: string;
  citations?: ChatCitation[];
};

const labelClass = "text-[11px] font-semibold uppercase tracking-[0.22em] text-black/50";
const fieldClass =
  "w-full rounded-[14px] border border-black/12 bg-white/65 px-4 py-3 text-sm text-black outline-none placeholder:text-black/35 shadow-[inset_0_1px_0_rgba(255,255,255,0.8)] transition focus:border-black/45";
const primaryButtonClass =
  "rounded-[14px] bg-black px-5 py-3 text-xs font-semibold uppercase tracking-[0.16em] text-white shadow-[0_10px_22px_rgba(0,0,0,0.18)] transition hover:bg-black/80 disabled:cursor-not-allowed disabled:bg-black/35";
const secondaryButtonClass =
  "rounded-[14px] border border-black/12 bg-white/55 px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.14em] text-black transition hover:border-black/35 hover:bg-white disabled:cursor-not-allowed disabled:opacity-40";
const iconButtonClass =
  "grid h-11 w-11 place-items-center rounded-[11px] border border-black/12 bg-white/55 text-black transition hover:border-black/35 hover:bg-white disabled:cursor-not-allowed disabled:opacity-40";
const panelClass = "rounded-[10px] border border-black/10 bg-white/35";

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
  const [logs, setLogs] = useState<AdminLogResponse | null>(null);
  const [logFilters, setLogFilters] = useState<LogFilters>({ level: "", search: "" });
  const [appliedLogFilters, setAppliedLogFilters] = useState<LogFilters>({ level: "", search: "" });
  const [logPage, setLogPage] = useState(1);
  const [logLoading, setLogLoading] = useState(false);
  const [logError, setLogError] = useState<string | null>(null);
  const [logRefreshKey, setLogRefreshKey] = useState(0);

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

  const loadLogs = useCallback(async () => {
    setLogLoading(true);
    setLogError(null);
    try {
      const params = new URLSearchParams({ page: String(logPage), pageSize: "25" });
      if (appliedLogFilters.level) {
        params.set("level", appliedLogFilters.level);
      }
      if (appliedLogFilters.search) {
        params.set("search", appliedLogFilters.search);
      }
      setLogs(await apiJson<AdminLogResponse>(`/admin/logs?${params}`));
    } catch (err) {
      setLogError(err instanceof Error ? err.message : "Unable to load system logs.");
    } finally {
      setLogLoading(false);
    }
  }, [appliedLogFilters, logPage]);

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

  useEffect(() => {
    if (status === "authenticated" && activeView === "logs") {
      void loadLogs();
    }
  }, [activeView, loadLogs, logRefreshKey, status]);

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

  function applyLogFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setLogPage(1);
    setAppliedLogFilters({
      level: logFilters.level,
      search: logFilters.search.trim(),
    });
    setLogRefreshKey((value) => value + 1);
  }

  if (status === "checking" || (status === "anonymous" && !contents)) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-[#080808] px-6 text-[#f8f7f2]">
        <p className={labelClass}>Checking session...</p>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-[#080808] px-3 py-3 text-black sm:px-6 sm:py-6">
      <div className="mx-auto min-h-[calc(100vh-1.5rem)] max-w-[1500px] overflow-hidden rounded-[22px] border border-white/10 bg-[#f8f7f2] shadow-[0_34px_120px_rgba(0,0,0,0.55)] sm:min-h-[calc(100vh-3rem)] sm:rounded-[28px]">
        <header className="border-b border-black/10 px-5 py-5 sm:px-8 lg:px-9">
          <div className="flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
            <div className="flex items-center gap-4">
              <ShieldMark />
              <div>
                <p className="text-[13px] font-bold uppercase tracking-[0.34em] text-black">Amanah Drive</p>
              </div>
            </div>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between lg:justify-end">
              <nav className="grid grid-cols-3 rounded-[10px] border border-black/12 bg-white/35 p-1 sm:min-w-[430px]" aria-label="Drive sections">
                <button
                  className={`rounded-[8px] px-4 py-3 text-sm transition ${activeView === "files" ? "bg-black text-white shadow-[0_10px_22px_rgba(0,0,0,0.20)]" : "text-black/70 hover:text-black"}`}
                  type="button"
                  onClick={() => setActiveView("files")}
                >
                  Files
                </button>
                <button
                  className={`rounded-[8px] px-4 py-3 text-sm transition ${activeView === "knowledge" ? "bg-black text-white shadow-[0_10px_22px_rgba(0,0,0,0.20)]" : "text-black/70 hover:text-black"}`}
                  type="button"
                  onClick={() => setActiveView("knowledge")}
                >
                  Search & Chat
                </button>
                <button
                  className={`rounded-[8px] px-4 py-3 text-sm transition ${activeView === "logs" ? "bg-black text-white shadow-[0_10px_22px_rgba(0,0,0,0.20)]" : "text-black/70 hover:text-black"}`}
                  type="button"
                  onClick={() => setActiveView("logs")}
                >
                  Logs
                </button>
              </nav>
              <button className="rounded-[10px] border border-black/12 bg-white/35 px-5 py-3 text-sm text-black transition hover:border-black/35 hover:bg-white" onClick={signOut} type="button">
                Logout <span aria-hidden="true" className="ml-2">[-&gt;</span>
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
        ) : activeView === "knowledge" ? (
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
        ) : (
          <LogsView
            error={logError}
            filters={logFilters}
            isLoading={logLoading}
            logs={logs}
            onApplyFilters={applyLogFilters}
            onFiltersChange={setLogFilters}
            onPageChange={setLogPage}
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
  const totalItems = (contents?.folders.length ?? 0) + (contents?.files.length ?? 0);
  const firstItem = totalItems === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const lastItem = (currentPage - 1) * pageSize + totalItems;

  return (
    <section className="px-5 py-7 sm:px-8 lg:px-9">
      <div className="mb-8">
        <h2 className="font-serif text-4xl font-normal leading-tight text-black sm:text-5xl">File management</h2>
        <p className="mt-2 text-base text-black/55">Organize, manage, and access your secure files.</p>
      </div>

      {error ? (
        <div className="mb-6 rounded-[10px] border border-red-900/25 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
          {error}
        </div>
      ) : null}

      <div className="grid gap-5 xl:grid-cols-[300px_minmax(0,1fr)] 2xl:grid-cols-[360px_minmax(0,1fr)]">
        <aside className={`${panelClass} p-5 sm:p-6`}>
          <p className={labelClass}>Create new</p>
          <div className="mt-5 rounded-[10px] border border-black/10 bg-white/45 p-4">
            <div className="flex gap-4">
              <div className="grid h-14 w-14 shrink-0 place-items-center rounded-full border border-black/8 bg-black/[0.03]">
                <FolderGlyph />
              </div>
              <div>
                <h3 className="text-base font-semibold">New Folder</h3>
                <p className="mt-1 text-sm leading-5 text-black/55">Create a new folder in your drive.</p>
              </div>
            </div>
            <input
              aria-label="Folder name"
              className={`${fieldClass} mt-5`}
              value={newFolderName}
              onChange={(event) => onNewFolderNameChange(event.target.value)}
              placeholder="Folder name"
            />
            <button className={`${primaryButtonClass} mt-3 w-full normal-case tracking-normal`} onClick={onCreateFolder} disabled={isWorking} type="button">
              Create Folder
            </button>
          </div>

          <div className="my-5 flex items-center gap-4">
            <div className="h-px flex-1 bg-black/10" />
            <span className="text-xs uppercase tracking-[0.22em] text-black/45">Or</span>
            <div className="h-px flex-1 bg-black/10" />
          </div>

          <p className={labelClass}>Upload files</p>
          <div className="mt-5 rounded-[10px] border border-black/10 bg-white/45 p-4">
            <div className="flex gap-4">
              <div className="grid h-14 w-14 shrink-0 place-items-center rounded-full border border-black/8 bg-black/[0.03]">
                <UploadGlyph />
              </div>
              <div>
                <h3 className="text-base font-semibold">Upload Files</h3>
                <p className="mt-1 text-sm leading-5 text-black/55">Add files to your drive. Supported formats below.</p>
              </div>
            </div>
            <label className="mt-5 flex min-h-[136px] cursor-pointer flex-col items-center justify-center rounded-[10px] border border-dashed border-black/22 bg-white/30 px-4 text-center text-sm text-black/55 transition hover:border-black/45 hover:bg-white/60">
              <DocumentGlyph />
              <span className="mt-3">Drag and drop files here or click to browse</span>
              <input
                className="sr-only"
                type="file"
                accept=".pdf,.md,.txt,application/pdf,text/markdown,text/plain"
                onChange={onUploadFile}
                disabled={isWorking}
              />
            </label>
            <label className={`${secondaryButtonClass} mt-4 block cursor-pointer text-center normal-case tracking-normal`}>
              Choose Files
              <input
                className="sr-only"
                type="file"
                accept=".pdf,.md,.txt,application/pdf,text/markdown,text/plain"
                onChange={onUploadFile}
                disabled={isWorking}
              />
            </label>
          </div>

          <div className="mt-5 flex gap-3 rounded-[9px] border border-black/10 bg-white/45 p-4">
            <InfoGlyph />
            <p className="text-sm leading-5 text-black/60">
              <span className="block font-semibold text-black">Supported formats</span>
              PDF, Markdown, and plain text.
            </p>
          </div>
        </aside>

        <section className={`${panelClass} overflow-hidden`}>
          <div className="flex flex-col gap-5 border-b border-black/10 px-5 py-5 sm:px-7 lg:flex-row lg:items-center lg:justify-between">
            <div>
              <p className={labelClass}>Your files</p>
              <div className="mt-3 flex flex-wrap items-center gap-2">
                {breadcrumbs.map((item, index) => (
                  <button
                    key={`${item.id ?? "root"}-${index}`}
                    className="text-base text-black transition hover:text-black/55"
                    onClick={() => onGoToBreadcrumb(index)}
                    type="button"
                  >
                    {item.name}
                    {index < breadcrumbs.length - 1 ? <span className="ml-2 text-black/35">/</span> : null}
                  </button>
                ))}
              </div>
            </div>
            <div className="flex w-full flex-col gap-3 sm:flex-row lg:w-auto">
              <div className="flex min-w-0 flex-1 items-center gap-2 rounded-[12px] border border-black/12 bg-white/55 px-4 py-3 text-sm text-black/40 lg:w-[270px]">
                <SearchGlyph />
                <span>Search files...</span>
              </div>
              <div className="grid grid-cols-2 gap-1 rounded-[12px] border border-black/12 bg-white/55 p-1">
                <span className="grid h-10 w-10 place-items-center rounded-[9px] bg-white text-black" aria-hidden="true">=</span>
                <span className="grid h-10 w-10 place-items-center text-black/55" aria-hidden="true">#</span>
              </div>
            </div>
          </div>

          <div className="hidden border-b border-black/10 px-7 py-4 text-[11px] font-semibold uppercase tracking-[0.18em] text-black/50 xl:grid xl:grid-cols-[minmax(160px,1fr)_70px_75px_130px_300px] xl:gap-3 2xl:grid-cols-[minmax(220px,1fr)_100px_100px_180px_310px] 2xl:gap-4">
            <span>Name</span>
            <span>Type</span>
            <span>Size</span>
            <span>Modified</span>
            <span>Actions</span>
          </div>

          {isLoading ? (
            <div className="p-8 text-sm text-black/60">Loading drive contents...</div>
          ) : (
            <div className="min-h-[420px] divide-y divide-black/10">
              {contents?.folders.map((folder) => (
                <div key={folder.id} className="grid gap-4 px-5 py-5 sm:px-7 xl:grid-cols-[minmax(160px,1fr)_70px_75px_130px_300px] xl:items-center xl:gap-3 2xl:grid-cols-[minmax(220px,1fr)_100px_100px_180px_310px] 2xl:gap-4">
                  <button className="flex min-w-0 items-center gap-4 text-left" onClick={() => onEnterFolder(folder)} type="button">
                    <span className="grid h-14 w-14 shrink-0 place-items-center rounded-[10px] border border-black/10 bg-white/55">
                      <FolderGlyph />
                    </span>
                    <span className="min-w-0">
                      <span className="block truncate font-semibold text-black">{folder.name}</span>
                      <span className="mt-1 block text-sm text-black/50" aria-hidden="true">Folder</span>
                    </span>
                  </button>
                  <span className="text-sm text-black/60 xl:block">Folder</span>
                  <span className="text-sm text-black/60">--</span>
                  <span className="text-sm leading-5 text-black/60">{formatDate(folder.updatedAt)}</span>
                  <div className="flex flex-wrap gap-2">
                    <button className={iconButtonClass} onClick={() => onRenameFolder(folder)} type="button" aria-label="Rename folder">
                      <PencilGlyph />
                    </button>
                    <button className={`${iconButtonClass} text-red-700`} onClick={() => onDeleteFolder(folder)} type="button" aria-label="Delete folder">
                      <TrashGlyph />
                    </button>
                  </div>
                </div>
              ))}

              {contents?.files.map((file) => (
                <div key={file.id} className="grid gap-4 px-5 py-5 sm:px-7 xl:grid-cols-[minmax(160px,1fr)_70px_75px_130px_300px] xl:items-center xl:gap-3 2xl:grid-cols-[minmax(220px,1fr)_100px_100px_180px_310px] 2xl:gap-4">
                  <div className="flex min-w-0 items-center gap-4">
                    <span className="grid h-14 w-14 shrink-0 place-items-center rounded-[10px] border border-black/10 bg-white/55">
                      <DocumentGlyph />
                    </span>
                    <div className="min-w-0">
                      <p className="truncate font-semibold text-black">{file.originalFileName}</p>
                      <p className="mt-1 text-sm text-black/50">{friendlyContentType(file.contentType)}</p>
                    </div>
                  </div>
                  <span className="w-fit rounded-[8px] border border-black/10 bg-white/55 px-3 py-2 text-xs text-black">{fileExtension(file.originalFileName)}</span>
                  <span className="text-sm text-black/60">{formatBytes(file.sizeBytes)}</span>
                  <span className="text-sm leading-5 text-black/60">{formatDate(file.updatedAt)}</span>
                  <div className="flex flex-wrap gap-2">
                    <button className={iconButtonClass} onClick={() => onDownloadFile(file)} type="button" aria-label={`Download ${file.originalFileName}`}>
                      <DownloadGlyph />
                    </button>
                    <button className={iconButtonClass} onClick={() => onRenameFile(file)} type="button" aria-label={`Rename ${file.originalFileName}`}>
                      <PencilGlyph />
                    </button>
                    <FileMoveControl
                      file={file}
                      folders={contents.folders}
                      target={moveTargets[file.id] ?? ""}
                      onMoveTargetChange={onMoveTargetChange}
                      onMoveFile={onMoveFile}
                    />
                    <button className={`${iconButtonClass} text-red-700`} onClick={() => onDeleteFile(file)} type="button" aria-label={`Delete ${file.originalFileName}`}>
                      <TrashGlyph />
                    </button>
                  </div>
                </div>
              ))}

              {contents && contents.folders.length === 0 && contents.files.length === 0 ? (
                <div className="flex min-h-[320px] items-center justify-center p-8 text-center text-sm text-black/60">This folder is empty.</div>
              ) : null}
            </div>
          )}

          <div className="flex flex-col gap-4 border-t border-black/10 px-5 py-5 text-sm text-black/55 sm:flex-row sm:items-center sm:justify-between sm:px-7">
            <span>
              Showing {firstItem} to {lastItem} of {totalItems} items
            </span>
            <div className="flex items-center gap-2">
              <button className={iconButtonClass} disabled={currentPage <= 1 || isLoading} onClick={() => onPageChange((value) => Math.max(1, value - 1))} type="button" aria-label="Previous page">
                &lt;
              </button>
              <span className="grid h-11 w-11 place-items-center rounded-[11px] bg-black text-sm font-semibold text-white">{contents?.page ?? currentPage}</span>
              <button
                className={iconButtonClass}
                disabled={isLoading || totalItems < pageSize}
                onClick={() => onPageChange((value) => value + 1)}
                type="button"
                aria-label="Next page"
              >
                &gt;
              </button>
            </div>
          </div>
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
    <section className="px-5 py-7 sm:px-8 lg:px-9">
      <div className="mb-7 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p className={labelClass}>Amanah Drive</p>
          <h2 className="mt-2 font-serif text-4xl font-normal leading-tight text-black sm:text-5xl">Search & Chat</h2>
        </div>
      </div>

      <div className="grid gap-5 xl:grid-cols-[minmax(0,0.86fr)_minmax(0,1.14fr)]">
        <section className={`${panelClass} flex min-h-[650px] flex-col p-5 sm:p-6`}>
          <div>
            <p className={labelClass}>Semantic search</p>
            <h3 className="mt-3 font-serif text-3xl font-normal leading-tight text-black">Find document passages</h3>
          </div>

          <form className="mt-6 rounded-[10px] border border-black/10 bg-white/35 p-4" onSubmit={onSearch}>
            <label className={labelClass} htmlFor="search-documents">
              Search documents
            </label>
            <div className="mt-4 flex flex-col gap-3 sm:flex-row">
              <div className="flex min-w-0 flex-1 items-center gap-3 rounded-full border border-black/12 bg-white/65 px-4">
                <SearchGlyph />
                <input
                  id="search-documents"
                  className="min-h-12 min-w-0 flex-1 bg-transparent text-sm text-black outline-none placeholder:text-black/35"
                  value={searchQuery}
                  onChange={(event) => onSearchQueryChange(event.target.value)}
                  placeholder="what is Amanah Drive"
                />
              </div>
              <button className="rounded-full bg-black px-6 py-3 text-xs font-semibold uppercase tracking-[0.2em] text-white transition hover:bg-black/80 disabled:cursor-not-allowed disabled:bg-black/35" disabled={searchLoading} type="submit">
                {searchLoading ? "Searching" : "Search"}
              </button>
            </div>
          </form>

          <div className="mt-6 flex items-center justify-between gap-4 text-sm text-black/50">
            <span>{searchResults.length} result{searchResults.length === 1 ? "" : "s"} found</span>
            <span>Sorted by relevance</span>
          </div>

          <div className="mt-6 min-h-[350px] flex-1 space-y-4">
            {searchError ? (
              <div className="rounded-[10px] border border-red-900/25 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
                {searchError}
              </div>
            ) : null}
            {searchLoading ? <p className="text-sm text-black/60">Searching processed document chunks...</p> : null}
            {!searchLoading && hasSearched && !searchError && searchResults.length === 0 ? <p className="text-sm text-black/60">No matching document sections found.</p> : null}
            {!searchLoading && !hasSearched ? <p className="text-sm leading-6 text-black/60">Search processed PDFs, Markdown, and text files by meaning, not just filenames.</p> : null}
            {searchResults.map((result) => (
              <article key={result.chunkId} className="rounded-[10px] border border-black/10 bg-white/35 p-4 sm:p-5">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                  <div className="flex min-w-0 gap-4">
                    <span className="mt-1 grid h-11 w-11 shrink-0 place-items-center rounded-[9px] border border-black/10 bg-white/55">
                      <DocumentGlyph />
                    </span>
                    <div className="min-w-0">
                      <h4 className="truncate font-serif text-2xl font-normal">{result.fileName}</h4>
                      <p className="mt-2 text-xs uppercase tracking-[0.14em] text-black/45">Semantic match</p>
                    </div>
                  </div>
                  <button className="rounded-full border border-black/12 bg-white/35 px-4 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-black transition hover:border-black/35" onClick={() => onDownloadSearchResult(result)} type="button">
                    Download <span aria-hidden="true" className="ml-2">v</span>
                  </button>
                </div>
                <div className="mt-5 flex items-center gap-3">
                  <span className={labelClass}>Relevance</span>
                  <span className="h-1.5 flex-1 rounded-full bg-black/8">
                    <span className="block h-full rounded-full bg-black" style={{ width: `${Math.max(8, Math.min(100, result.score * 100))}%` }} />
                  </span>
                  <span className="text-sm text-black/55">{formatScore(result.score)}</span>
                </div>
                <p className="mt-5 text-sm leading-7 text-black/65">{result.snippet}</p>
                <p className="mt-4 text-xs uppercase tracking-[0.14em] text-black/40">Chunk {result.chunkIndex} / Score {formatScore(result.score)}</p>
              </article>
            ))}
          </div>

          <div className="mt-6 flex gap-3 rounded-[10px] border border-black/10 bg-white/35 p-4">
            <InfoGlyph />
            <p className="text-sm leading-6 text-black/60">
              <span className="block text-xs font-semibold uppercase tracking-[0.18em] text-black">Search tips</span>
              Use natural language to find information in your documents.
            </p>
          </div>
        </section>

        <section className={`${panelClass} flex min-h-[650px] flex-col overflow-hidden`}>
          <div className="flex flex-col gap-4 border-b border-black/10 p-5 sm:flex-row sm:items-start sm:justify-between sm:p-6">
            <div>
              <p className={labelClass}>AI chat</p>
              <h3 className="mt-3 font-serif text-3xl font-normal leading-tight text-black">Ask your drive</h3>
              <p className="mt-2 text-sm text-black/50">Get answers from your documents using AI.</p>
              {conversationId ? <p className="mt-2 text-xs uppercase tracking-[0.14em] text-black/40">Conversation active</p> : null}
            </div>
            <button className="rounded-full border border-black/35 bg-white/35 px-5 py-3 text-xs font-semibold uppercase tracking-[0.18em] text-black transition hover:bg-white" onClick={onNewConversation} type="button">
              New conversation <span aria-hidden="true" className="ml-2">+</span>
            </button>
          </div>

          <div className="flex-1 space-y-5 overflow-x-hidden px-5 py-6 sm:px-8">
            {chatEntries.length === 0 ? (
              <div className="mx-auto max-w-[620px] rounded-[10px] border border-black/10 bg-white/35 p-5 text-sm leading-6 text-black/60">
                Ask a question after your files finish processing. Answers stay grounded in retrieved chunks and include source citations when available.
              </div>
            ) : null}
            {chatEntries.map((entry) => (
              <article key={entry.id} className={entry.role === "user" ? "ml-auto max-w-[760px] rounded-[10px] bg-black px-5 py-4 text-white shadow-[0_18px_35px_rgba(0,0,0,0.18)]" : "mr-auto max-w-[760px] rounded-[10px] border border-black/10 bg-white/45 px-5 py-4"}>
                <p className="text-[11px] font-semibold uppercase tracking-[0.18em] opacity-65">{entry.role === "user" ? "You" : "Amanah Drive"}</p>
                <p className="mt-3 whitespace-pre-wrap text-sm leading-7">{entry.content}</p>
                {entry.citations && entry.citations.length > 0 ? (
                  <div className="mt-5 space-y-3 border-t border-black/10 pt-4">
                    <p className={labelClass}>Sources & citations</p>
                    {entry.citations.map((citation) => (
                      <div key={`${citation.chunkId}-${citation.fileId ?? "none"}`} className="rounded-[8px] border border-black/10 bg-[#f8f7f2] p-3">
                        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                          <p className="text-sm font-semibold">{citation.fileName}</p>
                          <button className="text-xs font-semibold uppercase tracking-[0.14em] text-black/60 hover:text-black" onClick={() => onDownloadCitation(citation)} type="button">
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
              <div className="rounded-[10px] border border-red-900/25 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
                {chatError}
              </div>
            ) : null}
          </div>

          <form className="border-t border-black/10 p-5 sm:p-6" onSubmit={onAskQuestion}>
            <label className="sr-only" htmlFor="chat-question">
              Ask a question
            </label>
            <div className="rounded-[22px] border border-black/14 bg-white/55 p-3 shadow-[inset_0_1px_0_rgba(255,255,255,0.8)]">
              <textarea
                id="chat-question"
                className="min-h-16 w-full resize-none bg-transparent px-2 py-2 text-sm text-black outline-none placeholder:text-black/35"
                value={chatQuestion}
                onChange={(event) => onChatQuestionChange(event.target.value)}
                placeholder="Ask a follow-up question..."
              />
              <div className="flex items-center justify-between">
                <span className="text-black/45" aria-hidden="true">@</span>
                <button className="grid h-12 w-12 place-items-center rounded-full bg-black text-white transition hover:bg-black/80 disabled:cursor-not-allowed disabled:bg-black/35" disabled={chatLoading} type="submit">
                  <span className="sr-only">{chatLoading ? "Thinking" : "Send"}</span>
                  <SendGlyph />
                </button>
              </div>
            </div>
            <p className="mt-3 text-center text-xs text-black/45">Answers are generated from your documents. Verify important information.</p>
          </form>
        </section>
      </div>
    </section>
  );
}

function LogsView({
  error,
  filters,
  isLoading,
  logs,
  onApplyFilters,
  onFiltersChange,
  onPageChange,
}: {
  error: string | null;
  filters: LogFilters;
  isLoading: boolean;
  logs: AdminLogResponse | null;
  onApplyFilters: (event: FormEvent<HTMLFormElement>) => void;
  onFiltersChange: (filters: LogFilters) => void;
  onPageChange: (page: number) => void;
}) {
  return (
    <section className="px-5 py-7 sm:px-8 lg:px-9">
      <div className="mb-8">
        <p className={labelClass}>Administration</p>
        <h2 className="mt-3 font-serif text-4xl font-normal leading-tight text-black sm:text-5xl">System logs</h2>
        <p className="mt-2 text-base text-black/55">Inspect recent API activity and failures from persisted structured logs.</p>
      </div>

      <div className={`${panelClass} overflow-hidden`}>
        <form className="grid gap-4 border-b border-black/10 p-5 sm:grid-cols-[180px_minmax(0,1fr)_auto] sm:items-end sm:p-6" onSubmit={onApplyFilters}>
          <label className="block">
            <span className={labelClass}>Log level</span>
            <select
              className={`${fieldClass} mt-2`}
              value={filters.level}
              onChange={(event) => onFiltersChange({ ...filters, level: event.target.value })}
            >
              <option value="">All levels</option>
              <option value="Information">Information</option>
              <option value="Warning">Warning</option>
              <option value="Error">Error</option>
              <option value="Fatal">Fatal</option>
              <option value="Debug">Debug</option>
              <option value="Verbose">Verbose</option>
            </select>
          </label>
          <label className="block">
            <span className={labelClass}>Search logs</span>
            <input
              className={`${fieldClass} mt-2`}
              value={filters.search}
              onChange={(event) => onFiltersChange({ ...filters, search: event.target.value })}
              placeholder="Message, path, status, source..."
            />
          </label>
          <button className={primaryButtonClass} disabled={isLoading} type="submit">
            Apply filters
          </button>
        </form>

        {error ? (
          <div className="m-5 rounded-[10px] border border-red-900/25 bg-red-50 px-4 py-3 text-sm text-red-800 sm:m-6" role="alert">
            {error}
          </div>
        ) : null}

        <div className="min-h-[470px] divide-y divide-black/10">
          {isLoading && !logs ? <p className="p-6 text-sm text-black/55">Loading persisted logs...</p> : null}
          {!isLoading && logs?.entries.length === 0 ? <p className="p-6 text-sm text-black/55">No log entries match these filters.</p> : null}
          {logs?.entries.map((entry, index) => (
            <article className="grid gap-4 px-5 py-5 sm:px-6 lg:grid-cols-[170px_minmax(0,1fr)]" key={`${entry.timestamp}-${index}`}>
              <div>
                <span className={`inline-flex rounded-full border px-3 py-1 text-[10px] font-semibold uppercase tracking-[0.16em] ${logLevelClass(entry.level)}`}>
                  {entry.level}
                </span>
                <time className="mt-3 block text-xs leading-5 text-black/45" dateTime={entry.timestamp}>
                  {formatDate(entry.timestamp)}
                </time>
              </div>
              <div className="min-w-0">
                <p className="break-words font-mono text-sm leading-6 text-black">{entry.message}</p>
                {Object.keys(entry.properties).length > 0 ? (
                  <pre className="mt-3 overflow-x-auto rounded-[8px] border border-black/8 bg-black/[0.03] p-3 text-xs leading-5 text-black/55">
                    {JSON.stringify(entry.properties, null, 2)}
                  </pre>
                ) : null}
                {entry.exception ? (
                  <pre className="mt-3 overflow-x-auto whitespace-pre-wrap rounded-[8px] border border-red-900/20 bg-red-50 p-3 text-xs leading-5 text-red-900">
                    {entry.exception}
                  </pre>
                ) : null}
              </div>
            </article>
          ))}
        </div>

        <div className="flex items-center justify-between gap-4 border-t border-black/10 px-5 py-5 text-sm text-black/55 sm:px-6">
          <span>Page {logs?.page ?? 1}</span>
          <div className="flex items-center gap-2">
            <button
              className={secondaryButtonClass}
              disabled={isLoading || (logs?.page ?? 1) <= 1}
              onClick={() => onPageChange(Math.max(1, (logs?.page ?? 1) - 1))}
              type="button"
            >
              Previous
            </button>
            <button
              className={secondaryButtonClass}
              disabled={isLoading || !logs?.hasMore}
              onClick={() => onPageChange((logs?.page ?? 1) + 1)}
              type="button"
            >
              Next
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}

function FileMoveControl({
  file,
  folders,
  target,
  onMoveTargetChange,
  onMoveFile,
}: {
  file: FileItem;
  folders: Folder[];
  target: string;
  onMoveTargetChange: (fileId: string, value: string) => void;
  onMoveFile: (file: FileItem) => Promise<void>;
}) {
  return (
    <div className="flex rounded-[11px] border border-black/12 bg-white/55">
      <select
        aria-label={`Move ${file.originalFileName} to folder`}
        className="max-w-[92px] rounded-l-[11px] bg-transparent px-2 text-xs text-black outline-none"
        value={target}
        onChange={(event) => onMoveTargetChange(file.id, event.target.value)}
      >
        <option value="">Root</option>
        {folders.map((folder) => (
          <option key={folder.id} value={folder.id}>
            {folder.name}
          </option>
        ))}
      </select>
      <button className="grid h-11 w-11 place-items-center border-l border-black/10 text-black transition hover:bg-white" onClick={() => onMoveFile(file)} type="button" aria-label={`Move ${file.originalFileName}`}>
        <FolderGlyph />
      </button>
    </div>
  );
}

function ShieldMark() {
  return (
    <span className="grid h-9 w-9 place-items-center rounded-[7px] border-2 border-black" aria-hidden="true">
      <svg className="h-6 w-6" viewBox="0 0 24 24" fill="none">
        <path d="M12 3l7 2.8v5.4c0 4.4-2.8 7.9-7 9.8-4.2-1.9-7-5.4-7-9.8V5.8L12 3z" stroke="currentColor" strokeWidth="1.8" />
        <path d="M12 8v7m-3.2-3.2L12 15l4.2-5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </span>
  );
}

function FolderGlyph() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M3.5 7.5h6l2 2h9v8.8a2.2 2.2 0 0 1-2.2 2.2H5.7a2.2 2.2 0 0 1-2.2-2.2V7.5z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
    </svg>
  );
}

function UploadGlyph() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M12 16V4m0 0L7.5 8.5M12 4l4.5 4.5M5 15v3.5A1.5 1.5 0 0 0 6.5 20h11a1.5 1.5 0 0 0 1.5-1.5V15" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function DocumentGlyph() {
  return (
    <svg className="h-6 w-6" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M7 3.5h6.5L18 8v12.5H7V3.5z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
      <path d="M13.5 3.5V8H18" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
    </svg>
  );
}

function SearchGlyph() {
  return (
    <svg className="h-5 w-5 shrink-0" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="m20 20-4.3-4.3m1.3-5.2a6.5 6.5 0 1 1-13 0 6.5 6.5 0 0 1 13 0z" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}

function InfoGlyph() {
  return (
    <svg className="mt-0.5 h-5 w-5 shrink-0" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z" stroke="currentColor" strokeWidth="1.8" />
      <path d="M12 10v6m0-9h.01" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}

function DownloadGlyph() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M12 4v11m0 0 4-4m-4 4-4-4M5 19h14" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function PencilGlyph() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M4 20h4l10.5-10.5a2.1 2.1 0 0 0-3-3L5 17v3z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
      <path d="m14 8 3 3" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}

function TrashGlyph() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M5 7h14m-9 4v6m4-6v6M8 7l1-3h6l1 3m-9 0 1 13h8l1-13" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function SendGlyph() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="m4 12 16-8-5.5 16-3-6.5L4 12z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
    </svg>
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

function fileExtension(fileName: string) {
  const extension = fileName.split(".").pop();
  return extension && extension !== fileName ? extension.toLowerCase() : "file";
}

function friendlyContentType(contentType: string) {
  if (contentType.includes("/")) {
    return contentType.split("/").join(" / ").toUpperCase();
  }
  return contentType;
}

function formatDate(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "Unknown";
  }
  return new Intl.DateTimeFormat("en", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
}

function logLevelClass(level: string) {
  switch (level.toLowerCase()) {
    case "fatal":
    case "error":
      return "border-red-900/20 bg-red-50 text-red-800";
    case "warning":
      return "border-amber-900/20 bg-amber-50 text-amber-900";
    case "debug":
    case "verbose":
      return "border-black/10 bg-black/[0.03] text-black/55";
    default:
      return "border-emerald-900/20 bg-emerald-50 text-emerald-900";
  }
}
