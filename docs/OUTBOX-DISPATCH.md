# Outbox dispatch checkpoint — 8 October 2026

Implemented using the existing Outbox table. No broker, polling/callback integration, event fan-out, or replacement workflow engine was added. `Engine.cs` was not modified.

## Changes

- **Independent lanes:** business commands and mail/in-app notification deliveries have separate capacities, default **5 per lane per worker instance**. Each slot takes another due row as soon as it finishes; a slow slot does not hold up a whole batch. Multiple instances increase aggregate concurrency; these are not global deployment limits.
- **Claim before delivery:** an atomic conditional UPDATE changes `pending` (or expired `processing`) to `processing`. It checks the existing `Version` and assigns `ClaimId`, `ClaimedAt`, `LeaseUntil`. Competing claims cannot both succeed. `Version` advances with ownership changes; `ClaimId` identifies a particular attempt across heartbeat updates.
- **Lease and renewal:** default lease 30 seconds, renewed every 10 seconds in an independent scope/context. An expired lease can be reclaimed. Renewal failure cancels the attempt. Shutdown leaves an unfinished claim recoverable rather than acknowledging it.
- **Fenced acknowledgement:** only an unexpired, matching claim can mark its row sent. A conditional acknowledgement and the existing single `SaveChanges` for business result/state/history/new outbox messages share a transaction. A workflow concurrency failure rolls back the acknowledgement. No database transaction spans a target call.
- **Retry:** owned failed attempts retain the original exponential delay (`min(3600, 2^min(attempts,10) × 5 seconds)`). Business attempts are blocked after five failures; notifications/mail continue retrying as before. An expired/replaced owner cannot schedule a retry over the new owner.
- **Existing recovery:** `Recover` and the local target's atomic `Receipts` remain the source of recovery after a target commit whose reply was lost. The `OperationId` stays the Outbox ID. Notification's unique `OutboxId` index remains unchanged.
- **Late business results:** `WaitingState`/provider checks remain. Newly enqueued jobs also capture the waiting inquiry version, preventing an older operation from completing a later visit to the same state. Legacy queued jobs without that optional value retain their existing state check. Case's existing concurrency token still rejects changes made during the target call.
- **Command versus event:** a business command has one valid claim owner and a persisted result that executes the configured completion. Mail/notification delivery remains reliable, acknowledged Outbox work; it is not an untracked fire-and-forget task. Multiple subscribers/fan-out are outside this change.

Default limits can be overridden with the existing .NET configuration system under `Outbox`: `BusinessConcurrency`, `EventConcurrency`, `PollInterval`, `LeaseDuration`, `AttemptTimeout`. Default attempt timeout is 60 seconds.

**Timeout limitation:** cancellation is cooperative. Adapters must honor the cancellation token (including HTTP calls) and support idempotent recovery. This worker deliberately does not detach an uncooperative handler and release its concurrency slot while it still uses the scoped context. Timeout/cancellation never proves the remote operation failed. A target that ignores cancellation can occupy its business slot; it cannot occupy an event slot. Exactly-once external side effects require the target's OperationId/receipt contract, not merely an Outbox claim. The mail sender remains the existing placeholder; tests prove dispatch, not delivery to a real mailbox.

## Schema and rollout

SQL Server migration: `20261007221046_OutboxLease` adds three nullable columns. Existing SQLite demo data is preserved by `DemoUpgrade`. Stop all old workers before restarting with this version: an older binary does not understand leases. Production SQL Server execution was not tested in this local SQLite environment.

## Validation performed

| Check | Result |
|---|---|
| A: held business commands while mail and notifications drain | PASS, real dispatcher / isolated SQLite / instrumented test adapter |
| B: separate configured concurrency caps | PASS, measured business 2 / event 3, neither exceeded |
| C: competing claims | PASS, separate contexts racing the same row; one owner |
| D: abandoned lease and replacement worker | PASS, expired ownership reclaimed; actual BackgroundService shutdown/replacement exercised |
| E: stale acknowledgement / state / version | PASS, old owner rejected; re-entered waiting state rejected; concurrent state change rolls back acknowledgement and history |
| F: backoff / fifth-failure blocking / notification idempotency | PASS |
| G: heartbeat during held target calls | PASS, ownership renewed beyond several lease periods |
| Cooperative attempt timeout | PASS, retry/backoff without false workflow completion |
| Local target normal / reject / interruptOnce / slow | PASS through API + existing runtime; before/after and exactly one target mutation where expected |
| Slow LocalAddressTarget + real inquiry notification | PASS, notification visible while inquiry still waiting and target address unchanged |
| Generic field/process alias | PASS |
| Required documents API checks | 11 PASS |
| Initiation authorization checks | 10 PASS |
| History audience / ownership checks | PASS |
| Presentation checks | 10 PASS |
| Production backend build | PASS |
| SQL Server migration script generation | PASS; database execution not tested locally |
| Main local restart / existing completed inquiry #15 | PASS, final status, response, history and persisted result preserved |

Latest isolated API inquiries: normal #61, reject #62, interruptOnce #63, slow #64, generic mapping #65. Detailed results: `business-actions/api-results.json`.

**Not all legacy `/checks` scripts pass.** `integration.py:46`, `organization.py:44`, and `payload.py:60` still assume Admin can create inquiries and fail with HTTP 403. The same scripts fail at the same statements against the unchanged pre-dispatch binary (`business-actions-checkpoint`, isolated port 5082). This is a pre-existing incompatibility with the approved management-only Admin policy, not a dispatch regression. Authorization was not weakened and those tests were not changed. Baseline logs are `outbox-legacy-baseline-{integration,organization,payload}.txt`. Their later assertions remain unexecuted.

The existing browser E2E suite and Angular unit tests were not rerun for this infrastructure change. The prior `spawn EPERM` limitation remains; no new browser-suite pass or full accessibility certification is claimed.

## Reproduce

From the application root (PowerShell):

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' run --project checks/outbox/Outbox.Checks.csproj --no-restore --no-launch-profile
```

The console check uses an automatically created temporary database, real EF/worker/runtime, test-only gates and counters. It does not touch live inquiries. A first restore may be needed on another machine.

For the API checks, run the Development + Demo server on **isolated** port 5081 with its own content root and data, then:

```powershell
& 'C:/Users/shapira/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' checks/business-actions.py http://127.0.0.1:5081
& 'C:/Users/shapira/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' checks/inquiry-required.py http://127.0.0.1:5081
& 'C:/Users/shapira/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' checks/initiation.py http://127.0.0.1:5081
& 'C:/Users/shapira/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' checks/history-audience.py http://127.0.0.1:5081
```

`business-actions.py` extends the existing local-target fixture: `slow` delays ten seconds outside its write transaction; `reject` and `interruptOnce` are reused unchanged. These checks create isolated fictional test configuration/inquiries. Do not point mutating checks at live demo port 5080.

## Manual observation

On the isolated demo, use its existing address-change process with the controlled target's `slow` mode. Submit as the external user, approve as the employee, and refresh inquiry details: the waiting state remains while its approval notification is already available. After ten seconds the configured completion, public response and history appear. The console checks, rather than a UI counter, prove claim exclusivity, concurrency limits and lease recovery. No new UI or administrative controls were added.
