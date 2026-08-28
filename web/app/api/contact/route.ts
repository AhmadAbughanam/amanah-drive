import { type NextRequest, NextResponse } from "next/server";

export const runtime = "nodejs";

const CONTACT_RECIPIENT = "abughannam98@gmail.com";
const RATE_LIMIT_WINDOW_MS = 15 * 60 * 1000;
const RATE_LIMIT_MAX_REQUESTS = 5;
const MAX_REQUEST_BYTES = 20_000;
const RESEND_TIMEOUT_MS = 10_000;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

type RateLimitEntry = {
  count: number;
  resetAt: number;
};

const rateLimits = new Map<string, RateLimitEntry>();

function getClientIp(request: NextRequest) {
  const forwardedFor = request.headers.get("x-forwarded-for");
  return forwardedFor?.split(",")[0]?.trim() || request.headers.get("x-real-ip")?.trim() || "unknown";
}

function consumeRateLimit(ip: string, now = Date.now()) {
  for (const [key, entry] of rateLimits) {
    if (entry.resetAt <= now) {
      rateLimits.delete(key);
    }
  }

  const current = rateLimits.get(ip);
  if (!current) {
    rateLimits.set(ip, { count: 1, resetAt: now + RATE_LIMIT_WINDOW_MS });
    return { allowed: true, retryAfterSeconds: 0 };
  }

  if (current.count >= RATE_LIMIT_MAX_REQUESTS) {
    return {
      allowed: false,
      retryAfterSeconds: Math.max(1, Math.ceil((current.resetAt - now) / 1000)),
    };
  }

  current.count += 1;
  return { allowed: true, retryAfterSeconds: 0 };
}

function readString(value: unknown) {
  return typeof value === "string" ? value.trim() : "";
}

export async function POST(request: NextRequest) {
  const contentLength = Number(request.headers.get("content-length") ?? 0);
  if (Number.isFinite(contentLength) && contentLength > MAX_REQUEST_BYTES) {
    return NextResponse.json({ error: "Request is too large." }, { status: 413 });
  }

  const rateLimit = consumeRateLimit(getClientIp(request));
  if (!rateLimit.allowed) {
    return NextResponse.json(
      { error: "Too many messages. Please try again later." },
      { status: 429, headers: { "Retry-After": String(rateLimit.retryAfterSeconds) } },
    );
  }

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ error: "Invalid request body." }, { status: 400 });
  }

  if (!body || typeof body !== "object") {
    return NextResponse.json({ error: "Invalid request body." }, { status: 400 });
  }

  const fields = body as Record<string, unknown>;
  const website = readString(fields.website);
  if (website) {
    return NextResponse.json({ message: "Message sent successfully." });
  }

  const name = readString(fields.name);
  const email = readString(fields.email);
  const message = readString(fields.message);

  if (!name || name.length > 100) {
    return NextResponse.json({ error: "Enter a valid name." }, { status: 400 });
  }

  if (!EMAIL_PATTERN.test(email) || email.length > 254) {
    return NextResponse.json({ error: "Enter a valid email address." }, { status: 400 });
  }

  if (!message || message.length > 5000) {
    return NextResponse.json({ error: "Enter a message between 1 and 5000 characters." }, { status: 400 });
  }

  const apiKey = process.env.CONTACT_EMAIL_API_KEY?.trim();
  const sender = process.env.CONTACT_EMAIL_FROM?.trim();
  if (!apiKey || !sender) {
    console.error("Contact email delivery is not configured.");
    return NextResponse.json({ error: "Message delivery is temporarily unavailable." }, { status: 503 });
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), RESEND_TIMEOUT_MS);

  try {
    const response = await fetch("https://api.resend.com/emails", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${apiKey}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        from: sender,
        to: [CONTACT_RECIPIENT],
        reply_to: email,
        subject: `Portfolio message from ${name.replace(/[\r\n]+/g, " ")}`,
        text: `Name: ${name}\nEmail: ${email}\n\n${message}`,
      }),
      cache: "no-store",
      signal: controller.signal,
    });

    if (!response.ok) {
      console.error("Contact email delivery failed.", { status: response.status });
      return NextResponse.json({ error: "Your message could not be sent. Please try again." }, { status: 502 });
    }

    return NextResponse.json({ message: "Message sent successfully." });
  } catch (error) {
    console.error("Contact email delivery failed.", {
      reason: error instanceof Error && error.name === "AbortError" ? "timeout" : "network_error",
    });
    return NextResponse.json({ error: "Your message could not be sent. Please try again." }, { status: 502 });
  } finally {
    clearTimeout(timeout);
  }
}
