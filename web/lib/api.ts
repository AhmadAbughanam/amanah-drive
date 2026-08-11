import type { ApiErrorBody, AuthResponse } from "./types";

const API_BASE_URL = (process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:8080").replace(/\/$/, "");

let accessToken: string | null = null;
let onUnauthorized: (() => void) | null = null;

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
  }
}

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function getAccessToken() {
  return accessToken;
}

export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler;
}

export async function login(email: string, password: string) {
  const response = await fetch(`${API_BASE_URL}/auth/login`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });

  if (!response.ok) {
    throw new ApiError(await errorMessage(response), response.status);
  }

  const body = (await response.json()) as AuthResponse;
  setAccessToken(body.accessToken);
  return body.accessToken;
}

export async function refreshAccessToken() {
  const response = await fetch(`${API_BASE_URL}/auth/refresh`, {
    method: "POST",
    credentials: "include",
  });

  if (!response.ok) {
    setAccessToken(null);
    return null;
  }

  const body = (await response.json()) as AuthResponse;
  setAccessToken(body.accessToken);
  return body.accessToken;
}

export async function logout() {
  try {
    await apiFetch("/auth/logout", { method: "POST" });
  } finally {
    setAccessToken(null);
  }
}

export async function apiFetch(path: string, init: RequestInit = {}, retry = true): Promise<Response> {
  const headers = new Headers(init.headers);
  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    credentials: "include",
    headers,
  });

  if (response.status === 401 && retry) {
    const refreshedToken = await refreshAccessToken();
    if (refreshedToken) {
      return apiFetch(path, init, false);
    }

    onUnauthorized?.();
  }

  return response;
}

export async function apiJson<T>(path: string, init: RequestInit = {}) {
  const response = await apiFetch(path, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...init.headers,
    },
  });

  if (!response.ok) {
    throw new ApiError(await errorMessage(response), response.status);
  }

  return (await response.json()) as T;
}

export async function errorMessage(response: Response) {
  if (response.status === 413) {
    return "File too large for the configured upload limit.";
  }

  try {
    const body = (await response.json()) as ApiErrorBody;
    if (body.message) {
      return body.message;
    }
    if (body.detail) {
      return body.detail;
    }
    if (body.title) {
      return body.title;
    }
    if (body.errors) {
      return Object.values(body.errors).flat().join(" ");
    }
  } catch {
    // Fall through to a status-based message.
  }

  return `Request failed with status ${response.status}.`;
}
