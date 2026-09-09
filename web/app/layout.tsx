import type { Metadata } from "next";
import { Inter } from "next/font/google";
import { AuthProvider } from "./auth-provider";
import "./globals.css";

// Variable-weight Inter drives the whole app's typographic hierarchy (weight and tracking
// instead of color), matching the design direction of Linear/Vercel-style SaaS UI.
const inter = Inter({
  subsets: ["latin"],
  variable: "--font-inter",
  display: "swap",
});

export const metadata: Metadata = {
  title: "Ahmad Abughanam | Amanah Drive",
  description: "Portfolio and Amanah Drive file management shell",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className={inter.variable}>
      <body className="bg-[#0a0a0c] font-sans text-white antialiased">
        <AuthProvider>{children}</AuthProvider>
      </body>
    </html>
  );
}
