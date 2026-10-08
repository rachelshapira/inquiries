module.exports = {
  testDir: "./checks",
  testMatch: "inquiry.e2e.spec.cjs",
  timeout: 120_000,
  workers: 1,
  retries: 0,
  outputDir: "./docs/e2e-artifacts",
  reporter: [
    ["list"],
    ["json", { outputFile: "./docs/inquiry-e2e-result.json" }],
  ],
  use: {
    baseURL: process.env.INQUIRY_E2E_URL || "http://127.0.0.1:5080",
    viewport: { width: 1440, height: 1000 },
    headless: true,
    screenshot: "only-on-failure",
    trace: "retain-on-failure",
    launchOptions: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE
      ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE }
      : {},
  },
};
