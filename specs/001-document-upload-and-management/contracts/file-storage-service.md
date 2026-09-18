# Contract: IFileStorageService

**Feature**: 001-document-upload-and-management
**Implementations**: `LocalFileStorageService` (training), a cloud implementation later

The interface is the only route from the application to file content. `DocumentService` is
its only consumer, so an implementation change never reaches pages, controllers, or the
database schema.

```csharp
public interface IFileStorageService
{
    string BuildPath(int userId, int? projectId, string originalFileName);

    Task<string> UploadAsync(Stream content, string path, string contentType);

    Task<Stream?> DownloadAsync(string path);

    Task DeleteAsync(string path);

    Task<string> GetUrlAsync(string path, TimeSpan expiration);

    Task<bool> ExistsAsync(string path);
}
```

## Behavior

| Member | Contract |
|--------|----------|
| `BuildPath` | Returns a relative path `{userId}/{projectId or "personal"}/{guid}{extension}` where the extension comes from the original file name, lower-cased, and the rest is server generated. The original name never appears in the result. Called before any write so the path can be stored with the metadata. |
| `UploadAsync` | Writes the stream to `path`, creating intermediate directories. Returns the path that was written. Throws only for genuine storage failures; the caller compensates by deleting and reporting. Does not overwrite an existing path — paths are GUID-based and therefore unique. |
| `DownloadAsync` | Returns a readable stream positioned at the start, or `null` when nothing is stored at `path`. The caller disposes the stream. |
| `DeleteAsync` | Removes the stored content. Succeeds silently when the path is already absent, so that deletion stays idempotent. |
| `GetUrlAsync` | Returns the address the browser should use for the given path. The local implementation has no directly reachable address and returns the storage path itself, because callers route through the authorized endpoint `/api/documents/{id}/download`; a cloud implementation returns a time-limited signed URL honoring `expiration`. |
| `ExistsAsync` | Reports whether content is stored at `path`, used by health checks and tests. |

## Constraints on any implementation

- A path handed to these methods is always server-generated; implementations must still
  reject a path that escapes the storage root, so a bug elsewhere cannot become a path
  traversal.
- Content is never made publicly readable. The local implementation stores outside
  `wwwroot`; a cloud implementation uses private containers and signed URLs only.
- Paths are relative and portable, so the stored value works unchanged after a migration.
- Storage roots come from configuration, not from constants in code.

## Local implementation notes

- Root: `DocumentStorage:RootPath` in configuration, default `AppData/uploads`, resolved
  against the content root.
- The root directory is created at startup if missing.
- Writes use `FileMode.CreateNew` so a path collision surfaces as an error instead of
  silently replacing a file.
