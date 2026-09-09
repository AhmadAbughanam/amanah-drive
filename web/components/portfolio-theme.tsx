import type { ReactNode } from "react";

// Pure black-and-white system: no brand accent hue at all. Emphasis comes from inverted
// fills (solid white on black) and typography, not color. Status colors (success/warning/
// danger) are kept — they're functional signals in the logs/observability views, not
// decoration, and removing them would make those views genuinely harder to scan quickly.
export const portfolioPalette = {
  page: "#000000",
  surface: "#0a0a0a",
  raised: "#0d0d0d",
  inset: "#000000",
  border: "rgba(255,255,255,0.12)",
  accent: "#ffffff",
  accentText: "#ffffff",
  success: "#3fb950",
  warning: "#e3b341",
  danger: "#f85149",
} as const;

export const portfolioClasses = {
  label: "text-[11px] font-medium uppercase tracking-[0.16em] text-white/44",
  gradientText: "text-white",
  panel: "min-w-0 rounded-lg border border-white/12 bg-[#0a0a0a]",
  insetPanel: "min-w-0 rounded-lg border border-white/[0.10] bg-black",
  field:
    "w-full rounded-lg border border-white/14 bg-white/[0.02] px-4 py-3 text-sm text-white outline-none placeholder:text-white/32 transition focus:border-white/60 focus:bg-white/[0.03] focus-visible:ring-2 focus-visible:ring-white/25",
  primaryButton:
    "rounded-lg bg-white px-5 py-3 text-xs font-semibold uppercase tracking-[0.14em] text-black transition hover:bg-white/85 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-black disabled:cursor-not-allowed disabled:opacity-40",
  secondaryButton:
    "whitespace-nowrap rounded-lg border border-white/20 bg-transparent px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.12em] text-white/72 transition hover:border-white/40 hover:bg-white/[0.05] hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/60 disabled:cursor-not-allowed disabled:opacity-35",
  iconButton:
    "grid h-11 w-11 place-items-center rounded-lg border border-white/14 bg-transparent text-white/72 transition hover:border-white/30 hover:bg-white/[0.05] hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/60 disabled:cursor-not-allowed disabled:opacity-35",
  badge: "inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-medium",
} as const;

export const statusTone = {
  neutral: "border-white/16 bg-white/[0.04] text-white/62",
  accent: "border-white/30 bg-white/[0.08] text-white",
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
  return <div className={`h-px bg-white/[0.10] ${className}`} aria-hidden="true" />;
}
