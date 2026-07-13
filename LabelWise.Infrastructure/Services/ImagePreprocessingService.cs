using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Aplica apenas correções seguras para preservar a imagem completa antes do OCR/Vision.
/// </summary>
public sealed class ImagePreprocessingService : IImagePreprocessingService
{
    private readonly ILogger<ImagePreprocessingService> _logger;

    public ImagePreprocessingService(ILogger<ImagePreprocessingService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public byte[] EnhanceForOcr(byte[] imageBytes)
    {
        if (imageBytes is not { Length: > 0 })
            return imageBytes;

        try
        {
            using var input = new MemoryStream(imageBytes, writable: false);
            using var image = Image.Load(input);

            var originalWidth = image.Width;
            var originalHeight = image.Height;
            var decodedFormat = image.Metadata.DecodedImageFormat;

            _logger.LogInformation("[ImagePreprocessing] Original size: {Size}", imageBytes.Length);

            image.Mutate(ctx => ctx.AutoOrient());

            using var output = new MemoryStream();

            if (decodedFormat is null)
            {
                _logger.LogWarning("[ImagePreprocessing] Formato da imagem não identificado — usando imagem original.");
                return imageBytes;
            }

            image.Save(output, decodedFormat);

            var result = output.ToArray();

            _logger.LogInformation("[ImagePreprocessing] Processed size: {Size}", result.Length);

            _logger.LogDebug(
                "[ImagePreprocessing] Pré-processamento seguro aplicado: {OriginalWidth}x{OriginalHeight} → {ProcessedWidth}x{ProcessedHeight}, {Original}KB → {Enhanced}KB",
                originalWidth,
                originalHeight,
                image.Width,
                image.Height,
                imageBytes.Length / 1024,
                result.Length / 1024);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[ImagePreprocessing] Falha no pré-processamento seguro — usando imagem original.");
            return imageBytes;
        }
    }
}
