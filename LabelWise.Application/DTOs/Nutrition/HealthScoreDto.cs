namespace LabelWise.Application.DTOs.Nutrition
{
    public class HealthScoreDto
    {
        public int Value { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
