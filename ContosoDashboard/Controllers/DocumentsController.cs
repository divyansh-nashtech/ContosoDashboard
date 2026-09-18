using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ContosoDashboard.Services;

namespace ContosoDashboard.Controllers;

[Authorize]
[Route("api/documents")]
public class DocumentsController : Controller
{
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
