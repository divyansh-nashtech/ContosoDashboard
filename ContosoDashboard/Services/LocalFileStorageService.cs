namespace ContosoDashboard.Services;

public class LocalFileStorageService : IFileStorageService
{
    private const string PersonalFolder = "personal";

    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<LocalFileStorageService> logger)
    {
        var configuredRoot = configuration["DocumentStorage:RootPath"] ?? "AppData/uploads";

        _rootPath = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(environment.ContentRootPath, configuredRoot);

        _logger = logger;

        Directory.CreateDirectory(_rootPath);
    }

    public string BuildPath(int userId, int? projectId, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var scope = projectId.HasValue ? projectId.Value.ToString() : PersonalFolder;

        return $"{userId}/{scope}/{Guid.NewGuid()}{extension}";
    }

    public async Task<string> UploadAsync(Stream content, string path, string contentType)
    {
        var fullPath = ResolveFullPath(path);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(target);

        return path;
    }

    public Task<Stream?> DownloadAsync(string path)
    {
        var fullPath = ResolveFullPath(path);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Stored file {Path} was not found", path);
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string path)
    {
        var fullPath = ResolveFullPath(path);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<string> GetUrlAsync(string path, TimeSpan expiration)
    {
        // Local files are never served directly; the caller routes through the
        // authorized download endpoint. A cloud implementation returns a signed URL here.
        return Task.FromResult(path);
    }

    public Task<bool> ExistsAsync(string path)
    {
        return Task.FromResult(File.Exists(ResolveFullPath(path)));
    }

    private string ResolveFullPath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, path));

        if (!fullPath.StartsWith(Path.GetFullPath(_rootPath), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Storage path '{path}' resolves outside the storage root.");
        }

        return fullPath;
    }
}
