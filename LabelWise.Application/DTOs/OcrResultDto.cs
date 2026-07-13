using System.Collections.Generic;

namespace LabelWise.Application.DTOs
{
    public class OcrResultDto
    {
        public string RawText { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public List<OcrTextBlock> TextBlocks { get; set; } = new();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Metadata do provider de OCR utilizado.
        /// Útil para debug e validação de qual provider foi usado.
        /// </summary>
        public Dictionary<string, string>? ProviderMetadata { get; set; }
    }

    public class OcrTextBlock
    {
        public string Text { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public BoundingBox? BoundingBox { get; set; }
        public string BlockType { get; set; } = "TEXT"; // TEXT, TITLE, TABLE, etc.
    }

    public class BoundingBox
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        /// <summary>
        /// Coordenada X do centro do bloco de texto.
        /// Útil para clustering de colunas.
        /// </summary>
        public double X => Left + (Width / 2);

        /// <summary>
        /// Coordenada Y do centro do bloco de texto.
        /// Útil para clustering de linhas.
        /// </summary>
        public double Y => Top + (Height / 2);

        /// <summary>
        /// Coordenada X da borda direita.
        /// </summary>
        public double Right => Left + Width;

        /// <summary>
        /// Coordenada Y da borda inferior.
        /// </summary>
        public double Bottom => Top + Height;
    }
}
