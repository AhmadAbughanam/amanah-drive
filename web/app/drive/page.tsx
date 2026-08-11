"use client";

import { ChangeEvent, useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { apiFetch, apiJson, errorMessage } from "@/lib/api";
import type { FileItem, Folder, FolderContents } from "@/lib/types";
import { useAuth } from "../auth-provider";

type Breadcrumb = { id: string | null; name: string };

export default function DrivePage() {
  const router = useRouter();
  const { status, ensureSession, signOut } = useAuth();
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

  async function downloadFile(file: FileItem) {
    setError(null);
    try {
      const response = await apiFetch(`/drive/files/${file.id}/download`);
      if (!response.ok) {
        throw new Error(await errorMessage(response));
      }

      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = file.originalFileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Download failed.");
    }
  }

  if (status === "checking" || (status === "anonymous" && !contents)) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-slate-950 text-white">
        <p>Checking session...</p>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-[#f7f8fb]">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-4 px-5 py-5 md:px-8">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.16em] text-teal-700">Amanah Drive</p>
            <h1 className="text-2xl font-semibold text-slate-950">File management</h1>
          </div>
          <button className="rounded-md border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-800 hover:border-teal-700 hover:text-teal-800" onClick={signOut}>
            Logout
          </button>
        </div>
      </header>

      <section className="mx-auto max-w-7xl px-5 py-6 md:px-8">
        <div className="mb-5 flex flex-wrap items-center gap-2 text-sm">
          {breadcrumbs.map((item, index) => (
            <button
              key={`${item.id ?? "root"}-${index}`}
              className="rounded-md px-2 py-1 font-medium text-slate-700 hover:bg-white hover:text-teal-800"
              onClick={() => goToBreadcrumb(index)}
            >
              {item.name}
            </button>
          ))}
        </div>

        {error ? (
          <div className="mb-5 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">
            {error}
          </div>
        ) : null}

        <div className="grid gap-5 lg:grid-cols-[340px_1fr]">
          <aside className="space-y-5">
            <div className="rounded-lg border border-slate-200 bg-white p-5">
              <h2 className="font-semibold text-slate-950">Create folder</h2>
              <div className="mt-4 flex gap-2">
                <input
                  className="min-w-0 flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-teal-700"
                  value={newFolderName}
                  onChange={(event) => setNewFolderName(event.target.value)}
                  placeholder="Folder name"
                />
                <button
                  className="rounded-md bg-slate-950 px-3 py-2 text-sm font-semibold text-white hover:bg-teal-800 disabled:bg-slate-400"
                  onClick={createFolder}
                  disabled={isWorking}
                >
                  Add
                </button>
              </div>
            </div>

            <div className="rounded-lg border border-slate-200 bg-white p-5">
              <h2 className="font-semibold text-slate-950">Upload file</h2>
              <p className="mt-2 text-sm leading-6 text-slate-600">Supported: PDF, Markdown, and plain text.</p>
              <input
                className="mt-4 block w-full text-sm text-slate-700 file:mr-3 file:rounded-md file:border-0 file:bg-slate-950 file:px-3 file:py-2 file:text-sm file:font-semibold file:text-white hover:file:bg-teal-800"
                type="file"
                accept=".pdf,.md,.txt,application/pdf,text/markdown,text/plain"
                onChange={uploadFile}
                disabled={isWorking}
              />
            </div>
          </aside>

          <section className="rounded-lg border border-slate-200 bg-white">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-5 py-4">
              <h2 className="font-semibold text-slate-950">Contents</h2>
              <div className="flex items-center gap-2 text-sm text-slate-600">
                <button className="rounded-md border border-slate-300 px-3 py-1 disabled:opacity-40" disabled={page <= 1 || isLoading} onClick={() => setPage((value) => Math.max(1, value - 1))}>
                  Previous
                </button>
                <span>Page {contents?.page ?? page}</span>
                <button className="rounded-md border border-slate-300 px-3 py-1 disabled:opacity-40" disabled={isLoading || ((contents?.folders.length ?? 0) + (contents?.files.length ?? 0) < pageSize)} onClick={() => setPage((value) => value + 1)}>
                  Next
                </button>
              </div>
            </div>

            {isLoading ? (
              <div className="p-8 text-sm text-slate-600">Loading drive contents...</div>
            ) : (
              <div className="divide-y divide-slate-100">
                {contents?.folders.map((folder) => (
                  <div key={folder.id} className="grid gap-3 px-5 py-4 md:grid-cols-[1fr_auto] md:items-center">
                    <button className="text-left font-semibold text-slate-950 hover:text-teal-800" onClick={() => enterFolder(folder)}>
                      {folder.name}
                    </button>
                    <div className="flex flex-wrap gap-2">
                      <button className="rounded-md border border-slate-300 px-3 py-1.5 text-sm hover:border-teal-700" onClick={() => renameFolder(folder)}>
                        Rename
                      </button>
                      <button className="rounded-md border border-red-200 px-3 py-1.5 text-sm text-red-700 hover:bg-red-50" onClick={() => deleteFolder(folder)}>
                        Delete
                      </button>
                    </div>
                  </div>
                ))}

                {contents?.files.map((file) => (
                  <div key={file.id} className="grid gap-3 px-5 py-4 xl:grid-cols-[1fr_auto] xl:items-center">
                    <div>
                      <p className="font-semibold text-slate-950">{file.originalFileName}</p>
                      <p className="mt-1 text-xs text-slate-500">
                        {file.contentType} / {formatBytes(file.sizeBytes)}
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <button className="rounded-md border border-slate-300 px-3 py-1.5 text-sm hover:border-teal-700" onClick={() => downloadFile(file)}>
                        Download
                      </button>
                      <button className="rounded-md border border-slate-300 px-3 py-1.5 text-sm hover:border-teal-700" onClick={() => renameFile(file)}>
                        Rename
                      </button>
                      <select
                        className="rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                        value={moveTargets[file.id] ?? ""}
                        onChange={(event) => setMoveTargets((items) => ({ ...items, [file.id]: event.target.value }))}
                      >
                        <option value="">Root</option>
                        {contents.folders.map((folder) => (
                          <option key={folder.id} value={folder.id}>
                            {folder.name}
                          </option>
                        ))}
                      </select>
                      <button className="rounded-md border border-slate-300 px-3 py-1.5 text-sm hover:border-teal-700" onClick={() => moveFile(file)}>
                        Move
                      </button>
                      <button className="rounded-md border border-red-200 px-3 py-1.5 text-sm text-red-700 hover:bg-red-50" onClick={() => deleteFile(file)}>
                        Delete
                      </button>
                    </div>
                  </div>
                ))}

                {contents && contents.folders.length === 0 && contents.files.length === 0 ? (
                  <div className="p-8 text-sm text-slate-600">This folder is empty.</div>
                ) : null}
              </div>
            )}
          </section>
        </div>
      </section>
    </main>
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
