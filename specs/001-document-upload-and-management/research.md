# Research: Document Upload and Management

**Feature**: 001-document-upload-and-management
**Date**: 2026-09-18
**Input**: [spec.md](./spec.md), [.specify/memory/constitution.md](../../.specify/memory/constitution.md)

Each entry records the question raised during planning, the decision taken, the reasoning,
and the alternatives that were rejected.

## R1 — Where uploaded files are stored

**Question**: Where do files live in an offline training environment, and how is the layout
chosen so that a move to cloud object storage costs nothing?

**Decision**: Files are written under `ContosoDashboard/AppData/uploads` using the relative
path `{userId}/{projectId or "personal"}/{guid}{extension}`. Only the relative path is
stored in the database; the root comes from configuration
(`DocumentStorage:RootPath`, default `AppData/uploads`).

**Rationale**: `AppData` is outside `wwwroot`, so the static file middleware will never
serve it. Partitioning by user and project keeps directories small and makes ownership
visible on disk. The same relative path is a legal blob name, so migrating means copying
the tree and changing one registration. Storing a relative path keeps the database portable
between machines.

**Alternatives rejected**:
- Files under `wwwroot/uploads` — anything with the URL could read them, defeating the
  access rules (Constitution I).
- Bytes in a database column — simple, but loads whole files into memory and makes the
  training database awkward to copy; it also has no equivalent in blob storage.
- A flat directory of GUID names — no ownership signal on disk, and directory listings grow
  without bound.

## R2 — Order of operations during upload

**Question**: In what order are the file and the metadata record written, and what happens
when one of them fails?

**Decision**: Generate the GUID-based relative path, write the file to disk, then insert the
`Document` row inside the same service call. If the insert throws, delete the file that was
just written and surface the failure. If the write throws, no row is created and a partial
file is deleted.

**Rationale**: The row carries the path, so the path must exist first. Writing the file
first means the only inconsistency a failure can produce is an orphaned file, which the
compensating delete removes; the reverse order would produce a row pointing at nothing,
which breaks every later download. This satisfies FR-009 and FR-011c.

**Alternatives rejected**:
- Insert the row, then write the file, then update the row with the path — three database
  operations, a window in which the row has an empty path, and a unique index on path that
  a second concurrent upload would collide with.
- A transaction spanning disk and database — not available; the file system does not take
  part in the database transaction.

## R3 — Receiving the file in Blazor Server

**Question**: How is `InputFile` used without hitting stream disposal and component state
problems?

**Decision**: Read file name, size, and content type into local variables before opening
the stream; copy `OpenReadStream(MaxFileSizeBytes)` into a `MemoryStream`; rewind it; clear
the `IBrowserFile` reference and increment a counter bound to `@key` on the `InputFile`
component so it re-renders empty after a successful upload.

**Rationale**: The browser file stream is tied to the circuit and is disposed when the
component re-renders, so touching it after an `await` that triggers a render throws. Copying
first removes the dependency. `OpenReadStream` also defaults to 512 KB, so the 25 MB limit
must be passed explicitly. Re-keying is the documented way to reset the input element,
which otherwise keeps the previous selection.

**Alternatives rejected**:
- Streaming `IBrowserFile` straight to disk — fewer copies, but the stream is disposed
  mid-write when the progress update re-renders the component.
- A separate form post to an MVC endpoint — leaves the Blazor page, loses the progress
  indicator, and duplicates validation.

At 25 MB the buffered copy is acceptable for an internal dashboard; a cloud implementation
can stream directly to the blob client without changing the service surface.

## R4 — Serving files that live outside `wwwroot`

**Question**: How does a user download or preview a file that static file middleware cannot
serve?

**Decision**: Add `DocumentsController` with `GET /api/documents/{id}/download` and
`GET /api/documents/{id}/preview`. Both resolve the current user from claims, ask
`IDocumentService` whether that user may read the document, then stream the file with its
stored content type. Download sets a `Content-Disposition` attachment header with a
sanitized file name; preview sets an inline header and is refused for types other than PDF,
JPEG, and PNG.

**Rationale**: An MVC controller is the only place in this application that can return a
file response with headers; Blazor components render markup. Re-checking authorization in
the endpoint is the protection against a user guessing an identifier (FR-018). Controllers
also require `app.MapControllers()`, one line in `Program.cs`.

**Alternatives rejected**:
- `NavigationManager` to a static path — would require moving files into `wwwroot`.
- Base64 data URLs pushed through the circuit — memory-heavy and breaks the browser's own
  PDF viewer for larger files.

## R5 — Content validation instead of virus scanning

**Question**: The stakeholders require malware scanning, and the training environment has
no scanner. What is implemented?

**Decision**: A three-part check inside the upload pipeline: the extension must be on the
allow list (`.pdf .doc .docx .xls .xlsx .ppt .pptx .txt .jpg .jpeg .png`), the declared
content type must be the one expected for that extension, and the leading bytes of the file
must match that type's signature (`%PDF`, `PK\x03\x04` for Office Open XML, `\xD0\xCF\x11\xE0`
for legacy Office, `\xFF\xD8\xFF` for JPEG, `\x89PNG` for PNG; text is accepted without a
signature). The check lives in one method so a scanning service can be introduced behind it
without touching callers.

**Rationale**: Extension and content type are supplied by the client and can be forged; the
signature check is the part an attacker cannot control from the file picker. Together they
meet FR-011 and FR-011a within the offline constraint.

**Alternatives rejected**:
- Trusting the browser's content type alone — trivially forged.
- Shelling out to a local antivirus binary — not present in the training environment and
  not portable.

## R6 — Keys and category representation

**Question**: Integer or GUID primary key, and enum or text for category?

**Decision**: `DocumentId` is an integer identity, matching `UserId`, `ProjectId`, and
`TaskId`. `Category` is a string, validated against a static list of the six allowed values
exposed as `DocumentCategories.All`.

**Rationale**: Consistency with the existing schema (Constitution III) and with the
stakeholder's explicit technical constraints. Text categories are readable in the database,
sort and filter without a lookup, and adding a category later does not renumber anything.

**Alternatives rejected**:
- GUID keys — no benefit here and inconsistent with every other table.
- A C# enum stored as an integer — the specification lists categories as display strings
  and an enum would need a display-name map on every screen.

## R7 — Meeting the list and search budgets

**Question**: What query shape keeps the 2-second budget at 500 documents?

**Decision**: One query per view with explicit `Include` of uploader and project, filters
and sorting applied in the database via `IQueryable`, and a fixed page size of 25 with skip
and take. Search matches title, description, tags, uploader display name, and project name
with case-insensitive `Contains`, and is composed onto the same access-scoped query so a
user can never match a document they cannot read. Indexes are created on `UploadedByUserId`,
`ProjectId`, `UploadedDate`, `Category`, and a unique index on `FilePath`.

**Rationale**: Filtering in the database keeps the result set small; a single query with
`Include` avoids the per-row lookups that a naive list would produce. Access scoping applied
before the text predicate makes leaking a document through search impossible by
construction (FR-015).

**Alternatives rejected**:
- Loading all documents and filtering in memory — fails NFR-002 as the table grows and
  contradicts the constitution's performance standards.
- Full-text search — the database in use during training does not offer it, and `Contains`
  over a few thousand rows is well inside the budget.

## R8 — Notification reuse

**Question**: Do document notifications need their own mechanism?

**Decision**: Reuse `INotificationService.CreateNotificationAsync` with the existing
`NotificationType.ProjectUpdate` for project document additions and `SystemAnnouncement`
for shares and deletions of shared documents.

**Rationale**: The notification list, unread count, and page already work; adding enum
values would require touching the shared component and the existing database seed for no
user-visible gain. The title and message carry the document context.

**Alternatives rejected**:
- New `NotificationType` values — a schema-affecting change to an existing table for
  cosmetic benefit.
- A separate document notification table — duplicates a working subsystem.
