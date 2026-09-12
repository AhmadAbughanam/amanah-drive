"use client";

import type { ChangeEvent, FormEvent } from "react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import ReactMarkdown from "react-markdown";
import { Area, AreaChart, Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { portfolioClasses, portfolioPalette, SectionLabel, statusTone } from "@/components/portfolio-theme";
import { apiFetch, apiJson, errorMessage } from "@/lib/api";
import type { ActivityResponse, AdminLogResponse, AgentRunResponse, AgentRunStepResponse, ChatCitation, ChatHistoryResponse, ChatMessageResponse, ChatResponse, FileItem, Folder, FolderContents, ObservabilitySnapshot, SearchResponse, SearchResult } from "@/lib/types";
import { useAuth } from "../auth-provider";

type Breadcrumb = { id: string | null; name: string };
type AppView = "files" | "knowledge" | "logs" | "agent";
type ObservabilityCategory = "api" | "security" | "ai" | "activity" | "errors";
type LogFilters = { level: string; search: string; source: string; from: string; to: string };
type ChatEntry = {
  id: string;
  role: "user" | "assistant";
  content: string;
  citations?: ChatCitation[];
};

const labelClass = portfolioClasses.label;
const fieldClass = portfolioClasses.field;
const primaryButtonClass = portfolioClasses.primaryButton;
const secondaryButtonClass = portfolioClasses.secondaryButton;
const iconButtonClass = portfolioClasses.iconButton;
const panelClass = portfolioClasses.panel;
const chatMarkdownElements = ["p", "strong", "em", "ul", "ol", "li", "code", "br", "a"];

type MarkdownNode = {
  type: string;
  value?: string;
  url?: string;
  children?: MarkdownNode[];
};

function remarkCitationMarkers() {
  return (tree: MarkdownNode) => {
    transformMarkdownChildren(tree);
  };
}

function transformMarkdownChildren(node: MarkdownNode) {
  if (!node.children || node.type === "code" || node.type === "inlineCode") {
    return;
  }

  node.children = node.children.flatMap((child) => {
    if (child.type === "text" && typeof child.value === "string") {
      return splitCitationMarkers(child.value);
    }

    transformMarkdownChildren(child);
    return child;
  });
}

function splitCitationMarkers(value: string): MarkdownNode[] {
  const parts: MarkdownNode[] = [];
  const markerPattern = /\[(\d+)\]/g;
  let lastIndex = 0;

  for (const match of value.matchAll(markerPattern)) {
    const index = match.index ?? 0;
    if (index > lastIndex) {
      parts.push({ type: "text", value: value.slice(lastIndex, index) });
    }
    parts.push({
      type: "link",
      url: `#citation-${match[1]}`,
      children: [{ type: "text", value: match[0] }],
    });
    lastIndex = index + match[0].length;
  }

  if (lastIndex === 0) {
    return [{ type: "text", value }];
  }
  if (lastIndex < value.length) {
    parts.push({ type: "text", value: value.slice(lastIndex) });
  }

  return parts;
}

function citationReferenceFromHref(href: string | undefined): number | null {
  const match = /^#citation-(\d+)$/.exec(href ?? "");
  if (!match) {
    return null;
  }

  const reference = Number(match[1]);
  return Number.isSafeInteger(reference) && reference > 0 ? reference : null;
}

function ChatAnswer({
  content,
  citations,
  onCitationClick,
}: {
  content: string;
  citations: ChatCitation[];
  onCitationClick: (citation: ChatCitation) => void;
}) {
  const citationsByReference = new Map<number, ChatCitation>();
  for (const citation of citations) {
    if (!citationsByReference.has(citation.reference)) {
      citationsByReference.set(citation.reference, citation);
    }
  }

  return (
    <div className="mt-3 text-sm leading-7">
      <ReactMarkdown
        allowedElements={chatMarkdownElements}
        allowElement={(element) =>
          element.tagName !== "a" ||
          (typeof element.properties.href === "string" && element.properties.href.startsWith("#citation-"))
        }
        skipHtml
        unwrapDisallowed
        remarkPlugins={[remarkCitationMarkers]}
        components={{
          a: ({ href, children }) => {
            const reference = citationReferenceFromHref(href);
            const citation = reference === null ? undefined : citationsByReference.get(reference);
            if (!citation || reference === null) {
              return <>{children}</>;
            }

            return (
              <button
                aria-label={`Open citation ${reference}`}
                className="mx-0.5 inline-flex min-w-5 items-center justify-center rounded-[4px] border border-[#ffffff]/45 bg-[#ffffff]/15 px-1.5 py-0.5 text-xs font-bold leading-none text-[#ffffff] transition hover:border-[#ffffff]/80 hover:bg-[#ffffff]/25 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#ffffff]/70"
                onClick={() => onCitationClick(citation)}
                type="button"
              >
                {children}
              </button>
            );
          },
          p: ({ children }) => <p className="whitespace-pre-wrap [&:not(:first-child)]:mt-3">{children}</p>,
          strong: ({ children }) => <strong className="font-semibold text-white">{children}</strong>,
          em: ({ children }) => <em className="italic">{children}</em>,
          ul: ({ children }) => <ul className="my-3 list-disc space-y-1 pl-5">{children}</ul>,
          ol: ({ children }) => <ol className="my-3 list-decimal space-y-1 pl-5">{children}</ol>,
          li: ({ children }) => <li className="pl-1">{children}</li>,
          code: ({ children }) => <code className="rounded-[4px] border border-white/10 bg-white/[0.08] px-1 py-0.5 font-mono text-[0.9em] text-[#ffffff]">{children}</code>,
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}

function toChatEntry(message: ChatMessageResponse): ChatEntry {
  return {
    id: message.id,
    role: message.role === "assistant" ? "assistant" : "user",
    content: message.content,
    citations: message.citations,
  };
}

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
  const [agentRun, setAgentRun] = useState<AgentRunResponse | null>(null);

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
      return false;
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
      anchor.hidden = true;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1_000);
      return true;
    } catch (err) {
      onFailure(err instanceof Error ? err.message : "Download failed.");
      return false;
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
      try {
        const history = await apiJson<ChatHistoryResponse>(`/chat/${response.conversationId}`);
        setChatEntries(history.messages.map(toChatEntry));
      } catch {
        // Keep the immediately rendered answer when history hydration is unavailable.
      }
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
      <main className="flex min-h-screen items-center justify-center bg-[#000000] px-6 text-white">
        <p className={labelClass}>Checking session...</p>
      </main>
    );
  }

  const navItems: { id: AppView; label: string }[] = [
    { id: "files", label: "Files" },
    { id: "knowledge", label: "Search & Chat" },
    { id: "agent", label: "Agent" },
    { id: "logs", label: "Logs" },
  ];

  return (
    <main className="min-h-screen bg-black px-3 py-3 text-white sm:px-6 sm:py-6">
      <div className="mx-auto flex min-h-[calc(100vh-1.5rem)] max-w-[1500px] flex-col overflow-hidden rounded-[8px] border border-white/10 bg-[#0a0a0a] shadow-[0_34px_120px_rgba(0,0,0,0.65)] sm:min-h-[calc(100vh-3rem)] lg:flex-row">
        {/* Structural change from the prior top-tab nav: a persistent sidebar, matching the
            dominant pattern across every reference product researched (Notion, Linear, Vercel,
            file managers) rather than a horizontal tab bar. Collapses to a horizontal strip
            below the lg breakpoint so mobile isn't left with a fixed-width sidebar eating the
            viewport. */}
        <aside className="flex shrink-0 flex-col border-b border-white/10 bg-[#0a0a0a] lg:w-64 lg:border-b-0 lg:border-r">
          <div className="flex items-center gap-3 border-b border-white/10 px-5 py-5">
            <ShieldMark />
            <div className="min-w-0">
              <p className="truncate text-[13px] font-bold uppercase tracking-[0.28em] text-white">Amanah Drive</p>
              <p className="mt-1 truncate text-[10px] uppercase tracking-[0.18em] text-white/38">Private workspace</p>
            </div>
          </div>

          <nav className="flex gap-1 overflow-x-auto px-3 py-3 lg:flex-1 lg:flex-col lg:overflow-visible" aria-label="Drive sections">
            {navItems.map((item) => (
              <button
                key={item.id}
                className={`shrink-0 whitespace-nowrap rounded-lg px-4 py-3 text-left text-xs font-semibold uppercase tracking-[0.12em] transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/70 ${activeView === item.id ? "bg-white text-black" : "text-white/56 hover:bg-white/[0.06] hover:text-white"}`}
                type="button"
                onClick={() => setActiveView(item.id)}
                aria-current={activeView === item.id ? "page" : undefined}
              >
                {item.label}
              </button>
            ))}
          </nav>

          <div className="border-t border-white/10 p-3">
            <button className={`${secondaryButtonClass} w-full`} onClick={signOut} type="button">
              Logout <span aria-hidden="true" className="ml-2">[-&gt;</span>
            </button>
          </div>
        </aside>

        <div className="min-w-0 flex-1">
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
        ) : activeView === "logs" ? (
          <ObservabilityView />
        ) : (
          <AgentView run={agentRun} onRunChange={setAgentRun} onFilesChanged={loadContents} />
        )}
        </div>
      </div>
    </main>
  );
}

function AgentView({
  run,
  onRunChange,
  onFilesChanged,
}: {
  run: AgentRunResponse | null;
  onRunChange: (run: AgentRunResponse | null) => void;
  onFilesChanged: () => void | Promise<void>;
}) {
  const [instruction, setInstruction] = useState("");
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);
  const [approvalAction, setApprovalAction] = useState<"approve" | "reject" | null>(null);

  const applyRun = useCallback((nextRun: AgentRunResponse) => {
    onRunChange(nextRun);
    // The agent's file-mutating tools (create/rename/move/copy) only take effect once a run
    // reaches Completed - refresh the Files view in the background so newly changed files show
    // up there without the user having to manually reload or re-switch tabs.
    if (nextRun.status === "Completed") {
      void onFilesChanged();
    }
  }, [onFilesChanged, onRunChange]);

  useEffect(() => {
    if (!run || (run.status !== "Pending" && run.status !== "Running")) return;

    let cancelled = false;
    let timeoutId: number | undefined;
    const poll = async () => {
      try {
        const nextRun = await apiJson<AgentRunResponse>(`/agent/runs/${run.id}`);
        if (cancelled) return;
        setSubmitError(null);
        applyRun(nextRun);
        if (nextRun.status === "Pending" || nextRun.status === "Running") {
          timeoutId = window.setTimeout(poll, 750);
        }
      } catch (err) {
        if (cancelled) return;
        setSubmitError(err instanceof Error ? err.message : "Live agent progress is temporarily unavailable.");
        timeoutId = window.setTimeout(poll, 1500);
      }
    };

    timeoutId = window.setTimeout(poll, 250);
    return () => {
      cancelled = true;
      if (timeoutId !== undefined) window.clearTimeout(timeoutId);
    };
  }, [applyRun, run?.id, run?.status]);

  async function startRun(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const question = instruction.trim();
    setSubmitError(null);
    if (!question) {
      setSubmitError("Describe what you would like the agent to do.");
      return;
    }

    setSubmitting(true);
    try {
      applyRun(await apiJson<AgentRunResponse>("/agent/runs", {
        method: "POST",
        body: JSON.stringify(run ? { question, conversationId: run.conversationId } : { question }),
      }));
      setInstruction("");
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : "The agent could not start. Try again in a moment.");
    } finally {
      setSubmitting(false);
    }
  }

  async function resolveApproval(action: "approve" | "reject") {
    if (!run) return;
    setSubmitError(null);
    setApprovalAction(action);
    try {
      applyRun(await apiJson<AgentRunResponse>(`/agent/runs/${run.id}/${action}`, { method: "POST" }));
    } catch (err) {
      // Left `run` untouched on failure - it's still AwaitingApproval, so the prompt and
      // buttons below reappear automatically (approvalAction resets in `finally`) instead of
      // leaving the user stuck with no way to retry.
      setSubmitError(err instanceof Error ? err.message : "The agent action could not be completed.");
    } finally {
      setApprovalAction(null);
    }
  }

  function startNewConversation() {
    setInstruction("");
    setSubmitError(null);
    onRunChange(null);
  }

  const isAwaitingApproval = run?.status === "AwaitingApproval";
  const isRunActive = run?.status === "Pending" || run?.status === "Running";
  const isBusy = isSubmitting || approvalAction !== null;
  const pendingAction = run?.pendingActionSummary ?? run?.steps.find((step) => step.status === "PendingApproval")?.argumentsSummary;

  return (
    <section className="grid gap-5 px-5 py-6 sm:px-8 lg:grid-cols-[minmax(0,0.8fr)_minmax(0,1.2fr)] lg:px-9">
      <div className={`${panelClass} h-fit p-5`}>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <SectionLabel className="text-[#7ee787]">File agent</SectionLabel>
          {run ? <button className={secondaryButtonClass} disabled={isBusy || isRunActive || isAwaitingApproval} onClick={startNewConversation} type="button">New conversation</button> : null}
        </div>
        <h1 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-white">Ask the agent to work with your files.</h1>
        <p className="mt-2 text-sm leading-6 text-white/55">It can inspect your drive immediately and will ask before making a rename or move.</p>

        {/* Capability transparency: tell the user what the agent can and can't do before they
            type, rather than a blank box that only reveals its limits through trial and error. */}
        <div className="mt-4 flex flex-wrap gap-1.5 text-[11px]">
          {["Search & read files", "Create & copy files", "Rename & move (asks first)", "Read GitHub repos"].map((capability) => (
            <span key={capability} className="rounded-full border border-white/12 bg-white/[0.03] px-2.5 py-1 text-white/58">{capability}</span>
          ))}
        </div>

        <form className="mt-5 space-y-3" onSubmit={startRun}>
          <label className="block text-xs font-semibold uppercase tracking-[0.12em] text-white/58" htmlFor="agent-instruction">Instruction</label>
          <textarea
            className={`${fieldClass} min-h-32 resize-y py-3`}
            disabled={isBusy || isRunActive || isAwaitingApproval}
            id="agent-instruction"
            maxLength={4000}
            onChange={(event) => setInstruction(event.target.value)}
            placeholder="For example: find the latest invoice and move a copy into Finance."
            value={instruction}
          />
          <button className={`${primaryButtonClass} w-full`} disabled={isBusy || isRunActive || isAwaitingApproval || !instruction.trim()} type="submit">
            {isSubmitting || isRunActive ? "Agent is working…" : "Run agent"}
          </button>
        </form>
        {isAwaitingApproval ? <p className="mt-3 text-xs leading-5 text-white/45">Resolve the pending action before starting another run.</p> : null}
        {submitError ? <p className="mt-4 rounded-[6px] border border-red-300/20 bg-red-400/10 px-3 py-2 text-sm text-red-100" role="alert">{submitError}</p> : null}

        {run?.status === "AwaitingApproval" ? (
          <div className="mt-5 rounded-[7px] border border-amber-200/25 bg-amber-300/10 p-4">
            {approvalAction ? (
              // The prompt and buttons disappear the instant a decision is clicked, replaced by
              // this transitional state, rather than lingering (disabled) until the round-trip
              // finishes - it reappears automatically below only if the request actually fails.
              <p className="text-sm leading-6 text-white/80">{approvalAction === "approve" ? "Applying your approval…" : "Applying your rejection…"}</p>
            ) : (
              <>
                <p className="text-xs font-bold uppercase tracking-[0.14em] text-amber-100">Approval needed</p>
                <p className="mt-2 text-sm leading-6 text-white">{pendingAction ?? "The agent is ready to make a change."}?</p>
                <p className="mt-1 text-xs text-white/52">This action will only run if you approve it.</p>
                <div className="mt-4 flex flex-wrap gap-2">
                  <button className={primaryButtonClass} disabled={isBusy} onClick={() => void resolveApproval("approve")} type="button">
                    Approve
                  </button>
                  <button className={secondaryButtonClass} disabled={isBusy} onClick={() => void resolveApproval("reject")} type="button">
                    Reject
                  </button>
                </div>
              </>
            )}
          </div>
        ) : null}
      </div>

      <div className={`${panelClass} min-h-[430px] p-5`}>
        <SectionLabel className="text-[#7ee787]">Conversation transcript</SectionLabel>
        {!run ? (
          <div className="flex min-h-80 items-center justify-center text-center text-sm leading-6 text-white/45">Your active agent conversation will appear here.</div>
        ) : (
          <div className="mt-4 space-y-3">
            {run.steps.map((step) => <AgentTranscriptStep key={`${step.runId}:${step.sequence}`} step={step} />)}
            {run.status === "Pending" || run.status === "Running" ? (
              <div aria-live="polite" className="rounded-[7px] border border-[#ffffff]/20 bg-[#ffffff]/[0.07] px-4 py-3 text-sm text-[#ffffff]">
                {run.status === "Pending" ? "Agent run queued…" : "Agent is working…"}
              </div>
            ) : null}
            {run.status === "Completed" ? (
              <div className="rounded-[7px] border border-emerald-200/20 bg-emerald-300/[0.07] px-4 py-3">
                <p className="text-xs font-bold uppercase tracking-[0.14em] text-[#7ee787]">Completed</p>
                <ChatAnswer content={run.finalAnswer ?? "The agent completed its run."} citations={[]} onCitationClick={() => undefined} />
              </div>
            ) : null}
            {run.status === "Failed" ? <p className="rounded-[7px] border border-red-300/20 bg-red-400/10 px-4 py-3 text-sm text-red-100" role="alert">The agent stopped: {run.failureReason ?? "an unexpected error occurred."}</p> : null}
            {run.status === "IterationLimitReached" ? <p className="rounded-[7px] border border-amber-200/20 bg-amber-300/[0.08] px-4 py-3 text-sm leading-6 text-amber-50">The agent stopped after its safety limit of tool steps. You can start a new run with a more specific instruction.</p> : null}
          </div>
        )}
      </div>
    </section>
  );
}

function AgentTranscriptStep({ step }: { step: AgentRunStepResponse }) {
  if (step.role === "user") {
    return <div className="ml-auto max-w-[88%] rounded-[7px] border border-[#ffffff]/30 bg-[#ffffff]/10 px-4 py-3 text-sm leading-6 text-[#ffffff]"><p className="mb-1 text-[10px] font-bold uppercase tracking-[0.14em] text-[#ffffff]">You</p>{step.content}</div>;
  }

  if (step.role === "assistant") {
    return step.content ? <div className="max-w-[88%] rounded-[7px] border border-white/10 bg-white/[0.035] px-4 py-3 text-sm leading-6 text-white/75"><p className="mb-1 text-[10px] font-bold uppercase tracking-[0.14em] text-white/45">Agent</p>{step.content}</div> : null;
  }

  // Deliberately not a chat bubble: a tool call is the system doing something, not someone
  // talking, so it reads as a distinct system-trace entry — full width, a left rule instead of
  // a border-all card, monospace tool name — and its accent color reflects what actually
  // happened rather than a fixed color regardless of outcome.
  const toneByStatus: Record<string, { rule: string; badge: string }> = {
    PendingApproval: { rule: "border-l-[#e3b341]/60", badge: statusTone.warning },
    Executing: { rule: "border-l-[#ffffff]/60", badge: statusTone.accent },
    Executed: { rule: "border-l-[#3fb950]/60", badge: statusTone.success },
    Rejected: { rule: "border-l-white/25", badge: statusTone.neutral },
    Invalid: { rule: "border-l-[#f85149]/60", badge: statusTone.danger },
  };
  const tone = (step.status && toneByStatus[step.status]) || toneByStatus.Executed;

  return (
    <div className={`border-l-2 ${tone.rule} bg-white/[0.02] py-2.5 pl-4 pr-3`}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="font-mono text-[11px] font-semibold uppercase tracking-[0.1em] text-white/50">{formatToolName(step.toolName)}</p>
        {step.status ? <span className={`${portfolioClasses.badge} ${tone.badge}`}>{formatToolStatus(step.status)}</span> : null}
      </div>
      {step.argumentsSummary ? <p className="mt-1.5 text-sm text-white/85">{step.argumentsSummary}</p> : null}
      {step.resultSummary ? <p className="mt-1 text-xs leading-5 text-white/48">{step.resultSummary}</p> : null}
    </div>
  );
}

function formatToolName(name: string | null) {
  return (name ?? "agent action").replaceAll("_", " ");
}

function formatToolStatus(status: string) {
  return status.replace(/([a-z])([A-Z])/g, "$1 $2");
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
    <section className="relative bg-[#0a0a0a] px-5 py-7 sm:px-8 lg:px-9">
      <div className="relative mb-8 border-b border-white/10 pb-7">
        <SectionLabel>Secure workspace</SectionLabel>
        <h2 className="mt-3 text-4xl font-semibold tracking-[-0.02em] leading-tight text-white sm:text-5xl">File <span className={portfolioClasses.gradientText}>management</span></h2>
        <p className="mt-2 text-base text-white/58">Organize, manage, and access your secure files.</p>
      </div>

      {error ? (
        <div className="mb-6 rounded-[8px] border border-red-300/25 bg-red-400/10 px-4 py-3 text-sm text-red-200" role="alert">
          {error}
        </div>
      ) : null}

      <div className="grid gap-5 xl:grid-cols-[300px_minmax(0,1fr)] 2xl:grid-cols-[360px_minmax(0,1fr)]">
        <aside className={`${panelClass} p-5 sm:p-6`}>
          <p className={labelClass}>Create new</p>
          <div className={`${portfolioClasses.insetPanel} mt-5 p-4`}>
            <div className="flex gap-4">
              <div className="grid h-14 w-14 shrink-0 place-items-center rounded-[8px] border border-[#ffffff]/30 bg-[#ffffff]/10 text-[#ffffff]">
                <FolderGlyph />
              </div>
              <div>
                <h3 className="text-base font-semibold text-white">New Folder</h3>
                <p className="mt-1 text-sm leading-5 text-white/52">Create a new folder in your drive.</p>
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
            <div className="h-px flex-1 bg-white/10" />
            <span className="text-xs uppercase tracking-[0.22em] text-white/35">Or</span>
            <div className="h-px flex-1 bg-white/10" />
          </div>

          <p className={labelClass}>Upload files</p>
          <div className={`${portfolioClasses.insetPanel} mt-5 p-4`}>
            <div className="flex gap-4">
              <div className="grid h-14 w-14 shrink-0 place-items-center rounded-[8px] border border-[#ffffff]/30 bg-[#ffffff]/10 text-[#ffffff]">
                <UploadGlyph />
              </div>
              <div>
                <h3 className="text-base font-semibold text-white">Upload Files</h3>
                <p className="mt-1 text-sm leading-5 text-white/52">Add files to your drive. Supported formats below.</p>
              </div>
            </div>
            <label className="mt-5 flex min-h-[136px] cursor-pointer flex-col items-center justify-center rounded-[8px] border border-dashed border-white/20 bg-white/[0.025] px-4 text-center text-sm text-white/52 transition hover:border-[#ffffff]/55 hover:bg-[#ffffff]/[0.06] hover:text-white/75">
              <DocumentGlyph />
              <span className="mt-3">Drag and drop files here or click to browse</span>
              <input
                className="sr-only"
                type="file"
                accept=".pdf,.docx,.csv,.md,.txt,.png,.jpg,.jpeg,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/csv,text/markdown,text/plain,image/png,image/jpeg"
                onChange={onUploadFile}
                disabled={isWorking}
              />
            </label>
            <label className={`${secondaryButtonClass} mt-4 block cursor-pointer text-center normal-case tracking-normal`}>
              Choose Files
              <input
                className="sr-only"
                type="file"
                accept=".pdf,.docx,.csv,.md,.txt,.png,.jpg,.jpeg,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/csv,text/markdown,text/plain,image/png,image/jpeg"
                onChange={onUploadFile}
                disabled={isWorking}
              />
            </label>
          </div>

          <div className="mt-5 flex gap-3 rounded-[8px] border border-white/10 bg-white/[0.025] p-4 text-[#ffffff]">
            <InfoGlyph />
            <p className="text-sm leading-5 text-white/55">
              <span className="block font-semibold text-white/82">Supported formats</span>
              PDF, DOCX, CSV, Markdown, plain text, PNG, and JPEG.
            </p>
          </div>
        </aside>

        <section className={`${panelClass} overflow-hidden`}>
          <div className="flex flex-col gap-5 border-b border-white/10 bg-white/[0.018] px-5 py-5 sm:px-7 lg:flex-row lg:items-center lg:justify-between">
            <div>
              <p className={labelClass}>Your files</p>
              <div className="mt-3 flex flex-wrap items-center gap-2">
                {breadcrumbs.map((item, index) => (
                  <button
                    key={`${item.id ?? "root"}-${index}`}
                    className="text-base text-white/72 transition hover:text-[#ffffff]"
                    onClick={() => onGoToBreadcrumb(index)}
                    type="button"
                  >
                    {item.name}
                    {index < breadcrumbs.length - 1 ? <span className="ml-2 text-white/25">/</span> : null}
                  </button>
                ))}
              </div>
            </div>
            <div className="flex w-full flex-col gap-3 sm:flex-row lg:w-auto">
              <div className="flex min-w-0 flex-1 items-center gap-2 rounded-[8px] border border-white/10 bg-white/[0.035] px-4 py-3 text-sm text-white/32 lg:w-[270px]">
                <SearchGlyph />
                <span>Search files...</span>
              </div>
              <div className="grid grid-cols-2 gap-1 rounded-[8px] border border-white/10 bg-white/[0.035] p-1">
                <span className="grid h-10 w-10 place-items-center rounded-[6px] bg-[#ffffff]/12 text-[#ffffff]" aria-hidden="true">=</span>
                <span className="grid h-10 w-10 place-items-center text-white/42" aria-hidden="true">#</span>
              </div>
            </div>
          </div>

          <div className="hidden border-b border-white/10 px-7 py-4 text-[11px] font-semibold uppercase tracking-[0.18em] text-white/38 xl:grid xl:grid-cols-[minmax(160px,1fr)_70px_75px_130px_300px] xl:gap-3 2xl:grid-cols-[minmax(220px,1fr)_100px_100px_180px_310px] 2xl:gap-4">
            <span>Name</span>
            <span>Type</span>
            <span>Size</span>
            <span>Modified</span>
            <span>Actions</span>
          </div>

          {isLoading ? (
            <div className="p-8 text-sm text-white/52">Loading drive contents...</div>
          ) : (
            <div className="min-h-[420px] divide-y divide-white/[0.08]">
              {contents?.folders.map((folder) => (
                <div key={folder.id} className="grid gap-4 px-5 py-5 transition hover:bg-white/[0.025] sm:px-7 xl:grid-cols-[minmax(160px,1fr)_70px_75px_130px_300px] xl:items-center xl:gap-3 2xl:grid-cols-[minmax(220px,1fr)_100px_100px_180px_310px] 2xl:gap-4">
                  <button className="flex min-w-0 items-center gap-4 text-left" onClick={() => onEnterFolder(folder)} type="button">
                    <span className="grid h-14 w-14 shrink-0 place-items-center rounded-[8px] border border-[#ffffff]/25 bg-[#ffffff]/[0.07] text-[#ffffff]">
                      <FolderGlyph />
                    </span>
                    <span className="min-w-0">
                      <span className="block truncate font-semibold text-white/88">{folder.name}</span>
                      <span className="mt-1 block text-sm text-white/42" aria-hidden="true">Folder</span>
                    </span>
                  </button>
                  <span className="text-sm text-white/52 xl:block">Folder</span>
                  <span className="text-sm text-white/52">--</span>
                  <span className="text-sm leading-5 text-white/52">{formatDate(folder.updatedAt)}</span>
                  <div className="flex flex-wrap gap-2">
                    <button className={iconButtonClass} onClick={() => onRenameFolder(folder)} type="button" aria-label="Rename folder">
                      <PencilGlyph />
                    </button>
                    <button className={`${iconButtonClass} text-red-300 hover:border-red-300/45 hover:bg-red-400/10`} onClick={() => onDeleteFolder(folder)} type="button" aria-label="Delete folder">
                      <TrashGlyph />
                    </button>
                  </div>
                </div>
              ))}

              {contents?.files.map((file) => (
                <div key={file.id} className="grid gap-4 px-5 py-5 transition hover:bg-white/[0.025] sm:px-7 xl:grid-cols-[minmax(160px,1fr)_70px_75px_130px_300px] xl:items-center xl:gap-3 2xl:grid-cols-[minmax(220px,1fr)_100px_100px_180px_310px] 2xl:gap-4">
                  <div className="flex min-w-0 items-center gap-4">
                    <span className="grid h-14 w-14 shrink-0 place-items-center rounded-[8px] border border-[#ffffff]/25 bg-[#ffffff]/[0.07] text-[#ffffff]">
                      <DocumentGlyph />
                    </span>
                    <div className="min-w-0">
                      <p className="truncate font-semibold text-white/88">{file.originalFileName}</p>
                      <p className="mt-1 text-sm text-white/42">{friendlyContentType(file.contentType)}</p>
                    </div>
                  </div>
                  <span className="w-fit rounded-[8px] border border-[#ffffff]/25 bg-[#ffffff]/[0.07] px-3 py-2 text-xs text-[#ffffff]">{fileExtension(file.originalFileName)}</span>
                  <span className="text-sm text-white/52">{formatBytes(file.sizeBytes)}</span>
                  <span className="text-sm leading-5 text-white/52">{formatDate(file.updatedAt)}</span>
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
                    <button className={`${iconButtonClass} text-red-300 hover:border-red-300/45 hover:bg-red-400/10`} onClick={() => onDeleteFile(file)} type="button" aria-label={`Delete ${file.originalFileName}`}>
                      <TrashGlyph />
                    </button>
                  </div>
                </div>
              ))}

              {contents && contents.folders.length === 0 && contents.files.length === 0 ? (
                <div className="flex min-h-[320px] items-center justify-center p-8 text-center text-sm text-white/48">This folder is empty.</div>
              ) : null}
            </div>
          )}

          <div className="flex flex-col gap-4 border-t border-white/10 bg-white/[0.018] px-5 py-5 text-sm text-white/48 sm:flex-row sm:items-center sm:justify-between sm:px-7">
            <span>
              Showing {firstItem} to {lastItem} of {totalItems} items
            </span>
            <div className="flex items-center gap-2">
              <button className={iconButtonClass} disabled={currentPage <= 1 || isLoading} onClick={() => onPageChange((value) => Math.max(1, value - 1))} type="button" aria-label="Previous page">
                &lt;
              </button>
              <span className="grid h-11 w-11 place-items-center rounded-[8px] bg-[#ffffff] text-sm font-semibold text-[#000000]">{contents?.page ?? currentPage}</span>
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
  onDownloadCitation: (citation: ChatCitation) => Promise<boolean>;
  onDownloadSearchResult: (result: SearchResult) => void;
  onNewConversation: () => void;
  onSearch: (event: FormEvent<HTMLFormElement>) => Promise<void>;
  onSearchQueryChange: (value: string) => void;
}) {
  const citationCardRefs = useRef<Record<string, HTMLDivElement | null>>({});
  const [activeCitationCard, setActiveCitationCard] = useState<string | null>(null);
  const [selectedCitation, setSelectedCitation] = useState<ChatCitation | null>(null);
  const [downloadingCitation, setDownloadingCitation] = useState<string | null>(null);

  function citationCardKey(entryId: string, reference: number) {
    return `${entryId}-${reference}`;
  }

  function openCitation(entryId: string, citation: ChatCitation) {
    const cardKey = citationCardKey(entryId, citation.reference);
    setActiveCitationCard(cardKey);
    setSelectedCitation(citation);
  }

  async function downloadCitation(entryId: string, citation: ChatCitation) {
    const cardKey = citationCardKey(entryId, citation.reference);
    setDownloadingCitation(cardKey);
    try {
      await onDownloadCitation(citation);
    } finally {
      setDownloadingCitation(null);
    }
  }

  function closeCitationDialog() {
    setSelectedCitation(null);
    setActiveCitationCard(null);
  }

  return (
    <section className="bg-[#000000] px-5 py-7 sm:px-8 lg:px-9">
      <div className="relative mb-7 flex flex-col gap-4 border-b border-white/10 pb-7 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <SectionLabel>Amanah Drive</SectionLabel>
          <h2 className="mt-2 text-4xl font-semibold tracking-[-0.02em] leading-tight text-white sm:text-5xl">Search <span className={portfolioClasses.gradientText}>& Chat</span></h2>
        </div>
      </div>

      <div className="grid gap-5 xl:grid-cols-[minmax(0,0.86fr)_minmax(0,1.14fr)]">
        <section className={`${panelClass} flex min-h-[650px] flex-col p-5 sm:p-6`}>
          <div>
            <p className={labelClass}>Semantic search</p>
            <h3 className="mt-3 text-3xl font-semibold tracking-[-0.02em] leading-tight text-white/92">Find document passages</h3>
          </div>

          <form className="mt-6 rounded-[8px] border border-white/10 bg-[#0a0a0a] p-4" onSubmit={onSearch}>
            <label className={labelClass} htmlFor="search-documents">
              Search documents
            </label>
            <div className="mt-4 flex flex-col gap-3 sm:flex-row">
              <div className="flex min-w-0 flex-1 items-center gap-3 rounded-[8px] border border-white/12 bg-white/[0.045] px-4 text-white/55 transition focus-within:border-[#ffffff]/70 focus-within:ring-2 focus-within:ring-[#ffffff]/20">
                <SearchGlyph />
                <input
                  id="search-documents"
                  className="min-h-12 min-w-0 flex-1 bg-transparent text-sm text-white outline-none placeholder:text-white/32"
                  value={searchQuery}
                  onChange={(event) => onSearchQueryChange(event.target.value)}
                  placeholder="what is Amanah Drive"
                />
              </div>
              <button className={primaryButtonClass} disabled={searchLoading} type="submit">
                {searchLoading ? "Searching" : "Search"}
              </button>
            </div>
          </form>

          <div className="mt-6 flex items-center justify-between gap-4 text-sm text-white/42">
            <span>{searchResults.length} result{searchResults.length === 1 ? "" : "s"} found</span>
            <span>Sorted by relevance</span>
          </div>

          <div className="mt-6 min-h-[350px] flex-1 space-y-4">
            {searchError ? (
              <div className="rounded-[8px] border border-red-300/25 bg-red-400/10 px-4 py-3 text-sm text-red-200" role="alert">
                {searchError}
              </div>
            ) : null}
            {searchLoading ? <p className="text-sm text-white/52">Searching processed document chunks...</p> : null}
            {!searchLoading && hasSearched && !searchError && searchResults.length === 0 ? <p className="text-sm text-white/52">No matching document sections found.</p> : null}
            {!searchLoading && !hasSearched ? <p className="text-sm leading-6 text-white/52">Search processed documents by meaning, not just filenames.</p> : null}
            {searchResults.map((result) => (
              <article key={result.chunkId} className="rounded-[8px] border border-white/10 bg-white/[0.025] p-4 transition hover:border-[#ffffff]/30 hover:bg-[#ffffff]/[0.04] sm:p-5">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                  <div className="flex min-w-0 gap-4">
                    <span className="mt-1 grid h-11 w-11 shrink-0 place-items-center rounded-[8px] border border-[#ffffff]/25 bg-[#ffffff]/10 text-[#ffffff]">
                      <DocumentGlyph />
                    </span>
                    <div className="min-w-0">
                      <h4 className="truncate text-2xl font-semibold tracking-[-0.015em] text-white/90">{result.fileName}</h4>
                      <p className="mt-2 text-xs uppercase tracking-[0.14em] text-white/38">Semantic match</p>
                    </div>
                  </div>
                  <button className={secondaryButtonClass} onClick={() => onDownloadSearchResult(result)} type="button">
                    Download <span aria-hidden="true" className="ml-2">v</span>
                  </button>
                </div>
                <div className="mt-5 flex items-center gap-3">
                  <span className={labelClass}>Relevance</span>
                  <span className="h-1.5 flex-1 overflow-hidden rounded-full bg-white/[0.08]">
                    <span className="block h-full rounded-full bg-[#ffffff]" style={{ width: `${Math.max(8, Math.min(100, result.score * 100))}%` }} />
                  </span>
                  <span className="text-sm text-white/55">{formatScore(result.score)}</span>
                </div>
                <p className="mt-5 text-sm leading-7 text-white/68">{result.snippet}</p>
                <p className="mt-4 text-xs uppercase tracking-[0.14em] text-white/35">Passage {result.chunkIndex + 1} / Score {formatScore(result.score)}</p>
              </article>
            ))}
          </div>

          <div className="mt-6 flex gap-3 rounded-[8px] border border-white/10 bg-white/[0.025] p-4 text-[#ffffff]">
            <InfoGlyph />
            <p className="text-sm leading-6 text-white/55">
              <span className="block text-xs font-semibold uppercase tracking-[0.18em] text-white/82">Search tips</span>
              Use natural language to find information in your documents.
            </p>
          </div>
        </section>

        <section className={`${panelClass} flex min-h-[650px] flex-col overflow-hidden`}>
          <div className="flex flex-col gap-4 border-b border-white/10 bg-white/[0.018] p-5 sm:flex-row sm:items-start sm:justify-between sm:p-6">
            <div>
              <p className={labelClass}>AI chat</p>
              <h3 className="mt-3 text-3xl font-semibold tracking-[-0.02em] leading-tight text-white/92">Ask your drive</h3>
              <p className="mt-2 text-sm text-white/48">Get answers from your documents using AI.</p>
              {conversationId ? <p className="mt-2 text-xs uppercase tracking-[0.14em] text-[#ffffff]">Conversation active</p> : null}
            </div>
            <button className={secondaryButtonClass} onClick={onNewConversation} type="button">
              New conversation <span aria-hidden="true" className="ml-2">+</span>
            </button>
          </div>

          <div className="flex-1 space-y-5 overflow-x-hidden px-5 py-6 sm:px-8">
            {chatEntries.length === 0 ? (
              <div className="mx-auto max-w-[620px] rounded-[8px] border border-white/10 bg-white/[0.025] p-5 text-sm leading-6 text-white/52">
                Ask a question after your files finish processing. Answers stay grounded in retrieved chunks and include source citations when available.
              </div>
            ) : null}
            {chatEntries.map((entry) => (
              <article key={entry.id} className={entry.role === "user" ? "ml-auto max-w-[760px] rounded-[8px] border border-[#ffffff]/35 bg-[#ffffff]/[0.12] px-5 py-4 text-white shadow-[0_18px_35px_rgba(0,0,0,0.28)]" : "mr-auto max-w-[760px] rounded-[8px] border border-white/10 bg-white/[0.045] px-5 py-4 text-white/76"}>
                <p className="text-[11px] font-semibold uppercase tracking-[0.18em] opacity-65">{entry.role === "user" ? "You" : "Amanah Drive"}</p>
                {entry.role === "assistant" ? <ChatAnswer content={entry.content} citations={entry.citations ?? []} onCitationClick={(citation) => openCitation(entry.id, citation)} /> : <p className="mt-3 whitespace-pre-wrap text-sm leading-7">{entry.content}</p>}
                {entry.citations && entry.citations.length > 0 ? (
                  <div className="mt-6 border-t border-white/10 pt-5">
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
                      <div>
                        <p className={labelClass}>Answer sources</p>
                        <h4 className="mt-1.5 text-lg font-semibold text-white/90">Where this answer came from</h4>
                      </div>
                      <span className="w-fit rounded-full border border-white/12 bg-white/[0.04] px-2.5 py-1 text-[11px] text-white/48">
                        {entry.citations.length} cited passage{entry.citations.length === 1 ? "" : "s"}
                      </span>
                    </div>
                    <div className="mt-4 grid gap-3">
                      {entry.citations.map((citation, index) => {
                        const cardKey = citationCardKey(entry.id, citation.reference);
                        const isActive = activeCitationCard === cardKey;
                        const isDownloading = downloadingCitation === cardKey;

                        return (
                          <div
                            key={`${citation.chunkId}-${citation.fileId ?? "none"}-${citation.reference}-${index}`}
                            ref={(node) => {
                              citationCardRefs.current[cardKey] = node;
                            }}
                            className={`group rounded-[10px] border bg-white/[0.035] p-4 transition ${isActive ? "border-white/60 ring-1 ring-white/25" : "border-white/12 hover:border-white/28 hover:bg-white/[0.055]"}`}
                            data-citation-active={isActive ? "true" : undefined}
                          >
                            <div className="flex items-start gap-3">
                              <span className="grid h-8 w-8 shrink-0 place-items-center rounded-[7px] border border-white/20 bg-white/[0.08] text-xs font-bold text-white">
                                {citation.reference}
                              </span>
                              <button className="min-w-0 flex-1 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/60" onClick={() => openCitation(entry.id, citation)} type="button" aria-label={`View citation ${citation.reference} from ${citation.fileName}`}>
                                <span className="block break-words text-sm font-semibold text-white/88">{citation.fileName}</span>
                                <span className="mt-1 block text-xs text-white/42">
                                  {citation.chunkIndex === null ? "Retrieved passage" : `Passage ${citation.chunkIndex + 1}`}
                                  {citation.relevanceScore === null ? "" : ` · ${formatPercentage(citation.relevanceScore)} match`}
                                </span>
                              </button>
                              <span className="rounded-[5px] border border-white/10 px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.12em] text-white/42">{fileExtension(citation.fileName)}</span>
                            </div>
                            <p className="mt-3 max-h-[4.5rem] overflow-hidden text-sm leading-6 text-white/58">{citation.snippet}</p>
                            <div className="mt-3 flex items-center justify-between gap-3 border-t border-white/10 pt-3">
                              <button className="text-xs font-semibold text-white/72 transition hover:text-white" onClick={() => openCitation(entry.id, citation)} type="button">
                                View excerpt <span aria-hidden="true">→</span>
                              </button>
                              <button className="inline-flex items-center gap-1.5 text-xs font-semibold text-white/58 transition hover:text-white disabled:cursor-not-allowed disabled:opacity-35" disabled={!citation.fileId || isDownloading} onClick={() => void downloadCitation(entry.id, citation)} type="button" aria-label={`Download ${citation.fileName}`}>
                                <DownloadGlyph /> {isDownloading ? "Downloading…" : "Download file"}
                              </button>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                ) : null}
              </article>
            ))}
            {chatLoading ? <p className="text-sm text-white/52">Waiting for a grounded answer...</p> : null}
            {chatError ? (
              <div className="rounded-[8px] border border-red-300/25 bg-red-400/10 px-4 py-3 text-sm text-red-200" role="alert">
                {chatError}
              </div>
            ) : null}
          </div>

          <form className="border-t border-white/10 bg-white/[0.018] p-5 sm:p-6" onSubmit={onAskQuestion}>
            <label className="sr-only" htmlFor="chat-question">
              Ask a question
            </label>
            <div className="rounded-[8px] border border-white/12 bg-white/[0.045] p-3 transition focus-within:border-[#ffffff]/60 focus-within:ring-2 focus-within:ring-[#ffffff]/15">
              <textarea
                id="chat-question"
                className="min-h-16 w-full resize-none bg-transparent px-2 py-2 text-sm text-white outline-none placeholder:text-white/32"
                value={chatQuestion}
                onChange={(event) => onChatQuestionChange(event.target.value)}
                placeholder="Ask a follow-up question..."
              />
              <div className="flex items-center justify-between">
                <span className="text-white/35" aria-hidden="true">@</span>
                <button className="grid h-12 w-12 place-items-center rounded-[8px] bg-[#ffffff] text-[#000000] transition hover:brightness-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#ffffff] disabled:cursor-not-allowed disabled:opacity-35" disabled={chatLoading} type="submit">
                  <span className="sr-only">{chatLoading ? "Thinking" : "Send"}</span>
                  <SendGlyph />
                </button>
              </div>
            </div>
            <p className="mt-3 text-center text-xs text-white/35">Answers are generated from your documents. Verify important information.</p>
          </form>
        </section>
      </div>
      {selectedCitation ? <CitationSnippetDialog citation={selectedCitation} onClose={closeCitationDialog} onDownload={() => onDownloadCitation(selectedCitation)} /> : null}
    </section>
  );
}

function CitationSnippetDialog({ citation, onClose, onDownload }: { citation: ChatCitation; onClose: () => void; onDownload: () => Promise<boolean> }) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [isDownloading, setDownloading] = useState(false);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog || dialog.open) {
      return;
    }

    dialog.showModal();
  }, []);

  async function handleDownload() {
    setDownloading(true);
    try {
      await onDownload();
    } finally {
      setDownloading(false);
    }
  }

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby="citation-dialog-title"
      className="fixed inset-0 m-auto max-h-[calc(100vh-2rem)] w-[min(92vw,660px)] overflow-y-auto rounded-[12px] border border-[#ffffff]/30 bg-[#0a0a0a] p-0 text-white shadow-[0_28px_90px_rgba(0,0,0,0.7)] backdrop:bg-black/75"
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          dialogRef.current?.close();
        }
      }}
      onClose={onClose}
    >
      <div className="p-5 sm:p-7">
        <div className="flex items-start justify-between gap-5">
          <div>
            <p className={labelClass}>Source {citation.reference}</p>
            <h4 id="citation-dialog-title" className="mt-2 break-words text-2xl font-semibold tracking-[-0.02em] text-white/92 sm:text-3xl">{citation.fileName}</h4>
          </div>
          <button className={iconButtonClass} onClick={() => dialogRef.current?.close()} type="button" aria-label="Close citation">
            ×
          </button>
        </div>
        <div className="mt-5 flex flex-wrap gap-2 text-xs text-white/52">
          <span className="rounded-full border border-white/12 bg-white/[0.04] px-3 py-1.5">{citation.chunkIndex === null ? "Retrieved passage" : `Passage ${citation.chunkIndex + 1}`}</span>
          {citation.relevanceScore === null ? null : <span className="rounded-full border border-white/12 bg-white/[0.04] px-3 py-1.5">{formatPercentage(citation.relevanceScore)} relevance</span>}
          <span className="rounded-full border border-white/12 bg-white/[0.04] px-3 py-1.5 uppercase">{fileExtension(citation.fileName)}</span>
        </div>
        <div className="mt-5 rounded-[10px] border border-white/10 bg-white/[0.035] p-5">
          <p className={labelClass}>Retrieved excerpt</p>
          <p className="mt-3 whitespace-pre-wrap text-sm leading-7 text-white/72">{citation.snippet}</p>
        </div>
        <p className="mt-3 text-xs leading-5 text-white/38">This excerpt is from the passage used to ground the answer. Download the original file to review its full context.</p>
        <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <button className={secondaryButtonClass} onClick={() => dialogRef.current?.close()} type="button">
            Close
          </button>
          <button className={primaryButtonClass} disabled={!citation.fileId || isDownloading} onClick={() => void handleDownload()} type="button">
            {isDownloading ? "Downloading…" : "Download original"}
          </button>
        </div>
      </div>
    </dialog>
  );
}

function ObservabilityView() {
  const [range, setRange] = useState<"24h" | "7d" | "30d">("24h");
  const [category, setCategory] = useState<ObservabilityCategory>("api");
  const [snapshot, setSnapshot] = useState<ObservabilitySnapshot | null>(null);
  const [snapshotError, setSnapshotError] = useState<string | null>(null);
  const [snapshotLoading, setSnapshotLoading] = useState(true);
  const [logs, setLogs] = useState<AdminLogResponse | null>(null);
  const [logPage, setLogPage] = useState(1);
  const [logLoading, setLogLoading] = useState(false);
  const [logError, setLogError] = useState<string | null>(null);
  const emptyFilters: LogFilters = { level: "", search: "", source: "", from: "", to: "" };
  const [filters, setFilters] = useState<LogFilters>(emptyFilters);
  const [appliedFilters, setAppliedFilters] = useState<LogFilters>(emptyFilters);
  const [activity, setActivity] = useState<ActivityResponse | null>(null);
  const [activityPage, setActivityPage] = useState(1);
  const [activityLoading, setActivityLoading] = useState(false);
  const [activityError, setActivityError] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const loadSnapshot = useCallback(async () => {
    setSnapshotLoading(true);
    setSnapshotError(null);
    try {
      setSnapshot(await apiJson<ObservabilitySnapshot>(`/admin/observability?range=${range}`));
    } catch (err) {
      setSnapshotError(err instanceof Error ? err.message : "Unable to load observability metrics.");
    } finally {
      setSnapshotLoading(false);
    }
  }, [range]);

  const loadLogs = useCallback(async () => {
    if (category === "activity") return;
    setLogLoading(true);
    setLogError(null);
    try {
      const params = new URLSearchParams({ page: String(logPage), pageSize: "25", category });
      if (appliedFilters.level) params.set("level", appliedFilters.level);
      if (appliedFilters.search) params.set("search", appliedFilters.search);
      if (appliedFilters.source) params.set("source", appliedFilters.source);
      if (appliedFilters.from) params.set("from", new Date(appliedFilters.from).toISOString());
      if (appliedFilters.to) params.set("to", new Date(appliedFilters.to).toISOString());
      setLogs(await apiJson<AdminLogResponse>(`/admin/logs?${params}`));
    } catch (err) {
      setLogError(err instanceof Error ? err.message : "Unable to load system logs.");
    } finally {
      setLogLoading(false);
    }
  }, [appliedFilters, category, logPage]);

  const loadActivity = useCallback(async () => {
    setActivityLoading(true);
    setActivityError(null);
    try {
      setActivity(await apiJson<ActivityResponse>(`/admin/activity?page=${activityPage}&pageSize=25`));
    } catch (err) {
      setActivityError(err instanceof Error ? err.message : "Unable to load recent activity.");
    } finally {
      setActivityLoading(false);
    }
  }, [activityPage]);

  useEffect(() => {
    void loadSnapshot();
  }, [loadSnapshot, refreshKey]);

  useEffect(() => {
    if (category === "activity") {
      void loadActivity();
      return;
    }
    void loadLogs();
  }, [category, loadActivity, loadLogs, refreshKey]);

  useEffect(() => {
    if (category !== "activity") return;
    const refresh = window.setInterval(() => void loadActivity(), 15_000);
    return () => window.clearInterval(refresh);
  }, [category, loadActivity]);

  function selectCategory(nextCategory: ObservabilityCategory) {
    setCategory(nextCategory);
    setLogPage(1);
    setActivityPage(1);
  }

  function applyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setLogPage(1);
    setAppliedFilters({ ...filters, search: filters.search.trim() });
  }

  const requestChart = snapshot?.requests.map((point) => ({ ...point, label: formatMetricBucket(point.timestamp, range) })) ?? [];
  const aiChart = snapshot?.aiUsage.map((point) => ({ ...point, label: formatMetricBucket(point.timestamp, range) })) ?? [];
  const securityChart = snapshot?.security.map((point) => ({ ...point, label: formatMetricBucket(point.timestamp, range) })) ?? [];
  const tooltipStyle = { backgroundColor: portfolioPalette.raised, border: "1px solid rgba(255,255,255,0.14)", borderRadius: 8, color: "#fff" };
  const chartAxis = { fill: "rgba(255,255,255,0.45)", fontSize: 11 };

  return (
    <section className="min-h-[calc(100vh-118px)] bg-[#000000] px-4 py-7 text-white sm:px-7 lg:px-9 lg:py-10">
      <div className="mx-auto max-w-[1380px]">
        <div className="relative flex flex-col gap-6 border-b border-white/10 pb-8 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <SectionLabel className="text-[#ffffff]">Administration / observability</SectionLabel>
            <h2 className="mt-3 text-4xl font-semibold tracking-[-0.02em] leading-tight text-white sm:text-6xl">System <span className={portfolioClasses.gradientText}>signals</span></h2>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-white/48">Request health, security events, AI usage, activity, and retained structured logs.</p>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <div className="flex rounded-[8px] border border-white/12 bg-white/[0.035] p-1" aria-label="Metrics range">
              {(["24h", "7d", "30d"] as const).map((option) => (
                <button key={option} className={`rounded-[6px] border px-4 py-2 text-xs font-semibold uppercase tracking-[0.14em] transition ${range === option ? "border-[#ffffff]/45 bg-[#ffffff]/12 text-[#ffffff]" : "border-transparent text-white/50 hover:text-white"}`} onClick={() => setRange(option)} type="button">
                  {option}
                </button>
              ))}
            </div>
            <button className="rounded-[8px] border border-white/14 px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.14em] text-white/65 transition hover:border-[#ffffff]/65 hover:text-white" disabled={snapshotLoading || logLoading || activityLoading} onClick={() => setRefreshKey((value) => value + 1)} type="button">
              Refresh
            </button>
          </div>
        </div>

        {snapshotError ? <div className="mt-6 rounded-[8px] border border-red-400/25 bg-red-500/10 px-4 py-3 text-sm text-red-200" role="alert">{snapshotError}</div> : null}

        {/* Progressive disclosure: one hero signal answers "is everything okay?" before the
            detail tiles and charts below it — not four equal-weight numbers competing for
            attention at once. */}
        <div className="mt-7 grid gap-3 lg:grid-cols-[1.3fr_1fr]">
          <SystemHealthHero errorRatePercent={snapshot?.stats.errorRatePercent} requestsToday={snapshot?.stats.requestsToday} range={range} />
          <div className="grid grid-cols-2 gap-3 lg:grid-rows-2">
            <MetricCard label="Requests today (UTC)" value={formatInteger(snapshot?.stats.requestsToday)} detail="Completed API requests" accent="text-[#ffffff]" />
            <MetricCard label="Average latency" value={formatMilliseconds(snapshot?.stats.averageLatencyMilliseconds)} detail={`Across the selected ${range}`} accent="text-[#f0c674]" />
            <MetricCard
              label="AI spend this month"
              value={formatCurrency(snapshot?.stats.aiSpendThisMonthUsd)}
              detail={snapshot?.stats.aiPricingComplete === false ? "Known spend only; pricing is incomplete" : "Estimated from measured model tokens"}
              accent="text-[#ffffff]"
            />
            <MetricCard label="5xx error rate" value={formatPercent(snapshot?.stats.errorRatePercent)} detail={`Across the selected ${range}`} accent="text-[#ff9891]" />
          </div>
        </div>

        {/* Each panel is single-axis by design: two measures on different scales (count vs
            percent, tokens vs USD) become two panels, not one dual-axis chart — dual axes are
            a well-known reading-error trap (the two scales imply a false correlation between
            series). Categorical colors (accent + teal) are the one pair actually validated for
            CVD-safe adjacency (see the dataviz skill); every other panel here is a single series,
            so it can safely carry a status hue when the metric is itself a status signal
            (error rate, security events) without competing against anything else on screen. */}
        <div className="mt-6 grid gap-4 xl:grid-cols-2">
          <ChartPanel eyebrow="API traffic" title="Request volume">
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={requestChart} margin={{ top: 10, right: 4, left: -24, bottom: 0 }}>
                <CartesianGrid stroke="rgba(255,255,255,0.07)" vertical={false} />
                <XAxis dataKey="label" tick={chartAxis} axisLine={false} tickLine={false} minTickGap={24} />
                <YAxis tick={chartAxis} axisLine={false} tickLine={false} allowDecimals={false} />
                <Tooltip contentStyle={tooltipStyle} labelStyle={{ color: "#fff" }} />
                <Bar dataKey="requests" name="Requests" fill={portfolioPalette.accent} radius={[3, 3, 0, 0]} maxBarSize={24} />
              </BarChart>
            </ResponsiveContainer>
          </ChartPanel>

          <ChartPanel eyebrow="API traffic" title="5xx error rate">
            <ResponsiveContainer width="100%" height={220}>
              <AreaChart data={requestChart} margin={{ top: 10, right: 4, left: -24, bottom: 0 }}>
                <CartesianGrid stroke="rgba(255,255,255,0.07)" vertical={false} />
                <XAxis dataKey="label" tick={chartAxis} axisLine={false} tickLine={false} minTickGap={24} />
                <YAxis tick={chartAxis} axisLine={false} tickLine={false} unit="%" />
                <Tooltip contentStyle={tooltipStyle} labelStyle={{ color: "#fff" }} />
                <Area type="monotone" dataKey="errorRatePercent" name="5xx rate %" stroke={portfolioPalette.danger} fill={portfolioPalette.danger} fillOpacity={0.16} strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </ChartPanel>

          <ChartPanel eyebrow="Log mix" title="Level distribution">
            <ResponsiveContainer width="100%" height={220}>
              <AreaChart data={snapshot?.logLevels ?? []} margin={{ top: 10, right: 4, left: -24, bottom: 0 }}>
                <defs><linearGradient id="levelFill" x1="0" y1="0" x2="0" y2="1"><stop offset="5%" stopColor={portfolioPalette.accent} stopOpacity={0.7} /><stop offset="95%" stopColor={portfolioPalette.accent} stopOpacity={0.04} /></linearGradient></defs>
                <CartesianGrid stroke="rgba(255,255,255,0.07)" vertical={false} />
                <XAxis dataKey="level" tick={chartAxis} axisLine={false} tickLine={false} />
                <YAxis tick={chartAxis} axisLine={false} tickLine={false} allowDecimals={false} />
                <Tooltip contentStyle={tooltipStyle} labelStyle={{ color: "#fff" }} />
                <Area type="monotone" dataKey="count" stroke={portfolioPalette.accent} fill="url(#levelFill)" strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </ChartPanel>

          <ChartPanel eyebrow="Security" title="Security event timeline">
            <ResponsiveContainer width="100%" height={220}>
              <AreaChart data={securityChart} margin={{ top: 10, right: 4, left: -24, bottom: 0 }}>
                <CartesianGrid stroke="rgba(255,255,255,0.07)" vertical={false} />
                <XAxis dataKey="label" tick={chartAxis} axisLine={false} tickLine={false} minTickGap={24} />
                <YAxis tick={chartAxis} axisLine={false} tickLine={false} allowDecimals={false} />
                <Tooltip contentStyle={tooltipStyle} labelStyle={{ color: "#fff" }} />
                <Area type="stepAfter" dataKey="events" name="Events" stroke={portfolioPalette.warning} fill={portfolioPalette.warning} fillOpacity={0.16} strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </ChartPanel>

          <ChartPanel eyebrow="AI usage" title="Token volume">
            <ResponsiveContainer width="100%" height={220}>
              <AreaChart data={aiChart} margin={{ top: 10, right: 4, left: -24, bottom: 0 }}>
                <CartesianGrid stroke="rgba(255,255,255,0.07)" vertical={false} />
                <XAxis dataKey="label" tick={chartAxis} axisLine={false} tickLine={false} minTickGap={24} />
                <YAxis tick={chartAxis} axisLine={false} tickLine={false} allowDecimals={false} />
                <Tooltip contentStyle={tooltipStyle} labelStyle={{ color: "#fff" }} />
                <Legend wrapperStyle={{ fontSize: 11, color: "rgba(255,255,255,0.55)" }} />
                <Area type="monotone" dataKey="inputTokens" name="Input tokens" stackId="tokens" stroke={portfolioPalette.accent} fill={portfolioPalette.accent} fillOpacity={0.38} />
                <Area type="monotone" dataKey="outputTokens" name="Output tokens" stackId="tokens" stroke="#0d9488" fill="#0d9488" fillOpacity={0.38} />
              </AreaChart>
            </ResponsiveContainer>
          </ChartPanel>

          <ChartPanel eyebrow="AI usage" title="Estimated cost (USD)">
            <ResponsiveContainer width="100%" height={220}>
              <AreaChart data={aiChart} margin={{ top: 10, right: 4, left: -24, bottom: 0 }}>
                <CartesianGrid stroke="rgba(255,255,255,0.07)" vertical={false} />
                <XAxis dataKey="label" tick={chartAxis} axisLine={false} tickLine={false} minTickGap={24} />
                <YAxis tick={chartAxis} axisLine={false} tickLine={false} tickFormatter={(value) => `$${Number(value).toFixed(3)}`} />
                <Tooltip contentStyle={tooltipStyle} labelStyle={{ color: "#fff" }} />
                <Area type="monotone" dataKey="estimatedCostUsd" name="Estimated cost (USD)" stroke={portfolioPalette.warning} fill={portfolioPalette.warning} fillOpacity={0.16} strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </ChartPanel>
        </div>

        <div className="mt-6 grid gap-4 xl:grid-cols-2">
          <InsightPanel title="Top errors" empty="No warning or error signatures in this range.">
            {snapshot?.topErrors.map((item) => (
              <div className="flex items-start justify-between gap-5 border-b border-white/[0.08] py-4 last:border-0" key={item.signature}>
                <div className="min-w-0"><p className="break-words text-sm text-white/82">{item.signature}</p><p className="mt-1 text-xs text-white/38">Last seen {formatDate(item.lastSeen)}</p></div>
                <span className="rounded-full border border-red-300/20 bg-red-400/10 px-3 py-1 text-xs text-red-200">{item.count}</span>
              </div>
            ))}
          </InsightPanel>
          <InsightPanel title="Recent security events" empty="No tagged security events in this range.">
            {snapshot?.recentSecurityEvents.map((item, index) => (
              <div className="grid grid-cols-[10px_minmax(0,1fr)] gap-3 border-b border-white/[0.08] py-4 last:border-0" key={`${item.timestamp}-${index}`}>
                <span className="mt-1.5 h-2 w-2 rounded-full bg-[#ffffff] shadow-[0_0_12px_rgba(110,86,207,0.55)]" />
                <div><p className="text-xs font-semibold uppercase tracking-[0.14em] text-[#ffffff]">{item.event}</p><p className="mt-2 text-sm leading-6 text-white/65">{item.message}</p><p className="mt-2 text-xs text-white/35">{formatDate(item.timestamp)}</p></div>
              </div>
            ))}
          </InsightPanel>
        </div>

        <div className="mt-8 border-t border-white/10 pt-7">
          <div className="flex flex-wrap gap-2 pb-2" role="tablist" aria-label="Observability categories">
            {([
              ["api", "API"],
              ["security", "Security"],
              ["ai", "AI / Cost"],
              ["activity", "Activity"],
              ["errors", "Errors"],
            ] as const).map(([value, label]) => (
              <button key={value} role="tab" aria-selected={category === value} className={`shrink-0 rounded-[8px] border px-5 py-2.5 text-xs font-semibold uppercase tracking-[0.14em] transition ${category === value ? "border-[#ffffff]/45 bg-[#ffffff]/[0.12] text-white" : "border-white/14 text-white/50 hover:border-white/30 hover:text-white"}`} onClick={() => selectCategory(value)} type="button">
                {label}
              </button>
            ))}
          </div>

          {category === "activity" ? (
            <ObservabilityActivity activity={activity} error={activityError} isLoading={activityLoading} onPageChange={setActivityPage} />
          ) : (
            <div className="mt-5 overflow-hidden rounded-[8px] border border-white/12 bg-white/[0.025]">
              <form className="grid gap-4 border-b border-white/10 p-5 md:grid-cols-2 xl:grid-cols-[140px_170px_minmax(220px,1fr)_180px_180px_auto] xl:items-end" onSubmit={applyFilters}>
                <DarkSelect label="Level" value={filters.level} onChange={(value) => setFilters({ ...filters, level: value })} options={[["", "All levels"], ["Information", "Information"], ["Warning", "Warning"], ["Error", "Error"], ["Fatal", "Fatal"], ["Debug", "Debug"]]} />
                <DarkSelect label="Module" value={filters.source} onChange={(value) => setFilters({ ...filters, source: value })} options={[["", "All modules"], ["Auth", "Auth"], ["Drive", "Drive"], ["Processing", "Processing"], ["SearchChat", "Search / Chat"], ["Admin", "Admin"]]} />
                <label className="block"><span className="text-[10px] font-semibold uppercase tracking-[0.18em] text-white/38">Search logs</span><input className="mt-2 w-full rounded-[7px] border border-white/12 bg-black/25 px-3 py-2.5 text-sm text-white outline-none placeholder:text-white/25 focus:border-[#ffffff]/70" value={filters.search} onChange={(event) => setFilters({ ...filters, search: event.target.value })} placeholder="Message, path, source..." /></label>
                <DarkDate label="From" value={filters.from} onChange={(value) => setFilters({ ...filters, from: value })} />
                <DarkDate label="To" value={filters.to} onChange={(value) => setFilters({ ...filters, to: value })} />
                <button className={primaryButtonClass} disabled={logLoading} type="submit">Apply</button>
              </form>

              {logError ? <div className="m-5 rounded-[8px] border border-red-400/25 bg-red-500/10 px-4 py-3 text-sm text-red-200" role="alert">{logError}</div> : null}
              <div className="min-h-[380px] divide-y divide-white/[0.08]">
                {logLoading && !logs ? <p className="p-6 text-sm text-white/45">Loading persisted logs...</p> : null}
                {!logLoading && logs?.entries.length === 0 ? <p className="p-6 text-sm text-white/45">No log entries match these filters.</p> : null}
                {logs?.entries.map((entry, index) => (
                  <details className="group px-5 py-5 sm:px-6" key={`${entry.timestamp}-${index}`}>
                    <summary className="grid cursor-pointer list-none gap-4 outline-none sm:grid-cols-[110px_180px_minmax(0,1fr)_24px] sm:items-center [&::-webkit-details-marker]:hidden">
                      <span className={`w-fit rounded-full border px-3 py-1 text-[9px] font-semibold uppercase tracking-[0.14em] ${darkLogLevelClass(entry.level)}`}>{entry.level}</span>
                      <time className="text-xs text-white/38" dateTime={entry.timestamp}>{formatDate(entry.timestamp)}</time>
                      <span className="min-w-0 break-words font-mono text-xs leading-5 text-white/72">{entry.message}</span>
                      <span className="text-white/35 transition group-open:rotate-45" aria-hidden="true">+</span>
                    </summary>
                    <div className="mt-5 grid gap-4 border-t border-white/[0.08] pt-5 lg:grid-cols-2">
                      <div><p className="text-[10px] font-semibold uppercase tracking-[0.16em] text-white/35">Properties</p><pre className="mt-3 max-h-80 overflow-auto whitespace-pre-wrap break-all rounded-[7px] bg-black/35 p-4 text-xs leading-5 text-white/55">{JSON.stringify(redactForDisplay(entry.properties), null, 2)}</pre></div>
                      <div><p className="text-[10px] font-semibold uppercase tracking-[0.16em] text-white/35">Exception</p><pre className="mt-3 max-h-80 overflow-auto whitespace-pre-wrap break-words rounded-[7px] bg-black/35 p-4 text-xs leading-5 text-red-200/70">{entry.exception ?? "No exception attached."}</pre></div>
                    </div>
                  </details>
                ))}
              </div>
              <div className="flex items-center justify-between gap-4 border-t border-white/10 px-5 py-5 text-sm text-white/42 sm:px-6">
                <span>Page {logs?.page ?? 1}</span>
                <div className="flex gap-2"><DarkPageButton disabled={logLoading || (logs?.page ?? 1) <= 1} onClick={() => setLogPage(Math.max(1, (logs?.page ?? 1) - 1))}>Previous</DarkPageButton><DarkPageButton disabled={logLoading || !logs?.hasMore} onClick={() => setLogPage((logs?.page ?? 1) + 1)}>Next</DarkPageButton></div>
              </div>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}

function SystemHealthHero({
  errorRatePercent,
  requestsToday,
  range,
}: {
  errorRatePercent: number | undefined;
  requestsToday: number | undefined;
  range: "24h" | "7d" | "30d";
}) {
  // A simple, defensible health heuristic from the one number that most directly answers
  // "is everything okay?" — the same signal a real on-call dashboard would lead with.
  const hasData = errorRatePercent !== undefined && requestsToday !== undefined && requestsToday > 0;
  const status: "good" | "warning" | "danger" | "unknown" = !hasData
    ? "unknown"
    : errorRatePercent! < 1
      ? "good"
      : errorRatePercent! < 5
        ? "warning"
        : "danger";
  const copy = {
    good: { label: "Operating normally", tone: statusTone.success, dot: "bg-[#3fb950]" },
    warning: { label: "Elevated error rate", tone: statusTone.warning, dot: "bg-[#e3b341]" },
    danger: { label: "Degraded — high error rate", tone: statusTone.danger, dot: "bg-[#f85149]" },
    unknown: { label: "No traffic yet", tone: statusTone.neutral, dot: "bg-white/30" },
  }[status];

  return (
    <article className="flex flex-col justify-between rounded-lg border border-white/10 bg-[#0a0a0a] p-6">
      <div className="flex items-center justify-between">
        <p className="text-[10px] font-semibold uppercase tracking-[0.18em] text-white/38">System health</p>
        <span className={`${portfolioClasses.badge} ${copy.tone}`}>
          <span className={`h-1.5 w-1.5 rounded-full ${copy.dot}`} aria-hidden="true" />
          {copy.label}
        </span>
      </div>
      <p className="mt-6 text-6xl font-semibold tracking-[-0.03em] text-white">{hasData ? formatPercent(errorRatePercent) : "—"}</p>
      <p className="mt-3 text-sm text-white/48">5xx error rate across the selected {range}{hasData ? ` · ${formatInteger(requestsToday)} requests today` : ""}</p>
    </article>
  );
}

function MetricCard({ label, value, detail, accent }: { label: string; value: string; detail: string; accent: string }) {
  return <article className="rounded-[8px] border border-white/10 bg-white/[0.035] p-5"><p className="text-[10px] font-semibold uppercase tracking-[0.18em] text-white/38">{label}</p><p className={`mt-4 text-4xl font-semibold tracking-[-0.02em] ${accent}`}>{value}</p><p className="mt-3 text-xs leading-5 text-white/35">{detail}</p></article>;
}

function ChartPanel({ eyebrow, title, children }: { eyebrow: string; title: string; children: React.ReactNode }) {
  return <section className="min-w-0 rounded-[8px] border border-white/10 bg-white/[0.03] p-4 sm:p-5"><p className="text-[10px] font-semibold uppercase tracking-[0.18em] text-white/35">{eyebrow}</p><h3 className="mt-2 text-2xl font-semibold tracking-[-0.015em] text-white/90">{title}</h3><div className="mt-5 h-[270px] min-w-0">{children}</div></section>;
}

function InsightPanel({ title, empty, children }: { title: string; empty: string; children: React.ReactNode }) {
  const items = Array.isArray(children) ? children.filter(Boolean) : children;
  const isEmpty = Array.isArray(items) && items.length === 0;
  return <section className="rounded-[8px] border border-white/10 bg-white/[0.025] p-5"><h3 className="text-2xl font-semibold tracking-[-0.015em] text-white/90">{title}</h3><div className="mt-3 max-h-[330px] overflow-y-auto">{isEmpty ? <p className="py-5 text-sm text-white/38">{empty}</p> : children}</div></section>;
}

function ObservabilityActivity({ activity, error, isLoading, onPageChange }: { activity: ActivityResponse | null; error: string | null; isLoading: boolean; onPageChange: (page: number) => void }) {
  return <div className="mt-5 overflow-hidden rounded-[8px] border border-white/12 bg-white/[0.025]">
    {error ? <div className="m-5 rounded-[8px] border border-red-400/25 bg-red-500/10 px-4 py-3 text-sm text-red-200" role="alert">{error}</div> : null}
    <div className="min-h-[380px] divide-y divide-white/[0.08]">
      {isLoading && !activity ? <p className="p-6 text-sm text-white/45">Loading recent activity...</p> : null}
      {!isLoading && activity?.entries.length === 0 ? <p className="p-6 text-sm text-white/45">No activity has been recorded yet.</p> : null}
      {activity?.entries.map((entry) => <article className="grid gap-3 px-5 py-5 sm:grid-cols-[170px_minmax(0,1fr)] sm:px-6" key={entry.id}><div><span className="rounded-full border border-[#ffffff]/30 bg-[#ffffff]/10 px-3 py-1 text-[9px] font-semibold uppercase tracking-[0.14em] text-[#ffffff]">{activityTypeLabel(entry.type)}</span><time className="mt-3 block text-xs text-white/35" dateTime={entry.occurredAt}>{formatDate(entry.occurredAt)}</time></div><div><p className="text-xl font-medium text-white/82">{entry.summary}</p>{entry.fileId ? <p className="mt-2 break-all text-xs text-white/28">File {entry.fileId}</p> : null}{entry.conversationId ? <p className="mt-2 break-all text-xs text-white/28">Conversation {entry.conversationId}</p> : null}</div></article>)}
    </div>
    <div className="flex items-center justify-between border-t border-white/10 px-5 py-5 text-sm text-white/42"><span>Page {activity?.page ?? 1}</span><div className="flex gap-2"><DarkPageButton disabled={isLoading || (activity?.page ?? 1) <= 1} onClick={() => onPageChange(Math.max(1, (activity?.page ?? 1) - 1))}>Previous</DarkPageButton><DarkPageButton disabled={isLoading || !activity?.hasMore} onClick={() => onPageChange((activity?.page ?? 1) + 1)}>Next</DarkPageButton></div></div>
  </div>;
}

function DarkSelect({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: ReadonlyArray<readonly [string, string]> }) {
  return <label className="block"><span className="text-[10px] font-semibold uppercase tracking-[0.18em] text-white/38">{label}</span><select className="mt-2 w-full rounded-[7px] border border-white/12 bg-[#0a0a0a] px-3 py-2.5 text-sm text-white outline-none focus:border-[#ffffff]/70" value={value} onChange={(event) => onChange(event.target.value)}>{options.map(([optionValue, optionLabel]) => <option value={optionValue} key={optionValue || "all"}>{optionLabel}</option>)}</select></label>;
}

function DarkDate({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return <label className="block"><span className="text-[10px] font-semibold uppercase tracking-[0.18em] text-white/38">{label}</span><input type="datetime-local" className="mt-2 w-full rounded-[7px] border border-white/12 bg-[#0a0a0a] px-3 py-2.5 text-sm text-white outline-none focus:border-[#ffffff]/70 [color-scheme:dark]" value={value} onChange={(event) => onChange(event.target.value)} /></label>;
}

function DarkPageButton({ children, disabled, onClick }: { children: React.ReactNode; disabled: boolean; onClick: () => void }) {
  return <button className="rounded-[7px] border border-white/14 px-3 py-2 text-xs text-white/60 transition hover:border-white/35 hover:text-white disabled:cursor-not-allowed disabled:opacity-30" disabled={disabled} onClick={onClick} type="button">{children}</button>;
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
    <div className="flex rounded-[8px] border border-white/12 bg-white/[0.035]">
      <select
        aria-label={`Move ${file.originalFileName} to folder`}
        className="max-w-[92px] rounded-l-[8px] bg-[#0a0a0a] px-2 text-xs text-white/72 outline-none focus:text-white"
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
      <button className="grid h-11 w-11 place-items-center border-l border-white/10 text-white/72 transition hover:bg-[#ffffff]/10 hover:text-[#ffffff] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#ffffff]/70" onClick={() => onMoveFile(file)} type="button" aria-label={`Move ${file.originalFileName}`}>
        <FolderGlyph />
      </button>
    </div>
  );
}

function ShieldMark() {
  return (
    <span className="grid h-9 w-9 place-items-center rounded-[7px] border border-[#ffffff]/55 bg-[#ffffff]/10 text-[#ffffff]" aria-hidden="true">
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

function formatPercentage(score: number) {
  return `${Math.round(Math.max(0, Math.min(1, score)) * 100)}%`;
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

function formatMetricBucket(value: string, range: "24h" | "7d" | "30d") {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Unknown";
  return new Intl.DateTimeFormat("en", range === "24h" ? { hour: "numeric" } : { month: "short", day: "numeric" }).format(date);
}

function formatInteger(value: number | undefined) {
  return value === undefined ? "--" : new Intl.NumberFormat("en").format(value);
}

function formatPercent(value: number | undefined) {
  return value === undefined ? "--" : `${value.toFixed(2)}%`;
}

function formatMilliseconds(value: number | undefined) {
  return value === undefined ? "--" : `${value.toFixed(value >= 100 ? 0 : 1)} ms`;
}

function formatCurrency(value: number | undefined) {
  return value === undefined
    ? "--"
    : new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", minimumFractionDigits: 4, maximumFractionDigits: 6 }).format(value);
}

function redactForDisplay(value: unknown, key = ""): unknown {
  const normalizedKey = key.replace(/[_-]/g, "").toLowerCase();
  const sensitive = ["password", "authorization", "cookie", "secret", "apikey", "accesstoken", "refreshtoken", "servicetoken", "tokenhash"];
  if (sensitive.some((part) => normalizedKey.includes(part))) return "[REDACTED]";
  if (Array.isArray(value)) return value.map((item) => redactForDisplay(item));
  if (value && typeof value === "object") return Object.fromEntries(Object.entries(value).map(([childKey, childValue]) => [childKey, redactForDisplay(childValue, childKey)]));
  return value;
}

function activityTypeLabel(type: string) {
  switch (type) {
    case "FileUploaded":
      return "Upload";
    case "ProcessingCompleted":
      return "Processed";
    case "ProcessingFailed":
      return "Failed";
    case "ChatAnswered":
      return "Chat";
    default:
      return type;
  }
}

function darkLogLevelClass(level: string) {
  switch (level.toLowerCase()) {
    case "fatal":
    case "error":
      return "border-red-300/25 bg-red-400/10 text-red-200";
    case "warning":
      return "border-amber-200/25 bg-amber-300/10 text-amber-100";
    case "debug":
    case "verbose":
      return "border-white/12 bg-white/[0.03] text-white/45";
    default:
      return "border-emerald-200/20 bg-emerald-300/10 text-emerald-100";
  }
}
