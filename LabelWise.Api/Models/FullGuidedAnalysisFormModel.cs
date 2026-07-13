using Microsoft.AspNetCore.Http;

namespace LabelWise.Api.Models
{
    /// <summary>
    /// Modelo de form data para o endpoint de desenvolvimento de análise guiada completa.
    /// </summary>
    public class FullGuidedAnalysisFormModel
    {
        /// <summary>
        /// Imagem frontal da embalagem (opcional).
        /// </summary>
        public IFormFile? FrontImage { get; set; }

        /// <summary>
        /// Imagem da lista de ingredientes (recomendado).
        /// </summary>
        public IFormFile? IngredientsImage { get; set; }

        /// <summary>
        /// Imagem da tabela nutricional (recomendado).
        /// </summary>
        public IFormFile? NutritionImage { get; set; }

        /// <summary>
        /// Imagem da declaração de alérgenos (opcional).
        /// </summary>
        public IFormFile? AllergenImage { get; set; }

        /// <summary>
        /// Código de barras manual (opcional).
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Código do idioma (padrão: pt-BR).
        /// </summary>
        public string LanguageCode { get; set; } = "pt-BR";

        /// <summary>
        /// Informações do dispositivo/teste.
        /// </summary>
        public string DeviceInfo { get; set; } = "DevEndpoint-Test";
    }
}
