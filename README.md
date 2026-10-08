# Inquiry Workflow Platform

A generic, configuration-driven workflow engine for managing inquiries (ניהול פניות).
One engine runs many different processes — each process is a JSON definition
(states, transitions, fields, documents), not new code.

## What it is

- **Not a single inquiry app — a platform.** The same engine runs any process you
  define: document submission, agreement renewal, address change, and more.
- **State machine per process.** Each inquiry advances through configured
  transitions, with role-based permissions, review rounds, and audit history.
- **Dynamic forms** defined per process, validated server-side.
- **Business actions** let a process call an external system, with the result
  driving the next transition.

## Architecture

```
Client (Angular)  ->  API  ->  Engine  ->  Outbox  ->  Worker  ->  Target (external)
```

| Layer | Role |
|---|---|
| `server/Engine.cs` | The state machine: transitions, guards, permissions, history |
| `server/Domain.cs` | Entities; a process definition is stored as `DefinitionJson` |
| `server/Infrastructure/BusinessActions.cs` | Business-action dispatch and shared completion |
| `server/NotificationWorker.cs` | Drains the Outbox (notifications, mail, business actions) |
| `server/Infrastructure/OutboxLease.cs` | Claim / lease / fenced acknowledgement |
| `server/Infrastructure/*` | Adapters, RabbitMQ publisher, callback API, local demo target |
| `client/` | Angular UI |
| `checks/` | Acceptance and regression checks |
| `docs/` | Design and planning documents |

### Reliability model

- **Outbox pattern:** inquiry state and the "what to send" note are saved together
  (atomic), then a worker delivers them.
- **Claim + lease + heartbeat:** a worker owns a delivery attempt for a bounded time;
  a crashed worker's work is reclaimed, not stuck.
- **Recover + receipts:** the external target records a receipt per `OperationId`,
  so a lost reply never causes double execution.
- **Asynchronous dispatch (planned/extending):** for slow targets, the worker
  publishes a request and releases; the result returns via a secured callback API
  or scheduled polling. See `docs/`.

## Build and run

Requires **.NET SDK** and **Node.js**.

```powershell
# Server
cd server
dotnet build
dotnet run

# Client
cd client
npm ci
npm run build
```

The database (SQLite demo) is created and seeded automatically on first run;
`App_Data/` is gitignored and not committed.

## Configuration

Secrets and environment settings (RabbitMQ credentials, callback keys, mail
transport) come from local configuration, never from source. The mail sender is
currently a placeholder (`NullMailSender`) — swap in SMTP/SES/Graph without
touching the engine or worker.

## Status

- Human workflow (forms, reviews, approvals, permissions, history): working.
- Business action with local target (synchronous): working and verified.
- Asynchronous dispatch over RabbitMQ with callback: implemented; see `docs/`
  for the design and acceptance plan. Not connected to a real external system.
