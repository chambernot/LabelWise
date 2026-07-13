namespace LabelWise.Api.Models
{
    /// <summary>
    /// Form model para análise nutricional refatorada com upload de imagem.
    /// </summary>
    public class RefactoredNutritionAnalysisFormModel
    {
        /// <summary>
        /// Imagem do produto alimentício (frontal ou tabela nutricional).
        /// </summary>
        public IFormFile Image { get; set; } = null!;

        /// <summary>
        /// Código do idioma para análise (padrão: "pt").
        /// </summary>
        public string LanguageCode { get; set; } = "pt";
    }
}
