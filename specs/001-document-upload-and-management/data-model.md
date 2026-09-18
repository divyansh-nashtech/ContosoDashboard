# Data Model: Document Upload and Management

**Feature**: 001-document-upload-and-management
**Date**: 2026-09-18
**Source**: [spec.md](./spec.md) Key Entities, [research.md](./research.md) R1, R2, R6, R7

## Entities

### Document

Metadata for one stored file.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| DocumentId | int | Primary key, identity | Consistent with existing tables |
| Title | string | Required, max 255 | User supplied |
| Description | string? | Max 2000 | Optional |
| Category | string | Required, max 50 | One of `DocumentCategories.All` |
| Tags | string? | Max 500 | Comma-separated, trimmed and de-duplicated on save |
| FileName | string | Required, max 255 | Original name, display and download only |
| FilePath | string | Required, max 500, unique | Relative `{userId}/{projectId or "personal"}/{guid}{ext}` |
| ContentType | string | Required, max 255 | Accommodates Office MIME types |
| FileSizeBytes | long | Required, > 0 and ≤ 26,214,400 | 25 MB limit |
| UploadedByUserId | int | Required, FK → User | Owner |
| ProjectId | int? | FK → Project, null allowed | Null means a personal document |
| UploadedDate | DateTime | Required, UTC | Set by the service |
| UpdatedDate | DateTime | Required, UTC | Touched by metadata edit and file replacement |

**Navigation**: `UploadedByUser` (User), `Project` (Project?), `Shares`
(ICollection\<DocumentShare\>), `Activities` (ICollection\<DocumentActivity\>).

**Indexes**: `UploadedByUserId`, `ProjectId`, `UploadedDate`, `Category`, unique on
`FilePath`.

**Validation rules**

- Title, category, file name, path, and content type are required; category must be a
  member of the allowed set.
- Extension must be on the allow list; declared content type must match the extension; the
  file's leading bytes must match the expected signature (FR-002, FR-011).
- Size must not exceed 25 MB (FR-003).
- `ProjectId`, when set, must be a project the uploader manages or belongs to (FR-008).

### DocumentShare

One grant of read access to one user.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| DocumentShareId | int | Primary key, identity | |
| DocumentId | int | Required, FK → Document | Cascade delete with the document |
| SharedWithUserId | int | Required, FK → User | Recipient |
| SharedByUserId | int | Required, FK → User | Must be the document owner |
| SharedDate | DateTime | Required, UTC | |

**Indexes**: unique composite on (`DocumentId`, `SharedWithUserId`) — enforces FR-022;
index on `SharedWithUserId` for the "Shared with Me" list.

**Rules**: a recipient may read and download, never edit or delete (FR-023). Sharing with
the owner is rejected. Deleting the document deletes its shares (FR-028a).

### DocumentActivity

Append-only audit record.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| DocumentActivityId | int | Primary key, identity | |
| DocumentId | int | Required, FK → Document | Cascade delete with the document |
| UserId | int | Required, FK → User | Actor |
| Action | string | Required, max 20 | `Upload`, `Download`, `Update`, `Share`, `Delete` |
| Details | string? | Max 500 | For example the recipient of a share |
| Timestamp | DateTime | Required, UTC | |

**Indexes**: `DocumentId`, `Timestamp`.

### DocumentCategories (static values, not a table)

`Project Documents`, `Team Resources`, `Personal Files`, `Reports`, `Presentations`,
`Other`.

## Relationships

```text
User 1 ──< Document            (UploadedByUserId, restrict delete)
Project 1 ──< Document          (ProjectId, nullable, set null on project delete)
Document 1 ──< DocumentShare    (cascade delete)
User 1 ──< DocumentShare        (SharedWithUserId and SharedByUserId, restrict delete)
Document 1 ──< DocumentActivity (cascade delete)
User 1 ──< DocumentActivity     (restrict delete)
```

Restrict on the user relationships matches the convention already used for tasks and
projects and prevents a user deletion from silently discarding audit history. `ProjectId`
is nullable with set-null behavior, which is what FR-028 requires when a project is
deleted.

## Access rules

A user may **read** a document when any of the following holds:

1. They uploaded it.
2. It is associated with a project they manage or belong to.
3. It has been shared with them.
4. They hold the Administrator role.

A user may **edit metadata or replace the file** only when they uploaded it (FR-024,
FR-025). A user may **delete** it when they uploaded it or when they manage the associated
project (FR-026).

Membership is read from `ProjectMember` and `Project.ProjectManagerId`, the tables the
application already uses, so removing a user from a project changes their access on the next
request without touching document records (FR-027).

## Lifecycle

| Event | Effect |
|-------|--------|
| Upload | File written, then `Document` row inserted, then `Upload` activity, then notifications to other project members |
| Metadata edit | `Document` fields and `UpdatedDate` change; `Update` activity |
| File replacement | New file written, row updated to the new path, size, type, and name, previous file deleted, `Update` activity |
| Share | `DocumentShare` row inserted if absent, `Share` activity, notification to the recipient |
| Download or preview | `Download` activity |
| Delete | Shares, activities, and the row removed, stored file deleted, recipients notified |
| Uploader removed from project | No change to documents; project members keep access (FR-027) |
| Project deleted | `ProjectId` set to null on its documents; documents remain (FR-028) |

## Schema creation

The application creates the development schema with `EnsureCreated()` at startup, so adding
these entities requires deleting the local database file and restarting. No seed data is
added for documents; the feature starts empty.
