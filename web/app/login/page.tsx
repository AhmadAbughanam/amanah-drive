"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { portfolioClasses, Scribble, SectionLabel } from "@/components/portfolio-theme";
import { ApiError } from "@/lib/api";
import { useAuth } from "../auth-provider";

export default function LoginPage() {
  const router = useRouter();
  const { signIn } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
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
    <main className="min-h-screen bg-[#060608] px-3 py-3 text-white sm:px-6 sm:py-6">
      <section className="relative mx-auto flex min-h-[calc(100vh-1.5rem)] max-w-[1280px] flex-col overflow-hidden rounded-[8px] border border-white/10 bg-[#0b0b10] shadow-[0_34px_120px_rgba(0,0,0,0.65)] sm:min-h-[calc(100vh-3rem)]">
        <Scribble className="pointer-events-none absolute -right-10 -top-7 w-52 text-[#c084fc]/20 sm:w-64" />
        <header className="relative z-10 flex items-center justify-between border-b border-white/10 bg-[#0d0c13] px-6 py-6 sm:px-10 lg:px-14">
          <Link
            href="/"
            className="group flex items-center gap-3 outline-none focus-visible:ring-2 focus-visible:ring-[#c084fc]"
            aria-label="Ahmad Abughanam"
          >
            <span className="grid h-10 w-10 place-items-center rounded-full border border-[#c084fc]/55 text-sm font-semibold text-white transition group-hover:bg-[#c084fc]/10">AA</span>
            <span className="hidden text-xs font-semibold uppercase tracking-[0.22em] text-white/76 sm:block">Ahmad Abughanam</span>
          </Link>
          <Link
            href="/"
            className="group inline-flex flex-col gap-2 text-[11px] font-semibold uppercase tracking-[0.16em] text-white/58 outline-none transition hover:text-white focus-visible:ring-2 focus-visible:ring-[#60a5fa]"
          >
            <span>Back to portfolio</span>
            <span className="h-px w-full origin-left bg-gradient-to-r from-[#c084fc] via-[#f472b6] to-[#60a5fa] transition-transform group-hover:scale-x-75" aria-hidden="true" />
          </Link>
        </header>

        <div className="relative grid flex-1 gap-12 px-6 py-12 sm:px-10 md:grid-cols-[minmax(0,1fr)_minmax(360px,460px)] md:items-center md:gap-16 lg:px-20 lg:py-16">
          <div className="relative max-w-[590px]">
            <SectionLabel>Admin access</SectionLabel>
            <h1 className="mt-7 font-serif text-[48px] font-normal leading-[1.02] text-white sm:text-[64px] lg:text-[78px]">
              Amanah
              <br />
              <span className={portfolioClasses.gradientText}>Drive</span>
            </h1>
            <div className="mt-8 h-px w-16 bg-gradient-to-r from-[#c084fc] to-[#60a5fa] md:mt-10" />
            <p className="mt-7 max-w-[410px] text-[15px] leading-7 text-white/62 md:mt-9 md:text-base">
              Sign in with the single admin account. Registration is handled separately during bootstrap.
            </p>
            <Scribble className="mt-8 w-44 text-[#f472b6]/55" />
          </div>

          <form className="rounded-[8px] border border-white/10 bg-white/[0.035] p-6 shadow-[0_24px_80px_rgba(0,0,0,0.32)] sm:p-8" onSubmit={onSubmit}>
            <div className="border-b border-white/10 pb-6">
              <p className={portfolioClasses.label}>Secure sign in</p>
              <h2 className="mt-3 font-serif text-3xl text-white/92">Welcome back</h2>
            </div>
            <div className="space-y-6">
              <label className="mt-7 block">
                <span className={portfolioClasses.label}>Email</span>
                <input
                  className={`${portfolioClasses.field} mt-3`}
                  type="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  autoComplete="email"
                  required
                />
              </label>
              <label className="block">
                <span className={portfolioClasses.label}>Password</span>
                <input
                  className={`${portfolioClasses.field} mt-3`}
                  type="password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  autoComplete="current-password"
                  required
                />
              </label>

              {error ? (
                <div className="rounded-[8px] border border-red-300/25 bg-red-400/10 px-4 py-3 text-sm leading-6 text-red-200" role="alert">
                  {error}
                </div>
              ) : null}

              <button
                className={`${portfolioClasses.primaryButton} inline-flex w-full items-center justify-between py-4`}
                type="submit"
                disabled={isSubmitting}
              >
                <span>{isSubmitting ? "Signing in..." : "Sign in"}</span>
                <span aria-hidden="true">-&gt;</span>
              </button>
            </div>
          </form>
        </div>
      </section>
    </main>
  );
}
