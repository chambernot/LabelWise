namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Resultado do scoring nutricional avançado com suporte a múltiplos perfis de saúde.
    /// </summary>
    public class AdvancedNutritionScoreResult
    {
        /// <summary>Score geral de 0 a 100.</summary>
        public double OverallScore { get; set; }

        /// <summary>Score para perfil diabético (0–100).</summary>
        public double DiabetesScore { get; set; }

        /// <summary>Score para perfil hipertensão (0–100).</summary>
        public double HypertensionScore { get; set; }

        /// <summary>Score para perfil emagrecimento (0–100).</summary>
        public double WeightLossScore { get; set; }

        /// <summary>Score para perfil ganho de massa (0–100).</summary>
        public double MuscleGainScore { get; set; }

        /// <summary>Detalhamento dos blocos que compõem o score geral.</summary>
        public ScoreBreakdown Breakdown { get; set; } = new();

        /// <summary>Alertas nutricionais relevantes para o usuário.</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>Destaques positivos do produto.</summary>
        public List<string> Highlights { get; set; } = new();
    }

    /// <summary>
    /// Decomposição do score por bloco de avaliação.
    /// </summary>
    public class ScoreBreakdown
    {
        /// <summary>Score de qualidade nutricional (0–100, peso 40%).</summary>
        public double NutritionalQuality { get; set; }

        /// <summary>Score de nível de processamento (0–100, peso 20%).</summary>
        public double ProcessingLevel { get; set; }

        /// <summary>Score base de adequação ao perfil padrão (0–100, peso 20%).</summary>
        public double ProfileAdequacy { get; set; }

        /// <summary>Score de consistência de claims (0–100, peso 10%).</summary>
        public double ClaimsEvaluation { get; set; }

        /// <summary>Score de densidade nutricional (0–100, peso 10%).</summary>
        public double NutritionalDensity { get; set; }
    }
}
