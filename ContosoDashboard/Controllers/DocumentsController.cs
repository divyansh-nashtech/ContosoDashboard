using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ContosoDashboard.Services;

namespace ContosoDashboard.Controllers;

[Authorize]
[Route("api/documents")]
public class DocumentsController : Controller
{
    private static readonly string[] PreviewableContentTypes =
    {
        "application/pdf",
        "image/jpeg",
        "image/png"
    };

    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var file = await GetFileAsync(id);
        if (file == null)
        {
            return NotFound();
        }

        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("{id:int}/preview")]
    public async Task<IActionResult> Preview(int id)
    {
        var file = await GetFileAsync(id);
        if (file == null)
        {
            return NotFound();
        }

        if (!PreviewableContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            file.Content.Dispose();
            return BadRequest("This document type cannot be previewed in the browser.");
        }

        return File(file.Content, file.ContentType);
    }

    private async Task<DocumentFile?> GetFileAsync(int documentId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        return await _documentService.OpenForDownloadAsync(documentId, userId);
    }
}
