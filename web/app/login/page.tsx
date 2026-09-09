"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { portfolioClasses } from "@/components/portfolio-theme";
import { ApiError } from "@/lib/api";
import { useAuth } from "../auth-provider";

export default function LoginPage() {
  const router = useRouter();
  const { signIn } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      await signIn(email, password);
      router.push("/drive");
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        setError("Invalid email or password.");
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Unable to sign in.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="flex min-h-screen flex-col bg-[#000000] text-white">
      <header className="flex items-center justify-between px-6 py-6 sm:px-10">
        <Link
          href="/"
          className="group flex items-center gap-3 outline-none focus-visible:ring-2 focus-visible:ring-[#ffffff]"
          aria-label="Ahmad Abughanam"
        >
          <span className="grid h-9 w-9 place-items-center rounded-lg border border-white/14 text-xs font-semibold text-white transition group-hover:border-white/28">AA</span>
        </Link>
        <Link
          href="/"
          className="text-[11px] font-medium uppercase tracking-[0.16em] text-white/48 outline-none transition hover:text-white focus-visible:ring-2 focus-visible:ring-[#ffffff]"
        >
          Back to portfolio
        </Link>
      </header>

      <div className="flex flex-1 items-center justify-center px-6 py-12">
        <form
          className="w-full max-w-[400px] rounded-lg border border-white/10 bg-[#0a0a0a] p-7 sm:p-8"
          onSubmit={onSubmit}
          noValidate
        >
          <div className="mb-7">
            <p className={portfolioClasses.label}>Admin access</p>
            <h1 className="mt-3 text-2xl font-semibold tracking-[-0.02em] text-white">Sign in to Amanah Drive</h1>
            <p className="mt-2 text-sm leading-6 text-white/52">
              Single admin account. Registration is handled separately during bootstrap.
            </p>
          </div>

          <div className="space-y-5">
            <label className="block">
              <span className={portfolioClasses.label}>Email</span>
              <input
                className={`${portfolioClasses.field} mt-2.5`}
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                autoComplete="email"
                autoFocus
                required
              />
            </label>

            <div className="block">
              <label className={portfolioClasses.label} htmlFor="password">Password</label>
              <div className="relative mt-2.5">
                <input
                  id="password"
                  className={`${portfolioClasses.field} pr-11`}
                  type={showPassword ? "text" : "password"}
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  autoComplete="current-password"
                  required
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((value) => !value)}
                  className="absolute inset-y-0 right-0 flex w-11 items-center justify-center text-white/40 outline-none transition hover:text-white/80 focus-visible:text-white"
                  aria-label={showPassword ? "Hide typed characters" : "Reveal typed characters"}
                  aria-pressed={showPassword}
                >
                  {showPassword ? (
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden="true">
                      <path d="M3 3l18 18M10.58 10.58a2 2 0 002.83 2.83M9.88 4.24A9.77 9.77 0 0112 4c5 0 9 4 10 8-.31 1.24-.9 2.47-1.72 3.56M6.6 6.6C4.4 8.05 2.8 10 2 12c1 4 5 8 10 8 1.42 0 2.77-.32 3.98-.88" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  ) : (
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden="true">
                      <path d="M2 12s4-8 10-8 10 8 10 8-4 8-10 8-10-8-10-8z" strokeLinecap="round" strokeLinejoin="round" />
                      <circle cx="12" cy="12" r="3" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  )}
                </button>
              </div>
            </div>

            {error ? (
              <div className="rounded-lg border border-[#f85149]/30 bg-[#f85149]/[0.08] px-4 py-3 text-sm leading-6 text-[#ff9891]" role="alert">
                {error}
              </div>
            ) : null}

            <button className={`${portfolioClasses.primaryButton} w-full py-3.5`} type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Signing in…" : "Sign in"}
            </button>
          </div>
        </form>
      </div>
    </main>
  );
}
