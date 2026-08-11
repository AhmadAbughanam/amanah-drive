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

async function mockApi(page: import("@playwright/test").Page, options: { loginSucceeds: boolean; refreshSucceeds?: boolean }) {
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
}
