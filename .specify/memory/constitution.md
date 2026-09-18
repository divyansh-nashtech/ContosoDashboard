# ContosoDashboard Constitution

## Core Principles

### I. Security by Default

Every feature treats employee data as confidential company information. Authorization is
enforced in the service layer before any data access, never in the UI alone. Every read or
write of a record that belongs to a user, a project, or a team must verify that the
requesting user is allowed to perform that action, and must fail closed when the check
cannot be satisfied. User-supplied input — file names, search terms, metadata — is
validated against an allow list before it reaches storage, the database, or the browser.
Files are never served from a web-accessible directory.

### II. Layered Architecture with Stable Boundaries

The application keeps four layers: Razor pages and components (presentation), services
(business rules and authorization), Entity Framework entities and `ApplicationDbContext`
(data), and infrastructure adapters such as file storage. Pages never touch
`ApplicationDbContext` directly; they call a service interface. Infrastructure that may be
replaced later (local disk today, cloud object storage tomorrow) sits behind an interface
registered in dependency injection, so swapping an implementation requires no change to
pages, services, or schema.

### III. Consistency with the Existing Application

New code follows the conventions already present in ContosoDashboard: integer primary keys
named `<Entity>Id`, data annotations for validation and length limits, `DateTime.UtcNow`
for timestamps, `I<Name>Service` interfaces registered as scoped services, Bootstrap 5
markup, and the existing mock cookie authentication with role claims. A feature that would
require rewriting existing subsystems is out of scope; extend them instead.

### IV. Specification-Driven Change

Work begins with a specification, not with code. Requirements are written as testable
statements, ambiguities are resolved before planning, and the plan and task list are
derived from the approved specification. Implementation that discovers a gap in the
specification updates the specification first.

### V. Observable and Auditable Behavior

User-visible outcomes are explicit: every operation reports success or failure with an
actionable message. Security-relevant actions — uploads, downloads, deletions, and shares —
are recorded with the acting user, the affected record, and a UTC timestamp so
administrators can answer who did what and when.

## Security Requirements

- Authentication uses the existing cookie scheme; claims must always include
  `NameIdentifier`, `Name`, `Email`, `Role`, and `Department`.
- Authorization uses the four existing roles — Employee, TeamLead, ProjectManager,
  Administrator — and the project membership tables. No new role system.
- Object identifiers received from the browser are treated as untrusted: every lookup by id
  is paired with an ownership or membership check (protection against insecure direct
  object reference).
- Uploaded content is validated on extension, declared content type, and size before it is
  written, and is stored under a server-generated GUID name so that user input never forms
  a file-system path.
- Secrets and connection strings live in configuration, never in source.
- The security response headers already configured in `Program.cs` must remain in place.

## Performance Standards

- Interactive list and detail pages render within 2 seconds for the data volumes the
  dashboard supports (hundreds of records per user).
- Search returns results within 2 seconds.
- Queries are filtered and paged in the database, never in memory; a page that shows N rows
  must not issue N additional queries.
- Large payloads are streamed rather than buffered whole where the framework allows it, and
  request size limits are enforced before reading a request body.

## Quality Requirements

- The solution builds with no errors and no new warnings.
- Service-layer rules — validation, authorization, and state transitions — are covered by
  automated tests; UI-only concerns are verified manually against the acceptance scenarios
  in the specification.
- Public service methods return explicit results (`bool`, a result object, or `null` for
  "not found or not permitted") instead of throwing for expected conditions.
- Code is self-explanatory: intention-revealing names, small methods, no commented-out code.

## Technical Governance

- Stack: .NET 9, ASP.NET Core Blazor Server, Entity Framework Core, Bootstrap 5.
- The development database is created by `EnsureCreated()` at startup; schema changes
  therefore require recreating the local database file rather than a migration during
  training.
- New NuGet dependencies require justification: prefer the framework and what the project
  already references.
- The feature must run fully offline — no cloud service is required at development time.

## Governance

This constitution governs all work in this repository and supersedes ad hoc preference.
Specifications, plans, and task lists are checked against these principles before
implementation begins, and the implementation is checked against them before the work is
considered complete. A deviation is permitted only when it is recorded in the plan's
Complexity Tracking section together with the simpler alternative that was rejected and the
reason it was insufficient. Amendments are made by editing this file, stating the rationale,
and incrementing the version: MAJOR for a removed or redefined principle, MINOR for a new
principle or section, PATCH for clarifications.

**Version**: 1.0.0 | **Ratified**: 2026-09-18 | **Last Amended**: 2026-09-18
