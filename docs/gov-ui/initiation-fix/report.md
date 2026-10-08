**Process initiation and configured document journey — verified 6 October 2026**

This fix is limited to initiation authorization and the draft/document journey. General UI migration and organizational-tree work remain paused. The approved gov-il-ui foundation is reused; no unrelated screens were redesigned.

**Authoritative workflow permission**

The existing model defines roles on transitions, not states. An account may initiate a process when it is active, has existing read/write access to the selected provider context, and its role is included in an outgoing transition from the definition's initialState. Runtime guards are not required to pass before draft creation: completing required fields/documents is the purpose of the draft. Permission is per process and provider, and only the latest version is offered. Non-submission initial transitions remain supported; this is not a global role check.

Engine.CanInitiateProcess delegates scope/ownership checks to existing CanWrite and reads existing transition roles. Authentication and active-account checks also remain enforced by the API group/filter. POST /cases re-evaluates the helper and retains latest-version, field, routing and audit checks. No creatorRoles field, workflow engine, authorization-role catalog, schema migration or dependency was added.

GET /api/cases/creation-options returns eligible process definitions, eligible provider records, and the permitted process/provider pairs. Angular uses this server result for dashboard/list creation visibility, the ?new route guard, process choices and provider choices. It does not reproduce role/scope policy. Capabilities are cleared when the session changes, refreshed on entry, and are never trusted as authorization for POST.

**Document journey**

Creation still creates a partial draft and navigates to its detail. A short creation message with a keyboard-accessible link makes the next step obvious. Each configured required document has its name, missing/uploaded/expired presentation, accessible file chooser, optional validity date and upload action. File selection is kept separately for each requirement. Both rows use the existing upload method and API, version checks, history and secure file download.

Completion uses the configured requirements and current document versions. The server exposes document validity using the same unchanged validity predicate as the submission guard; expiration cannot falsely produce a completed count. The display updates 0/2 → 1/2 → 2/2. Button availability and exact blocking reasons continue to come from the existing server action/guard response, rather than independent frontend validation. All configured documents are required in the current schema; no optional-document concept was invented.

Requirements remain visible when canUpload is false, alongside a short explanation. Upload controls disappear in that state. Current filenames link to the secure inquiry/document endpoint; older versions remain accessible through the existing disclosure. No raw storage URL or inline preview was introduced. With no requirements and no uploaded files, no empty document section is rendered.

**Authorization matrix**

| Scenario | Result |
|---|---|
| Authorized Provider | Eligible process offered; real draft, uploads, submission and full closure journey passed |
| Read-only account | No dashboard/list creation action; direct ?new navigation returns to queue; direct POST rejected |
| Reviewer / Approver excluded by initial transition roles | Baseline process not eligible; direct POST rejected |
| Partial process access | Permitted processes offered; admin-only test process absent; attempted POST rejected |
| Workflow explicitly permits Reviewer | That process becomes eligible and draft creation succeeds without role-specific implementation code |
| Unrelated Provider | Creation outside provider ownership and document downloads rejected |

The matrix used test configurations/accounts created through real APIs in the isolated QA copy, never by bypassing workflow state in the database. No explicit create-on-behalf exception was found in current requirements/configuration. Existing configured initial-transition permissions are preserved, including non-submission transitions that can hand control to another role later. Routing that legitimately removes an internal creator's visibility retains its existing canView=false response.

**Regression and browser evidence**

- Backend build passed; frontend strict compilation/minified production bundle passed.
- 43 existing API checks passed: integration 12, organization 10, payload 10, required-field/document/access 11.
- 9 new authorization checks passed; 10 presentation checks passed.
- Existing saved Playwright inquiry scenario passed through the supported in-app browser adapter, using real upload controls and workflow transitions. QA inquiry #26 progressed Draft → Submitted → InTreatment → ClosedWithResponse. Persisted response, final external view and history were verified. Assertions for missing fields/documents were preserved and completion-counter assertions added. Only upload selectors changed to match the per-requirement controls. Standalone Playwright CLI execution is not claimed.
- Both uploaded PDFs were downloaded by the owner and compared byte-for-byte to the fixtures. An unrelated Provider received 403 for each.
- A configured no-document process created and completed through its existing transition; the rendered completed inquiry has no document section.
- Nine axe 4.13.0 audits across creation, writable draft and read-only draft at 1440/768/375 returned zero violations and no incomplete checks. Manual review verified Hebrew RTL, mixed filenames/dates, native labels, action placement, disabled submission explanations and keyboard focus. Enter on the document-jump action focuses the section heading with a visible 2px blue outline. Automated results are not a claim of full accessibility compliance.
- Captured draft, partial, complete, read-only and no-document states at 1440/768/375 have no page-level horizontal overflow. Native scroll screenshots were assembled into full pages without altering UI content; transient success toasts were dismissed before capture.

**Create-on-behalf and limitations**

No current documented/configured create-only exception requiring a new creator permission was discovered. No such infrastructure was added. Initial-state roles remain configurable through existing transitions. API data, workflow state, transitions, routing, document versioning, history, response persistence and secure-download behavior are reused.

Inline PDF/image preview remains a separate enhancement; current downloads are authorized attachments. Angular unit tests were retried without changing configuration and remain blocked before execution by esbuild spawn EPERM. The backend build also reports NU1900 because its configured vulnerability feed is unavailable; compilation succeeded. No tests were removed or weakened. No gov-il-ui skill update was needed.

The main local application was restarted at http://127.0.0.1:5080 with this fix and its existing database/files. Regression fixtures live only in the QA copy. Demo login may need to be selected again after restart.

**Screenshots**

| State | Desktop 1440 | Tablet 768 | Mobile 375 |
|---|---|---|---|
| Immediately after draft creation — 0/2 | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/draft-1440.png) | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/draft-768.png) | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/draft-375.png) |
| One required document uploaded — 1/2 | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/partial-1440.png) | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/partial-768.png) | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/partial-375.png) |
| Both uploaded, submission enabled — 2/2 | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/complete-1440.png) | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/complete-768.png) | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/complete-375.png) |
| Visible requirements without upload permission | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/readonly-1440.png) | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/readonly-768.png) | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/readonly-375.png) |
| Configured no-document workflow completed | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/no-documents-1440.png) | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/no-documents-768.png) | [View](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/no-documents-375.png) |

Evidence: [authorization](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/authorization-results.json), [browser E2E](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/e2e-result.json), [navigation/keyboard](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/ui-results.json), [secure downloads](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/download-results.json), [axe](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/axe-results.json), [validation summary](C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system/docs/gov-ui/initiation-fix/validation-results.json).
