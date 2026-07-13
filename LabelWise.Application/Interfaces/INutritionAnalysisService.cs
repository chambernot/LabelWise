using LabelWise.Application.DTOs.Nutrition;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Serviço para análise nutricional simplificada de produtos alimentícios.
    /// </summary>
    public interface INutritionAnalysisService
    {
        /// <summary>
        /// Analisa uma imagem de produto alimentício e retorna uma avaliação nutricional simplificada.
        /// </summary>
        /// <param name="imageData">Bytes da imagem do produto.</param>
        /// <param name="fileName">Nome do arquivo da imagem.</param>
        /// <param name="languageCode">Código do idioma para respostas.</param>
        /// <param name="requestedProfiles">Perfis específicos para filtrar (opcional).</param>
        /// <param name="userId">ID do usuário autenticado para persistência do histórico (opcional).</param>
        /// <returns>Resultado da análise nutricional simplificada.</returns>
        Task<NutritionAnalysisResponseDto> AnalyzeProductImageAsync(
            byte[] imageData,
            string fileName,
            string languageCode = "pt",
            List<string>? requestedProfiles = null,
            Guid? userId = null,
            string? deviceId = null);
    }
}
