namespace ContosoDashboard.Services;

public interface IFileStorageService
{
    string BuildPath(int userId, int? projectId, string originalFileName);

    Task<string> UploadAsync(Stream content, string path, string contentType);

    Task<Stream?> DownloadAsync(string path);

    Task DeleteAsync(string path);

    Task<string> GetUrlAsync(string path, TimeSpan expiration);

    Task<bool> ExistsAsync(string path);
}
