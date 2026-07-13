namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Request para análise nutricional simplificada de imagem de produto.
    /// </summary>
    public class SimpleNutritionAnalysisRequest
    {
        /// <summary>
        /// Dados da imagem em formato byte array.
        /// </summary>
        public byte[] ImageData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Nome do arquivo original.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Código do idioma para respostas (padrão: "pt").
        /// </summary>
        public string LanguageCode { get; set; } = "pt";

        /// <summary>
        /// Perfis de saúde específicos para filtrar (opcional).
        /// Se vazio, retorna todos os perfis.
        /// </summary>
        public List<string>? Profiles { get; set; }
    }
}
