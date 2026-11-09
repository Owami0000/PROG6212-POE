using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>Raised when an image cannot be stored (not configured, Azure unavailable, etc.).</summary>
public class BlobStorageException : Exception
{
    public BlobStorageException(string message, Exception? inner = null) : base(message, inner) { }
}

public interface IBlobStorageService
{
    /// <summary>Uploads an image to Azure Blob Storage and returns its URL.</summary>
    Task<string> UploadAsync(IFormFile file, string folder, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores uploaded images in Azure Blob Storage. Only the API talks to Azure - the MVC app never does.
/// Settings come from configuration (user secrets / environment variables), never from source code:
///   AzureBlobStorage:ConnectionString
///   AzureBlobStorage:ContainerName   (default "raceday-images")
///   AzureBlobStorage:PublicAccess    (default true - lets browsers display the blob URL directly)
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private readonly IConfiguration _config;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IConfiguration config, ILogger<BlobStorageService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<string> UploadAsync(IFormFile file, string folder, CancellationToken cancellationToken = default)
    {
        var connectionString = _config["AzureBlobStorage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
            throw new BlobStorageException("Azure Blob Storage is not configured.");

        var containerName = _config["AzureBlobStorage:ContainerName"] ?? "raceday-images";
        var publicAccess = _config.GetValue("AzureBlobStorage:PublicAccess", true);

        try
        {
            var container = new BlobContainerClient(connectionString, containerName);
            await container.CreateIfNotExistsAsync(
                publicAccess ? PublicAccessType.Blob : PublicAccessType.None,
                cancellationToken: cancellationToken);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var blob = container.GetBlobClient($"{folder}/{Guid.NewGuid():N}{extension}");

            await using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType }, cancellationToken: cancellationToken);
            return blob.Uri.ToString();
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Blob upload failed.");
            throw new BlobStorageException("The image could not be stored.", ex);
        }
        catch (ArgumentException ex) // malformed connection string
        {
            _logger.LogError(ex, "Azure Blob Storage connection string is invalid.");
            throw new BlobStorageException("Azure Blob Storage is not configured correctly.", ex);
        }
    }
}
