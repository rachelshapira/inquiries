# Payment inquiry with required fields and attachments

The existing editor published **בירור תשלום עבור שירות · version 2** on 6 October 2026. The existing workflow runtime and models are unchanged. Version 1 inquiries keep their original workflow.

**Draft → Submitted → InTreatment → ClosedWithResponse**

Draft is needed because the existing file endpoint attaches documents to an already created inquiry. Saving a partial draft is allowed; submission enforces completeness on the server.

Required fields: request details, invoice number, payment amount (number), service date (date), and reply email (email). Required documents: **חשבונית** and **אישור ביצוע שירות**. The existing uploader accepts PDF, PNG and JPEG up to 10 MB per file. The existing document storage, versioning, download authorization and history are reused.

## Try the full flow

1. Open http://127.0.0.1:5080/cases?new=1 and sign in as **נועה לוי — נותן שירות**.
2. Select **בירור תשלום עבור שירות · גרסה 2**. Enter a subject and fill the five fields marked with an asterisk. Click **יצירת פנייה**.
3. The inquiry starts at **Draft**. In **מסמכים וגרסאות**, select **חשבונית**, choose a file, and click **העלאת מסמך**. Repeat for **אישור ביצוע שירות**. Synthetic sample PDFs are available in `checks/fixtures`.
4. Check that **הגשת הפנייה** remains disabled while a required field or document is missing. If you update fields on the detail screen, click **שמירת פרטים** before performing a workflow action.
5. Click **הגשת הפנייה**. The state becomes **Submitted** and the external upload controls disappear.
6. Use **החלפת משתמש**, sign in as **יעל ישראלי — גורם מטפל**, and find the inquiry number in **תור פניות**. Open it and inspect/download both documents.
7. Click **התחלת טיפול**. The state becomes **InTreatment**.
8. Enter a response in **מענה או הערה לפעולה**, then click **מענה וסגירת הפנייה**. The state becomes **ClosedWithResponse**. Empty responses cannot close the inquiry.
9. Switch back to **נועה לוי** and open the same inquiry. Verify the saved fields, two files, final status, response and history. Refresh to verify persistence.

Completed main-local example: http://127.0.0.1:5080/cases/7. Local validation draft #8 is also available for manually trying file uploads.

## Administrator configuration

In **בונה התהליכים**, select the payment inquiry. **טופס ומסמכים** defines field names, types, required flags, viewing/editing roles, editing states and required document kinds. In **מפת התהליך**, the external submission action uses the existing **שדות חובה ומסמכים נדרשים מלאים ובתוקף** guard and **יצירת סבב בדיקה** effect. Employee treatment has no extra guard; closure uses the existing required-reason guard. Publish a new version after validation. Existing inquiries retain their original version.

The submission effect creates the existing document review tasks. This flow records them but does not require a document-review approval gate before closure. No new review engine or extra workflow checks were added.

## Verification

- The saved Playwright scenario completed through the supported in-app browser on the main instance (#7) and an isolated copy (#12), using actual PDF uploads and existing workflow transitions.
- Eleven real API checks passed: every required field, whitespace-only values, missing documents, invalid number/date/email, unchanged inquiry after rejected transitions, persisted form values, PDF download content and denial to an unrelated provider.
- Strict frontend compilation/build passed. No backend source/model/schema changes were needed.
- AXE checks of creation, draft upload/detail and final detail screens: zero violations and zero incomplete checks.
- The standard Playwright CLI remains unverified in the restricted agent environment: its worker spawn was previously blocked with EPERM, and automatic approval review rejected separate browser startup. No bypass was attempted.

The single standard test now uses the attachment scenario. From the `workflow-system` directory, with the local demo running and version 2 already published:

```powershell
npm ci
npx playwright install chromium
npm run test:e2e
```

It defaults to the local demo on port 5080 and creates one test inquiry. To use an isolated instance with the same published version, set `INQUIRY_E2E_URL`. API checks: `python checks/inquiry-required.py [base-url]` (creates one clearly labelled draft).

Evidence: `inquiry-attachments-proof.json`, `inquiry-attachments-isolated-proof.json`, `inquiry-attachments-runtime.json`, and `accessibility-inquiry-attachments.json`.
