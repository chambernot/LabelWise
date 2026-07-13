namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Score nutricional unificado — única fonte de verdade para pontuação.
    /// Substitui nutritionalScore e advancedScore.
    /// </summary>
    public class UnifiedNutritionScore
    {
        /// <summary>Pontuação de 0 a 100.</summary>
        public int Value { get; set; }

        /// <summary>Rótulo semântico: Excelente | Bom | Atenção | Ruim.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Cor de exibição: green | light_green | yellow | red.</summary>
        public string Color { get; set; } = string.Empty;

        public string Confidence { get; set; } = "low";

        public string Reliability { get; set; } = "unsafe_read";

        /// <summary>Principal nutriente problemático identificado.</summary>
        public string PrincipalOffender { get; set; } = "nenhum relevante";

        /// <summary>Destaques positivos do produto.</summary>
        public List<string> Highlights { get; set; } = new();

        /// <summary>Alertas nutricionais relevantes para o usuário.</summary>
        public List<string> Warnings { get; set; } = new();
    }
}
