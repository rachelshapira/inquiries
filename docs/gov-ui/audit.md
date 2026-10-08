# Government-service UI audit and inquiry detail pilot

Date: 6 October 2026. Skill: gov-il-ui. Scope: inspect the running application as a whole, then apply the smallest shared presentation foundation to inquiry details only. Business behavior, APIs, permissions, workflow definitions, guards, storage and routing remain unchanged.

## Inspection and evidence

Inspected the running dashboard, inquiry queue and creation form, inquiry details, providers, review tasks, process editor and organization hierarchy using existing demo accounts and persisted inquiries. The pilot uses existing payment inquiry #7: five submitted fields, two PDF attachments, a persisted employee response and seven history entries. Its long existing title deliberately remains unchanged. Draft #8 supplies realistic editable fields and missing-document guards. No primary database records were changed for this audit.

Before screenshots: [details](before-detail-desktop.png), [queue](before-queue.png), [providers](before-providers.png), [tasks](before-tasks.png), [process editor](before-processes.png), [organization](before-organization.png).

## 1. Global/systemic problems

| Finding | Effect | Smallest foundation change |
|---|---|---|
| Rounded bordered panels are the default wrapper for nearly every subject | Everything appears equally important; the page is visually heavy | Plain sections, spacing and dividers as the default; highlighting reserved for meaningful content |
| Mixed green shades, small secondary text and inconsistent heading sizes | Weak professional identity and scanning hierarchy | Shared semantic color and typography tokens |
| Identity, status, actions and supporting information compete | Users must scan too much before knowing what to do | Stable sequence: identity/status → response or next action → details/files → supporting information |
| Hebrew content mixes with technical state names, payload terminology and version metadata | Internal implementation distracts from the service | Translate known display labels and keep technical information in supporting disclosures |
| Shared styles are coupled to broad selectors | A global refresh could unintentionally alter every screen | Explicit opt-in foundation, used on one route before wider migration |

## 2. Shared component problems

| Component | Finding | Pilot treatment |
|---|---|---|
| Panels/cards | Repeated padding, borders and rounded surfaces obscure hierarchy | Replace detail-page wrappers with semantic sections |
| Buttons | Weak distinction between main and secondary actions | Blue primary, blue outline secondary, visible keyboard focus; existing disabled guards retained |
| Read-only fields | Disabled controls make completed information appear unavailable | Native definition lists with readable values; editable fields remain controls |
| Status | Repeated status summaries and raw English keys | One visible status with Hebrew display text; persisted keys unchanged |
| Files | Each attachment reads like another card | Plain separated rows; descriptive download links and existing version/date information |
| History/map | Supporting detail consumes initial attention | Native keyboard-operable disclosures; existing graph reused |
| RTL/mixed text | Filenames, email and numbers can disturb reading order | Isolated values with bdi and logical CSS properties |

## 3. Page-specific problems

| Page | Findings | Next action, not applied now |
|---|---|---|
| Dashboard | Metric cards compete with the task users need to complete | Reduce simultaneous emphasis and establish a primary work entry |
| Inquiry queue / new inquiry | Heavy table container; mixed state labels; required-field and document guidance lacks a clear hierarchy | Adopt shared table, status and form patterns after pilot review |
| Inquiry detail | Repeated state information, empty action area, response buried in history, disabled read-only fields and excessive document/task containers | Fixed in this pilot |
| Providers | Oversized container around a table, small metadata and directional glyphs | Simplify table presentation and isolate mixed-direction content |
| Review tasks | Repeated rows do not clearly foreground the document to review | Improve row hierarchy. Existing closed inquiry #7 still has pending review-task records: this is a separate business/data issue and was not changed |
| Process editor | Graph, transition cards, properties and step containers duplicate information | Reduce presentation repetition later; retain the existing editor and engine |
| Organization | Nested unit cards and button emphasis obscure parent/child relationships | Establish tree indentation and a quieter selected-unit detail area later |

## Proposed and applied foundation

The minimum change is a small set of shared CSS tokens and opt-in service-page styles, reusing existing Angular components and native HTML. No component framework, new theme engine or new abstraction was added. The detail route opts in via `.service-page`; shell palette adjustments are scoped to that route. Other pages retain their existing presentation.

Applied changes:

- Shared blue/ink/neutral colors, 8/16/24/32 spacing tokens and consistent heading/body/help roles.
- A single inquiry heading and state, followed by either available treatment actions or the existing final response.
- Plain information sections and read-only values; the response is the one emphasized content block.
- Existing required fields, document types, upload controls and action guards remain intact, with clearer labels and nearby explanations. Partial drafts can still be saved.
- Assignment, document checks, workflow map and history remain available through native disclosures. Employee review controls open when relevant.
- Existing response is read from the matching completed workflow transition history entry; no response model or workflow state was introduced.
- Loading and failed-load presentation is explicit, with the existing reload path offered as retry.

## Rules and source attribution

Official IGDS sources were inspected in the browser on 6 October 2026. These are design guidance, not a claim of legal compliance or official government endorsement.

| Classification | Guidance | Application |
|---|---|---|
| OFFICIAL | [IGDS typography](https://igds.gov.il/4988d5140/p/14bf03-typography) | Desktop 32px page heading, 24px section heading, 20px emphasis, 16px body and 14px supporting roles. Mobile headings reduce to fit. Rubik was not bundled: existing Segoe UI/Arial fallback remains, so typography adoption is partial |
| OFFICIAL | [IGDS primitive colors](https://igds.gov.il/4988d5140/p/94569b-color/b/13bc73) and [semantic colors](https://igds.gov.il/4988d5140/p/94569b-color/b/797d42) | Israel Blue 500 #0068F5 for primary actions, Blue 600 #0057CC for hover/active text, Blue 50 #EBF3FF for supporting surfaces, Dawn 900 #0C3058 for ink and neutral dividers. Darker secondary text preserves contrast |
| OFFICIAL | [IGDS forms](https://igds.gov.il/4988d5140/p/24285b-forms) | Group related fields, concise persistent labels and essential help, maximum two fields per desktop row and one on mobile, clear action consequence and bottom-of-section save/upload actions |
| OFFICIAL | [IGDS buttons](https://igds.gov.il/4988d5140/p/615f08-button) | Distinguishable primary and secondary actions, existing native buttons and semantic labels |
| RECOMMENDED | gov-il-ui skill: task-first information architecture and restrained surfaces | Reduce default cards; prioritize response/action; retain supporting information in disclosures |
| RECOMMENDED | gov-il-ui skill: RTL, accessibility and responsive verification | Logical spacing, isolated mixed-direction values, visible focus, native controls, real rendered desktop/tablet/mobile checks |

The 1128px content maximum, 600px/900px breakpoints and chosen disclosure grouping are project-specific decisions, not prescribed government dimensions. No government logo was copied.

## Rendered results and checks

- [Desktop, 1440px](detail-desktop.png), [tablet, 768px](detail-tablet.png), [mobile, 375px](detail-mobile.png), [mobile file list and keyboard focus](detail-mobile-files.png).
- All three inspected layouts had document scroll width equal to available client width: no horizontal page overflow. Tablet uses two field columns; mobile uses one. [Measured evidence](viewport-results.json).
- Boundaries at 599/601px and 899/901px also fit without overflow. [Evidence](breakpoint-results.json).
- Keyboard checks: document links are reachable with visible focus; Enter opens history; workflow disclosure and its text alternative remain available.
- axe 4.13.0: zero violations and zero incomplete checks in both draft and completed detail views at all three sizes. A desktop active-navigation contrast failure was found and corrected from Blue 500 to Blue 600. [Results](accessibility.json).
- Real browser E2E passed in an isolated local database copy: required form fields, two PDF uploads, blocked premature submission, Submitted → InTreatment → ClosedWithResponse, persisted response, external read-only view and history. [Result](e2e-result.json). The saved scenario ran through the supported in-app Playwright interface; the standalone CLI runner was not executed because worker/browser launch is restricted in this environment.
- Eleven real server validation and access checks passed against the isolated copy, including required field types, document gates and attachment access. No status was changed directly in the database.
- Strict Angular compilation and local production bundle completed successfully.
- Missing inquiry renders a clear failed-load message with back/retry controls rather than remaining on a loading message.

Automated checks and keyboard inspection are not a screen-reader audit or accessibility certification. Broader pages were inspected visually, not certified accessible.

## Scope and continuation

Changed production files: `client/src/styles.css`, `client/src/app/pages/case-detail/case-detail.html`, `client/src/app/pages/case-detail/case-detail.ts`. Test selectors in `checks/inquiry-flow.mjs` were adjusted for translated display labels, read-only values and the history disclosure. Business assertions and API status keys remain unchanged. No backend source, schema, workflow definition or permissions were changed.

Review this detail page before migration. If the hierarchy and visual restraint are accepted, apply these existing tokens and section patterns to the inquiry queue/creation form next, then organization and provider/task tables. Treat the visual workflow editor as a separate presentation follow-up. Do not redesign the workflow mechanism or generalize the foundation further without a concrete need.
