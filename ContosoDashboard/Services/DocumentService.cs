using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

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

public record DocumentResult(bool Succeeded, string? Error, Document? Document)
{
    public static DocumentResult Success(Document document) => new(true, null, document);

    public static DocumentResult Failure(string error) => new(false, error, null);
}

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

public interface IDocumentService
{
    Task<DocumentResult> UploadAsync(DocumentUploadRequest request, int userId);
    Task<DocumentPage> GetMyDocumentsAsync(int userId, DocumentQuery query);
    Task<DocumentPage> SearchAsync(int userId, DocumentQuery query);
    Task<Document?> GetDocumentAsync(int documentId, int userId);
    Task<DocumentFile?> OpenForDownloadAsync(int documentId, int userId);
    Task<List<Document>> GetRecentDocumentsAsync(int userId, int count);
    Task<int> GetDocumentCountAsync(int userId);
}

public class DocumentService : IDocumentService
{
    private static readonly Dictionary<string, string[]> AllowedTypes = new()
    {
        [".pdf"] = new[] { "application/pdf" },
        [".doc"] = new[] { "application/msword" },
        [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        [".xls"] = new[] { "application/vnd.ms-excel" },
        [".xlsx"] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        [".ppt"] = new[] { "application/vnd.ms-powerpoint" },
        [".pptx"] = new[] { "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        [".txt"] = new[] { "text/plain" },
        [".jpg"] = new[] { "image/jpeg" },
        [".jpeg"] = new[] { "image/jpeg" },
        [".png"] = new[] { "image/png" }
    };

    private static readonly Dictionary<string, byte[][]> ExpectedSignatures = new()
    {
        [".pdf"] = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } },
        [".docx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        [".xlsx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        [".pptx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        [".doc"] = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0 } },
        [".xls"] = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0 } },
        [".ppt"] = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0 } },
        [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47 } }
    };

    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly INotificationService _notifications;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService storage,
        INotificationService notifications,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _storage = storage;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<DocumentResult> UploadAsync(DocumentUploadRequest request, int userId)
    {
        var validationError = ValidateMetadata(request);
        if (validationError != null)
        {
            return DocumentResult.Failure(validationError);
        }

        var contentError = await ValidateContentAsync(request);
        if (contentError != null)
        {
            return DocumentResult.Failure(contentError);
        }

        if (request.ProjectId.HasValue && !await CanUploadToProjectAsync(request.ProjectId.Value, userId))
        {
            return DocumentResult.Failure("You can only upload documents to projects you manage or belong to.");
        }

        var path = _storage.BuildPath(userId, request.ProjectId, request.FileName);

        try
        {
            await _storage.UploadAsync(request.Content, path, request.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store uploaded file at {Path}", path);
            await _storage.DeleteAsync(path);
            return DocumentResult.Failure("The document could not be saved. Please try again.");
        }

        var document = new Document
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Category = request.Category,
            Tags = NormalizeTags(request.Tags),
            FileName = Path.GetFileName(request.FileName),
            FilePath = path,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            UploadedByUserId = userId,
            ProjectId = request.ProjectId,
            UploadedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        try
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record document metadata for {Path}", path);
            await _storage.DeleteAsync(path);
            return DocumentResult.Failure("The document could not be saved. Please try again.");
        }

        await RecordActivityAsync(document.DocumentId, userId, DocumentActions.Upload, document.FileName);
        await NotifyProjectMembersAsync(document, userId);

        return DocumentResult.Success(document);
    }

    public async Task<DocumentPage> GetMyDocumentsAsync(int userId, DocumentQuery query)
    {
        var documents = _context.Documents.Where(d => d.UploadedByUserId == userId);

        return await ApplyQueryAsync(documents, query);
    }

    public async Task<DocumentPage> SearchAsync(int userId, DocumentQuery query)
    {
        var documents = await AccessibleDocumentsAsync(userId);

        return await ApplyQueryAsync(documents, query);
    }

    public async Task<Document?> GetDocumentAsync(int documentId, int userId)
    {
        var documents = await AccessibleDocumentsAsync(userId);

        return await documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);
    }

    public async Task<DocumentFile?> OpenForDownloadAsync(int documentId, int userId)
    {
        var document = await GetDocumentAsync(documentId, userId);
        if (document == null)
        {
            return null;
        }

        var content = await _storage.DownloadAsync(document.FilePath);
        if (content == null)
        {
            return null;
        }

        await RecordActivityAsync(document.DocumentId, userId, DocumentActions.Download, document.FileName);

        return new DocumentFile(content, document.ContentType, SanitizeFileName(document.FileName));
    }

    public async Task<List<Document>> GetRecentDocumentsAsync(int userId, int count)
    {
        return await _context.Documents
            .Where(d => d.UploadedByUserId == userId)
            .Include(d => d.Project)
            .OrderByDescending(d => d.UploadedDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<int> GetDocumentCountAsync(int userId)
    {
        return await _context.Documents.CountAsync(d => d.UploadedByUserId == userId);
    }

    private static string? ValidateMetadata(DocumentUploadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return "Select a file to upload.";
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "A document title is required.";
        }

        if (!DocumentCategories.All.Contains(request.Category))
        {
            return "Select a category for the document.";
        }

        if (request.FileSizeBytes <= 0)
        {
            return "The selected file is empty.";
        }

        if (request.FileSizeBytes > DocumentLimits.MaxFileSizeBytes)
        {
            return "The file exceeds the 25 MB limit.";
        }

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!AllowedTypes.ContainsKey(extension))
        {
            return $"Unsupported file type. Allowed types: {string.Join(", ", AllowedTypes.Keys)}.";
        }

        return null;
    }

    // Stands in for the virus scan required by the specification: an extension, declared
    // type and file signature check that a scanning service can be introduced behind.
    private static async Task<string?> ValidateContentAsync(DocumentUploadRequest request)
    {
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();

        if (!AllowedTypes.TryGetValue(extension, out var allowedContentTypes))
        {
            return "Unsupported file type.";
        }

        if (!allowedContentTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return "The file content type does not match its extension.";
        }

        if (!ExpectedSignatures.TryGetValue(extension, out var signatures))
        {
            return null;
        }

        var longestSignature = signatures.Max(s => s.Length);
        var header = new byte[longestSignature];

        request.Content.Position = 0;
        var read = await request.Content.ReadAsync(header);
        request.Content.Position = 0;

        var matches = signatures.Any(signature =>
            read >= signature.Length && header.Take(signature.Length).SequenceEqual(signature));

        return matches ? null : "The file contents do not match the file type.";
    }

    private async Task<bool> CanUploadToProjectAsync(int projectId, int userId)
    {
        return await _context.Projects.AnyAsync(p =>
            p.ProjectId == projectId &&
            (p.ProjectManagerId == userId || p.ProjectMembers.Any(pm => pm.UserId == userId)));
    }

    private async Task<IQueryable<Document>> AccessibleDocumentsAsync(int userId)
    {
        var isAdministrator = await _context.Users
            .AnyAsync(u => u.UserId == userId && u.Role == UserRole.Administrator);

        if (isAdministrator)
        {
            return _context.Documents;
        }

        return _context.Documents.Where(d =>
            d.UploadedByUserId == userId ||
            d.Shares.Any(s => s.SharedWithUserId == userId) ||
            (d.ProjectId != null &&
                (d.Project!.ProjectManagerId == userId ||
                 d.Project.ProjectMembers.Any(pm => pm.UserId == userId))));
    }

    private static async Task<DocumentPage> ApplyQueryAsync(IQueryable<Document> documents, DocumentQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            documents = documents.Where(d => d.Category == query.Category);
        }

        if (query.ProjectId.HasValue)
        {
            documents = documents.Where(d => d.ProjectId == query.ProjectId);
        }

        if (query.UploadedFrom.HasValue)
        {
            documents = documents.Where(d => d.UploadedDate >= query.UploadedFrom.Value);
        }

        if (query.UploadedTo.HasValue)
        {
            var inclusiveEnd = query.UploadedTo.Value.Date.AddDays(1);
            documents = documents.Where(d => d.UploadedDate < inclusiveEnd);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();

            documents = documents.Where(d =>
                d.Title.ToLower().Contains(term) ||
                (d.Description != null && d.Description.ToLower().Contains(term)) ||
                (d.Tags != null && d.Tags.ToLower().Contains(term)) ||
                d.UploadedByUser.DisplayName.ToLower().Contains(term) ||
                (d.Project != null && d.Project.Name.ToLower().Contains(term)));
        }

        var totalCount = await documents.CountAsync();

        documents = Sort(documents, query);

        var items = await documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Skip((Math.Max(query.Page, 1) - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new DocumentPage(items, totalCount);
    }

    private static IQueryable<Document> Sort(IQueryable<Document> documents, DocumentQuery query)
    {
        return (query.SortBy, query.SortDescending) switch
        {
            ("Title", true) => documents.OrderByDescending(d => d.Title),
            ("Title", false) => documents.OrderBy(d => d.Title),
            ("Category", true) => documents.OrderByDescending(d => d.Category),
            ("Category", false) => documents.OrderBy(d => d.Category),
            ("FileSizeBytes", true) => documents.OrderByDescending(d => d.FileSizeBytes),
            ("FileSizeBytes", false) => documents.OrderBy(d => d.FileSizeBytes),
            (_, false) => documents.OrderBy(d => d.UploadedDate),
            _ => documents.OrderByDescending(d => d.UploadedDate)
        };
    }

    private async Task RecordActivityAsync(int documentId, int userId, string action, string? details)
    {
        _context.DocumentActivities.Add(new DocumentActivity
        {
            DocumentId = documentId,
            UserId = userId,
            Action = action,
            Details = details,
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    private async Task NotifyProjectMembersAsync(Document document, int uploaderId)
    {
        if (!document.ProjectId.HasValue)
        {
            return;
        }

        var project = await _context.Projects
            .Include(p => p.ProjectMembers)
            .FirstOrDefaultAsync(p => p.ProjectId == document.ProjectId.Value);

        if (project == null)
        {
            return;
        }

        var recipientIds = project.ProjectMembers
            .Select(pm => pm.UserId)
            .Append(project.ProjectManagerId)
            .Distinct()
            .Where(id => id != uploaderId)
            .ToList();

        var uploaderName = await _context.Users
            .Where(u => u.UserId == uploaderId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync() ?? "A team member";

        foreach (var recipientId in recipientIds)
        {
            await _notifications.CreateNotificationAsync(new Notification
            {
                UserId = recipientId,
                Title = "New project document",
                Message = $"{uploaderName} added \"{document.Title}\" to {project.Name}.",
                Type = NotificationType.ProjectUpdate,
                Priority = NotificationPriority.Informational
            });
        }
    }

    private static string? NormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return null;
        }

        var normalized = tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count == 0 ? null : string.Join(", ", normalized);
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var invalid = Path.GetInvalidFileNameChars();

        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
