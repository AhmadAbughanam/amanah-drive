import { expect, test } from "@playwright/test";

const apiBaseUrl = "http://localhost:8080";

test("quiet portfolio admin access navigates to login", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Ahmad Maher Abughanam" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Amanah Drive" })).toBeVisible();
  await expect(page.locator('a[href="/login"]')).toHaveCount(1);
  await page.getByRole("link", { name: "Admin access" }).click();

  await expect(page).toHaveURL("/login");
  await expect(page.getByRole("heading", { name: "Amanah Drive" })).toBeVisible();
});

test("portfolio contact form sends a message", async ({ page }) => {
  await page.route("**/api/contact", async (route) => {
    expect(route.request().postDataJSON()).toMatchObject({
      name: "Ahmad",
      email: "ahmad@example.com",
      message: "Project enquiry",
      website: "",
    });
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ message: "Message sent successfully." }),
    });
  });

  await page.goto("/");
  await page.getByLabel("Name").fill("Ahmad");
  await page.getByLabel("Email").fill("ahmad@example.com");
  await page.getByLabel("Message").fill("Project enquiry");
  await page.getByRole("button", { name: "Send message" }).click();

  await expect(page.getByRole("status")).toHaveText("Message sent successfully.");
});

test("contact route validates requests and rate limits by IP", async ({ request }) => {
  const ip = "203.0.113.42";

  for (let attempt = 0; attempt < 5; attempt += 1) {
    const response = await request.post("/api/contact", {
      headers: { "x-forwarded-for": ip },
      data: { name: "", email: "invalid", message: "", website: "" },
    });
    expect(response.status()).toBe(400);
  }

  const limited = await request.post("/api/contact", {
    headers: { "x-forwarded-for": ip },
    data: { name: "Ahmad", email: "ahmad@example.com", message: "Hello", website: "" },
  });

  expect(limited.status()).toBe(429);
  expect(limited.headers()["retry-after"]).toBeTruthy();
});

test("valid login reaches drive", async ({ page }) => {
  await mockApi(page, { loginSucceeds: true });

  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@example.com");
  await page.getByLabel("Password").fill("correct horse battery staple");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page).toHaveURL("/drive");
  await expect(page.getByRole("heading", { name: "File management" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Documents" })).toBeVisible();
});

test("authenticated admin log viewer renders persisted entries", async ({ page }) => {
  await mockApi(page, {
    loginSucceeds: true,
    adminLogs: {
      page: 1,
      pageSize: 25,
      hasMore: false,
      entries: [
        {
          timestamp: "2026-08-16T10:01:00Z",
          level: "Warning",
          message: "Document processing job {JobId} failed",
          exception: null,
          properties: { JobId: "job-123" },
        },
      ],
    },
  });

  await signIn(page);
  await page.getByRole("button", { name: "Logs" }).click();

  await expect(page.getByRole("heading", { name: "System signals" })).toBeVisible();
  await expect(page.getByText("Document processing job {JobId} failed")).toBeVisible();
  await page.getByText("Document processing job {JobId} failed").click();
  await expect(page.getByText("job-123")).toBeVisible();
});

test("authenticated activity view renders domain entries", async ({ page }) => {
  await mockApi(page, {
    loginSucceeds: true,
    adminActivity: {
      page: 1,
      pageSize: 25,
      hasMore: false,
      entries: [
        {
          id: "55555555-5555-5555-5555-555555555555",
          type: "ProcessingCompleted",
          summary: "Finished processing report.pdf",
          occurredAt: "2026-08-17T10:01:00Z",
          fileId: "22222222-2222-2222-2222-222222222222",
          conversationId: null,
        },
      ],
    },
  });

  await signIn(page);
  await page.getByRole("button", { name: "Logs" }).click();
  await page.getByRole("tab", { name: "Activity" }).click();

  await expect(page.getByText("Finished processing report.pdf")).toBeVisible();
  await expect(page.getByText("Processed", { exact: true })).toBeVisible();
});

test("search results render in the authenticated app", async ({ page }) => {
  await mockApi(page, {
    loginSucceeds: true,
    searchResults: [
      {
        chunkId: "33333333-3333-3333-3333-333333333333",
        fileId: "22222222-2222-2222-2222-222222222222",
        fileName: "notes.txt",
        chunkIndex: 1,
        snippet: "Amanah Drive stores processed document chunks for semantic search.",
        score: 0.87,
      },
    ],
  });

  await signIn(page);
  await page.getByRole("button", { name: "Search & Chat" }).click();
  await page.getByLabel("Search documents").fill("semantic search");
  await page.getByRole("button", { name: "Search", exact: true }).click();

  await expect(page.getByText("notes.txt")).toBeVisible();
  await expect(page.getByText("Amanah Drive stores processed document chunks for semantic search.")).toBeVisible();
  await expect(page.getByText("Chunk 1 / Score 0.870")).toBeVisible();
});

test("empty search state renders clearly", async ({ page }) => {
  await mockApi(page, { loginSucceeds: true, searchResults: [] });

  await signIn(page);
  await page.getByRole("button", { name: "Search & Chat" }).click();
  await page.getByLabel("Search documents").fill("nothing matches");
  await page.getByRole("button", { name: "Search", exact: true }).click();

  await expect(page.getByText("No matching document sections found.")).toBeVisible();
});

test("chat answer with citation renders", async ({ page }) => {
  await mockApi(page, {
    loginSucceeds: true,
    chatResponse: {
      conversationId: "44444444-4444-4444-4444-444444444444",
      answer: "Amanah Drive uses retrieved chunks to ground answers.[1] It can cite the same source twice.[1] Inline code `[1]` stays literal.",
      citations: [
        {
          reference: 1,
          chunkId: "33333333-3333-3333-3333-333333333333",
          fileId: "22222222-2222-2222-2222-222222222222",
          fileName: "notes.txt",
          snippet: "Retrieved chunks are passed to the AI service with the user question.",
        },
      ],
    },
  });

  await signIn(page);
  await page.getByRole("button", { name: "Search & Chat" }).click();
  await page.getByLabel("Ask a question").fill("How are answers grounded?");
  await page.getByRole("button", { name: "Send" }).click();

  await expect(page.getByText("How are answers grounded?")).toBeVisible();
  await expect(page.getByText("Amanah Drive uses retrieved chunks to ground answers.")).toBeVisible();
  await expect(page.getByText("Retrieved chunks are passed to the AI service with the user question.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Open citation 1" })).toHaveCount(2);
  await expect(page.locator("code").getByText("[1]")).toBeVisible();

  await page.getByRole("button", { name: "Open citation 1" }).first().click();

  await expect(page.locator('[data-citation-active="true"]')).toContainText("notes.txt");
  const dialog = page.getByRole("dialog", { name: "notes.txt" });
  await expect(dialog).toContainText("Retrieved chunks are passed to the AI service with the user question.");
  await expect(dialog.getByRole("button", { name: "Download source" })).toBeVisible();
});

test("chat error state renders cleanly", async ({ page }) => {
  await mockApi(page, { loginSucceeds: true, chatStatus: 502 });

  await signIn(page);
  await page.getByRole("button", { name: "Search & Chat" }).click();
  await page.getByLabel("Ask a question").fill("Will this fail?");
  await page.getByRole("button", { name: "Send" }).click();

  await expect(page.getByText("Request failed with status 502.")).toBeVisible();
});

test("agent run shows its transcript, approval, and completed markdown answer", async ({ page }) => {
  const pendingRun = agentRun({
    status: "AwaitingApproval",
    pendingToolName: "rename_folder",
    pendingActionSummary: "Rename a folder to “Invoices 2026”",
    steps: [
      agentStep(1, "user", { content: "Rename my invoices folder" }),
      agentStep(2, "assistant"),
      agentStep(3, "tool", { toolName: "rename_folder", argumentsSummary: "Rename a folder to “Invoices 2026”", resultSummary: "Waiting for your approval.", status: "PendingApproval", requiresApproval: true }),
    ],
  });
  const completedRun = agentRun({
    status: "Completed",
    finalAnswer: "**Done.** The folder is ready.",
    steps: [
      ...pendingRun.steps.slice(0, 2),
      agentStep(3, "tool", { toolName: "rename_folder", argumentsSummary: "Rename a folder to “Invoices 2026”", resultSummary: "Completed.", status: "Executed", requiresApproval: true }),
      agentStep(4, "assistant", { content: "The rename completed." }),
    ],
  });
  await mockApi(page, { loginSucceeds: true, agentStartResponse: pendingRun, agentApproveResponse: completedRun });

  await signIn(page);
  await page.getByRole("button", { name: "Agent" }).click();
  await page.getByLabel("Instruction").fill("Rename my invoices folder");
  await page.getByRole("button", { name: "Run agent" }).click();

  await expect(page.getByText("Rename a folder to “Invoices 2026”?" )).toBeVisible();
  await expect(page.getByText("Tool · rename folder")).toBeVisible();
  await page.getByRole("button", { name: "Approve" }).click();

  await expect(page.getByText("Completed", { exact: true })).toBeVisible();
  await expect(page.getByText("Done.", { exact: true })).toBeVisible();
  await expect(page.getByText("The folder is ready.")).toBeVisible();
});

test("agent rejection resumes the run and shows the returned answer", async ({ page }) => {
  const pendingRun = agentRun({
    status: "AwaitingApproval",
    pendingToolName: "move_file",
    pendingActionSummary: "Move a file to a folder",
    steps: [
      agentStep(1, "user", { content: "Move the report" }),
      agentStep(2, "tool", { toolName: "move_file", argumentsSummary: "Move a file to a folder", resultSummary: "Waiting for your approval.", status: "PendingApproval", requiresApproval: true }),
    ],
  });
  const completedRun = agentRun({
    status: "Completed",
    finalAnswer: "I left the file where it was.",
    steps: [
      agentStep(1, "user", { content: "Move the report" }),
      agentStep(2, "tool", { toolName: "move_file", argumentsSummary: "Move a file to a folder", resultSummary: "You rejected this action.", status: "Rejected", requiresApproval: true }),
    ],
  });
  await mockApi(page, { loginSucceeds: true, agentStartResponse: pendingRun, agentRejectResponse: completedRun });

  await signIn(page);
  await page.getByRole("button", { name: "Agent" }).click();
  await page.getByLabel("Instruction").fill("Move the report");
  await page.getByRole("button", { name: "Run agent" }).click();
  await page.getByRole("button", { name: "Reject" }).click();

  await expect(page.getByText("You rejected this action.")).toBeVisible();
  await expect(page.getByText("I left the file where it was.")).toBeVisible();
});

test("invalid login shows an error and stays on login", async ({ page }) => {
  await mockApi(page, { loginSucceeds: false });

  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@example.com");
  await page.getByLabel("Password").fill("wrong password");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page).toHaveURL("/login");
  await expect(page.getByText("Invalid email or password.")).toBeVisible();
});

test("unauthenticated drive visit redirects to login", async ({ page }) => {
  await mockApi(page, { loginSucceeds: false, refreshSucceeds: false });

  await page.goto("/drive");

  await expect(page).toHaveURL("/login");
});

async function signIn(page: import("@playwright/test").Page) {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@example.com");
  await page.getByLabel("Password").fill("correct horse battery staple");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/drive");
}

async function mockApi(
  page: import("@playwright/test").Page,
  options: {
    loginSucceeds: boolean;
    refreshSucceeds?: boolean;
    searchResults?: Array<{
      chunkId: string;
      fileId: string;
      fileName: string;
      chunkIndex: number;
      snippet: string;
      score: number;
    }>;
    chatResponse?: {
      conversationId: string;
      answer: string;
      citations: Array<{
        reference: number;
        chunkId: string;
        fileId: string | null;
        fileName: string;
        snippet: string;
      }>;
    };
    chatStatus?: number;
    adminLogs?: {
      page: number;
      pageSize: number;
      hasMore: boolean;
      entries: Array<{
        timestamp: string;
        level: string;
        message: string;
        exception: string | null;
        properties: Record<string, unknown>;
      }>;
    };
    adminActivity?: {
      page: number;
      pageSize: number;
      hasMore: boolean;
      entries: Array<{
        id: string;
        type: string;
        summary: string;
        occurredAt: string;
        fileId: string | null;
        conversationId: string | null;
      }>;
    };
    observability?: Record<string, unknown>;
    agentStartResponse?: Record<string, unknown>;
    agentApproveResponse?: Record<string, unknown>;
    agentRejectResponse?: Record<string, unknown>;
  },
) {
  await page.route(`${apiBaseUrl}/auth/login`, async (route) => {
    if (!options.loginSucceeds) {
      await route.fulfill({ status: 401, json: { message: "Unauthorized" } });
      return;
    }

    await route.fulfill({ status: 200, json: { accessToken: "test-access-token" } });
  });

  await page.route(`${apiBaseUrl}/auth/refresh`, async (route) => {
    if (options.refreshSucceeds) {
      await route.fulfill({ status: 200, json: { accessToken: "refreshed-token" } });
      return;
    }

    await route.fulfill({ status: 401, json: { message: "Unauthorized" } });
  });

  await page.route(`${apiBaseUrl}/auth/logout`, async (route) => {
    await route.fulfill({ status: 204 });
  });

  await page.route(`${apiBaseUrl}/drive/folders**`, async (route) => {
    await route.fulfill({
      status: 200,
      json: {
        parentFolderId: null,
        page: 1,
        pageSize: 10,
        folders: [
          {
            id: "11111111-1111-1111-1111-111111111111",
            name: "Documents",
            parentFolderId: null,
            createdAt: "2026-08-11T00:00:00Z",
            updatedAt: "2026-08-11T00:00:00Z",
          },
        ],
        files: [
          {
            id: "22222222-2222-2222-2222-222222222222",
            folderId: null,
            originalFileName: "notes.txt",
            contentType: "text/plain",
            sizeBytes: 42,
            checksumSha256: "abc",
            processingJobId: null,
            createdAt: "2026-08-11T00:00:00Z",
            updatedAt: "2026-08-11T00:00:00Z",
          },
        ],
      },
    });
  });

  await page.route(`${apiBaseUrl}/admin/logs**`, async (route) => {
    await route.fulfill({
      status: 200,
      json: options.adminLogs ?? {
        page: 1,
        pageSize: 25,
        hasMore: false,
        entries: [],
      },
    });
  });

  await page.route(`${apiBaseUrl}/admin/activity**`, async (route) => {
    await route.fulfill({
      status: 200,
      json: options.adminActivity ?? {
        page: 1,
        pageSize: 25,
        hasMore: false,
        entries: [],
      },
    });
  });

  await page.route(`${apiBaseUrl}/admin/observability**`, async (route) => {
    await route.fulfill({
      status: 200,
      json: options.observability ?? {
        range: "24h",
        from: "2026-08-28T12:00:00Z",
        to: "2026-08-29T12:00:00Z",
        stats: {
          requestsToday: 12,
          errorRatePercent: 1.5,
          averageLatencyMilliseconds: 42.3,
          aiSpendThisMonthUsd: 0.0042,
          aiPricingComplete: true,
        },
        requests: [{ timestamp: "2026-08-29T11:00:00Z", requests: 12, errors: 1, errorRatePercent: 8.33 }],
        logLevels: [{ level: "Information", count: 10 }, { level: "Warning", count: 2 }, { level: "Error", count: 1 }],
        aiUsage: [{ timestamp: "2026-08-29T11:00:00Z", inputTokens: 120, outputTokens: 30, estimatedCostUsd: 0.0042, operations: 1, failures: 0, unpricedOperations: 0 }],
        security: [{ timestamp: "2026-08-29T11:00:00Z", events: 1 }],
        recentSecurityEvents: [{ timestamp: "2026-08-29T11:00:00Z", event: "LoginFailed", message: "Admin login failed", source: "AuthService" }],
        topErrors: [{ signature: "Processing failed", message: "Processing failed", exceptionType: null, level: "Error", count: 1, lastSeen: "2026-08-29T11:00:00Z" }],
      },
    });
  });

  await page.route(`${apiBaseUrl}/search**`, async (route) => {
    await route.fulfill({
      status: 200,
      json: {
        results: options.searchResults ?? [],
      },
    });
  });

  let lastChatQuestion = "";
  const chatResponse = options.chatResponse ?? {
    conversationId: "44444444-4444-4444-4444-444444444444",
    answer: "Default mocked answer.",
    citations: [],
  };

  await page.route(`${apiBaseUrl}/chat`, async (route) => {
    if (options.chatStatus && options.chatStatus >= 400) {
      await route.fulfill({ status: options.chatStatus });
      return;
    }

    lastChatQuestion = String(route.request().postDataJSON()?.question ?? "");

    await route.fulfill({
      status: 200,
      json: chatResponse,
    });
  });

  await page.route(`${apiBaseUrl}/chat/**`, async (route) => {
    await route.fulfill({
      status: 200,
      json: {
        conversationId: chatResponse.conversationId,
        createdAt: "2026-08-31T00:00:00Z",
        updatedAt: "2026-08-31T00:00:00Z",
        page: 1,
        pageSize: 20,
        messages: [
          { id: "history-user", role: "user", content: lastChatQuestion, citations: [], createdAt: "2026-08-31T00:00:00Z" },
          { id: "history-assistant", role: "assistant", content: chatResponse.answer, citations: chatResponse.citations, createdAt: "2026-08-31T00:00:01Z" },
        ],
      },
    });
  });

  await page.route(`${apiBaseUrl}/agent/runs`, async (route) => {
    await route.fulfill({ status: 201, json: options.agentStartResponse ?? agentRun({ status: "Completed", finalAnswer: "Default agent answer." }) });
  });

  await page.route(`${apiBaseUrl}/agent/runs/**`, async (route) => {
    const action = route.request().url().endsWith("/approve") ? options.agentApproveResponse : options.agentRejectResponse;
    await route.fulfill({ status: 200, json: action ?? agentRun({ status: "Completed", finalAnswer: "Default agent answer." }) });
  });
}

function agentRun(overrides: Record<string, unknown>) {
  return {
    id: "66666666-6666-6666-6666-666666666666",
    status: "Completed",
    finalAnswer: null,
    failureReason: null,
    pendingToolName: null,
    pendingActionSummary: null,
    steps: [],
    createdAt: "2026-08-31T00:00:00Z",
    updatedAt: "2026-08-31T00:00:00Z",
    ...overrides,
  };
}

function agentStep(sequence: number, role: string, overrides: Record<string, unknown> = {}) {
  return {
    sequence,
    role,
    content: null,
    toolName: null,
    argumentsSummary: null,
    resultSummary: null,
    status: null,
    requiresApproval: false,
    createdAt: "2026-08-31T00:00:00Z",
    ...overrides,
  };
}
