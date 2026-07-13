namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Parser especializado para declarações de alérgenos.
    /// Foco: separar containsAllergens e mayContainAllergens, reconhecer frases específicas
    /// </summary>
    public interface IAllergenParser
    {
        /// <summary>
        /// Extrai informações de alérgenos do texto OCR.
        /// Identifica "contém", "pode conter", "não contém", "contém derivados de"
        /// </summary>
        /// <param name="ocrText">Texto extraído pela OCR (deve conter declarações de alérgenos)</param>
        /// <returns>Resultado do parsing com alérgenos confirmados e potenciais</returns>
        AllergenParseResult Parse(string ocrText);
    }
}
