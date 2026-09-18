# Contract: IDocumentService

**Feature**: 001-document-upload-and-management
**Consumers**: `Pages/Documents.razor`, `Pages/Index.razor`, `Pages/ProjectDetails.razor`,
`Controllers/DocumentsController`

Every method takes the acting user's id and performs its own authorization; callers never
assume a caller-side check is sufficient. Expected conditions are reported through the
return value rather than exceptions.

## Types

```csharp
public record DocumentUploadRequest(
    string Title,
    string? Description,
    string Category,
    string? Tags,
    int? ProjectId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Stream Content);

public record DocumentResult(bool Succeeded, string? Error, Document? Document);

public record DocumentQuery(
    string? SearchTerm = null,
    string? Category = null,
    int? ProjectId = null,
    DateTime? UploadedFrom = null,
    DateTime? UploadedTo = null,
    string SortBy = "UploadedDate",
    bool SortDescending = true,
    int Page = 1,
    int PageSize = 25);

public record DocumentPage(IReadOnlyList<Document> Items, int TotalCount);

public record DocumentFile(Stream Content, string ContentType, string FileName);
```

## Operations

| Method | Authorization | Success | Failure |
|--------|---------------|---------|---------|
| `Task<DocumentResult> UploadAsync(DocumentUploadRequest request, int userId)` | Any authenticated user; when `ProjectId` is set, the user must manage or belong to that project | File stored, row inserted, activity written, project members notified | Validation message for size, extension, content type, signature, missing title or category, unknown category, unauthorized project, or storage failure — nothing is persisted |
| `Task<DocumentPage> GetMyDocumentsAsync(int userId, DocumentQuery query)` | Returns only documents uploaded by the user | Filtered, sorted, paged list with uploader and project loaded | Empty page |
| `Task<DocumentPage> GetSharedWithMeAsync(int userId, DocumentQuery query)` | Returns only documents with a share row for the user | As above | Empty page |
| `Task<DocumentPage> SearchAsync(int userId, DocumentQuery query)` | Access scope applied before the search predicate | Documents the user may read that match the term | Empty page |
| `Task<List<Document>> GetProjectDocumentsAsync(int projectId, int userId)` | User must manage or belong to the project | Documents for the project | Empty list when not permitted |
| `Task<List<Document>> GetRecentDocumentsAsync(int userId, int count)` | Uploader only | The user's most recent uploads | Empty list |
| `Task<int> GetDocumentCountAsync(int userId)` | Uploader only | Count for the dashboard card | `0` |
| `Task<Document?> GetDocumentAsync(int documentId, int userId)` | Read access rules | The document | `null` when missing or not permitted |
| `Task<DocumentFile?> OpenForDownloadAsync(int documentId, int userId)` | Read access rules | Stream, content type, sanitized file name; writes a `Download` activity | `null` when missing, not permitted, or the stored file is absent |
| `Task<DocumentResult> UpdateMetadataAsync(int documentId, string title, string? description, string category, string? tags, int userId)` | Uploader only | Fields and `UpdatedDate` saved, `Update` activity | Validation or authorization message |
| `Task<DocumentResult> ReplaceFileAsync(int documentId, DocumentUploadRequest request, int userId)` | Uploader only | New file stored, row repointed, previous file deleted, `Update` activity | Validation or authorization message; the existing file is left untouched |
| `Task<DocumentResult> DeleteAsync(int documentId, int userId)` | Uploader, or the manager of the associated project, or an administrator | Shares, activities, row, and stored file removed; recipients notified | Authorization message |
| `Task<DocumentResult> ShareAsync(int documentId, int shareWithUserId, int userId)` | Uploader only; recipient must exist and must not be the owner | Share row inserted when absent, `Share` activity, recipient notified | Authorization message; sharing twice succeeds without creating a duplicate |

## Guarantees

- No method returns a document the acting user may not read.
- `UploadAsync` and `ReplaceFileAsync` leave no stored file behind when the database write
  fails, and no row behind when the file write fails.
- Every operation that changes or reads file content writes one `DocumentActivity` row.
- Validation messages are safe to show to the user and never disclose whether a document
  exists when the user has no access to it.
