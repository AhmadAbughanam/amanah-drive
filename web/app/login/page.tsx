"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
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
    <main className="min-h-screen bg-[#080808] px-3 py-7 text-[#0b0b0b] md:px-8 md:py-10">
      <section className="mx-auto flex min-h-[calc(100vh-56px)] max-w-[430px] flex-col overflow-hidden rounded-[48px] border-[9px] border-[#151515] bg-[#f7f7f5] shadow-[0_18px_60px_rgba(0,0,0,0.65)] md:min-h-[calc(100vh-80px)] md:max-w-[1280px] md:rounded-[18px] md:border-0">
        <header className="flex items-center justify-between px-9 pt-10 md:px-24 md:pt-12">
          <Link
            href="/"
            className="font-serif text-[46px] leading-none text-black outline-none transition hover:text-black/60 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-black md:text-[54px]"
            aria-label="Ahmad Abughanam"
          >
            AA
          </Link>
          <Link
            href="/"
            className="inline-flex flex-col gap-2 text-xs font-semibold uppercase text-black outline-none transition hover:text-black/60 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-black"
          >
            <span>Back to portfolio</span>
            <span className="h-px w-full bg-black" aria-hidden="true" />
          </Link>
        </header>

        <div className="grid flex-1 px-9 py-12 md:grid-cols-[1fr_460px] md:items-center md:gap-20 md:px-24 md:py-16">
          <div className="max-w-[590px]">
            <p className="text-sm font-semibold uppercase text-black md:text-base">Admin Access</p>
            <h1 className="mt-7 font-serif text-[42px] font-normal leading-[1.08] text-black md:text-[72px]">
              Amanah
              <br />
              Drive
            </h1>
            <div className="mt-8 h-px w-14 bg-black/80 md:mt-10" />
            <p className="mt-7 max-w-[330px] text-[15px] leading-7 text-black/72 md:mt-10 md:text-base">
              Sign in with the single admin account. Registration is handled separately during bootstrap.
            </p>
          </div>

          <form className="mt-12 border-t border-black/20 pt-8 md:mt-0 md:border-t-0 md:border-l md:pl-12 md:pt-0" onSubmit={onSubmit}>
            <div className="space-y-6">
              <label className="block">
                <span className="text-xs font-semibold uppercase text-black/60">Email</span>
                <input
                  className="mt-3 w-full border-0 border-b border-black/25 bg-transparent px-0 py-3 text-base text-black outline-none transition placeholder:text-black/35 focus:border-black focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-black"
                  type="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  autoComplete="email"
                  required
                />
              </label>
              <label className="block">
                <span className="text-xs font-semibold uppercase text-black/60">Password</span>
                <input
                  className="mt-3 w-full border-0 border-b border-black/25 bg-transparent px-0 py-3 text-base text-black outline-none transition placeholder:text-black/35 focus:border-black focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-black"
                  type="password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  autoComplete="current-password"
                  required
                />
              </label>

              {error ? (
                <div className="border border-black bg-[#080808] px-4 py-3 text-sm leading-6 text-white" role="alert">
                  {error}
                </div>
              ) : null}

              <button
                className="inline-flex w-full items-center justify-between bg-[#080808] px-5 py-4 text-sm font-semibold uppercase text-white outline-none transition hover:bg-black/80 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-black disabled:cursor-not-allowed disabled:bg-black/35 md:w-auto md:min-w-56"
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
