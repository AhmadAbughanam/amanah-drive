"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { getAccessToken, login, logout, refreshAccessToken, setUnauthorizedHandler } from "@/lib/api";

type AuthStatus = "checking" | "authenticated" | "anonymous";

type AuthContextValue = {
  status: AuthStatus;
  accessToken: string | null;
  signIn: (email: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
  ensureSession: () => Promise<boolean>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const [accessToken, setTokenState] = useState<string | null>(getAccessToken());
  const [status, setStatus] = useState<AuthStatus>(accessToken ? "authenticated" : "anonymous");

  const clearSession = useCallback(() => {
    setTokenState(null);
    setStatus("anonymous");
    router.push("/login");
  }, [router]);

  useEffect(() => {
    setUnauthorizedHandler(clearSession);
    return () => setUnauthorizedHandler(null);
  }, [clearSession]);

  const ensureSession = useCallback(async () => {
    if (getAccessToken()) {
      setTokenState(getAccessToken());
      setStatus("authenticated");
      return true;
    }

    setStatus("checking");
    const refreshed = await refreshAccessToken();
    setTokenState(refreshed);
    setStatus(refreshed ? "authenticated" : "anonymous");
    return Boolean(refreshed);
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    const token = await login(email, password);
    setTokenState(token);
    setStatus("authenticated");
  }, []);

  const signOut = useCallback(async () => {
    await logout();
    setTokenState(null);
    setStatus("anonymous");
    router.push("/login");
  }, [router]);

  const value = useMemo<AuthContextValue>(
    () => ({ status, accessToken, signIn, signOut, ensureSession }),
    [accessToken, ensureSession, signIn, signOut, status],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside AuthProvider");
  }

  return context;
}
