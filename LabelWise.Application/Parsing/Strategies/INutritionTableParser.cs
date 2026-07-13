namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Parser especializado para tabelas nutricionais.
    /// Foco: extrair valores nutricionais (calorias, macro/micronutrientes, porção)
    /// </summary>
    public interface INutritionTableParser
    {
        /// <summary>
        /// Extrai informações nutricionais do texto OCR de uma tabela nutricional.
        /// </summary>
        /// <param name="ocrText">Texto extraído pela OCR (deve ser de uma tabela nutricional)</param>
        /// <returns>Resultado do parsing com dados nutricionais</returns>
        NutritionTableParseResult Parse(string ocrText);
    }
}
