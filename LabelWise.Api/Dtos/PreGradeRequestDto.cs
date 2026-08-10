namespace LabelWise.Api.Dtos
{
    public class PreGradeRequestDto
    {
        public decimal CurrentRawValue { get; set; }
        public IFormFile? FrontStraight { get; set; }
        public IFormFile? FrontAngled { get; set; }
        public IFormFile? BackStraight { get; set; }
        public IFormFile? BackAngled { get; set; }
    }
}
