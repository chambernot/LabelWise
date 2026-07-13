namespace LabelWise.Domain.Enums
{
    /// <summary>
    /// Define os tipos de captura de imagem no processo de análise de produtos.
    /// Cada tipo representa uma parte específica do produto que será analisada.
    /// </summary>
    public enum CaptureType
    {
        /// <summary>
        /// Captura do código de barras (EAN, UPC, etc.) para identificação do produto.
        /// Usado para buscar informações em bases de dados externas (Open Food Facts, etc.).
        /// </summary>
        Barcode = 1,

        /// <summary>
        /// Captura da embalagem frontal do produto.
        /// Contém marca, nome do produto, claims nutricionais e marketing.
        /// </summary>
        FrontPackaging = 2,

        /// <summary>
        /// Captura da tabela nutricional (nutrition facts).
        /// Contém valores energéticos, macro e micronutrientes.
        /// </summary>
        NutritionTable = 3,

        /// <summary>
        /// Captura da lista de ingredientes.
        /// Contém todos os ingredientes em ordem decrescente de quantidade.
        /// </summary>
        IngredientsList = 4,

        /// <summary>
        /// Captura da declaração de alérgenos.
        /// Contém alertas sobre presença ou traços de alérgenos.
        /// </summary>
        AllergenStatement = 5
    }
}
