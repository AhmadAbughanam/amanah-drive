import type { ReactNode } from "react";

export const portfolioPalette = {
  page: "#060608",
  band: "#0b0b10",
  raised: "#0d0c13",
  inset: "#020203",
  purple: "#c084fc",
  pink: "#f472b6",
  blue: "#60a5fa",
} as const;

export const portfolioClasses = {
  label: "text-[11px] font-semibold uppercase tracking-[0.2em] text-white/48",
  gradientText: "bg-gradient-to-r from-[#c084fc] via-[#f472b6] to-[#60a5fa] bg-clip-text text-transparent",
  panel: "min-w-0 rounded-[8px] border border-white/10 bg-white/[0.035]",
  insetPanel: "min-w-0 rounded-[8px] border border-white/[0.08] bg-[#0d0c13]",
  field:
    "w-full rounded-[8px] border border-white/12 bg-white/[0.045] px-4 py-3 text-sm text-white outline-none placeholder:text-white/32 transition focus:border-[#c084fc]/70 focus:bg-white/[0.06] focus-visible:ring-2 focus-visible:ring-[#c084fc]/20",
  primaryButton:
    "rounded-[8px] bg-gradient-to-r from-[#c084fc] via-[#f472b6] to-[#60a5fa] px-5 py-3 text-xs font-semibold uppercase tracking-[0.16em] text-[#060608] transition hover:brightness-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#c084fc] focus-visible:ring-offset-2 focus-visible:ring-offset-[#060608] disabled:cursor-not-allowed disabled:opacity-40",
  secondaryButton:
    "whitespace-nowrap rounded-[8px] border border-white/14 bg-white/[0.035] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.14em] text-white/72 transition hover:border-[#60a5fa]/55 hover:bg-[#60a5fa]/10 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#60a5fa]/70 disabled:cursor-not-allowed disabled:opacity-35",
  iconButton:
    "grid h-11 w-11 place-items-center rounded-[8px] border border-white/12 bg-white/[0.035] text-white/72 transition hover:border-[#c084fc]/55 hover:bg-[#c084fc]/10 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#c084fc]/70 disabled:cursor-not-allowed disabled:opacity-35",
} as const;

export function SectionLabel({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <p className={`text-xs font-semibold uppercase tracking-[0.2em] text-white/48 ${className}`}>{children}</p>;
}

export function Scribble({ className = "" }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 240 90" fill="none" aria-hidden="true">
      <path
        d="M4 46C27 6 44 80 66 38C85 3 104 80 126 39C144 6 167 73 188 37C204 10 220 22 236 50"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
    </svg>
  );
}
