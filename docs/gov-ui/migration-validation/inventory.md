# Remaining UI migration inventory

Read foundation-validation/report.md before implementation. Classification is based on the actual lazy routes, shell navigation and rendered screens, not hypothetical pages.

| Route / interaction | Class | Existing pattern / scope |
| --- | --- | --- |
| `/` dashboard | A | Operational summary + existing queue table, existing status distribution and activity. Prioritize existing attention counts; introduce no metrics. |
| `/providers` list | A | Queue/list. |
| `/providers` open record / create / edit | A | Existing inline entity form + related inquiries. Preserve the same selection and save handlers. No separate detail route exists. |
| `/tasks` | A | Work queue with existing open-only filter, result, due date, note and inquiry link. No separate task-detail route exists; actions remain on inquiry detail. |
| `/organization` shell / members / account form | A | Administration + list + form. Preserve roles, access scopes, unit selection and conditional provider assignment. |
| `/organization` unit editor | A | Existing configuration form; preserve identity and parent options. |
| `/organization` organizational tree interaction | B | Keep the existing tree layout/interaction. Indented flat rows lack branch navigation/collapse, and deeper hierarchies need an explicit browsing decision. Only shared shell consistency applies. |
| `/processes?section=routing` rule list/edit/test | A | Existing administration list and forms; conditions already use labelled native controls. Preserve match mode, ordering, field/operator catalog, rule versions, immediate save and preview. |
| `/processes` other configuration sections | Approved | Already opt into the foundation. No redesign; inspect shared consistency only. There is no separate process/form-list route. |
| `/cases`, `/cases?new=1`, `/cases/:id` | Approved | Preserve approved list/form/detail. Inspect shared consistency only. |
| `/routing` | Redirect | Existing redirect to process-scoped routing; no independent UI to migrate. |
| Sign-in/demo chooser + shell/navigation/messages | A | Shared form/list/navigation primitives; preserve account selection and authentication behavior. |
| Unknown routes | Redirect | No independent UI. |

## Category B decision — organization browsing

User goal: locate a unit, understand its ancestors/descendants and open its people/permissions. Current problem: every unit is always visible in a flat indented list, so a larger tree becomes difficult to scan and keyboard navigation has no branch model. Applying table/form colors cannot resolve that interaction.

Options: (1) retain the current rows and add expandable branches/search; (2) introduce a master tree with a separate selected-unit inspector. Recommended: option 1, a conservative extension, after approval. This would affect a hierarchy primitive, not the approved operational page patterns. No hierarchy redesign is included in this migration.
