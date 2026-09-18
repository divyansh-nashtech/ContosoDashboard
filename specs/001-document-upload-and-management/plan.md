# Implementation Plan: Document Upload and Management

**Branch**: `001-document-upload-and-management` | **Date**: 2026-09-18 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-document-upload-and-management/spec.md`

## Summary

Add document upload, organization, retrieval, and sharing to the existing ContosoDashboard
Blazor Server application. Documents are uploaded from a new Documents page, validated
(extension allow list, declared content type, file signature, 25 MB limit), written to a
local directory outside `wwwroot` under a GUID file name, and described by a `Document`
record in the existing SQL database. Access is decided in a new `DocumentService` from the
uploader, the associated project's membership, explicit shares, and the Administrator role,
and is enforced again in the download endpoint. Storage sits behind `IFileStorageService`
so the local implementation can be exchanged for cloud object storage without touching
pages, services, or schema.

## Technical Context

**Language/Version**: C# 13 on .NET 9  
**Primary Dependencies**: ASP.NET Core Blazor Server, Entity Framework Core 9, Bootstrap 5  
**Storage**: Relational database through `ApplicationDbContext` for metadata; local file
system under `AppData/uploads` for file content  
**Testing**: xUnit for service-layer rules, manual verification of the acceptance scenarios
in the running application  
**Target Platform**: ASP.NET Core server application, modern desktop browsers  
**Project Type**: Single web application (`ContosoDashboard/`)  
**Performance Goals**: Document list within 2 s at 500 documents; search within 2 s; upload
of 25 MB within 30 s  
**Constraints**: Offline capable, no cloud SDKs, no rewrite of existing subsystems, mock
cookie authentication retained, development schema created by `EnsureCreated()`  
**Scale/Scope**: Internal dashboard — hundreds of users, a few thousand documents, six new
source files plus small edits to four existing ones

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | How this plan complies | Status |
|-----------|------------------------|--------|
| I. Security by Default | Every read and write goes through `DocumentService`, which resolves access from uploader, project membership, share records, and role before touching data; the download endpoint re-checks; files live outside `wwwroot` under GUID names; extensions are allow-listed | PASS |
| II. Layered Architecture | Pages call `IDocumentService`; the service calls `ApplicationDbContext` and `IFileStorageService`; `LocalFileStorageService` is the only class that touches `System.IO` | PASS |
| III. Consistency | Integer `DocumentId`, data annotations, `DateTime.UtcNow`, scoped `I<Name>Service` registrations, Bootstrap markup, existing roles and notification service | PASS |
| IV. Specification-Driven | Plan derives from the clarified specification; the research log records each decision against a requirement | PASS |
| V. Observable and Auditable | Uploads, downloads, updates, deletions, and shares are written to `DocumentActivity` with actor and UTC timestamp; every page action reports success or failure | PASS |
| Security Requirements | `Department` claim added to login so team-based checks work; identifiers from the browser are always paired with an access check; size and type checked before writing | PASS |
| Performance Standards | Queries are filtered, projected, and paged in the database; lists load related names with explicit `Include`; no per-row queries | PASS |
| Quality Requirements | Build must stay warning-clean for new code; service rules covered by unit tests; expected conditions return results rather than throw | PASS |

No deviations. Complexity Tracking is therefore empty.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-upload-and-management/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── document-service.md
│   └── file-storage-service.md
├── checklists/
│   └── requirements.md
├── spec.md
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs                     # new — metadata entity and category constants
│   ├── DocumentShare.cs                # new — share grant
│   └── DocumentActivity.cs             # new — audit record
├── Data/
│   └── ApplicationDbContext.cs         # edit — three DbSets, relationships, indexes
├── Services/
│   ├── IFileStorageService.cs          # new — storage abstraction
│   ├── LocalFileStorageService.cs      # new — local disk implementation
│   ├── DocumentService.cs              # new — validation, authorization, orchestration
│   └── DashboardService.cs             # edit — document count and recent documents
├── Controllers/
│   └── DocumentsController.cs          # new — authorized download endpoint
├── Pages/
│   ├── Documents.razor                 # new — upload, list, filter, search, share
│   ├── Index.razor                     # edit — Recent Documents widget, summary card
│   ├── ProjectDetails.razor            # edit — project documents section
│   └── Login.cshtml.cs                 # edit — add Department claim
├── Shared/
│   └── NavMenu.razor                   # edit — Documents entry
├── Program.cs                          # edit — service registration, controllers, limits
└── AppData/uploads/                    # runtime — created on demand, git-ignored

tests/
└── ContosoDashboard.Tests/
    ├── DocumentServiceTests.cs         # new — validation and authorization rules
    └── LocalFileStorageServiceTests.cs # new — path generation and round trip
```

**Structure Decision**: The application is a single ASP.NET Core project, so the feature
extends the existing `Models`, `Data`, `Services`, `Pages`, and `Shared` folders rather
than introducing a new project. One new folder, `Controllers`, is added because files
stored outside `wwwroot` need an endpoint that can run an authorization check before
streaming bytes; Blazor Server components cannot serve a file response. Unit tests live in
a separate `tests/ContosoDashboard.Tests` project so the web project keeps no test
dependencies.

## Phase 0 — Research

Open questions about the technology approach were resolved before design; see
[research.md](./research.md). Decisions in brief:

1. Local storage layout and the interface that abstracts it.
2. Ordering of disk write and database insert, and rollback on failure.
3. Blazor `InputFile` buffering and component state after upload.
4. Serving files from outside `wwwroot` with authorization.
5. Content validation in place of a virus scanner.
6. Category as text and identifiers as integers.
7. Search and filter shape that meets the 2-second budget.

## Phase 1 — Design

- [data-model.md](./data-model.md) — entities, fields, relationships, indexes, and the
  lifecycle rules from the clarification sessions.
- [contracts/document-service.md](./contracts/document-service.md) — the service surface
  used by pages and the controller, with the authorization rule for each operation.
- [contracts/file-storage-service.md](./contracts/file-storage-service.md) — the storage
  abstraction and the guarantees a future cloud implementation must keep.
- [quickstart.md](./quickstart.md) — how to build, run, and exercise the feature.

Post-design constitution re-check: PASS. The design adds no new dependency, keeps
authorization in the service layer, and leaves the storage boundary replaceable.

## Phase 2 — Task Generation Approach

`/speckit.tasks` derives tasks from this plan in dependency order: entities and context
first, then storage, then service, then the controller and pages, then integration points,
then tests and documentation. Tasks are grouped by the user stories in the specification so
that the MVP — user stories 1 and 2, upload and retrieval — can be completed and
demonstrated before sharing, project views, and maintenance are started.

## Complexity Tracking

No constitution violations; no entries.
