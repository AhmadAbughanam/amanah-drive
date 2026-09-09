import type { ReactNode } from "react";

// Design direction: near-monochrome surfaces + one restrained accent, typography-driven
// hierarchy, border-first components. Modeled on the pattern shared by Linear, Vercel, and
// Stripe's dashboards rather than a rotating multi-color gradient system — see docs cited in
// the redesign that introduced this file's current values.
export const portfolioPalette = {
  page: "#0a0a0c",
  surface: "#111114",
  raised: "#0d0d10",
  inset: "#050506",
  border: "rgba(255,255,255,0.10)",
  accent: "#6e56cf",
  accentText: "#c9bdfb",
  success: "#3fb950",
  warning: "#e3b341",
  danger: "#f85149",
} as const;

export const portfolioClasses = {
  label: "text-[11px] font-medium uppercase tracking-[0.16em] text-white/44",
  gradientText: "text-[#c9bdfb]",
  panel: "min-w-0 rounded-lg border border-white/10 bg-[#111114]",
  insetPanel: "min-w-0 rounded-lg border border-white/[0.08] bg-[#0d0d10]",
  field:
    "w-full rounded-lg border border-white/12 bg-white/[0.02] px-4 py-3 text-sm text-white outline-none placeholder:text-white/32 transition focus:border-[#6e56cf]/70 focus:bg-white/[0.03] focus-visible:ring-2 focus-visible:ring-[#6e56cf]/25",
  primaryButton:
    "rounded-lg bg-[#6e56cf] px-5 py-3 text-xs font-semibold uppercase tracking-[0.14em] text-white transition hover:bg-[#7d64e0] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#6e56cf] focus-visible:ring-offset-2 focus-visible:ring-offset-[#0a0a0c] disabled:cursor-not-allowed disabled:opacity-40",
  secondaryButton:
    "whitespace-nowrap rounded-lg border border-white/14 bg-transparent px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.12em] text-white/72 transition hover:border-white/28 hover:bg-white/[0.04] hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#6e56cf]/60 disabled:cursor-not-allowed disabled:opacity-35",
  iconButton:
    "grid h-11 w-11 place-items-center rounded-lg border border-white/12 bg-transparent text-white/72 transition hover:border-white/24 hover:bg-white/[0.04] hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#6e56cf]/60 disabled:cursor-not-allowed disabled:opacity-35",
  badge: "inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-medium",
} as const;

export const statusTone = {
  neutral: "border-white/14 bg-white/[0.03] text-white/62",
  accent: "border-[#6e56cf]/35 bg-[#6e56cf]/[0.10] text-[#c9bdfb]",
  success: "border-[#3fb950]/30 bg-[#3fb950]/[0.10] text-[#7ee787]",
  warning: "border-[#e3b341]/30 bg-[#e3b341]/[0.10] text-[#f0c674]",
  danger: "border-[#f85149]/30 bg-[#f85149]/[0.10] text-[#ff9891]",
} as const;

export function SectionLabel({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <p className={`text-[11px] font-medium uppercase tracking-[0.16em] text-white/44 ${className}`}>{children}</p>;
}

export function Badge({ tone = "neutral", children }: { tone?: keyof typeof statusTone; children: ReactNode }) {
  return <span className={`${portfolioClasses.badge} ${statusTone[tone]}`}>{children}</span>;
}

export function Divider({ className = "" }: { className?: string }) {
  return <div className={`h-px bg-white/[0.08] ${className}`} aria-hidden="true" />;
}
