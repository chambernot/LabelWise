using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Models.Nutrition
{
    /// <summary>
    /// Dados enriquecidos resultantes da validação e fallback nutricional aplicados
    /// sobre o retorno bruto da IA. O JSON original da IA nunca é alterado.
    /// </summary>
    public class NutritionEnrichedData
    {
        /// <summary>
        /// Perfil nutricional normalizado (valores inválidos removidos, fallback aplicado se necessário).
        /// </summary>
        public EstimatedNutritionProfileDto NormalizedProfile { get; set; } = new();

        /// <summary>
        /// Indica se algum campo de fallback foi preenchido pelo backend.
        /// </summary>
        public bool FallbackUsed { get; set; }

        /// <summary>
        /// Confiança geral dos dados: "alta", "media" ou "baixa".
        /// </summary>
        public string Confidence { get; set; } = "baixa";

        /// <summary>
        /// Indica inconsistência calórica detectada (calorias menores que a metade do esperado
        /// a partir dos macros). Não é corrigida automaticamente.
        /// </summary>
        public bool HasCaloriesInconsistency { get; set; }

        /// <summary>
        /// Nível de processamento inferido: "in_natura", "processado" ou "ultraprocessado".
        /// </summary>
        public string ProcessingLevel { get; set; } = "desconhecido";

        /// <summary>
        /// Principal nutriente problemático identificado pelo backend.
        /// </summary>
        public string PrincipalOffender { get; set; } = "nenhum relevante";

        /// <summary>
        /// Avisos técnicos gerados durante a validação (ex: valor substituído por fallback).
        /// </summary>
        public List<string> ValidationWarnings { get; set; } = new();

        public NutritionConfidenceResult? ConfidenceDetails { get; set; }

        /// <summary>
        /// Indica se os dados gerais possuem baixa confiança, ativando modo seguro global.
        /// </summary>
        public bool IsLowConfidenceMode { get; set; }
    }
}
