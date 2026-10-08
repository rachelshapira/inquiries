# Known issues — current checkpoint

P1 = correctness/security/business-flow blocker; P2 = important, not blocking the current Golden flow; P3 = improvement/deferred/technical limitation. No known P1 blocker remains in the verified current flow. Counts: **P1: 0; P2: 5; P3: 4**.

| Priority | Issue | Status | Next step |
|---|---|---|---|
| P2 | Provider registration validation mismatch | Open | Align client required validation with existing server requirement in a separate fix. |
| P2 | Organizational-tree browsing UX | Deferred | Obtain the pending UX decision before changing interaction. |
| P2 | Task summary lacks document name | Open | Decide the smallest summary-data enrichment separately. |
| P2 | Simple/no-document Golden coverage | Coverage Gap | Use an approved eligible existing configuration when available. |
| P2 | Partial process eligibility coverage | Coverage Gap | Verify an approved existing permitted/forbidden process combination when available. |
| P3 | Angular unit/standalone browser test environment | Environment | Diagnose execution restrictions in a separate session without weakening tests. |
| P3 | Inline document preview | Deferred | Decide whether business users need inline PDF/image viewing. |
| P3 | UI migration report/approval closure | Deferred | Finish the existing consistency review separately; do not redesign approved screens. |
| P3 | Real outbound mail delivery | Deferred | Configure a real delivery implementation only when the business flow requires it. |

## Open/deferred details

**ISSUE:** Provider registration validation mismatch  
**PRIORITY:** P2  
**STATUS:** Open  
**IMPACT:** Client pattern validation permits an empty registration, while server SetProvider requires 5–12 digits. A user can reach save and receive a server rejection. Confirmed cheaply from current providers.ts and Program.cs; no fix made.  
**NEXT STEP:** Add consistent client required validation and verify its error presentation separately.

**ISSUE:** Organizational-tree browsing UX  
**PRIORITY:** P2  
**STATUS:** Deferred  
**IMPACT:** Expanded indented rows become harder to navigate as the hierarchy grows. The specific browsing interaction remains category B, rather than a mechanical styling change.  
**NEXT STEP:** Approve branch expansion/search or a tree/inspector composition before implementation.

**ISSUE:** Task summary lacks document name  
**PRIORITY:** P2  
**STATUS:** Open  
**IMPACT:** The task API returns documentId rather than the document's human-readable name; employees must open the inquiry for document context. Current task endpoint/model confirms the gap.  
**NEXT STEP:** Review a minimal summary projection/data-contract change separately.

**ISSUE:** Simple/no-document Golden coverage  
**PRIORITY:** P2  
**STATUS:** Coverage Gap  
**IMPACT:** SKIPPED — no latest eligible external-user configuration supports this complete journey. Historical no-document configuration is not an eligible substitute; this is not evidence of an architecture failure.  
**NEXT STEP:** Run the full external/employee/external journey when an approved suitable configuration exists.

**ISSUE:** Partial process eligibility coverage  
**PRIORITY:** P2  
**STATUS:** Coverage Gap  
**IMPACT:** SKIPPED — no current user/configuration combination supplies both an eligible and an ineligible process type. Earlier artificial-fixture checks are not presented as current Golden coverage.  
**NEXT STEP:** Verify UI, navigation and API behavior using an approved existing partial-eligibility configuration when available.

**ISSUE:** Test execution environment  
**PRIORITY:** P3  
**STATUS:** Environment  
**IMPACT:** Angular unit tests fail before execution with spawn EPERM; standalone Playwright launch is also restricted. Current browser adapter and lightweight API/presentation checks work, but do not replace a full unit-suite result.  
**NEXT STEP:** Resolve environment execution permissions separately, preserving tests and configuration.

**ISSUE:** Inline document preview  
**PRIORITY:** P3  
**STATUS:** Deferred  
**IMPACT:** Uploaded PDFs/images cannot be previewed inline. Secure authorized attachment download is implemented and proven.  
**NEXT STEP:** Confirm the business need before adding preview behavior.

**ISSUE:** UI migration report/approval closure  
**PRIORITY:** P3  
**STATUS:** Deferred  
**IMPACT:** Four representative patterns are approved and remaining category-A working changes/captures exist, but the final migration report/approval closure remains pending. Do not describe the entire application migration as complete.  
**NEXT STEP:** Resume the existing consistency review only when separately authorized, preserving category-B deferrals.

**ISSUE:** Real outbound mail delivery  
**PRIORITY:** P3  
**STATUS:** Deferred  
**IMPACT:** The sendMail effect/outbox exists, but NullMailSender does not deliver real mail. The current Golden flow proves the in-application response, not external email delivery.  
**NEXT STEP:** Supply and test a real delivery implementation when required; do not advertise delivery meanwhile.

## Recently resolved

- **RESOLVED — inconsistent initiation authorization:** existing initial-state transition roles plus account/provider scope now determine server eligibility and POST authorization; UI/direct navigation reuse that capability. Golden authorization PASS.
- **RESOLVED — authorized creator could create but not continue:** the required-document Golden creator retained access/upload/submission permission. Configured requirements remain visible to read-only viewers, with explanations instead of hidden instructions. Full document journey PASS.

Sources: [NEXT-SESSION.md](../NEXT-SESSION.md), [Golden checkpoint](golden-checkpoint/results.json), [initiation fix report](gov-ui/initiation-fix/report.md), and the two narrowly checked existing validation/task implementations. No broad review, product fix or test rerun was performed for these documents.
