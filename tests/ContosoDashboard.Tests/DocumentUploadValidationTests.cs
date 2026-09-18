using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests;

public class DocumentUploadValidationTests : IDisposable
{
    private readonly DocumentTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task UploadAsync_StoresFileMetadataAndActivity()
    {
        var result = await Upload(DocumentTestContext.PdfRequest(description: "Quarterly status", tags: "q3, status"));

        Assert.True(result.Succeeded, result.Error);
        var document = result.Document!;

        Assert.Equal("Q3 Status Report", document.Title);
        Assert.Equal(DocumentCategories.Reports, document.Category);
        Assert.Equal("q3-report.pdf", document.FileName);
        Assert.Equal(SeededUsers.NiKangEmployee, document.UploadedByUserId);
        Assert.True(await _context.Storage.ExistsAsync(document.FilePath));
        Assert.True(await _context.Db.DocumentActivities.AnyAsync(a =>
            a.DocumentId == document.DocumentId && a.Action == DocumentActions.Upload));
    }

    [Fact]
    public async Task UploadAsync_NeverBuildsThePathFromTheSuppliedFileName()
    {
        var result = await Upload(DocumentTestContext.PdfRequest(fileName: "../../etc/passwd.pdf"));

        Assert.True(result.Succeeded, result.Error);
        Assert.StartsWith($"{SeededUsers.NiKangEmployee}/personal/", result.Document!.FilePath);
        Assert.DoesNotContain("passwd", result.Document.FilePath);
        Assert.DoesNotContain("..", result.Document.FilePath);
    }

    [Theory]
    [InlineData("over the size limit")]
    [InlineData("unsupported extension")]
    [InlineData("content type mismatch")]
    [InlineData("executable renamed as pdf")]
    [InlineData("missing title")]
    [InlineData("blank title")]
    [InlineData("missing category")]
    [InlineData("unknown category")]
    [InlineData("project the user does not belong to")]
    public async Task UploadAsync_RejectsAndPersistsNothing(string scenario)
    {
        var request = scenario switch
        {
            "over the size limit" => DocumentTestContext.PdfRequest(size: DocumentLimits.MaxFileSizeBytes + 1),
            "unsupported extension" => DocumentTestContext.PdfRequest(
                fileName: "setup.exe", contentType: "application/octet-stream", content: new byte[64]),
            "content type mismatch" => DocumentTestContext.PdfRequest(contentType: "image/png"),
            "executable renamed as pdf" => DocumentTestContext.PdfRequest(
                content: new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 }),
            "missing title" => DocumentTestContext.PdfRequest(title: ""),
            "blank title" => DocumentTestContext.PdfRequest(title: "   "),
            "missing category" => DocumentTestContext.PdfRequest(category: ""),
            "unknown category" => DocumentTestContext.PdfRequest(category: "Not a real category"),
            _ => DocumentTestContext.PdfRequest(projectId: SeededUsers.SampleProjectId)
        };

        // The unauthorized-project case is the only one that needs a different user.
        var userId = scenario.StartsWith("project") ? SeededUsers.Administrator : SeededUsers.NiKangEmployee;
        var result = await _context.Documents.UploadAsync(request, userId);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Equal(0, await _context.Db.Documents.CountAsync());
        Assert.Equal(0, _context.StoredFileCount(userId));
    }

    [Fact]
    public async Task UploadAsync_ReportsTheLimitAndTheSignatureFailureToTheUser()
    {
        var tooBig = await Upload(DocumentTestContext.PdfRequest(size: DocumentLimits.MaxFileSizeBytes + 1));
        var spoofed = await Upload(DocumentTestContext.PdfRequest(content: new byte[] { 0x4D, 0x5A, 0x90, 0x00 }));

        Assert.Contains("25 MB", tooBig.Error);
        Assert.Contains("do not match", spoofed.Error);
    }

    [Fact]
    public async Task UploadAsync_AllowsProjectMembersAndNotifiesTheOtherMembers()
    {
        var result = await Upload(DocumentTestContext.PdfRequest(
            title: "Design notes", category: DocumentCategories.ProjectDocuments,
            projectId: SeededUsers.SampleProjectId));

        Assert.True(result.Succeeded, result.Error);

        var notified = await _context.Db.Notifications
            .Where(n => n.Title == "New project document")
            .Select(n => n.UserId)
            .ToListAsync();

        Assert.Contains(SeededUsers.CamilleProjectManager, notified);
        Assert.Contains(SeededUsers.FlorisTeamLead, notified);
        Assert.DoesNotContain(SeededUsers.NiKangEmployee, notified);
    }

    [Fact]
    public async Task UploadAsync_RemovesTheStoredFileWhenTheDatabaseWriteFails()
    {
        var storage = new RecordingFileStorageService(_context.Storage);
        var service = _context.CreateServiceWithUnusableDatabase(storage);

        var result = await service.UploadAsync(DocumentTestContext.PdfRequest(), SeededUsers.NiKangEmployee);

        Assert.False(result.Succeeded);
        Assert.NotNull(storage.LastWrittenPath);
        Assert.False(await _context.Storage.ExistsAsync(storage.LastWrittenPath!));
        Assert.Equal(0, await _context.Db.Documents.CountAsync());
    }

    private Task<DocumentResult> Upload(DocumentUploadRequest request) =>
        _context.Documents.UploadAsync(request, SeededUsers.NiKangEmployee);

    private sealed class RecordingFileStorageService(IFileStorageService inner) : IFileStorageService
    {
        public string? LastWrittenPath { get; private set; }

        public string BuildPath(int userId, int? projectId, string originalFileName) =>
            inner.BuildPath(userId, projectId, originalFileName);

        public Task<string> UploadAsync(Stream content, string path, string contentType)
        {
            LastWrittenPath = path;
            return inner.UploadAsync(content, path, contentType);
        }

        public Task<Stream?> DownloadAsync(string path) => inner.DownloadAsync(path);

        public Task DeleteAsync(string path) => inner.DeleteAsync(path);

        public Task<string> GetUrlAsync(string path, TimeSpan expiration) => inner.GetUrlAsync(path, expiration);

        public Task<bool> ExistsAsync(string path) => inner.ExistsAsync(path);
    }
}
