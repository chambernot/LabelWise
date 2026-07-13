namespace LabelWise.Application.DTOs
{
    public class OcrRequestDto
    {
        public string ImagePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}
