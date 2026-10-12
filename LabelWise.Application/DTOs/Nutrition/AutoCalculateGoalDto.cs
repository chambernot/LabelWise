namespace LabelWise.Application.DTOs.Nutrition
{
    public class AutoCalculateGoalDto
    {
        public decimal WeightKg { get; set; }
        public int TargetCalories { get; set; }
        public decimal TargetProteinG { get; set; }
        public decimal TargetCarbsG { get; set; }
        public decimal TargetFatG { get; set; }
        public int TargetWaterMl { get; set; }
        public string ShortExplanation { get; set; } = string.Empty;
    }
}