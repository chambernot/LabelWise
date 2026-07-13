namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Detecta código de barras (EAN/GTIN/QR) a partir dos bytes de uma imagem.
    /// Retorna null se nenhum barcode for encontrado.
    /// </summary>
    public interface IBarcodeDetectorService
    {
        string? DetectBarcode(byte[] imageData);
    }
}
