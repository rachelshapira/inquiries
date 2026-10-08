# Four-screen gov-il-ui foundation review

Date: 6 October 2026. Scope: Inquiry Detail, Inquiry Queue, Create Inquiry and Workflow Builder only. Inquiry Detail remains the reference implementation. This is not an application-wide migration or an accessibility certification.

## Visual evidence

Each AFTER image is a full-page capture of the rendered application. Desktop comparisons show the same first viewport before and after; the separate desktop files include the whole page. Native browser frames were stitched at observed scroll positions, with fixed navigation included once. No application content was fabricated for the screenshots.

| Screen | Before / After desktop | Full desktop, 1440px | Tablet, 768px | Mobile, 375px |
| --- | --- | --- | --- | --- |
| Inquiry Detail | [Comparison](comparison-detail.png) | [Desktop](after-detail-desktop.png) | [Tablet](after-detail-tablet.png) | [Mobile](after-detail-mobile.png) |
| Inquiry Queue | [Comparison](comparison-queue.png) | [Desktop](after-queue-desktop.png) | [Tablet](after-queue-tablet.png) | [Mobile](after-queue-mobile.png) |
| Create Inquiry | [Comparison](comparison-create.png) | [Desktop](after-create-desktop.png) | [Tablet](after-create-tablet.png) | [Mobile](after-create-mobile.png) |
| Workflow Builder | [Comparison](comparison-workflow.png) | [Desktop](after-workflow-desktop.png) | [Tablet](after-workflow-tablet.png) | [Mobile](after-workflow-mobile.png) |

[Four mobile screens together](mobile-comparison.png). Original desktop baselines: [Detail](before-detail.png), [Queue](before-queue.png), [Create](before-create.png), [Workflow](before-workflow.png).

## Shared foundation and reusable primitives

The four pages opt into the existing shared stylesheet through `service-page`; other screens retain their current styling. There is no second component library or workflow abstraction.

- Page containers and headers: consistent alignment, spacing and action hierarchy. Detail has an intentional 1128px maximum content area and a 72ch paragraph limit. List and administration workspaces can use up to 1600px. Wider space supports field grouping and scanning rather than long paragraphs.
- Typography: locally bundled Rubik variable font with Hebrew support; 32px page title, 24px section title, 20px subheading, 16px body/control text and readable 14px secondary labels. Labels remain quieter than values. The fallback is Segoe UI, Arial, sans-serif.
- Color and controls: government blue primary actions, restrained navy text, readable muted labels, white sections, quiet separators, shared native form controls and a visible keyboard focus treatment. Shadows and nested decorative cards were removed from these screens.
- Shared `businessTitle()` presentation helper removes recognized generated E2E/TEST markers without modifying original stored titles, search data or identifiers. Public inquiry numbers are displayed separately. `stateLabel()` translates the existing known runtime statuses; configured labels remain intact.
- Shared semantic table, empty-result treatment, form grouping, helper/error text, native expandable sections and action styles. CSS primitives were reused rather than introducing wrapper components solely for architecture.
- Existing `ProcessGraph` gains selected-transition presentation, scoped colors and compact long-name labels. Full state names remain in accessible names, SVG titles and the inspector. Its layout and workflow semantics remain unchanged.

## Page-specific result

### Inquiry Detail

The refined structure was retained. A meaningful business title replaces generated test prefixes, with inquiry number and process metadata on a separate line. Metadata contrast and font size are stronger without competing with values. The response remains prominent; closed inquiries show no unnecessary action area. Secondary ownership, history and process details remain expandable. Related fields use a wider two-column desktop layout while narrative content retains a comfortable reading width.

### Inquiry Queue

The list now has one clear title, a primary create action, visible search/filter labels and a results count. The desktop table keeps five useful columns: public number, subject/process, provider, human-readable status and updated date. The round column was removed from presentation. Titles link directly to inquiry details. On mobile, the same semantic table becomes compact stacked rows; it is not replaced with separate card components. Original search and filter predicates remain unchanged.

### Create Inquiry

The existing form opens as a focused form view with a back/cancel action. Service provider and inquiry type precede subject and dynamic request fields. Required-for-submission and optional fields are explained without preventing existing partial drafts. Existing validation is shown beside the relevant field. The documents section exists only when the selected definition includes document requirements; its copy explains the existing upload-after-creation flow. Public selection labels no longer expose version numbers. Dynamic definitions and backend contracts are unchanged.

### Workflow Builder

Process/version identity and published/draft context remain visible. The map is the primary workspace, beside the inspector on desktop and above it at narrower widths. Selected states and transitions have stronger visual indication. Transition configuration is grouped into basic settings, conditions/routes, permissions, effects and a collapsed internal identifier. Existing conditional routing, guards, role controls, effects, validation, simulation, publishing and version behavior remain available. Configuration navigation remains the existing editable section navigation; it is not presented as an official non-interactive step indicator. A small focus fix makes “back to map” focus the actual selected canvas state.

## Realistic states reviewed

Visual review included native screenshots and rendered DOM/keyboard inspection, not only automated checks.

| Screen | States exercised | Additional evidence |
| --- | --- | --- |
| Detail | Active with actions; closed with response; documents; no documents; long Hebrew with mixed reference/email text; missing optional fields | [Active actions](state-detail-active-actions.png), [Long Hebrew](state-detail-long-hebrew-mobile.png), [No documents](state-detail-no-documents-mobile.png), [Missing optional data](state-detail-missing-optional-mobile.png) |
| Queue | Multiple statuses; long Hebrew titles; filtered results; no results | [Filtered](state-queue-filtered.png), [Empty](state-queue-empty.png), [Long titles](state-queue-long-title-mobile.png) |
| Create | Existing payment definition with required documents; a simple definition without documents; existing validation errors | [Simple form](state-create-simple.png), [Validation](state-create-validation.png) |
| Builder | Existing complex workflow; simple three-state workflow; selected state and transition; long state names; conditions; validation errors; draft and published context; existing preview | [Selected transition desktop](state-workflow-selected-transition-desktop.png), [Tablet](state-workflow-selected-transition-tablet.png), [Mobile](state-workflow-selected-transition-mobile.png), [Conditions](state-workflow-conditions-mobile.png), [Long state name](state-workflow-long-name-mobile.png), [Validation](state-workflow-validation.png), [Simple draft](state-workflow-simple-draft.png) |

All twelve final representative captures at 1440/768/375px had no page-level horizontal overflow, and loaded Rubik successfully. Complex workflow maps retain intentional local canvas scrolling. On mobile, selecting a transition moves focus to its inspector; returning to the map focuses the selected state. Native disclosures work with Enter. The existing discard dialog wraps keyboard focus in both directions.

Long titles and Hebrew content wrap; dates, numbers and email/reference text were inspected in RTL context. Empty and irrelevant document areas are absent. Selected states retain their complete accessible names even when their visible canvas names are shortened. The complex administration screen is intentionally denser and longer than operational screens.

Some additional state screenshots were captured in an isolated QA environment and include its audit controls. The final twelve representative captures exclude those controls. Additional-state captures can precede the final wording-only cleanup; they document configuration/state behavior, while the final representative images document the visual baseline.

## Regression and accessibility results

- Final strict Angular compilation and production asset build: passed.
- Existing integration checks: 12 passed; payload/routing checks: 10 passed; organization checks: 10 passed; required-field/document/access checks: 11 passed. Total: 43 checks.
- Presentation helper checks: 7 passed.
- Existing saved attachment E2E scenario: passed through the supported in-app browser adapter. Inquiry #32 was created with the original generated title retained in data, validated, uploaded with required documents, submitted, found in the employee queue, moved through the existing transitions, responded to, closed and viewed by the external user. History was verified. [E2E evidence](e2e-result.json).
- The standalone Playwright CLI was previously blocked by the environment; it was not claimed as a CLI pass. The saved scenario used the browser adapter and real runtime, without directly changing database status.
- Angular unit-test CLI was attempted and failed at `spawn EPERM` in esbuild before test execution. Unit tests therefore remain unverified by that runner; this is not an assertion failure.
- Axe 4.13.0: 34 recorded audits, zero reported violations. [Full axe results](axe-results.json). Workflow audits contain incomplete contrast checks on SVG labels and, in expanded conditions, partially obscured labels. They must not be interpreted as a complete automated accessibility pass.
- Manual computed-color checks: primary blue on white 4.89:1; muted text on white 6.34:1; muted text on selected pale blue 5.67:1; dark state text on selected pale blue 11.90:1 and terminal pale green 12.10:1. Keyboard focus, native labels, table headers, status text and responsive behavior were checked separately. These checks do not establish full screen-reader or accessibility certification.

Regression scenarios and additional configuration fixtures ran against an isolated snapshot on port 5081. The user-facing database and backend behavior on port 5080 were preserved. Backend code, contracts, workflow schema, authorization, routing, persistence, concurrency, business validation and versioning were not changed.

## Official guidance and typography decision

The existing gov-il-ui skill remains the baseline. Official IGDS references were inspected for [typography](https://igds.gov.il/4988d5140/p/14bf03-typography), [tables](https://igds.gov.il/4988d5140/p/06a72e-table), [forms](https://igds.gov.il/4988d5140/p/24285b-forms) and [buttons](https://igds.gov.il/4988d5140/p/615f08-button). Applied principles include Hebrew Rubik hierarchy, labelled native controls, readable semantic tables, restrained color, clear actions, RTL layout and progressive disclosure.

Rubik was obtained from the official [Google Fonts Rubik repository](https://github.com/google/fonts/tree/main/ofl/rubik), which includes Hebrew metadata. The font and [SIL Open Font License](https://github.com/google/fonts/blob/main/ofl/rubik/OFL.txt) are bundled locally, including the copyright/license file. No runtime request to an external font host or new package dependency is required. The build copies these assets to the existing static-file directory. The typography decision is resolved.

## Skill ambiguities and changes

No gov-il-ui skill changes were made.

- Available guidance does not establish a universal page content maximum. The chosen detail reading width and broader list/admin workspace are project layout decisions, not claims of an official fixed width.
- Referenced grid breakpoint guidance differs around 768/784px. Existing project responsive boundaries were retained and inspected at the requested dimensions instead of migrating every screen to a new breakpoint system.
- A default preference against decorative cards does not prohibit meaningful map/inspector grouping in complex administration. Those workspace boundaries were retained, while nested decorative containers were flattened.
- The official non-clickable Step Indicator is not interchangeable with the builder's existing clickable configuration navigation. Its behavior was retained.

These were application decisions within the skill's principles; none required an application-specific rule to be added to the skill.

## Remaining screens — not migrated

**A. Mostly mechanical migration using established patterns:** service provider list/detail, basic administration lists/forms, review-task list/detail and routing-rule list/edit controls. They can reuse headers, tables, statuses, field grouping, validation, disclosures and action styles while keeping their behavior. Each still needs rendered responsive verification.

**B. Individual UX/design decisions:** dashboard information prioritization, organization hierarchy and permission editing, complex routing authoring/preview, review-task operational prioritization if expanded, and administration screens with multiple competing workspaces. Their information architecture should be reviewed before applying styles. Classification is a planning assessment, not permission to migrate these screens now.

## Remaining limitations

The complex map intentionally scrolls inside its workspace, and expanded transition conditions produce a long mobile inspector. This preserves generic configuration capability; mobile is usable but desktop remains the preferable authoring environment. Arbitrary malformed/demo business titles are not silently rewritten: the presentation helper strips recognized technical test markers only. Search continues to operate on original stored titles, as requested. Existing unknown configured workflow labels remain unchanged. Standalone Angular unit and Playwright runner verification requires resolving the environment's process-launch restriction. Axe incomplete items and comprehensive assistive-technology testing remain outside the claim made here.

The foundation is ready for review across DETAIL + LIST + FORM + COMPLEX ADMIN. No additional page migration was performed.
