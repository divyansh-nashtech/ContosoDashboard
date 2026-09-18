using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests;

public class DocumentUploadValidationTests : IDisposable
{
    private readonly DocumentTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task UploadAsync_StoresFileAndMetadata()
    {
        var result = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(description: "Quarterly status", tags: "q3, status"),
            SeededUsers.NiKangEmployee);

        Assert.True(result.Succeeded, result.Error);
        var document = result.Document!;

        Assert.Equal("Q3 Status Report", document.Title);
        Assert.Equal(DocumentCategories.Reports, document.Category);
        Assert.Equal("q3-report.pdf", document.FileName);
        Assert.Equal(SeededUsers.NiKangEmployee, document.UploadedByUserId);
        Assert.True(await _context.Storage.ExistsAsync(document.FilePath));
    }

    [Fact]
    public async Task UploadAsync_StoresFileUnderGuidNameOutsideUserControl()
    {
        var result = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(fileName: "../../etc/passwd.pdf"),
            SeededUsers.NiKangEmployee);

        Assert.True(result.Succeeded, result.Error);
        Assert.StartsWith($"{SeededUsers.NiKangEmployee}/personal/", result.Document!.FilePath);
        Assert.DoesNotContain("passwd", result.Document.FilePath);
        Assert.DoesNotContain("..", result.Document.FilePath);
    }

    [Fact]
    public async Task UploadAsync_RecordsUploadActivity()
    {
        var result = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(), SeededUsers.NiKangEmployee);

        Assert.True(await _context.Db.DocumentActivities.AnyAsync(a =>
            a.DocumentId == result.Document!.DocumentId &&
            a.UserId == SeededUsers.NiKangEmployee &&
            a.Action == DocumentActions.Upload));
    }

    [Fact]
    public async Task UploadAsync_RejectsFileOverSizeLimit()
    {
        var result = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(size: DocumentLimits.MaxFileSizeBytes + 1),
            SeededUsers.NiKangEmployee);

        Assert.False(result.Succeeded);
        Assert.Contains("25 MB", result.Error);
        await AssertNothingPersisted();
    }

    [Fact]
    public async Task UploadAsync_RejectsUnsupportedExtension()
    {
        var result = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(
                fileName: "setup.exe", contentType: "application/octet-stream", content: new byte[64]),
            SeededUsers.NiKangEmployee);

        Assert.False(result.Succeeded);
        await AssertNothingPersisted();
    }

    [Fact]
    public async Task UploadAsync_RejectsContentTypeThatDoesNotMatchExtension()
    {
        var result = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(contentType: "image/png"),
            SeededUsers.NiKangEmployee);

        Assert.False(result.Succeeded);
        await AssertNothingPersisted();
    }

    [Fact]
    public async Task UploadAsync_RejectsExecutableRenamedAsPdf()
    {
        var result = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(content: new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 }),
            SeededUsers.NiKangEmployee);

        Assert.False(result.Succeeded);
        Assert.Contains("do not match", result.Error);
        await AssertNothingPersisted();
    }

    [Theory]
    [InlineData("", DocumentCategories.Reports)]
    [InlineData("   ", DocumentCategories.Reports)]
    [InlineData("Valid title", "")]
    [InlineData("Valid title", "Not a real category")]
    public async Task UploadAsync_RejectsMissingOrUnknownMetadata(string title, string category)
    {
        var result = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(title: title, category: category),
            SeededUsers.NiKangEmployee);

        Assert.False(result.Succeeded);
        await AssertNothingPersisted();
    }

    [Fact]
    public async Task UploadAsync_RejectsProjectTheUserDoesNotBelongTo()
    {
        var result = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(projectId: SeededUsers.SampleProjectId),
            SeededUsers.Administrator);

        Assert.False(result.Succeeded);
        await AssertNothingPersisted();
    }

    [Fact]
    public async Task UploadAsync_AllowsProjectMemberAndNotifiesTheOtherMembers()
    {
        var result = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(
                title: "Design notes",
                category: DocumentCategories.ProjectDocuments,
                projectId: SeededUsers.SampleProjectId),
            SeededUsers.NiKangEmployee);

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

        var result = await service.UploadAsync(
            DocumentTestContext.PdfRequest(), SeededUsers.NiKangEmployee);

        Assert.False(result.Succeeded);
        Assert.NotNull(storage.LastWrittenPath);
        Assert.False(await _context.Storage.ExistsAsync(storage.LastWrittenPath!));
        Assert.Equal(0, await _context.Db.Documents.CountAsync());
    }

    private async Task AssertNothingPersisted()
    {
        Assert.Equal(0, await _context.Db.Documents.CountAsync());
        Assert.Equal(0, _context.StoredFileCount(SeededUsers.NiKangEmployee));
    }

    private sealed class RecordingFileStorageService : IFileStorageService
    {
        private readonly IFileStorageService _inner;

        public RecordingFileStorageService(IFileStorageService inner) => _inner = inner;

        public string? LastWrittenPath { get; private set; }

        public string BuildPath(int userId, int? projectId, string originalFileName) =>
            _inner.BuildPath(userId, projectId, originalFileName);

        public async Task<string> UploadAsync(Stream content, string path, string contentType)
        {
            LastWrittenPath = path;
            return await _inner.UploadAsync(content, path, contentType);
        }

        public Task<Stream?> DownloadAsync(string path) => _inner.DownloadAsync(path);

        public Task DeleteAsync(string path) => _inner.DeleteAsync(path);

        public Task<string> GetUrlAsync(string path, TimeSpan expiration) => _inner.GetUrlAsync(path, expiration);

        public Task<bool> ExistsAsync(string path) => _inner.ExistsAsync(path);
    }
}
