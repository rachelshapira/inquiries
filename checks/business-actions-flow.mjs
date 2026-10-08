import assert from 'node:assert/strict';

async function login(page, base, name) {
  await page.goto(base + '/cases');
  if (await page.getByRole('button', { name: 'החלפת משתמש', exact: true }).count())
    await page.getByRole('button', { name: 'החלפת משתמש', exact: true }).click();
  await page.getByRole('button', { name, exact: true }).click();
  await page.getByRole('heading', { name: /הפניות שלי|תור פניות/ }).waitFor({ state: 'visible' });
}

// Uses the existing dynamic wizard, queue, action UI and runtime; no direct status writes.
export async function runAddressInquiryE2E(page, base, options = {}) {
  assert.equal(base, 'http://127.0.0.1:5081', 'Use isolated QA');
  const before = options.before ?? 'רחוב הדוגמה 10, עיר ניסוי';
  const after = options.after ?? 'רחוב העתיד 40, עיר דמיונית';
  const success = options.success ?? true;
  const expected = success ? 'done' : 'failed';
  const steps = [];
  await login(page, base, 'נ נועה לוי נותן שירות · יחידה ארגונית');
  await page.getByRole('button', { name: 'פתיחת פנייה', exact: true }).click();
  const processSelect = page.getByRole('combobox', { name: 'סוג הפנייה', exact: true });
  await processSelect.waitFor({ state: 'visible' });
  await processSelect.selectOption({ label: 'בקשה לשינוי כתובת — הדגמה מקומית' });
  await page.getByLabel('נושא הפנייה', { exact: true }).fill('בקשה לעדכון כתובת למשלוח הודעות');
  await page.getByLabel('כתובת חדשה (נתוני הדגמה בלבד) *', { exact: true }).waitFor({ state: 'visible' });
  await page.getByLabel('כתובת חדשה (נתוני הדגמה בלבד) *', { exact: true }).fill(after);
  await page.getByRole('button', { name: 'שמירה והמשך', exact: true }).click();
  await page.getByRole('button', { name: 'הגשת הפנייה', exact: true }).waitFor({ state: 'visible' });
  assert.equal(await page.getByRole('heading', { name: 'מסמכים להגשה', exact: true }).count(), 0);
  await page.getByRole('button', { name: 'הגשת הפנייה', exact: true }).click();
  await page.getByRole('heading', { name: 'בקשה לעדכון כתובת למשלוח הודעות', exact: true }).waitFor({ state: 'visible' });
  const inquiryId = Number(new URL(await page.url()).pathname.split('/').pop());
  assert.ok(inquiryId > 0);
  assert.equal(await page.locator('.service-identity .service-status').getAttribute('data-state'), 'review');
  assert.equal(await page.getByRole('button', { name: 'אישור ועדכון הכתובת', exact: true }).count(), 0);
  steps.push('External user creates configured dynamic no-document inquiry and submits through workflow guard');

  await login(page, base, 'י יעל ישראלי גורם מטפל · יחידה ארגונית');
  const queueLink = page.locator(`a[href="/cases/${inquiryId}"]`);
  await queueLink.waitFor({ state: 'visible' });
  await queueLink.click();
  const approve = page.getByRole('button', { name: 'אישור ועדכון הכתובת', exact: true });
  await approve.waitFor({ state: 'visible' });
  assert.equal(await approve.isEnabled(), false);
  await page.getByLabel('מענה או הערה לפעולה', { exact: true }).fill('בקשת ההדגמה נבדקה ואושרה.');
  await page.getByLabel('הצגת המענה או ההערה למגיש הבקשה', { exact: true }).uncheck();
  await approve.click();
  await page.getByRole('heading', { name: 'ממתינים לתוצאת הפעולה', exact: true }).waitFor({ state: 'visible' });
  assert.equal(await page.locator('.service-identity .service-status').getAttribute('data-state'), 'applying');
  assert.equal(await approve.count(), 0);
  if (options.capture) await options.capture('pending');
  steps.push('Employee finds same inquiry in queue; approval queues action; waiting state exposes no manual completion');

  const deadline = Date.now() + 45000;
  while (await page.locator('.service-identity .service-status').getAttribute('data-state') !== expected) {
    assert.ok(Date.now() < deadline, 'Business result was not displayed');
    await page.waitForTimeout(500);
    await page.reload();
    await page.getByRole('heading', { name: 'בקשה לעדכון כתובת למשלוח הודעות', exact: true }).waitFor({ state: 'visible' });
  }
  const response = await page.locator('.service-response-text').innerText();
  assert.ok(response.includes(success ? 'הכתובת עודכנה' : 'הכתובת לא השתנתה'));
  if (success) { assert.ok(response.includes(before)); assert.ok(response.includes(after)); }
  assert.equal(await page.getByRole('button', { name: 'בדיקת עדכון', exact: true }).count(), 0);
  steps.push('Target outcome triggers configured automatic workflow transition and public response');

  await login(page, base, 'נ נועה לוי נותן שירות · יחידה ארגונית');
  await page.locator(`a[href="/cases/${inquiryId}"]`).click();
  await page.reload();
  await page.getByRole('heading', { name: 'המענה לפנייה', exact: true }).waitFor({ state: 'visible' });
  assert.equal(await page.locator('.service-identity .service-status').getAttribute('data-state'), expected);
  assert.equal(await page.locator('.service-response-text').innerText(), response);
  assert.ok((await page.locator('article').innerText()).includes(success ? 'הכתובת עודכנה' : 'עדכון הכתובת לא בוצע'));
  assert.equal(await page.getByRole('button', { name: 'אישור ועדכון הכתובת', exact: true }).count(), 0);
  assert.equal(await page.getByText('בקשת ההדגמה נבדקה ואושרה.', { exact: true }).count(), 0);
  await page.getByText('עדכונים בפנייה', { exact: true }).click();
  await page.locator('.service-history').waitFor({ state: 'visible' });
  assert.ok((await page.locator('.service-history').innerText()).includes(response));
  assert.equal(await page.locator('.service-history strong').filter({ hasText: success ? /^הכתובת עודכנה$/ : /^עדכון הכתובת לא בוצע$/ }).count(), 1);
  if (options.capture) await options.capture('external-final');
  steps.push('Same external owner reloads final result, before/after and public history; internal approval note stays private');
  return { passed: true, inquiryId, expected, before, after, response, steps };
}
