# Quickstart: Document Upload and Management

**Feature**: 001-document-upload-and-management
**Branch**: `001-document-upload-and-management`

## Prerequisites

- .NET SDK 9.0 or later
- A browser

No cloud account, connection string, or external service is needed; the feature runs
entirely offline.

## Build and run

```bash
cd ContosoDashboard
dotnet restore
dotnet build
dotnet run
```

The application listens on `http://localhost:5000`. Sign in by choosing a user from the
list on the login page:

| User | Role | Use for |
|------|------|---------|
| Ni Kang | Employee | Uploading, searching, sharing |
| Floris Kregel | TeamLead | Receiving shares, project documents |
| Camille Nicole | ProjectManager | Managing documents on "ContosoDashboard Development" |
| System Administrator | Administrator | Audit view of all documents |

## Storage layout

Uploaded files are written under `ContosoDashboard/AppData/uploads`, outside `wwwroot`, as:

```text
AppData/uploads/{userId}/{projectId or "personal"}/{guid}{extension}
```

The directory is created on first upload and is excluded from source control. To start
from an empty state, stop the application and delete the `AppData/uploads` directory and
the local database file.

The storage root can be moved with configuration:

```json
"DocumentStorage": {
  "RootPath": "AppData/uploads"
}
```

## Schema changes

The development schema is created by `EnsureCreated()` at startup, so the three new tables
appear only in a database created after this feature was added. If the application starts
against an older database file, stop it, delete `ContosoDashboard/ContosoDashboard.db`
(and its `-shm` and `-wal` companions), and run again.

## Try the feature

1. Sign in as **Ni Kang**.
2. Open **Documents** in the left navigation.
3. Select **Upload Document**, choose a PDF under 25 MB, enter a title, pick the category
   **Reports**, and submit. The progress indicator runs and the document appears in the
   list.
4. Filter by category, sort by size, and search for part of the title.
5. Select **Download** and confirm the original file is returned; select **Preview** on a
   PDF or image and confirm it opens in the browser.
6. Select **Share**, choose **Floris Kregel**, and confirm. Sign out, sign in as Floris,
   and check the notification and the **Shared with Me** tab.
7. Sign back in as Ni Kang, upload a document to the project **ContosoDashboard
   Development**, and confirm it appears on the project page for every member.
8. Edit a document's metadata, replace its file, then delete it and confirm the file is
   gone from `AppData/uploads`.

## Expected rejections

| Attempt | Expected result |
|---------|-----------------|
| File larger than 25 MB | Rejected with the size limit in the message; nothing stored |
| `.exe` or another type outside the allow list | Rejected with the supported types listed |
| Renaming a `.exe` to `.pdf` | Rejected — the file signature does not match |
| Missing title or category | Rejected before any file is written |
| Uploading to a project you do not belong to | Rejected as unauthorized |
| Requesting `/api/documents/{id}/download` for another user's document | `404`, and no bytes returned |

## Run the tests

```bash
dotnet test
```

The test project covers validation and authorization rules in `DocumentService` and the
path and round-trip behavior of `LocalFileStorageService`.
