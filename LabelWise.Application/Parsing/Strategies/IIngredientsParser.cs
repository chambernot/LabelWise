namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Parser especializado para listas de ingredientes.
    /// Foco: extrair ingredientes após "INGREDIENTES", limpar ruídos, normalizar delimitadores
    /// </summary>
    public interface IIngredientsParser
    {
        /// <summary>
        /// Extrai lista de ingredientes do texto OCR.
        /// </summary>
        /// <param name="ocrText">Texto extraído pela OCR (deve conter seção de ingredientes)</param>
        /// <returns>Resultado do parsing com lista de ingredientes</returns>
        IngredientsParseResult Parse(string ocrText);
    }
}
