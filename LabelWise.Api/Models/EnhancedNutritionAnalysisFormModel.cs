using Microsoft.AspNetCore.Http;

namespace LabelWise.Api.Models
{
    /// <summary>
    /// Form model para análise nutricional enriquecida.
    /// Necessário para suporte correto no Swagger/OpenAPI com multipart/form-data.
    /// </summary>
    public class EnhancedNutritionAnalysisFormModel
    {
        /// <summary>
        /// Imagem do produto a ser analisado.
        /// </summary>
        public IFormFile Image { get; set; } = null!;

        /// <summary>
        /// Contexto adicional opcional para ajudar a interpretação.
        /// </summary>
        public string? AdditionalContext { get; set; }
    }
}
