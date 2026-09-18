using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ContosoDashboard.Tests;

/// Seeded users and projects come from ApplicationDbContext.SeedData.
public static class SeededUsers
{
    public const int Administrator = 1;
    public const int CamilleProjectManager = 2;
    public const int FlorisTeamLead = 3;
    public const int NiKangEmployee = 4;
    public const int SampleProjectId = 1;
}

public sealed class DocumentTestContext : IDisposable
{
    private readonly string _root;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public DocumentTestContext()
    {
        _root = Path.Combine(Path.GetTempPath(), $"contoso-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_root);

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_root, "test.db")}")
            .Options;

        Db = new ApplicationDbContext(_options);
        Db.Database.EnsureCreated();

        Storage = new LocalFileStorageService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["DocumentStorage:RootPath"] = _root })
                .Build(),
            new TestEnvironment { ContentRootPath = _root },
            NullLogger<LocalFileStorageService>.Instance);

        Documents = CreateService(Storage);
    }

    public ApplicationDbContext Db { get; }

    public LocalFileStorageService Storage { get; }

    public IDocumentService Documents { get; }

    public DocumentService CreateService(IFileStorageService storage) =>
        new(Db, storage, new NotificationService(Db), NullLogger<DocumentService>.Instance);

    /// A service whose database rejects writes, for exercising the compensating delete.
    public DocumentService CreateServiceWithUnusableDatabase(IFileStorageService storage)
    {
        var unusable = new ApplicationDbContext(_options);
        unusable.Dispose();

        return new DocumentService(unusable, storage, new NotificationService(unusable), NullLogger<DocumentService>.Instance);
    }

    public int StoredFileCount(int userId) =>
        Directory.Exists(Path.Combine(_root, userId.ToString()))
            ? Directory.GetFiles(Path.Combine(_root, userId.ToString()), "*", SearchOption.AllDirectories).Length
            : 0;

    public static DocumentUploadRequest PdfRequest(
        string title = "Q3 Status Report",
        string category = DocumentCategories.Reports,
        string fileName = "q3-report.pdf",
        string contentType = "application/pdf",
        long? size = null,
        int? projectId = null,
        string? description = null,
        string? tags = null,
        byte[]? content = null)
    {
        var bytes = content ?? PdfBytes();

        return new DocumentUploadRequest(
            title, description, category, tags, projectId,
            fileName, contentType, size ?? bytes.Length, new MemoryStream(bytes));
    }

    public static byte[] PdfBytes(int sizeBytes = 2048)
    {
        var bytes = new byte[sizeBytes];
        "%PDF-1.7 test document"u8.ToArray().CopyTo(bytes, 0);
        return bytes;
    }

    public void Dispose()
    {
        Db.Dispose();
        Directory.Delete(_root, true);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ContosoDashboard.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
    }
}
