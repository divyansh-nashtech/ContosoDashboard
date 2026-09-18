namespace ContosoDashboard.Tests;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly DocumentTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public void BuildPath_UsesUserProjectAndGuidWithTheOriginalExtension()
    {
        var path = _context.Storage.BuildPath(SeededUsers.NiKangEmployee, SeededUsers.SampleProjectId, "Budget Q3.PDF");

        var segments = path.Split('/');
        Assert.Equal(3, segments.Length);
        Assert.Equal(SeededUsers.NiKangEmployee.ToString(), segments[0]);
        Assert.Equal(SeededUsers.SampleProjectId.ToString(), segments[1]);
        Assert.EndsWith(".pdf", segments[2]);
        Assert.True(Guid.TryParse(Path.GetFileNameWithoutExtension(segments[2]), out _));
        Assert.DoesNotContain("Budget", path);
    }

    [Fact]
    public void BuildPath_UsesThePersonalFolderWhenThereIsNoProject()
    {
        var path = _context.Storage.BuildPath(SeededUsers.NiKangEmployee, null, "notes.txt");

        Assert.StartsWith($"{SeededUsers.NiKangEmployee}/personal/", path);
    }

    [Fact]
    public void BuildPath_IsUniquePerCall()
    {
        var first = _context.Storage.BuildPath(SeededUsers.NiKangEmployee, null, "notes.txt");
        var second = _context.Storage.BuildPath(SeededUsers.NiKangEmployee, null, "notes.txt");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task UploadAsync_ThenDownloadAsync_ReturnsTheSameBytes()
    {
        var content = DocumentTestContext.PdfBytes(4096);
        var path = _context.Storage.BuildPath(SeededUsers.NiKangEmployee, null, "report.pdf");

        await _context.Storage.UploadAsync(new MemoryStream(content), path, "application/pdf");

        await using var stored = await _context.Storage.DownloadAsync(path);
        Assert.NotNull(stored);

        using var buffer = new MemoryStream();
        await stored!.CopyToAsync(buffer);
        Assert.Equal(content, buffer.ToArray());
    }

    [Fact]
    public async Task DownloadAsync_ReturnsNullWhenNothingIsStoredAtThePath()
    {
        Assert.Null(await _context.Storage.DownloadAsync($"{SeededUsers.NiKangEmployee}/personal/{Guid.NewGuid()}.pdf"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheFileAndIsSafeToRepeat()
    {
        var path = _context.Storage.BuildPath(SeededUsers.NiKangEmployee, null, "report.pdf");
        await _context.Storage.UploadAsync(new MemoryStream(DocumentTestContext.PdfBytes()), path, "application/pdf");

        await _context.Storage.DeleteAsync(path);
        Assert.False(await _context.Storage.ExistsAsync(path));

        await _context.Storage.DeleteAsync(path);
    }

    [Fact]
    public async Task UploadAsync_RefusesToWriteOutsideTheStorageRoot()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _context.Storage.UploadAsync(new MemoryStream(DocumentTestContext.PdfBytes()), "../../escaped.pdf", "application/pdf"));
    }

    [Fact]
    public async Task DownloadAsync_RefusesToReadOutsideTheStorageRoot()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _context.Storage.DownloadAsync("../../../etc/passwd"));
    }
}
