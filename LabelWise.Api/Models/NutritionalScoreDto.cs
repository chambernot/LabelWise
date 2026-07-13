namespace LabelWise.Api.Models
{
    public class NutritionalScoreDto
    {
        public int Value { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string SafeLabel { get; set; } = string.Empty;
        public string RecommendationLevel { get; set; } = string.Empty;
        public string SemanticRecommendation { get; set; } = string.Empty;
        public string AbsoluteRecommendation { get; set; } = string.Empty;
        public string ComparativeRecommendation { get; set; } = string.Empty;
        public string ScoreInterpretation { get; set; } = string.Empty;
        public string AbsoluteLabel { get; set; } = string.Empty;
        public string ComparativeLabel { get; set; } = string.Empty;
        public string ProcessingLevel { get; set; } = string.Empty;
        public bool RequiresModeration { get; set; }
        public bool? IsUltraProcessed { get; set; }
        public string Confidence { get; set; } = string.Empty;
        public string PrincipalOffender { get; set; } = string.Empty;
    }
}
