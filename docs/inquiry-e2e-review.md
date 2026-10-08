# Existing workflow: inspection and one business inquiry proof

Reviewed and verified on 6 October 2026.

## Outcome

The existing mechanism ran **בירור תשלום עבור שירות** from submission to an employee response and closure. The process was configured and published through the existing visual editor. Main local inquiry **#6** is available at `http://127.0.0.1:5080/cases/6`.

No new engine, inquiry model, workflow-state model, node types, runtime abstraction, conditions, timers, SLA, scripts, integrations, or parallel execution were added.

## Inspection before changes

1. **Definition storage:** `WorkflowDb.Processes` contains `ProcessVersion` records. Each record stores a process key, version number, publication time, and `DefinitionJson`. `Definition` includes its initial state, states, transitions, fields, and required documents. Existing inquiries retain their published version.
2. **Inquiry type → workflow:** “סוג הפנייה” selects the latest published version for a process key. The creation request sends `ProcessVersionId`, which is stored on `Case`. There is no separate inquiry-type entity or mapping table; the published process is the inquiry type in this application.
3. **Initial state:** `POST /api/cases` loads the selected definition and sets `Case.State = Definition.InitialState`. Creation never needs a database status override. Submitting the existing creation form starts this inquiry at the configured Submitted node.
4. **Persistence:** `Case.State` is the sole current workflow state. `Case.ProcessVersionId` identifies its definition. `Case.Version` provides optimistic concurrency. There is no duplicate state in another model.
5. **Allowed transitions:** `Engine.View` uses transitions from the inquiry's immutable definition, filtering by current state, role, and effective write access. It returns guard errors as `BlockedReason`. The detail page renders these server-provided actions.
6. **Execution:** `POST /api/cases/{id}/actions/{action}` checks access, write scope, and request version, then calls `Engine.Execute`. The engine selects a transition for the current state and action, verifies its role and guard, resolves its target, applies configured effects, changes `Case.State`, and calls `Touch`. One `SaveChanges` transaction commits the state, history, task changes, and outbox record.
7. **History:** `Engine.Touch` stores actor, action label, note, round, and timestamp in `History`. This simple flow has distinct action labels, so creation, treatment, and closure are traceable. Ordinary transitions do not currently store separate From/To columns; those are not required to prove this flow. The employee's closing response is the existing closure history note.
8. **Visual editor → runtime:** the editor edits a `Definition`, sends it to the existing simulator, and publishes it through `POST /api/processes`. Publishing validates the definition and adds an immutable version. The inquiry runtime reads that same JSON; the graph is a view of the definition, not another execution mechanism.
9. **Actually used:** existing authentication/session handling, provider ownership, inquiry creation, process versions, organizational visibility, queue filtering, details, allowed actions, execution, reason validation, concurrency, history, and persistence. The normal action endpoint also creates its existing outbox record.
10. **Extra or unused for this flow:** document review rounds, document versioning, approvals, payload branching, routing-rule administration, simulation of routing, mail sending, assignment, and organization administration are not needed for this payment inquiry. Several were explicitly required earlier and are used by the document processes; they are not globally dead code. The previous mandatory-document rule and always-draft wording were unnecessary coupling for this flow.

## Classification

| Category | Existing implementation and decision |
|---|---|
| KEEP | `Definition`, immutable `ProcessVersion`, `Case.State`, existing action endpoint, `Engine.Execute`, role/scope checks, optimistic concurrency, `History`, visual editor, queue and detail screens. |
| SIMPLIFY | Reuse the closing action's note as the response. Hide document/task panels when an inquiry has neither requirements, documents, nor review tasks. Keep one existing list with role-specific “הפניות שלי” / “תור פניות” headings. |
| FIX | Allow zero required documents when publishing; make the editor's document input update its definition immediately; replace “יצירת טיוטה” with “יצירת פנייה”; label the action note for responses; distinguish the detail-page aside landmark for accessibility. |
| DELETE | No globally unused workflow subsystem was demonstrated. No working runtime feature was deleted. An unused test-config import and a redundant test assertion wrapper were removed after the first E2E passed. |
| NOT NEEDED FOR THIS E2E | Review rounds, approvals, conditional routes, timers, SLA, integrations, mail, handler assignment, and routing-rule configuration. No further generalization was added. |

## Configured workflow

Process key: `payment-inquiry`. Name: **בירור תשלום עבור שירות**. Version: **1**. Required documents: **none**. One existing text-area field holds the external user's request details.

The editor preserves fixed internal identifiers. The published state labels supply the business meaning:

| Displayed state | Existing persisted ID | Action to next state | Who may execute | Guard |
|---|---|---|---|---|
| Submitted (start) | `draft` | התחלת טיפול | Reviewer / Admin | `none` |
| InTreatment | `review` | מענה וסגירת הפנייה | Reviewer / Admin | `reason` |
| ClosedWithResponse (terminal) | `approved` | None | None | None |

`reason` rejects an empty or whitespace-only closing response. The response is persisted in the closure `History.Note`, and the external user reads it in the existing history panel. No new response table or state field was introduced.

The new-inquiry form's submit creates the inquiry directly at Submitted. This flow has no separate pre-submission draft step. Other inquiry types still start at their own configured initial node.

External access is the existing provider-level ownership rule, rather than a newly invented per-person ownership model. Local sign-in uses the existing demo accounts; production authentication was not changed.

## Test evidence

Exactly one Playwright test is defined in `checks/inquiry.e2e.spec.cjs`. It calls the saved scenario in `checks/inquiry-flow.mjs`. The scenario configures the workflow through the editor or reuses it, submits the inquiry through the external user's form, verifies its number/type/start state, finds it in the employee's queue, executes the existing treatment action, requires a response for closure, closes it, reloads to prove persistence, and switches back to the external user to verify response, terminal state, and all three history events.

The saved scenario passed through the supported in-app browser Playwright interface on both an isolated local database and the main local instance. No database status updates or mocked transitions were used.

**Runner limitation:** the standard Playwright CLI cannot spawn its worker in this sandbox (`spawn EPERM`). Automatic approval review also rejected starting a separate headless browser because sandbox approval is disabled. Network permission did not remove that process restriction. The successful scenario execution above is through the supported in-app Playwright interface; it is not a claim that the standard CLI runner passed here.

Additional real-runtime checks against the isolated instance passed: provider transition → 403; wrong-state transition → 403; empty response → 400; stale version → 409. Rejected requests preserved state, version, and history. The same existing action endpoint then persisted a valid response and terminal state. The original 12 integration checks passed, including document-review behavior.

Frontend build and backend build succeeded. The backend build retained the existing NU1900 warning because the configured private NuGet vulnerability feed was unavailable. AXE checks for the final external detail screen and new-inquiry/list screen reported zero violations and zero incomplete checks.

Evidence files:

- `inquiry-e2e-proof.json`: isolated browser scenario result.
- `inquiry-e2e-local-proof.json`: main local inquiry #6 result.
- `inquiry-runtime-validation.json`: actual definition and server rejection checks.
- `accessibility-inquiry-e2e.json`: AXE results.
- `inquiry-e2e-external.jpg`: main local external-user final screen.

## Run the standard test outside the restricted agent runner

Start an isolated instance of the existing API in Development with `Demo=true`, listening on port 5081, with its own content root and a copy of the built `wwwroot`. Do not point test setup at production.

From the `workflow-system` directory:

```powershell
npm ci
npx playwright install chromium
npm run test:e2e
```

For an already installed browser, set `PLAYWRIGHT_CHROMIUM_EXECUTABLE` to its executable path. `INQUIRY_E2E_URL` can select another local instance. The test creates one inquiry and retains its audit evidence; it reuses the process when it already exists.

## Review after proof

This E2E requires only a versioned definition, a persisted inquiry state, authorized actions, a nonempty closing note, visibility, and history. Those parts already existed and were retained. The generic document prerequisite was removed and irrelevant document UI is hidden for this flow. Features supporting the earlier document workflows remain because they are demonstrably used; “not required by this E2E” was not treated as “safe to delete globally.”
