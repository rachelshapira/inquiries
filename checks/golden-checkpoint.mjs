import assert from 'node:assert/strict';
import { runAttachmentInquiryE2E } from './inquiry-flow.mjs';
export async function runGoldenCheckpoint(page, baseURL, configuration, fixtures) {
  // Preflight reads existing definitions/accounts and checks real API authorization.
  const documents = configuration.process.definition.documents;
  assert.ok(documents.length);
  const required = await runAttachmentInquiryE2E(page, baseURL, { ...fixtures, documents });
  await page.reload();
  await page.getByText(required.response, { exact: true }).waitFor({ state: 'visible' });
  for (const kind of documents) await page.getByRole('heading', { name: kind + ' חובה', exact: true }).waitFor({ state: 'visible' });
  await page.goto(baseURL + '/cases');
  await page.getByRole('button', { name: 'החלפת משתמש', exact: true }).click();
  await page.getByRole('button', { name: 'א איתי ברק גורם מאשר · יחידה ארגונית', exact: true }).click();
  await page.getByRole('heading', { name: 'תור פניות', exact: true }).waitFor({ state: 'visible' });
  assert.equal(await page.getByRole('button', { name: 'פתיחת פנייה', exact: true }).count(), 0);
  await page.goto(baseURL + '/');
  assert.equal(await page.getByRole('link', { name: 'פתיחת פנייה', exact: true }).count(), 0);
  await page.goto(baseURL + '/cases?new=1');
  await page.getByRole('heading', { name: 'תור פניות', exact: true }).waitFor({ state: 'visible' });
  assert.equal(await page.getByRole('heading', { name: 'פתיחת פנייה', exact: true }).count(), 0);
  assert.equal(configuration.authorizationApi, 'PASS');
  return { simple: configuration.simple, requiredDocuments: 'PASS', authorization: 'PASS', partialEligibility: configuration.partial, required,
    configuration: { processId: configuration.process.id, key: configuration.process.key, number: configuration.process.number, documents }, configurationWrites: 0 };
}
