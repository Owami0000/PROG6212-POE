namespace RaceDay.Api.Services;

/// <summary>Validation for uploaded images (event banners and profile pictures).</summary>
public static class ImageRules
{
    public const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/gif", "image/webp" };
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    /// <summary>Returns an error message, or null when the file is acceptable.</summary>
    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0) return "No file was supplied.";
        if (file.Length > MaxBytes) return "The image must be 5 MB or smaller.";
        if (!AllowedContentTypes.Contains(file.ContentType?.ToLowerInvariant()))
            return "Only JPG, PNG, GIF or WEBP images are allowed.";
        if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            return "Only JPG, PNG, GIF or WEBP images are allowed.";
        return null;
    }
}
