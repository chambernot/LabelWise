namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Parser especializado para frente da embalagem.
    /// Foco: extrair nome do produto e marca, ignorar linhas que parecem tabela nutricional
    /// </summary>
    public interface IFrontPackagingParser
    {
        /// <summary>
        /// Extrai nome do produto e marca da frente da embalagem.
        /// </summary>
        /// <param name="ocrText">Texto extraído pela OCR (deve ser da frente da embalagem)</param>
        /// <returns>Resultado do parsing com nome e marca do produto</returns>
        FrontPackagingParseResult Parse(string ocrText);
    }
}
