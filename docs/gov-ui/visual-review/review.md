# Inquiry detail: critical visual review

Reviewed the actual running page on 6 October 2026 using gov-il-ui and the current official IGDS typography, status badge and forms references. This is a review of visual direction, not an accessibility certificate. Only the inquiry-detail pilot and its scoped shared presentation styles were changed. No other page was migrated.

## Visual judgment

The first pilot had replaced cards, but still inherited their spacing: a detached status, oversized response block, widely separated metadata, repeated dividers and supporting sections that made the page unnecessarily long. Blue colors alone did not resolve that.

The revised composition is a more credible government-service record: identity and status together, an immediately discoverable response, readable business information, compact document rows and treatment controls placed after the context needed to decide. It is an IGDS-aligned visual candidate, not a finished reproduction of the government component library. The remaining font and shell limitations are described below.

## Review against the ten criteria

| Criterion | Critical finding in the first pilot | Refinement and current judgment |
|---|---|---|
| 1. Visual hierarchy | Inquiry number was too quiet; status sat far away at the opposite edge. Response emphasis was too large | Number, process and compact status now form one identity row. Title remains dominant. Response stays first in a completed inquiry, while actor/time remain secondary. Business details and attachments follow |
| 2. Density | Generous margins accumulated; every section had card-like padding. File versions were stretched across the full width | Reduced related spacing, response padding and section height. Same existing closed inquiry measured approximately 1488px before and 1101px after at the 1440px viewport: about 26% shorter. Text remains readable. Space below a short inquiry is the remaining viewport, not an empty panel |
| 3. Typography | Section headings competed with the title; response text was oversized | 32px desktop title, 24px medium section headings, 16px body and 14px quiet labels/metadata. Mobile headings reduce to fit. Label/value distinction is consistent. Existing Hebrew font fallback remains a limitation |
| 4. Sections | Dividers had become the replacement for cards, rather than indicating useful boundaries | Removed the title/details divider and response/details divider. Separators remain between different business subjects, files and disclosures. Missing optional data is one quiet inline item, not a full-width empty block |
| 5. Actions | An expanded note form appeared before the employee had read the inquiry. Multiple buttons could be equally prominent | Business context and files precede decisions. One existing first action is primary; alternatives are secondary. The note field opens when the primary action requires it and is otherwise available through disclosure. Disabled-action reasons remain visible. A header shortcut moves focus to the action area on long pages. Closed inquiries reserve no action area |
| 6. Status | Raw keys had been translated, but status remained visually detached and supporting counts repeated process metadata | One current human-readable badge beside identity. Removed disclosure event/round counts and routine route diagnostics. Process graph remains available only in the administrator's supporting disclosure. This is a presentation choice; API access and permissions were not changed |
| 7. RTL | Related file metadata was placed at opposite sides; date formats differed | File title, download and metadata now align together on the Hebrew start edge. Dates use the existing dd.MM.yyyy convention, including read-only date fields. Email, filenames and identifiers stay isolated. Native expanders and the history line are on the right, with newest events first. Action shortcut was checked for scrolling/focus without leaving the route |
| 8. Government design feel | Color/type tokens had improved appearance, but the composition remained an admin template with excessive air | Improved task-based reading order, restrained surface emphasis, predictable field grouping, business language and quiet supporting controls. The sidebar and workflow-themed brand mark still reflect the original application; they were retained to avoid a shell redesign |
| 9. Simplicity | Refresh, duplicate shell slogans, first-version labels, route names, disclosure counts and irrelevant post-closure checks added noise | Removed routine refresh from this screen, duplicate shell footer/slogans and first/current version labels. Later/previous versions and validity dates remain visible when meaningful. Closed external users do not see internal document checks. Assignment, operational checks and history remain available to the users who need them |
| 10. Realistic states | A single closed screenshot could hide problems in other states | Reviewed active approval #3, closed payment inquiry #7 with response and two PDFs, empty attachments and missing optional information in #1, editable required fields, and a long Hebrew paragraph fixture in an isolated copy of #1. Live inquiry values were not edited |

## Official guidance versus composition decisions

- **OFFICIAL:** [IGDS Typography](https://igds.gov.il/4988d5140/p/14bf03-typography) documents 32px Large Title, 24px Small-Medium, 16px Body and 14px Label roles. The pilot adopts those sizes/weights and their hierarchy. Rubik is specified by IGDS but is not bundled in this application; no font was downloaded automatically.
- **OFFICIAL:** [Status Badge](https://igds.gov.il/4988d5140/p/922083-status-badge) describes a compact status/description next to a title, with text and an optional icon. The pilot brings the existing text badge into the identity area. Status mapping and persisted workflow keys are unchanged.
- **OFFICIAL:** [Forms](https://igds.gov.il/4988d5140/p/24285b-forms) calls for meaningful grouping, predictable order, concise labels, persistent essential help, at most two desktop fields per row and one mobile field, and clear bottom-of-form actions. Existing draft save/upload behavior was retained; this guidance was not used to add autosave or modify validation.
- **OFFICIAL:** [Button](https://igds.gov.il/4988d5140/p/615f08-button) distinguishes primary, secondary and lower-emphasis actions. Existing action order is reused to establish visual emphasis; no workflow priority was changed.
- **RECOMMENDED:** gov-il-ui's task-first detail composition, no-default-cards approach, progressive disclosure, compact empty states, logical RTL spacing and mixed-direction isolation guided the structural refinements. The content width, spacing choices and testing widths are project decisions, not universal official mandates.

## Screenshots

The comparison uses the same existing inquiry #7 and the same visible desktop region. The AFTER files below contain the entire default page, not only its first viewport.

- [Before versus after desktop](desktop-comparison.png)
- [1440px desktop, full page](after-desktop.png)
- [768px tablet, full page](after-tablet.png)
- [375px mobile, full page](after-mobile.png)
- [Active inquiry with primary and secondary actions](after-active-desktop.png)
- [No attachments and missing optional information](after-empty-optional-desktop.png)
- [Long Hebrew paragraphs, desktop](after-long-hebrew-desktop.png)
- [Long Hebrew paragraphs, mobile](after-long-hebrew-mobile.png)
- [Expanded RTL history, mobile](history-mobile.png)

The browser's full-page capture misframed this RTL page. Complete screenshots were therefore assembled from overlapping actual top/bottom browser frames, aligned by rendered pixels. Native frames were normalized back to the requested CSS widths; fixed navigation is shown once. No content or UI state was fabricated. The raw segments and [capture measurements](capture-manifest.json) are retained alongside the resulting images.

## Remaining limitations and scope

Rubik's exact Hebrew appearance remains unverified here because the application still uses its existing Segoe UI/Arial fallback. The existing workflow-shaped brand icon and sidebar are still recognizable as application chrome. Existing test-generated titles, identifiers and the small test PDF were preserved rather than rewriting live data to improve screenshots. These limitations should be considered when approving the direction.

Long Hebrew content was added only through the existing form/API in an isolated local database copy. The live records on port 5080 were read without data changes. No backend code, schema, workflows, transition guards, routing or authorization rules changed.

Secondary regression evidence: strict Angular compilation succeeded; the existing browser E2E with required fields, PDF uploads, submission, employee treatment, required response, closure and external history passed in the isolated copy. [Result](regression-result.json). The only test change was the history disclosure selector after its redundant count was removed. Visual assessment, rather than these checks, drove this refinement.

Migration remains paused for visual approval. The queue, creation form, dashboard, workflow builder, organizational tree and all other screens remain outside this change.
