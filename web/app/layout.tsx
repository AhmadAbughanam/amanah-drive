import type { Metadata } from "next";
import { AuthProvider } from "./auth-provider";
import "./globals.css";

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
    <html lang="en">
      <body>
        <AuthProvider>{children}</AuthProvider>
      </body>
    </html>
  );
}
