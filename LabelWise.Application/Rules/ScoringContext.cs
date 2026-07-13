namespace LabelWise.Application.Rules
{
    public class ScoringContext
    {
        public double GeneralScore { get; set; } = 1.0; // 1.0 is neutral, reduce or increase
        public double PersonalizedScore { get; set; } = 1.0;
    }
}
