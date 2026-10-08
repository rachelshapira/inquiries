# Platform checkpoint — 6 October 2026, Asia/Jerusalem

## Current checkpoint — 7 October 2026

Golden #4 is implemented and proven against a controlled fictional local target. Read [docs/BUSINESS-ACTIONS.md](docs/BUSINESS-ACTIONS.md) first; the remainder below is the historical 6 October checkpoint.

- Existing workflow + transactional outbox reused. Generic configured Business Action dispatch; Change Address handler stays outside the engine.
- Real API and browser E2E: actual before/after, success, rejection without target mutation, interrupted delivery after target commit, one update on retry, final external response/history and restart persistence PASS.
- Existing document browser E2E, authorization, document/access, presentation and history privacy checks PASS. Angular/standalone Playwright spawn EPERM limitation remains; no tests weakened.
- Isolated QA on 5081 is stopped after verification. Main is running on http://127.0.0.1:5080 using `work/business-actions-checkpoint/Workflow.Api.dll`. Demonstration inquiry: http://127.0.0.1:5080/cases/15. Fictional local address data only; no real-system integration.
- Evidence: `docs/business-actions/`. New job payload/results reuse Outbox.Message; no workflow-schema migration or duplicated inquiry state.
- Preserve later history-audience and shared edit/create wizard changes; those were implemented after the historical checkpoint below.
- Deferred: real adapter, binding-authoring UI, blocked-job reconciliation controls, real target concurrency policy. Do not generalize or integrate a real system without a concrete next use case/authorization.

---


Demo guide: [docs/DEMO.md](docs/DEMO.md). Known issues: [docs/KNOWN-ISSUES.md](docs/KNOWN-ISSUES.md).

Main application: http://127.0.0.1:5080. Verification used an isolated copy of current data/files on 5081; QA is now stopped. No product source or process/account/routing configuration was changed this checkpoint. Do not start further UI work.

## Validated capabilities

Existing generic workflow/process engine; server-enforced process initiation authorization; per-process/provider eligibility; dynamic forms; configuration-driven required documents; upload/authorized download; document completion; submission guards; routing; employee handling; response/closure; history/audit; persistence after reload; approved gov-il-ui foundation. This is not proof that every possible workflow is supported.

## Golden E2E results

| Scenario | Result | Evidence/reason |
|---|---|---|
| #1 Simple / no documents | SKIPPED — MISSING EXISTING TEST CONFIGURATION | All latest processes eligible to existing external users require documents. Historical payment v1 has documents=[] but is obsolete and excludes the external role. No definitions were changed to force a pass. |
| #2 Required documents | PASS | Existing payment-inquiry v2, process version ID 6; isolated inquiry #13. Same creator retained access, upload and submission capability. Counts came from Definition.documents: 0/N → 1/N → N/N. Incomplete submission blocked; uploads, employee handling/response/closure, original external reload, final response/data/documents/history passed. |
| #3 Authorization | PASS | Existing Approver: no eligible process, no creation action, direct ?new navigation returns to queue, direct POST rejected with 403. Existing Provider control created a draft and received upload capability. |
| Partial process eligibility | SKIPPED — NO EXISTING PARTIAL-ELIGIBILITY CONFIGURATION | Existing active users have all eligible process types or none. Provider scope is not misreported as partial process eligibility. |

Real API assertions verified process/version, provider ownership, routed unit, history and final state. Both PDFs downloaded byte-for-byte for employee and owner; unrelated Provider received 403. Lightweight regressions: 11 existing server validation/access checks and 10 presentation checks PASS. Broader suites were not rerun because they publish artificial configuration. Previous 43-check results remain historical evidence.

Genericity PASS: application initiation/documents/runtime logic uses definitions, transition roles, existing access scopes and current document state. No payment/invoice/document-name/username branches were introduced. Test fixture names are permitted test data; existing technical-title cleanup is presentation only.

Evidence: docs/golden-checkpoint/preflight.json, browser-result.json, persistence-download.json, results.json and external-final.png. No complete screenshot suite was repeated.

## Known limitations/backlog

- Angular unit tests: KNOWN BROKEN / ENVIRONMENT ISSUE — esbuild spawn EPERM before execution. Not retried, weakened or investigated here.
- Standalone Playwright launch remains restricted; the supported in-app browser adapter is the verified execution path. Default playwright.config.cjs matches only inquiry.e2e.spec.cjs; do not claim default discovery of the new Golden entry.
- Inline document preview deferred; secure attachment download works.
- Organizational-tree browsing UX is deferred category B, awaiting approval.
- General UI migration remains paused. Category-A working changes/captures are in docs/gov-ui/migration-validation; final migration report/approval closure remains pending.
- Provider registration: server requires it; client accepts an empty value. Existing validation mismatch remains documented.
- Task summary returns document ID rather than human-readable document name; summary enrichment remains deferred.
- No current create-on-behalf exception requiring creatorRoles was found. Existing initial-transition permissions and routing visibility handoff remain intact.
- The CURRENT configuration does not prove a complete no-document external/employee journey or partial process eligibility; preserve their SKIPPED status.
- Demo uses SQLite; production uses SQL Server/organizational authentication. NullMailSender is a placeholder.

## Next milestone: Golden E2E #4

External Request → Internal Business Action: **בקשה לשינוי כתובת**.

FIRST inspect existing Effects/outbox infrastructure. Intended future E2E: external request → address payload → submission → configured validation/routing/approval → business action handler → controlled target-system update → success/failure result → inquiry history → workflow continuation/closure → external outcome.

Address-specific logic belongs behind an application/integration handler, never inside the generic workflow engine. Do not add speculative infrastructure. Answer before implementing:

1. Which existing Effects infrastructure can be reused?
2. What is actually missing?
3. What is the smallest required architecture?
4. Synchronous or asynchronous execution?
5. How is success represented?
6. How is failure represented?
7. What retries are necessary?
8. What idempotency is necessary?
9. How does the result enter history?
10. How is E2E tested against a local controlled target rather than production?
11. Does this real flow actually require a new generic platform capability?

Current effects: none/newRound/closeTasks/sendMail. Engine.Execute handles round/task effects; Program.cs enqueues notification/mail outbox entries transactionally. Inspect the existing worker and mail interface before proposing business-action handling. Golden #4 was NOT implemented.

## Actual project paths

- server/Engine.cs: execution, guards, CanInitiateProcess, CanUpload, access, detail projection.
- server/Domain.cs: Definition/Transition/ProcessVersion/Case/Document/History/Outbox and EF model.
- server/Program.cs: authentication/filter, creation-options, cases/actions/documents endpoints and outbox insertion.
- server/Seed.cs and server/App_Data/demo.db: seed and CURRENT persisted DefinitionJson versions. Inspect latest definitions first.
- server/Routing.cs, RoutingApi.cs, PayloadRules.cs: hierarchy scopes, routing and payload decisions.
- server/OrganizationApi.cs: account read/write permissions.
- server/Infrastructure/MailSender.cs: existing delivery interface/placeholder. Locate NotificationWorker before deciding how to handle business effects.
- client/src/app/core/api.ts, models.ts and app.routes.ts: server capabilities/direct creation protection.
- client/src/app/pages/cases and case-detail: creation/form/list, required documents/upload, response/history/actions.
- client/src/app/pages/processes and shared/process-graph: existing visual workflow editor.
- checks/golden-preflight.py: existing configuration inventory and API authorization/control; no configuration writes.
- checks/golden-checkpoint.mjs and golden-checkpoint.spec.cjs: saved document/browser authorization pack.
- checks/inquiry-flow.mjs and inquiry.e2e.spec.cjs: existing real browser workflow scenario.
- checks/inquiry-required.py and presentation.mjs: inexpensive checkpoint regressions.
- checks/integration.py, organization.py, payload.py, initiation.py: broader suites that create test configuration; inappropriate under an existing-config-only instruction.

## Minimum working commands

Project root: C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/outputs/workflow-system.

Main API and built frontend run together. No second frontend server is needed. From project root, only when backend code changes:

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' build server/Workflow.Api.csproj --no-restore -o ../../work/initiation-final
```

To start main (already running), from server/:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:Demo='true'
& 'C:/Program Files/dotnet/dotnet.exe' ../../../work/initiation-final/Workflow.Api.dll --urls http://127.0.0.1:5080
```

Strict/minified frontend build, from client/:

```powershell
& 'C:/Users/shapira/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe' build-local.mjs
```

Existing isolated QA runtime/data: C:/Users/shapira/Documents/Codex/2026-10-04/skill-creator-c-users-shapira-codex/work/checkpoint-server. From that directory, with Development/Demo=true, start dotnet ./Workflow.Api.dll --urls http://127.0.0.1:5081. Its native runtimes/ directory is required. Do not run mutation-producing tests against main data.

From project root with QA running:

```powershell
& 'C:/Users/shapira/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' checks/golden-preflight.py http://127.0.0.1:5081
& 'C:/Users/shapira/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' checks/inquiry-required.py http://127.0.0.1:5081
& 'C:/Users/shapira/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe' checks/presentation.mjs
```

Working browser entry: import checks/golden-checkpoint.mjs using the supported in-app Playwright-compatible page adapter (govPage in the current CUA session), read docs/golden-checkpoint/preflight.json, then:

```javascript
await runGoldenCheckpoint(govPage, 'http://127.0.0.1:5081', configuration, {
  invoice: absoluteProjectPath + '/checks/fixtures/demo-invoice.pdf',
  service: absoluteProjectPath + '/checks/fixtures/demo-service.pdf'
});
```

Save output to docs/golden-checkpoint/browser-result.json. Rebind the tab/adapter in a fresh browser session. The saved .spec.cjs is the equivalent normal Playwright entry for an environment that can launch workers. ng test --watch=false and standalone Playwright launch are KNOWN BROKEN / ENVIRONMENT ISSUES here, not normal working commands.

## Files and Git status

Modified this checkpoint: checks/inquiry-flow.mjs only; document-array/count assertions now accept existing definition configuration. Scenarios, guards and workflow actions were preserved.

New: checks/golden-preflight.py, checks/golden-checkpoint.mjs, checks/golden-checkpoint.spec.cjs, NEXT-SESSION.md and docs/golden-checkpoint evidence. No application code or configuration was changed this checkpoint.

Previously approved important fix remains in server/Engine.cs, server/Program.cs, Angular core/routes and cases/detail/dashboard: shared server initiation eligibility and configured document journey. UI migration edits predate this checkpoint.

Actual Git status: **not a Git repository (or any parent)**. No tracked/untracked diff can be reported, and no clean-tree claim is made. Nothing was committed. This ledger records checkpoint writes rather than invented Git status.

STOP. Next session begins with Effects/business-action inspection for Golden #4.
