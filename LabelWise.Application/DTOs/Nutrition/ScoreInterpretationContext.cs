namespace LabelWise.Application.DTOs.Nutrition
{
    public class ScoreInterpretationContext
    {
        public int Score { get; set; }
        public string? ProductName { get; set; }
        public string? Category { get; set; }
        public List<string> VisibleClaims { get; set; } = new();
        public string? PrincipalOffender { get; set; }
        public EstimatedNutritionProfileDto? NutritionProfile { get; set; }
        public ProductClassificationDto? Classification { get; set; }
    }
}
