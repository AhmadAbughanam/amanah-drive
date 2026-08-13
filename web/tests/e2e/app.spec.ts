import { expect, test } from "@playwright/test";

const apiBaseUrl = "http://localhost:8080";

test("portfolio CTA navigates to login", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Ahmad Maher Abughanam" })).toBeVisible();
  await page.getByRole("link", { name: "Amanah Drive" }).click();

  await expect(page).toHaveURL("/login");
  await expect(page.getByRole("heading", { name: "Amanah Drive" })).toBeVisible();
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
      answer: "Amanah Drive uses retrieved chunks to ground answers.",
      citations: [
        {
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
});

test("chat error state renders cleanly", async ({ page }) => {
  await mockApi(page, { loginSucceeds: true, chatStatus: 502 });

  await signIn(page);
  await page.getByRole("button", { name: "Search & Chat" }).click();
  await page.getByLabel("Ask a question").fill("Will this fail?");
  await page.getByRole("button", { name: "Send" }).click();

  await expect(page.getByText("Request failed with status 502.")).toBeVisible();
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
        chunkId: string;
        fileId: string | null;
        fileName: string;
        snippet: string;
      }>;
    };
    chatStatus?: number;
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

  await page.route(`${apiBaseUrl}/search**`, async (route) => {
    await route.fulfill({
      status: 200,
      json: {
        results: options.searchResults ?? [],
      },
    });
  });

  await page.route(`${apiBaseUrl}/chat`, async (route) => {
    if (options.chatStatus && options.chatStatus >= 400) {
      await route.fulfill({ status: options.chatStatus });
      return;
    }

    await route.fulfill({
      status: 200,
      json:
        options.chatResponse ??
        {
          conversationId: "44444444-4444-4444-4444-444444444444",
          answer: "Default mocked answer.",
          citations: [],
        },
    });
  });
}
