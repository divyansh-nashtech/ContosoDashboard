# Tasks: Document Upload and Management

**Feature**: 001-document-upload-and-management
**Date**: 2026-09-18
**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [data-model.md](./data-model.md),
[contracts/](./contracts/)

**Conventions**

- `[P]` marks tasks that touch different files and can be worked in parallel.
- Each task names the file it changes and the requirement it satisfies.
- Paths are relative to the repository root.

## Implementation Strategy

**MVP first (Tasks: T001 - T038)** — Setup, Foundational, User Story 1 (upload), and User
Story 2 (browse, search, download). Completing this range gives a demonstrable feature: an
employee can upload a document and find, preview, and download it again, with every access
authorized and audited. Stop here, test the acceptance scenarios of user stories 1 and 2,
and commit before continuing.

**Increment 2 (Tasks: T039 - T052)** — User Story 3 (sharing) and User Story 4 (project
documents), the collaboration half of the feature.

**Increment 3 (Tasks: T053 - T066)** — User Story 5 (maintenance), task integration,
tests, and documentation.

Each increment leaves the application in a working, committable state.

---

## Phase 1: Setup (T001 - T004)

- [ ] **T001** Confirm the project builds and runs from a clean checkout: `dotnet restore`,
  `dotnet build`, `dotnet run`, sign in as Ni Kang. (Baseline for every later test.)
- [ ] **T002** Add `DocumentStorage:RootPath` with the value `AppData/uploads` to
  `ContosoDashboard/appsettings.json`. (R1, NFR-006)
- [ ] **T003** [P] Add `ContosoDashboard/AppData/` to `.gitignore` so uploaded files are
  never committed. (R1)
- [ ] **T004** [P] Delete the local development database file so the schema is recreated
  with the new tables on the next run. (quickstart.md, `EnsureCreated()`)

## Phase 2: Foundational (T005 - T015)

*Blocks every user story. No user-visible behavior yet.*

- [ ] **T005** [P] Create `ContosoDashboard/Models/Document.cs` with the fields, lengths,
  and annotations from data-model.md, plus the static `DocumentCategories.All` list.
  (FR-004, FR-006, R6)
- [ ] **T006** [P] Create `ContosoDashboard/Models/DocumentShare.cs`. (FR-020)
- [ ] **T007** [P] Create `ContosoDashboard/Models/DocumentActivity.cs` with the action
  constants. (FR-033)
- [ ] **T008** Add `Documents`, `DocumentShares`, and `DocumentActivities` DbSets to
  `ContosoDashboard/Data/ApplicationDbContext.cs`. (data-model.md)
- [ ] **T009** Configure relationships and delete behavior in `OnModelCreating`: restrict on
  user relationships, cascade from document to shares and activities, set null on project
  deletion. (FR-027, FR-028)
- [ ] **T010** Add indexes on `UploadedByUserId`, `ProjectId`, `UploadedDate`, `Category`,
  the unique index on `FilePath`, and the unique composite on
  (`DocumentId`, `SharedWithUserId`). (FR-022, NFR-002, R7)
- [ ] **T011** [P] Create `ContosoDashboard/Services/IFileStorageService.cs` exactly as in
  contracts/file-storage-service.md. (NFR-006)
- [ ] **T012** Create `ContosoDashboard/Services/LocalFileStorageService.cs`: path building,
  root resolution from configuration, directory creation, `CreateNew` writes, idempotent
  delete, traversal guard. (FR-010, R1)
- [ ] **T013** Register `IFileStorageService` and controllers in
  `ContosoDashboard/Program.cs`, and raise the Blazor SignalR message size so a 25 MB file
  can be received. (FR-003, R3, R4)
- [ ] **T014** Add the `Department` claim to the sign-in claims in
  `ContosoDashboard/Pages/Login.cshtml.cs`. (Constitution security requirements)
- [ ] **T015** [P] Add the Documents entry to `ContosoDashboard/Shared/NavMenu.razor`.
  (FR-001)

## Phase 3: User Story 1 — Upload a work document (P1) (T016 - T026)

**Goal**: An employee can upload a validated document with metadata and see it stored.

- [ ] **T016** Create `ContosoDashboard/Services/DocumentService.cs` with `IDocumentService`
  and the types from contracts/document-service.md. (Contract)
- [ ] **T017** Implement validation in `DocumentService`: required title and category,
  category membership, size limit, extension allow list. (FR-002, FR-003, FR-004)
- [ ] **T018** Implement content-type and file-signature checks as one replaceable method.
  (FR-011, FR-011a, R5)
- [ ] **T019** Implement project authorization for uploads using `ProjectMember` and
  `Project.ProjectManagerId`. (FR-008)
- [ ] **T020** Implement `UploadAsync`: build path, write file, insert row, and delete the
  written file if the insert fails. (FR-009, FR-011c)
- [ ] **T021** Write the `Upload` activity row inside `UploadAsync`. (FR-033)
- [ ] **T022** Notify the other members of the project when a document is uploaded to it,
  reusing `INotificationService`. (FR-031, R8)
- [ ] **T023** Register `IDocumentService` in `ContosoDashboard/Program.cs`.
- [ ] **T024** Create `ContosoDashboard/Pages/Documents.razor` with the upload form: file
  picker, title, description, category, tags, project, and submit. (FR-001, FR-004, FR-005)
- [ ] **T025** Implement the Blazor upload pattern in `Documents.razor`: read metadata
  before opening the stream, copy to `MemoryStream`, clear the selection, re-key the input,
  show progress and the result message. (FR-007, R3)
- [ ] **T026** Limit the project dropdown to projects the user manages or belongs to.
  (FR-008)

**Checkpoint**: acceptance scenarios 1, 3, 4, and 5 of user story 1 pass.

## Phase 4: User Story 2 — Find and open documents (P1) (T027 - T038)

**Goal**: An employee can list, sort, filter, search, download, and preview documents.

- [ ] **T027** Implement the access-scoped base query in `DocumentService` (uploader,
  project membership, share, administrator). (FR-018)
- [ ] **T028** Implement `GetMyDocumentsAsync` with sorting, filtering, and paging in the
  database. (FR-012, FR-013, FR-014, NFR-002)
- [ ] **T029** Implement `SearchAsync` over title, description, tags, uploader name, and
  project name, composed onto the access-scoped query. (FR-015, NFR-003)
- [ ] **T030** Implement `GetDocumentAsync` returning `null` when the document is missing or
  not permitted. (FR-018)
- [ ] **T031** Implement `OpenForDownloadAsync`, including the missing-file case and the
  `Download` activity row. (FR-016, FR-033)
- [ ] **T032** Create `ContosoDashboard/Controllers/DocumentsController.cs` with the
  authorized download endpoint and a sanitized `Content-Disposition` file name. (FR-016,
  FR-011b, R4)
- [ ] **T033** Add the preview endpoint, inline for PDF, JPEG, and PNG only. (FR-017)
- [ ] **T034** Add the document table to `Documents.razor`: title, category, project, size,
  type, upload date, and row actions. (FR-012)
- [ ] **T035** Add sort controls to `Documents.razor`. (FR-013)
- [ ] **T036** Add category, project, and date-range filters plus the search box to
  `Documents.razor`. (FR-014, FR-015)
- [ ] **T037** Add paging controls and an empty state to `Documents.razor`. (NFR-002)
- [ ] **T038** Add the "Recent Documents" widget and the document count card to
  `ContosoDashboard/Pages/Index.razor`, backed by `GetRecentDocumentsAsync` and
  `GetDocumentCountAsync`. (FR-029, FR-030)

**Checkpoint — MVP complete**: all acceptance scenarios of user stories 1 and 2 pass.
Test manually, then commit.

## Phase 5: User Story 3 — Share documents with colleagues (P2) (T039 - T046)

- [ ] **T039** Implement `ShareAsync` with owner-only authorization and rejection of sharing
  with oneself. (FR-020)
- [ ] **T040** Make sharing idempotent through the unique share index and an existence
  check. (FR-022)
- [ ] **T041** Send the recipient an in-app notification on share, and write the `Share`
  activity. (FR-021, FR-033)
- [ ] **T042** Implement `GetSharedWithMeAsync`, explicit shares only. (FR-021)
- [ ] **T043** Add the share dialog to `Documents.razor` with a user picker. (FR-020)
- [ ] **T044** Add the "Shared with Me" tab to `Documents.razor` showing owner and shared
  date. (FR-021)
- [ ] **T045** Hide edit, replace, and delete actions for documents the user did not upload,
  and enforce the same rule in the service. (FR-023)
- [ ] **T046** Notify recipients when a shared document is deleted. (FR-028a)

## Phase 6: User Story 4 — Organize project documents (P2) (T047 - T052)

- [ ] **T047** Implement `GetProjectDocumentsAsync` with membership authorization. (FR-019)
- [ ] **T048** Add the documents section to `ContosoDashboard/Pages/ProjectDetails.razor`
  with uploader and upload date. (FR-019)
- [ ] **T049** Allow upload directly from the project page, pre-selecting the project.
  (FR-019)
- [ ] **T050** Allow the project manager to delete any document in their project. (FR-026)
- [ ] **T051** Confirm that removing a member leaves project documents in place and visible
  to the remaining members. (FR-027)
- [ ] **T052** Confirm that deleting a project unlinks its documents instead of deleting
  them. (FR-028)

## Phase 7: User Story 5 — Maintain and remove documents (P3) (T053 - T058)

- [ ] **T053** Implement `UpdateMetadataAsync`, uploader only. (FR-024)
- [ ] **T054** Implement `ReplaceFileAsync`, including deletion of the superseded file and
  the same validation as upload. (FR-025)
- [ ] **T055** Implement `DeleteAsync`: shares, activities, row, stored file, and the
  `Delete` activity. (FR-026)
- [ ] **T056** Add the edit dialog to `Documents.razor`. (FR-024)
- [ ] **T057** Add the replace-file action to `Documents.razor`. (FR-025)
- [ ] **T058** Add delete with a confirmation step. (FR-026)

## Phase 8: Integration, tests, and documentation (T059 - T066)

- [ ] **T059** Show documents related to a task and allow upload from the task detail view,
  associating the document with the task's project. (FR-032)
- [ ] **T060** Add an administrator view of all documents and recorded activity. (FR-034)
- [ ] **T061** Create the `tests/ContosoDashboard.Tests` xUnit project referencing the web
  project, then add unit tests for upload validation: size, extension, content type,
  signature, missing metadata. (FR-002, FR-003, FR-004, FR-011)
- [ ] **T062** [P] Unit tests for the access rules, including refusal by identifier.
  (FR-018)
- [ ] **T063** [P] Unit tests for the compensating delete when the database write fails.
  (FR-009)
- [ ] **T064** [P] Unit tests for `LocalFileStorageService` path building, round trip, and
  traversal refusal. (FR-010)
- [ ] **T065** Verify the performance budgets with 500 seeded documents. (NFR-002, NFR-003)
- [ ] **T066** Update `README.md` with the feature, the storage layout, and the reset
  instructions from quickstart.md.

## Dependencies

```text
Phase 1 (T001-T004)
        ↓
Phase 2 (T005-T015)
        ↓
Phase 3 (T016-T026)  ← user story 1
        ↓
Phase 4 (T027-T038)  ← user story 2, completes the MVP
        ↓
Phase 5 (T039-T046)  ← user story 3        Phase 6 (T047-T052)  ← user story 4
        ↓                                           ↓
Phase 7 (T053-T058)  ← user story 5
        ↓
Phase 8 (T059-T066)
```

Phases 5 and 6 depend only on the MVP and can proceed in either order.

## Parallel opportunities

- T005, T006, T007 — three independent model files.
- T003, T004 — unrelated setup steps.
- T061 through T064 — independent test files, once the services they cover exist.
