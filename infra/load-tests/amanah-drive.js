import http from "k6/http";
import { check, sleep } from "k6";
import { Counter, Rate, Trend } from "k6/metrics";

const baseUrl = (__ENV.BASE_URL || "http://api:8080").replace(/\/$/, "");
const query = __ENV.SEARCH_QUERY || "Amanah Drive";

const requestErrors = new Rate("request_error_rate");
const rateLimited = new Counter("rate_limited_requests");
const loginDuration = new Trend("login_duration", true);
const folderDuration = new Trend("folders_success_duration", true);
const searchDuration = new Trend("search_success_duration", true);
const chatDuration = new Trend("chat_success_duration", true);
const logsDuration = new Trend("logs_success_duration", true);

export const options = {
  scenarios: {
    user_sessions: {
      executor: "constant-vus",
      vus: Number(__ENV.VUS || 1),
      duration: __ENV.DURATION || "30s",
      gracefulStop: "30s",
    },
  },
  summaryTrendStats: ["count", "avg", "med", "p(90)", "p(95)", "p(99)", "max"],
};

function jsonHeaders(token) {
  return {
    Authorization: `Bearer ${token}`,
    "Content-Type": "application/json",
  };
}

function record(response, metric, endpoint) {
  const successful = response.status >= 200 && response.status < 300;
  if (successful) {
    metric.add(response.timings.duration, { endpoint });
  }

  if (response.status === 429) {
    rateLimited.add(1, { endpoint });
  }

  requestErrors.add(!successful, { endpoint });
  check(response, {
    [`${endpoint} returned 2xx`]: () => successful,
  });
}

export function setup() {
  let accessToken = __ENV.ACCESS_TOKEN || "";
  const email = __ENV.LOGIN_EMAIL || "load-test@example.invalid";

  if (__ENV.LOGIN_PASSWORD) {
    const response = http.post(
      `${baseUrl}/auth/login`,
      JSON.stringify({ email, password: __ENV.LOGIN_PASSWORD }),
      { headers: { "Content-Type": "application/json" }, tags: { endpoint: "login" } },
    );

    loginDuration.add(response.timings.duration, { endpoint: "login" });
    check(response, { "login returned 200": (result) => result.status === 200 });
    if (response.status !== 200) {
      throw new Error(`Login failed with status ${response.status}`);
    }

    accessToken = response.json("accessToken");
  } else {
    const response = http.post(
      `${baseUrl}/auth/login`,
      JSON.stringify({ email, password: "load-test-invalid-password" }),
      { headers: { "Content-Type": "application/json" }, tags: { endpoint: "login" } },
    );

    loginDuration.add(response.timings.duration, { endpoint: "login" });
    check(response, { "invalid login was rejected": (result) => result.status === 401 });
  }

  if (!accessToken) {
    throw new Error("Set ACCESS_TOKEN, or set LOGIN_EMAIL and LOGIN_PASSWORD for a real login.");
  }

  return { accessToken };
}

export default function (data) {
  const headers = jsonHeaders(data.accessToken);
  const selection = Math.random();

  if (selection < 0.45) {
    const response = http.get(`${baseUrl}/drive/folders?page=1&pageSize=50`, {
      headers,
      tags: { endpoint: "folders" },
    });
    record(response, folderDuration, "folders");
  } else if (selection < 0.70) {
    const response = http.get(`${baseUrl}/admin/logs?page=1&pageSize=50`, {
      headers,
      tags: { endpoint: "logs" },
    });
    record(response, logsDuration, "logs");
  } else if (selection < 0.95) {
    const response = http.get(`${baseUrl}/search?query=${encodeURIComponent(query)}&topK=5`, {
      headers,
      tags: { endpoint: "search" },
    });
    record(response, searchDuration, "search");
  } else {
    const response = http.post(
      `${baseUrl}/chat`,
      JSON.stringify({ question: query, conversationId: null }),
      { headers, tags: { endpoint: "chat" }, timeout: "90s" },
    );
    record(response, chatDuration, "chat");
  }

  sleep(0.5 + Math.random());
}
