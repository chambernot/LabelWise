namespace LabelWise.Application.Confidence
{
    /// <summary>
    /// Representa uma pontuação de confiança com valor numérico e nível categórico.
    /// </summary>
    public readonly struct ConfidenceScore
    {
        /// <summary>
        /// Valor numérico da confiança (0.0 a 1.0)
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Nível categórico da confiança
        /// </summary>
        public ConfidenceLevel Level { get; }

        /// <summary>
        /// Texto explicativo sobre a pontuação
        /// </summary>
        public string Reason { get; }

        public ConfidenceScore(double value, string? reason = null)
        {
            Value = Math.Clamp(value, 0.0, 1.0);
            Level = DetermineLevel(Value);
            Reason = reason ?? string.Empty;
        }

        private static ConfidenceLevel DetermineLevel(double value) => value switch
        {
            >= ConfidenceThresholds.High => ConfidenceLevel.High,
            >= ConfidenceThresholds.Medium => ConfidenceLevel.Medium,
            >= ConfidenceThresholds.Low => ConfidenceLevel.Low,
            _ => ConfidenceLevel.VeryLow
        };

        public static ConfidenceScore High(string? reason = null) => new(0.90, reason);
        public static ConfidenceScore Medium(string? reason = null) => new(0.65, reason);
        public static ConfidenceScore Low(string? reason = null) => new(0.40, reason);
        public static ConfidenceScore VeryLow(string? reason = null) => new(0.20, reason);

        public override string ToString() => $"{Level} ({Value:P0})";
    }

    /// <summary>
    /// Níveis de confiança categóricos
    /// </summary>
    public enum ConfidenceLevel
    {
        VeryLow = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    /// <summary>
    /// Thresholds padrão para classificação de confiança
    /// </summary>
    public static class ConfidenceThresholds
    {
        /// <summary>Threshold para confiança alta (≥90%)</summary>
        public const double High = 0.90;

        /// <summary>Threshold para confiança média (≥65%)</summary>
        public const double Medium = 0.65;

        /// <summary>Threshold para confiança baixa (≥40%)</summary>
        public const double Low = 0.40;

        /// <summary>
        /// Threshold mínimo aceitável para classificação Safe (≥70%)
        /// </summary>
        public const double SafeClassificationMinimum = 0.70;

        /// <summary>
        /// Penalização máxima aplicável ao score (50%)
        /// </summary>
        public const double MaxScorePenalty = 0.50;
    }
}
