"use client";

import { FormEvent, useState } from "react";

type SubmissionState =
  | { status: "idle" }
  | { status: "submitting" }
  | { status: "success"; message: string }
  | { status: "error"; message: string };

export function ContactForm() {
  const [submission, setSubmission] = useState<SubmissionState>({ status: "idle" });

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const formData = new FormData(form);

    setSubmission({ status: "submitting" });

    try {
      const response = await fetch("/api/contact", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: formData.get("name"),
          email: formData.get("email"),
          message: formData.get("message"),
          website: formData.get("website"),
        }),
      });
      const result = (await response.json().catch(() => null)) as { message?: string; error?: string } | null;

      if (!response.ok) {
        throw new Error(result?.error ?? "Your message could not be sent. Please try again.");
      }

      form.reset();
      setSubmission({ status: "success", message: result?.message ?? "Message sent successfully." });
    } catch (error) {
      setSubmission({
        status: "error",
        message: error instanceof Error ? error.message : "Your message could not be sent. Please try again.",
      });
    }
  }

  const isSubmitting = submission.status === "submitting";

  return (
    <form onSubmit={handleSubmit} className="relative space-y-7">
      <p className="font-serif text-3xl text-white sm:text-4xl">Send a message</p>
      <label className="block">
        <span className="text-xs font-semibold uppercase tracking-[0.16em] text-white/48">Name</span>
        <input name="name" required maxLength={100} className="mt-3 w-full border-0 border-b border-white/22 bg-transparent px-0 py-3 text-base text-white outline-none transition placeholder:text-white/24 focus:border-[#c084fc]" />
      </label>
      <label className="block">
        <span className="text-xs font-semibold uppercase tracking-[0.16em] text-white/48">Email</span>
        <input name="email" type="email" required maxLength={254} className="mt-3 w-full border-0 border-b border-white/22 bg-transparent px-0 py-3 text-base text-white outline-none transition placeholder:text-white/24 focus:border-[#f472b6]" />
      </label>
      <label className="block">
        <span className="text-xs font-semibold uppercase tracking-[0.16em] text-white/48">Message</span>
        <textarea name="message" required maxLength={5000} rows={5} className="mt-3 w-full resize-y rounded-[8px] border border-white/18 bg-white/[0.025] p-4 text-base leading-7 text-white outline-none transition placeholder:text-white/24 focus:border-[#60a5fa]" />
      </label>
      <label className="absolute -left-[10000px] top-auto h-px w-px overflow-hidden" aria-hidden="true">
        Website
        <input name="website" type="text" tabIndex={-1} autoComplete="off" />
      </label>
      <button type="submit" disabled={isSubmitting} className="rounded-full bg-white px-6 py-3.5 text-xs font-semibold uppercase tracking-[0.16em] text-black transition hover:bg-[#e9d5ff] disabled:cursor-wait disabled:opacity-60">
        {isSubmitting ? "Sending..." : "Send message"}
      </button>
      {submission.status === "success" && (
        <p role="status" className="text-sm text-[#86efac]">{submission.message}</p>
      )}
      {submission.status === "error" && (
        <p role="alert" className="text-sm text-[#fda4af]">{submission.message}</p>
      )}
    </form>
  );
}
