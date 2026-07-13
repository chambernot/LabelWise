namespace LabelWise.Application.DTOs.Nutrition
{
    public class ProductClassificationDto
    {
        public HealthProfileResult? Diabetic { get; set; }
        public HealthProfileResult? BloodPressure { get; set; }
        public HealthProfileResult? WeightLoss { get; set; }
        public HealthProfileResult? MuscleGain { get; set; }
    }
}
