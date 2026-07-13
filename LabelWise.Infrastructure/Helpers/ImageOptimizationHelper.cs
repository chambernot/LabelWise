using SixLabors.ImageSharp;

namespace LabelWise.Infrastructure.Helpers;

public static class ImageOptimizationHelper
{
    public static async Task<OptimizedImagePayload> OptimizeForVisionAsync(
        byte[] imageBytes,
        string fileName,
        int maxDimension = 2048,
        int jpegQuality = 90,
        int passthroughMaxBytes = 512 * 1024)
    {
        var originalMimeType = GetMimeType(fileName);

        await using var input = new MemoryStream(imageBytes, writable: false);
        using var image = await Image.LoadAsync(input);

        return new OptimizedImagePayload(imageBytes, originalMimeType, image.Width, image.Height, false);
    }

    private static string GetMimeType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }
}

public sealed record OptimizedImagePayload(byte[] Bytes, string MimeType, int Width, int Height, bool WasOptimized);
