using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests;

public class DocumentAccessTests : IAsyncLifetime
{
    private readonly DocumentTestContext _context = new();

    private int _personalDocumentId;
    private int _projectDocumentId;

    public async Task InitializeAsync()
    {
        var personal = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(description: "Quarterly status", tags: "q3, status"),
            SeededUsers.NiKangEmployee);

        var project = await _context.Documents.UploadAsync(
            DocumentTestContext.PdfRequest(
                title: "Design notes",
                category: DocumentCategories.ProjectDocuments,
                fileName: "design.pdf",
                projectId: SeededUsers.SampleProjectId),
            SeededUsers.NiKangEmployee);

        _personalDocumentId = personal.Document!.DocumentId;
        _projectDocumentId = project.Document!.DocumentId;
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetDocumentAsync_AllowsTheUploader()
    {
        Assert.NotNull(await _context.Documents.GetDocumentAsync(_personalDocumentId, SeededUsers.NiKangEmployee));
    }

    [Fact]
    public async Task GetDocumentAsync_AllowsProjectMembersForProjectDocuments()
    {
        Assert.NotNull(await _context.Documents.GetDocumentAsync(_projectDocumentId, SeededUsers.CamilleProjectManager));
        Assert.NotNull(await _context.Documents.GetDocumentAsync(_projectDocumentId, SeededUsers.FlorisTeamLead));
    }

    [Fact]
    public async Task GetDocumentAsync_RefusesUsersWithNoRelationshipToTheDocument()
    {
        Assert.Null(await _context.Documents.GetDocumentAsync(_personalDocumentId, SeededUsers.CamilleProjectManager));
    }

    [Fact]
    public async Task GetDocumentAsync_AllowsAdministrators()
    {
        Assert.NotNull(await _context.Documents.GetDocumentAsync(_personalDocumentId, SeededUsers.Administrator));
    }

    [Fact]
    public async Task GetDocumentAsync_AllowsUsersTheDocumentIsSharedWith()
    {
        _context.Db.DocumentShares.Add(new DocumentShare
        {
            DocumentId = _personalDocumentId,
            SharedWithUserId = SeededUsers.FlorisTeamLead,
            SharedByUserId = SeededUsers.NiKangEmployee
        });
        await _context.Db.SaveChangesAsync();

        Assert.NotNull(await _context.Documents.GetDocumentAsync(_personalDocumentId, SeededUsers.FlorisTeamLead));
    }

    [Fact]
    public async Task OpenForDownloadAsync_ReturnsTheStoredBytesToTheUploader()
    {
        var file = await _context.Documents.OpenForDownloadAsync(_personalDocumentId, SeededUsers.NiKangEmployee);

        Assert.NotNull(file);
        Assert.Equal("q3-report.pdf", file!.FileName);
        Assert.Equal("application/pdf", file.ContentType);

        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer);
        await file.Content.DisposeAsync();

        Assert.Equal(DocumentTestContext.PdfBytes(), buffer.ToArray());
    }

    [Fact]
    public async Task OpenForDownloadAsync_RefusesAUserWithoutAccess()
    {
        Assert.Null(await _context.Documents.OpenForDownloadAsync(_personalDocumentId, SeededUsers.CamilleProjectManager));
    }

    [Fact]
    public async Task OpenForDownloadAsync_ReturnsNullForAnUnknownDocument()
    {
        Assert.Null(await _context.Documents.OpenForDownloadAsync(9999, SeededUsers.NiKangEmployee));
    }

    [Fact]
    public async Task OpenForDownloadAsync_RecordsDownloadActivity()
    {
        await _context.Documents.OpenForDownloadAsync(_personalDocumentId, SeededUsers.NiKangEmployee);

        Assert.True(await _context.Db.DocumentActivities.AnyAsync(a =>
            a.DocumentId == _personalDocumentId && a.Action == DocumentActions.Download));
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
    public async Task SearchAsync_MatchesTitleDescriptionAndTagsCaseInsensitively()
    {
        foreach (var term in new[] { "q3 status", "QUARTERLY", "status" })
        {
            var results = await _context.Documents.SearchAsync(
                SeededUsers.NiKangEmployee, new DocumentQuery(SearchTerm: term));

            Assert.Contains(results.Items, d => d.DocumentId == _personalDocumentId);
        }
    }

    [Fact]
    public async Task GetMyDocumentsAsync_FiltersSortsAndPages()
    {
        var byCategory = await _context.Documents.GetMyDocumentsAsync(
            SeededUsers.NiKangEmployee, new DocumentQuery(Category: DocumentCategories.Reports));
        Assert.Single(byCategory.Items);

        var byProject = await _context.Documents.GetMyDocumentsAsync(
            SeededUsers.NiKangEmployee, new DocumentQuery(ProjectId: SeededUsers.SampleProjectId));
        Assert.Equal(_projectDocumentId, Assert.Single(byProject.Items).DocumentId);

        var byTitle = await _context.Documents.GetMyDocumentsAsync(
            SeededUsers.NiKangEmployee, new DocumentQuery(SortBy: "Title", SortDescending: false));
        Assert.Equal("Design notes", byTitle.Items[0].Title);

        var firstPage = await _context.Documents.GetMyDocumentsAsync(
            SeededUsers.NiKangEmployee, new DocumentQuery(PageSize: 1));
        Assert.Single(firstPage.Items);
        Assert.Equal(2, firstPage.TotalCount);
    }

    [Fact]
    public async Task DashboardQueries_AreScopedToTheUploader()
    {
        Assert.Equal(2, await _context.Documents.GetDocumentCountAsync(SeededUsers.NiKangEmployee));
        Assert.Equal(0, await _context.Documents.GetDocumentCountAsync(SeededUsers.CamilleProjectManager));

        var recent = await _context.Documents.GetRecentDocumentsAsync(SeededUsers.NiKangEmployee, 5);
        Assert.Equal("Design notes", recent[0].Title);
    }
}
