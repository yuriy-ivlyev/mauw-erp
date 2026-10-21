using System.Text.RegularExpressions;

namespace Mauw.Erp.Application.Services;

public interface IImageStorageService
{
    /// <summary>Saves the image at images/{targetId}/{fileName} and returns the S3 key.</summary>
    Task<string> UploadImageAsync(Guid targetId, Stream imageStream, string fileName, string contentType, CancellationToken ct = default);

    /// <exception cref="FileNotFoundException">When the key does not exist.</exception>
    Task<(Stream Stream, string ContentType)> GetImageStreamAsync(string filePath, CancellationToken ct = default);

    Task DeleteImageAsync(string filePath, CancellationToken ct = default);
}

public static partial class ImageStorageKeys
{
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex SafeFileName();

    public static bool IsValidFileName(string? fileName) =>
        fileName is not null && SafeFileName().IsMatch(fileName) && !fileName.Contains("..");

    public static string ToUrl(string key) => $"/api/{key}";

    public static string Build(Guid targetId, string fileName) =>
        IsValidFileName(fileName)
            ? $"images/{targetId:D}/{fileName}"
            : throw new ArgumentException("Invalid file name.", nameof(fileName));
}
