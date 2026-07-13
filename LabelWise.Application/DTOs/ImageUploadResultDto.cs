using System;

namespace LabelWise.Application.DTOs
{
    /// <summary>
    /// Representa o resultado do upload de imagem.
    /// </summary>
    public class ImageUploadResultDto
    {
        public string ImagePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
