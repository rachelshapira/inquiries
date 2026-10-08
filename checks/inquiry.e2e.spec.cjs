const { test } = require("playwright/test");
const fs = require("node:fs/promises");
const path = require("node:path");
test("required fields and uploads → submission → employee response → external final status", async ({
  page,
}, testInfo) => {
  const { runAttachmentInquiryE2E } = await import("./inquiry-flow.mjs");
  const result = await runAttachmentInquiryE2E(
    page,
    process.env.INQUIRY_E2E_URL || "http://127.0.0.1:5080",
    { invoice: path.resolve(__dirname, 'fixtures/demo-invoice.pdf'),
      service: path.resolve(__dirname, 'fixtures/demo-service.pdf') },
  );
  await fs.writeFile(
    path.resolve(__dirname, "../docs/inquiry-attachments-proof.json"),
    JSON.stringify(result, null, 2),
  );
  const screenshot = path.resolve(
    __dirname,
    "../docs/inquiry-e2e-external.png",
  );
  await page.screenshot({ path: screenshot, fullPage: true });
  await testInfo.attach("External final inquiry and response", {
    path: screenshot,
    contentType: "image/png",
  });
});
