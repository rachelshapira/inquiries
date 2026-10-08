import assert from "node:assert/strict";
const processName = "בירור תשלום עבור שירות";
const processKey = "payment-inquiry";
async function login(page, baseURL, name) {
  await page.goto(baseURL + "/cases");
  if (
    await page.getByRole("button", { name: "החלפת משתמש", exact: true }).count()
  )
    await page
      .getByRole("button", { name: "החלפת משתמש", exact: true })
      .click();
  await page.getByRole("button", { name, exact: true }).click();
  await page
    .getByRole("heading", { name: /הפניות שלי|תור פניות/ })
    .waitFor({ state: "visible" });
}
async function openTransitionSettings(page) {
  for (const group of await page.locator('.workflow-inspector .inspector-group').all()) {
    if (await group.getAttribute('open') === null) await group.locator('summary').press('Enter');
  }
}
// Configure only through the existing editor. No API writes or database setup.
async function configureProcess(page, baseURL) {
  await page.goto(baseURL + "/processes");
  await page
    .getByRole("combobox", { name: "תהליך קיים", exact: true })
    .waitFor({ state: "visible" });
  await page
    .getByRole("combobox", { name: "תהליך קיים", exact: true })
    .locator("option")
    .filter({ hasText: "גרסה" })
    .first()
    .waitFor({ state: "attached" });
  const labels = await page
    .getByRole("combobox", { name: "תהליך קיים", exact: true })
    .locator("option")
    .allTextContents();
  const existing = labels.find((label) => label.includes(processName));
  if (existing) {
    await page
      .getByRole("combobox", { name: "תהליך קיים", exact: true })
      .selectOption({ label: existing.trim() });
    return { number: Number(existing.match(/גרסה (\d+)/)[1]) };
  }

  await page.getByRole("button", { name: "תהליך חדש", exact: true }).click();
  await page.getByLabel(/^שם התהליך/).fill(processName);
  await page.getByText("מזהה התהליך", { exact: true }).click();
  await page.getByLabel(/^מזהה קבוע באנגלית/).fill(processKey);
  await page.getByRole("button", { name: "2 מפת התהליך", exact: true }).click();

  // Remove the approval action, then retarget the preceding action to the final state.
  await page.getByRole("button", { name: "ממתין לאישור", exact: true }).click();
  await page
    .getByRole("button", { name: "אישור הפנייה אל אושרה", exact: true })
    .click();
  await openTransitionSettings(page);
  await page
    .getByRole("button", { name: "הסרת הפעולה מהטיוטה", exact: true })
    .click();
  await page.getByRole("button", { name: "בבדיקה", exact: true }).click();
  await page
    .getByRole("button", { name: "העברה לאישור אל ממתין לאישור", exact: true })
    .click();
  await openTransitionSettings(page);
  await page.getByLabel(/^שם הפעולה/).fill("מענה וסגירת הפנייה");
  await page
    .getByRole("combobox", { name: "יעד ברירת מחדל", exact: true })
    .selectOption("approved");
  await page
    .getByRole("combobox", { name: "תנאים לביצוע הפעולה", exact: true })
    .selectOption("reason");
  await page
    .getByRole("button", { name: "חזרה לפרטי השלב", exact: true })
    .click();
  await page.getByRole("button", { name: "ממתין לאישור", exact: true }).click();
  await page.getByText("אפשרויות נוספות", { exact: true }).click();
  await page
    .getByRole("button", { name: "הסרת שלב מהטיוטה", exact: true })
    .click();

  await page
    .getByRole("button", { name: "טיוטה · התחלה", exact: true })
    .click();
  await page
    .getByRole("button", { name: "הגשת הפנייה אל בבדיקה", exact: true })
    .click();
  await openTransitionSettings(page);
  await page.getByLabel(/^שם הפעולה/).fill("התחלת טיפול");
  const roles = page.locator("fieldset").filter({
    has: page
      .locator("legend")
      .getByText("מי רשאי לבצע את הפעולה?", { exact: true }),
  });
  await roles
    .getByRole("checkbox", { name: "נותן שירות", exact: true })
    .uncheck();
  await roles.getByRole("checkbox", { name: "גורם מטפל", exact: true }).check();
  await page
    .getByRole("combobox", { name: "תנאים לביצוע הפעולה", exact: true })
    .selectOption("none");
  await page
    .getByRole("checkbox", { name: "יצירת סבב בדיקה", exact: true })
    .uncheck();
  await page
    .getByRole("button", { name: "חזרה לפרטי השלב", exact: true })
    .click();

  for (const [currentName, label] of [
    ["טיוטה · התחלה", "Submitted"],
    ["בבדיקה", "InTreatment"],
    ["אושרה · סיום", "ClosedWithResponse"],
  ]) {
    await page.getByRole("button", { name: currentName, exact: true }).click();
    await page.getByLabel("שם השלב", { exact: true }).fill(label);
  }
  await page
    .getByRole("button", { name: "3 טופס ומסמכים", exact: true })
    .click();
  await page
    .getByLabel("מסמכים נדרשים, מופרדים בפסיק", { exact: true })
    .fill("");
  await page
    .getByLabel("מסמכים נדרשים, מופרדים בפסיק", { exact: true })
    .press("Tab");
  await page
    .getByRole("button", { name: "5 בדיקה ופרסום", exact: true })
    .click();
  await page
    .getByText("0 מסמכים נדרשים", { exact: true })
    .waitFor({ state: "visible" });
  await page
    .getByRole("button", { name: "הסרת הפעולה האחרונה", exact: true })
    .click();
  await page
    .getByRole("button", { name: "בדיקת הגדרה וסימולציה", exact: true })
    .click();
  await page.locator('p.note[role="status"]').waitFor({ state: "visible" });
  assert.equal(
    await page
      .getByRole("button", { name: "פרסום גרסה חדשה", exact: true })
      .isEnabled(),
    true,
  );
  await page
    .getByRole("button", { name: "פרסום גרסה חדשה", exact: true })
    .click();
  await page.getByText(/גרסה 1 פורסמה/).waitFor({ state: "visible" });
  return { number: 1 };
}

async function runInquiryE2E(page, baseURL) {
  const log = [];
  await login(page, baseURL, /מ מנהל (המטה|המערכת) מנהל מערכת · מטה/);
  const process = await configureProcess(page, baseURL);
  log.push("Workflow configured through existing visual editor");
  const title = `בירור תשלום עבור שירות סיעוד – E2E ${Date.now()}`;
  const response =
    "התשלום עבור השירות אושר ונכלל במחזור התשלומים הבא. מספר האסמכתה: PAY-2026-1042.";
  await login(page, baseURL, "נ נועה לוי נותן שירות · יחידה ארגונית");
  await page.getByRole("button", { name: "פתיחת פנייה", exact: true }).click();
  await page.getByLabel("נושא הפנייה", { exact: true }).fill(title);
  await page
    .getByRole("combobox", { name: "סוג הפנייה", exact: true })
    .selectOption({ label: processName });
  await page
    .getByLabel("פרטי הבקשה", { exact: true })
    .fill("מבקשת לברר מתי יועבר התשלום עבור השירות שסופק בחודש ספטמבר.");
  await page.getByRole("button", { name: "יצירת פנייה", exact: true }).click();
  await page.locator(".page-heading .badge").waitFor({ state: "visible" });
  const heading = await page.locator(".page-heading .eyebrow").textContent();
  const id = Number(heading.match(/#(\d+)/)[1]);
  assert.ok(id > 0);
  assert.ok(heading.includes(processName));
  assert.equal(
    await page
      .getByRole("heading", { name: "מסמכים וגרסאות", exact: true })
      .count(),
    0,
  );
  assert.equal(
    (await page.locator(".page-heading .badge").textContent()).trim(),
    "הוגשה",
  );
  assert.equal(
    await page.locator(".page-heading .badge").getAttribute("data-state"),
    "draft",
  );
  assert.equal(
    await page
      .getByRole("button", { name: "התחלת טיפול", exact: true })
      .count(),
    0,
  );
  log.push(
    "External submission: number exists, configured workflow, Submitted start state, no employee transition exposed",
  );

  await login(page, baseURL, "י יעל ישראלי גורם מטפל · יחידה ארגונית");
  await page
    .getByRole("searchbox", { name: "חיפוש פניות", exact: true })
    .fill(String(id));
  await page.locator(`a[href="/cases/${id}"]`).click();
  await page.getByRole("button", { name: "התחלת טיפול", exact: true }).click();
  await page
    .getByText("בטיפול", { exact: true })
    .first()
    .waitFor({ state: "visible" });
  assert.equal(
    await page.locator(".page-heading .badge").getAttribute("data-state"),
    "review",
  );
  assert.equal(
    await page
      .getByRole("button", { name: "התחלת טיפול", exact: true })
      .count(),
    0,
  );
  assert.equal(
    await page
      .getByRole("button", { name: "מענה וסגירת הפנייה", exact: true })
      .isEnabled(),
    false,
  );
  log.push(
    "Employee queue, allowed transition to InTreatment, wrong-state action unavailable, empty response cannot close",
  );
  await page.getByLabel("מענה או הערה לפעולה", { exact: true }).fill(response);
  await page
    .getByRole("button", { name: "מענה וסגירת הפנייה", exact: true })
    .click();
  await page
    .getByText("נסגרה עם מענה", { exact: true })
    .first()
    .waitFor({ state: "visible" });
  assert.equal(
    await page.locator(".page-heading .badge").getAttribute("data-state"),
    "approved",
  );
  await page.getByText(response, { exact: true }).waitFor({ state: "visible" });
  await page.reload();
  await page.getByText(response, { exact: true }).waitFor({ state: "visible" });
  assert.equal(
    (await page.locator(".page-heading .badge").textContent()).trim(),
    "נסגרה עם מענה",
  );
  log.push("Response and final state persist after reload");

  await login(page, baseURL, "נ נועה לוי נותן שירות · יחידה ארגונית");
  await page
    .getByRole("searchbox", { name: "חיפוש פניות", exact: true })
    .fill(String(id));
  await page.locator(`a[href="/cases/${id}"]`).click();
  await page.getByText(response, { exact: true }).waitFor({ state: "visible" });
  assert.equal(
    (await page.locator(".page-heading .badge").textContent()).trim(),
    "נסגרה עם מענה",
  );
  assert.equal(
    await page.locator(".page-heading .badge").getAttribute("data-state"),
    "approved",
  );
  const history = await page.locator(".timeline li").allTextContents();
  assert.ok(history.some((h) => h.includes("פתיחת פנייה")));
  assert.ok(history.some((h) => h.includes("התחלת טיפול")));
  assert.ok(
    history.some(
      (h) => h.includes("מענה וסגירת הפנייה") && h.includes(response),
    ),
  );
  assert.equal(
    await page
      .getByRole("button", { name: "מענה וסגירת הפנייה", exact: true })
      .count(),
    0,
  );
  log.push(
    "External user opens same inquiry, sees persisted response, final status and workflow history",
  );
  return {
    passed: true,
    baseURL,
    inquiryId: id,
    title,
    processKey,
    processNumber: process.number,
    response,
    states: ["Submitted", "InTreatment", "ClosedWithResponse"],
    history,
    checks: log,
  };
}
export { runInquiryE2E };

// Version 2 is published with the existing editor: Draft → Submitted → treatment → closure.
async function runAttachmentInquiryE2E(page, baseURL, fixtures) {
  const documents = fixtures.documents ?? ['חשבונית', 'אישור ביצוע שירות'];
  const title = `בירור תשלום עם מסמכים – E2E ${Date.now()}`;
  const response = 'החשבונית ואישור ביצוע השירות התקבלו. הבירור הושלם; התשלום יטופל במחזור הבא.';
  await login(page, baseURL, 'נ נועה לוי נותן שירות · יחידה ארגונית');
  await page.getByRole('button', { name: 'פתיחת פנייה', exact: true }).click();
  await page.getByLabel('נושא הפנייה', { exact: true }).fill(title);
  await page.getByRole('combobox', { name: 'סוג הפנייה', exact: true })
    .selectOption({ label: processName });
  // A partial draft is allowed; submission must still enforce every required field.
  await page.getByRole('button', { name: 'שמירה והמשך', exact: true }).click();
  await page.getByRole('heading', { name: 'מסמכים', exact: true }).waitFor({ state: 'visible' });
  const id = Number(new URL(await page.url()).searchParams.get('draft'));
  assert.ok(id > 0);
  assert.equal((await page.locator('.service-heading .service-status').textContent()).trim(), 'טיוטה');
  await page.getByText('0 מתוך ' + documents.length + ' הושלמו', { exact: true }).waitFor({ state: 'visible' });
  if (fixtures.onDocumentStage) await fixtures.onDocumentStage('draft');
  await page.getByRole('button', { name: 'המשך', exact: true }).click();
  assert.equal(await page.getByRole('button', { name: 'הגשת הפנייה', exact: true }).isEnabled(), false);
  await page.getByText('שדה חובה חסר: פרטי הבקשה', { exact: false }).waitFor({ state: 'visible' });
  await page.getByRole('button', { name: 'עריכת פרטים', exact: true }).click();
  const values = [
    ['פרטי הבקשה', 'בירור תשלום עבור שירות שסופק, עם חשבונית ואישור ביצוע.'],
    ['מספר חשבונית', 'DEMO-2026-1042'],
    ['סכום לתשלום', '1250'],
    ['תאריך השירות', '2026-09-30'],
    ['דואר אלקטרוני למענה', 'demo@example.org'],
  ];
  for (const [label, value] of values) await page.getByLabel(label + ' *', { exact: true }).fill(value);
  await page.getByRole('button', { name: 'שמירה והמשך', exact: true }).click();
  await page.getByRole('button', { name: 'המשך', exact: true }).click();
  await page.getByText('חסר מסמך בתוקף: חשבונית', { exact: false }).waitFor({ state: 'visible' });
  await page.getByRole('button', { name: 'עריכת מסמכים', exact: true }).click();
  for (const [index, kind] of documents.entries()) {
    const filePath = index === 0 ? fixtures.invoice : fixtures.service;

    const chooserPromise = page.waitForEvent('filechooser');
    await page.locator('[data-document-kind="' + kind + '"] input[type="file"]').click();
    await (await chooserPromise).setFiles(filePath);
    await page.getByRole('button', { name: 'העלאת ' + kind, exact: true }).click();
    await page.getByRole('link', { name: new RegExp(filePath.split(/[\\/]/).pop().replace('.', '\\.')) })
      .waitFor({ state: 'visible' });
    await page.getByText((index + 1) + ' מתוך ' + documents.length + ' הושלמו', { exact: true }).waitFor({ state: 'visible' });
    if (fixtures.onDocumentStage) await fixtures.onDocumentStage(index + 1 < documents.length ? 'partial' : 'complete');
    if (index + 1 < documents.length) {
      await page.getByRole('button', { name: 'המשך', exact: true }).click();
      assert.equal(await page.getByRole('button', { name: 'הגשת הפנייה', exact: true }).isEnabled(), false);
      await page.getByText('חסר מסמך בתוקף: ' + documents[index + 1], { exact: false }).waitFor({ state: 'visible' });
      await page.getByRole('button', { name: 'עריכת מסמכים', exact: true }).click();
    }
  }
  await page.getByRole('button', { name: 'המשך', exact: true }).click();
  assert.equal(await page.getByRole('button', { name: 'הגשת הפנייה', exact: true }).isEnabled(), true);
  await page.getByRole('button', { name: 'הגשת הפנייה', exact: true }).click();
  await page.getByText('הוגשה', { exact: true }).first().waitFor({ state: 'visible' });
  assert.equal(await page.locator('input[type="file"]').count(), 0);
  await login(page, baseURL, 'י יעל ישראלי גורם מטפל · יחידה ארגונית');
  await page.getByRole('searchbox', { name: 'חיפוש פניות', exact: true }).fill(String(id));
  await page.locator(`a[href="/cases/${id}"]`).click();
  for (const filePath of [fixtures.invoice, fixtures.service]) {
    await page.getByRole('link', { name: new RegExp(filePath.split(/[\\/]/).pop().replace('.', '\\.')) })
      .waitFor({ state: 'visible' });
  }
  await page.getByRole('button', { name: 'התחלת טיפול', exact: true }).click();
  await page.getByText('בטיפול', { exact: true }).first().waitFor({ state: 'visible' });
  assert.equal(await page.getByRole('button', { name: 'מענה וסגירת הפנייה', exact: true }).isEnabled(), false);
  await page.getByLabel('מענה או הערה לפעולה', { exact: true }).fill(response);
  await page.getByRole('button', { name: 'מענה וסגירת הפנייה', exact: true }).click();
  await page.getByText('נסגרה עם מענה', { exact: true }).first().waitFor({ state: 'visible' });
  await page.reload();
  await page.locator('.service-response-text').filter({ hasText: response }).waitFor({ state: 'visible' });
  await login(page, baseURL, 'נ נועה לוי נותן שירות · יחידה ארגונית');
  await page.getByRole('searchbox', { name: 'חיפוש פניות', exact: true }).fill(String(id));
  await page.locator(`a[href="/cases/${id}"]`).click();
  await page.locator('.service-response-text').filter({ hasText: response }).waitFor({ state: 'visible' });
  assert.equal((await page.locator('.page-heading .badge').textContent()).trim(), 'נסגרה עם מענה');
  assert.equal(JSON.stringify(await page.locator('.service-value dt').allTextContents()), JSON.stringify(values.map(([label]) => label)));
  assert.equal(await page.locator('.service-fields input, .service-fields textarea').count(), 0);
  await page.getByText('עדכונים בפנייה', { exact: true }).click();
  const history = await page.locator('.timeline li').allTextContents();
  for (const action of ['פתיחת פנייה', 'העלאת מסמך', 'הגשת הפנייה', 'התחלת טיפול', 'מענה וסגירת הפנייה'])
    assert.ok(history.some((entry) => entry.includes(action)), action);
  return { passed: true, inquiryId: id, title, response, processKey, processNumber: 2,
    states: ['Draft', 'Submitted', 'InTreatment', 'ClosedWithResponse'], history,
    checks: ['Missing required fields block submission', 'Each required document blocks submission until uploaded',
      'Real UI file uploads', 'Employee queue and existing transitions', 'Persisted response and final external view'] };
}
export { runAttachmentInquiryE2E };
