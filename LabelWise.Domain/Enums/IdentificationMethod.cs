namespace LabelWise.Domain.Enums
{
    /// <summary>
    /// Método ou fonte utilizada para identificação do produto.
    /// </summary>
    public enum IdentificationMethod
    {
        /// <summary>
        /// Identificação através de leitura de código de barras (EAN, UPC).
        /// </summary>
        BarcodeScanning = 1,

        /// <summary>
        /// Identificação através de consulta em base de dados externa (Open Food Facts, etc.).
        /// </summary>
        ExternalDatabase = 2,

        /// <summary>
        /// Identificação através de OCR na embalagem frontal (nome + marca).
        /// </summary>
        OcrFrontPackaging = 3,

        /// <summary>
        /// Identificação através de reconhecimento visual (AI/ML).
        /// </summary>
        VisualRecognition = 4,

        /// <summary>
        /// Entrada manual do usuário.
        /// </summary>
        ManualEntry = 5,

        /// <summary>
        /// Identificação através de múltiplos métodos (estratégia de fallback).
        /// </summary>
        Composite = 6
    }
}
