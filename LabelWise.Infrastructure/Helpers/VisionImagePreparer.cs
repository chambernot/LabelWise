using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LabelWise.Infrastructure.Helpers;

/// <summary>
/// Garante que a imagem chegue à OpenAI Vision em qualidade ótima:
///   - Sempre colorida (rejeita imagens monocromáticas/grayscale).
///   - Auto-orientação aplicada (corrige fotos rotacionadas).
///   - Lado mais curto entre 1536 px e 2048 px (resolução suficiente para
///     OCR de tabelas nutricionais pequenas e/ou rotacionadas dentro da foto,
///     sem inflar custo de tokens da OpenAI além do necessário).
///   - Lado mais longo limitado a 2048 px (limite recomendado da Vision API
///     em "high detail").
///   - Reencodada como JPEG com qualidade 92 (boa nitidez de texto, tamanho
///     controlado).
///
/// Regra genérica, sem heurística por produto.
/// </summary>
public static class VisionImagePreparer
{
    private const int MinShortSide = 1536;
    private const int MaxLongSide = 2048;
    private const int JpegQuality = 92;
    private const int GrayscaleColorDeltaThreshold = 8;
    private const int GrayscaleMinColorfulSamples = 5;

    public static VisionImageResult Prepare(byte[] originalBytes)
    {
        if (originalBytes is null || originalBytes.Length == 0)
            return VisionImageResult.Fail("Imagem vazia.");

        try
        {
            using var image = Image.Load<Rgba32>(originalBytes);

            if (IsLikelyGrayscale(image))
            {
                return VisionImageResult.Fail(
                    "Imagem aparenta estar em preto e branco. Envie uma foto colorida do rótulo.");
            }

            image.Mutate(ctx => ctx.AutoOrient());

            var (targetWidth, targetHeight) = ComputeTargetSize(image.Width, image.Height);

            if (targetWidth != image.Width || targetHeight != image.Height)
            {
                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth, targetHeight),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Lanczos3
                }));
            }

            using var output = new MemoryStream();
            image.Save(output, new JpegEncoder { Quality = JpegQuality });

            return VisionImageResult.Ok(
                output.ToArray(),
                "image/jpeg",
                image.Width,
                image.Height);
        }
        catch (Exception ex)
        {
            return VisionImageResult.Fail($"Falha ao preparar imagem: {ex.Message}");
        }
    }

    private static (int width, int height) ComputeTargetSize(int width, int height)
    {
        var shortSide = Math.Min(width, height);
        var longSide = Math.Max(width, height);

        double scale = 1.0;

        if (shortSide < MinShortSide)
            scale = (double)MinShortSide / shortSide;

        if (longSide * scale > MaxLongSide)
            scale = (double)MaxLongSide / longSide;

        if (Math.Abs(scale - 1.0) < 0.001)
            return (width, height);

        var newWidth = Math.Max(1, (int)Math.Round(width * scale));
        var newHeight = Math.Max(1, (int)Math.Round(height * scale));
        return (newWidth, newHeight);
    }

    private static bool IsLikelyGrayscale(Image<Rgba32> image)
    {
        var stepX = Math.Max(1, image.Width / 64);
        var stepY = Math.Max(1, image.Height / 64);
        var colorfulSamples = 0;

        for (var y = 0; y < image.Height; y += stepY)
        {
            for (var x = 0; x < image.Width; x += stepX)
            {
                var pixel = image[x, y];
                if (Math.Abs(pixel.R - pixel.G) > GrayscaleColorDeltaThreshold ||
                    Math.Abs(pixel.R - pixel.B) > GrayscaleColorDeltaThreshold ||
                    Math.Abs(pixel.G - pixel.B) > GrayscaleColorDeltaThreshold)
                {
                    colorfulSamples++;
                    if (colorfulSamples >= GrayscaleMinColorfulSamples)
                        return false;
                }
            }
        }

        return true;
    }
}

public sealed record VisionImageResult(
    bool Success,
    byte[] Bytes,
    string MimeType,
    int Width,
    int Height,
    string? ErrorMessage)
{
    public static VisionImageResult Ok(byte[] bytes, string mimeType, int width, int height) =>
        new(true, bytes, mimeType, width, height, null);

    public static VisionImageResult Fail(string error) =>
        new(false, Array.Empty<byte>(), string.Empty, 0, 0, error);
}
