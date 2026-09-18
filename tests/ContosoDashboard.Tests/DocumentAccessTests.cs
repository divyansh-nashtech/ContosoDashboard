using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests;

public class DocumentAccessTests : IAsyncLifetime
{
    private readonly DocumentTestContext _context = new();

    private int _personalId;
    private int _projectId;

    public async Task InitializeAsync()
    {
        var personal = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(description: "Quarterly status", tags: "q3, status"),
            SeededUsers.NiKangEmployee);

        var project = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(
                title: "Design notes", category: DocumentCategories.ProjectDocuments,
                fileName: "design.pdf", projectId: SeededUsers.SampleProjectId),
            SeededUsers.NiKangEmployee);

        _personalId = personal.Document!.DocumentId;
        _projectId = project.Document!.DocumentId;
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(SeededUsers.NiKangEmployee, true)]       // uploader
    [InlineData(SeededUsers.Administrator, true)]        // administrator
    [InlineData(SeededUsers.CamilleProjectManager, false)] // no relationship to a personal document
    [InlineData(SeededUsers.FlorisTeamLead, false)]
    public async Task GetDocumentAsync_AppliesTheAccessRulesToAPersonalDocument(int userId, bool allowed)
    {
        var document = await _context.Documents.GetDocumentAsync(_personalId, userId);

        Assert.Equal(allowed, document != null);
        Assert.Equal(allowed, await _context.Documents.OpenForDownloadAsync(_personalId, userId) != null);
    }

    [Theory]
    [InlineData(SeededUsers.CamilleProjectManager)] // project manager
    [InlineData(SeededUsers.FlorisTeamLead)]        // project member
    public async Task GetDocumentAsync_AllowsTheProjectTeamForAProjectDocument(int userId)
    {
        Assert.NotNull(await _context.Documents.GetDocumentAsync(_projectId, userId));
    }

    [Fact]
    public async Task GetDocumentAsync_AllowsUsersTheDocumentIsSharedWith()
    {
        _context.Db.DocumentShares.Add(new DocumentShare
        {
            DocumentId = _personalId,
            SharedWithUserId = SeededUsers.FlorisTeamLead,
            SharedByUserId = SeededUsers.NiKangEmployee
        });
        await _context.Db.SaveChangesAsync();

        Assert.NotNull(await _context.Documents.GetDocumentAsync(_personalId, SeededUsers.FlorisTeamLead));
    }

    [Fact]
    public async Task GetDocumentAsync_ReturnsNullForAnUnknownIdentifier()
    {
        Assert.Null(await _context.Documents.GetDocumentAsync(9999, SeededUsers.NiKangEmployee));
        Assert.Null(await _context.Documents.OpenForDownloadAsync(9999, SeededUsers.NiKangEmployee));
    }

    [Fact]
    public async Task OpenForDownloadAsync_ReturnsTheStoredBytesAndRecordsTheActivity()
    {
        var file = await _context.Documents.OpenForDownloadAsync(_personalId, SeededUsers.NiKangEmployee);

        Assert.NotNull(file);
        Assert.Equal("q3-report.pdf", file!.FileName);
        Assert.Equal("application/pdf", file.ContentType);

        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer);
        await file.Content.DisposeAsync();
        Assert.Equal(DocumentTestContext.PdfBytes(), buffer.ToArray());

        Assert.True(await _context.Db.DocumentActivities.AnyAsync(a =>
            a.DocumentId == _personalId && a.Action == DocumentActions.Download));
    }

    [Theory]
    [InlineData("q3 status")]
    [InlineData("QUARTERLY")]
    [InlineData("status")]
    public async Task SearchAsync_MatchesTitleDescriptionAndTagsCaseInsensitively(string term)
    {
        var results = await _context.Documents.SearchAsync(SeededUsers.NiKangEmployee, new DocumentQuery(SearchTerm: term));

        Assert.Contains(results.Items, d => d.DocumentId == _personalId);
    }

    [Fact]
    public async Task SearchAsync_NeverReturnsDocumentsTheUserCannotRead()
    {
        var results = await _context.Documents.SearchAsync(
            SeededUsers.CamilleProjectManager, new DocumentQuery(SearchTerm: "Q3"));

        Assert.Empty(results.Items);
        Assert.Equal(0, results.TotalCount);
    }

    [Fact]
    public async Task GetMyDocumentsAsync_FiltersSortsAndPages()
    {
        var byCategory = await Query(new DocumentQuery(Category: DocumentCategories.Reports));
        Assert.Equal(_personalId, Assert.Single(byCategory.Items).DocumentId);

        var byProject = await Query(new DocumentQuery(ProjectId: SeededUsers.SampleProjectId));
        Assert.Equal(_projectId, Assert.Single(byProject.Items).DocumentId);

        var byTitle = await Query(new DocumentQuery(SortBy: "Title", SortDescending: false));
        Assert.Equal("Design notes", byTitle.Items[0].Title);

        var firstPage = await Query(new DocumentQuery(PageSize: 1));
        Assert.Single(firstPage.Items);
        Assert.Equal(2, firstPage.TotalCount);
    }

    [Fact]
    public async Task DashboardQueries_AreScopedToTheUploader()
    {
        Assert.Equal(2, await _context.Documents.GetDocumentCountAsync(SeededUsers.NiKangEmployee));
        Assert.Equal(0, await _context.Documents.GetDocumentCountAsync(SeededUsers.CamilleProjectManager));
        Assert.Equal("Design notes", (await _context.Documents.GetRecentDocumentsAsync(SeededUsers.NiKangEmployee, 5))[0].Title);
    }

    private Task<DocumentPage> Query(DocumentQuery query) =>
        _context.Documents.GetMyDocumentsAsync(SeededUsers.NiKangEmployee, query);
}
