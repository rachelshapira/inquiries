const { test, expect } = require('playwright/test');
const fs = require('node:fs/promises');
const path = require('node:path');

// Prerequisite: checks/business-actions.py prepares the isolated QA process.
for (const mode of ['normal', 'reject', 'interruptOnce']) {
  test(`configured business action: ${mode}`, async ({ page, request }, testInfo) => {
    const base = 'http://127.0.0.1:5081';
    const headers = { 'X-Workflow-Client': 'portal' };
    expect((await request.post(base + '/api/demo/login/admin', { headers, data: {} })).ok()).toBeTruthy();
    const session = await (await request.get(base + '/api/session')).json();
    const providerId = session.accounts.find(account => account.id === 'provider').providerId;
    const targetPath = base + '/api/demo/business-target/' + providerId;
    const setup = await request.post(targetPath, {
      headers, data: { address: 'רחוב הדוגמה 10, עיר ניסוי', mode },
    });
    expect(setup.ok()).toBeTruthy();
    const before = await setup.json();
    const { runAddressInquiryE2E } = await import('./business-actions-flow.mjs');
    const result = await runAddressInquiryE2E(page, base, { success: mode !== 'reject' });
    const after = await (await request.get(targetPath)).json();
    expect(after.address).toBe(mode === 'reject' ? before.address : result.after);
    expect(after.changes - before.changes).toBe(mode === 'reject' ? 0 : 1);
    const jobs = await (await request.get(base + `/api/cases/${result.inquiryId}/business-actions`)).json();
    expect(jobs).toHaveLength(1);
    expect(jobs[0].status).toBe('sent');
    expect(jobs[0].result.success).toBe(mode !== 'reject');
    if (mode === 'interruptOnce') expect(jobs[0].attempts).toBe(1);
    await fs.writeFile(path.resolve(__dirname, `../docs/business-actions/playwright-${mode}.json`), JSON.stringify({ ...result, before, after, job: jobs[0] }, null, 2));
    await testInfo.attach('External result', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
  });
}
