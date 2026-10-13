using HospitalRecords.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HospitalRecords.Infrastructure.Services;

public class FileSystemObjectStorageService : IObjectStorageService
{
    private readonly string _baseStoragePath;
    private readonly ILogger<FileSystemObjectStorageService> _logger;

    public FileSystemObjectStorageService(IConfiguration configuration, ILogger<FileSystemObjectStorageService> logger)
    {
        _logger = logger;
        _baseStoragePath = configuration["Storage:BasePath"] 
            ?? Path.Combine(AppContext.BaseDirectory, "StorageVault");

        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<string> UploadAsync(Stream stream, string storageKey, string contentType, CancellationToken cancellationToken = default)
    {
        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_baseStoragePath, normalizedKey);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write file with immutability guarantee (do not overwrite existing historical versions)
        if (File.Exists(fullPath))
        {
            _logger.LogWarning("Storage key {StorageKey} already exists. Enforcing version immutability.", storageKey);
            return storageKey;
        }

        stream.Position = 0;
        using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        _logger.LogInformation("Saved immutable document asset: {StorageKey} ({ContentType})", storageKey, contentType);
        return storageKey;
    }

    public Task<Stream?> DownloadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_baseStoragePath, normalizedKey);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        var memoryStream = new MemoryStream();
        using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fileStream.CopyTo(memoryStream);
        }

        memoryStream.Position = 0;
        return Task.FromResult<Stream?>(memoryStream);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_baseStoragePath, normalizedKey);

        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_baseStoragePath, normalizedKey);

        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted document physical file: {StorageKey}", storageKey);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete physical file {StorageKey}", storageKey);
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }
}
