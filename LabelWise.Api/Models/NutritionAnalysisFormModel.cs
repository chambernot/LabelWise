using Microsoft.AspNetCore.Http;

namespace LabelWise.Api.Models
{
    /// <summary>
    /// Form model para análise nutricional simplificada.
    /// Necessário para suporte correto no Swagger/OpenAPI com multipart/form-data.
    /// </summary>
    public class NutritionAnalysisFormModel
    {
        /// <summary>
        /// Imagem do produto a ser analisado.
        /// </summary>
        public IFormFile File { get; set; } = null!;

        /// <summary>
        /// Código do idioma para respostas (padrão: "pt").
        /// </summary>
        public string LanguageCode { get; set; } = "pt";

        /// <summary>
        /// Perfis de saúde específicos para filtrar, separados por vírgula (opcional).
        /// Exemplo: "diabetic,weightLoss"
        /// </summary>
        public string? Profiles { get; set; }

        /// <summary>
        /// Identificador único do dispositivo para controle de acesso e histórico.
        /// </summary>
        public string? DeviceId { get; set; }
    }
}
