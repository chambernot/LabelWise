using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ZXing;
using ZXing.Common;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Detecta códigos de barras EAN/GTIN em imagens usando ZXing.Net + ImageSharp.
    ///
    /// Converte a imagem para RGB bruto e usa RGBLuminanceSource, evitando
    /// a incompatibilidade do binding ZXing.ImageSharp com ImageSharp v3.
    ///
    /// Estratégia:
    ///   1. Tenta leitura direta com TryHarder + TryInverted.
    ///   2. Se falhar e a imagem for grande, tenta após redimensionar para 1000px
    ///      (melhora detecção em fotos com barcode pequeno em relação à imagem).
    /// </summary>
    public class BarcodeDetectorService : IBarcodeDetectorService
    {
        private static readonly BarcodeFormat[] SupportedFormats =
        [
            BarcodeFormat.EAN_13,
            BarcodeFormat.EAN_8,
            BarcodeFormat.UPC_A,
            BarcodeFormat.UPC_E,
            BarcodeFormat.CODE_128,
            BarcodeFormat.CODE_39,
            BarcodeFormat.QR_CODE,
            BarcodeFormat.DATA_MATRIX
        ];

        private readonly ILogger<BarcodeDetectorService> _logger;

        public BarcodeDetectorService(ILogger<BarcodeDetectorService> logger)
        {
            _logger = logger;
        }

        public string? DetectBarcode(byte[] imageData)
        {
            try
            {
                using var image = Image.Load<Rgb24>(imageData);

                var result = TryDecode(image);
                if (result != null)
                {
                    _logger.LogInformation("Barcode detectado: {Barcode} (formato: {Format})",
                        result.Text, result.BarcodeFormat);
                    return result.Text;
                }

                // Segunda tentativa: redimensionar para 1000px de largura
                if (image.Width > 1200)
                {
                    int targetWidth  = 1000;
                    int targetHeight = (int)(image.Height * (targetWidth / (double)image.Width));

                    using var resized = image.Clone(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(targetWidth, targetHeight),
                        Mode = ResizeMode.Stretch
                    }));

                    result = TryDecode(resized);
                    if (result != null)
                    {
                        _logger.LogInformation("Barcode detectado após resize: {Barcode} (formato: {Format})",
                            result.Text, result.BarcodeFormat);
                        return result.Text;
                    }
                }

                _logger.LogDebug("Nenhum barcode encontrado na imagem");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao tentar detectar barcode na imagem");
                return null;
            }
        }

        private static Result? TryDecode(Image<Rgb24> image)
        {
            // Copia os pixels para um array RGB plano compatível com RGBLuminanceSource
            int width  = image.Width;
            int height = image.Height;
            var rgb    = new byte[width * height * 3];

            image.CopyPixelDataTo(rgb);

            var luminance = new RGBLuminanceSource(rgb, width, height, RGBLuminanceSource.BitmapFormat.RGB24);

            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    TryHarder             = true,
                    TryInverted           = true,
                    ReturnCodabarStartEnd = false,
                    PossibleFormats       = SupportedFormats
                }
            };

            return reader.Decode(luminance);
        }
    }
}
