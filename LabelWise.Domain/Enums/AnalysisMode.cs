using System.Text.Json.Serialization;

namespace LabelWise.Domain.Enums
{
    /// <summary>
    /// Modo de análise nutricional baseado no conteúdo da imagem.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AnalysisMode
    {
        /// <summary>
        /// Estado indeterminado — sem dados úteis ou análise inválida.
        /// Usado pela State Machine quando o estado é <c>NoData</c> ou <c>Invalid</c>.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Análise baseada apenas na imagem frontal da embalagem.
        /// Valores nutricionais são estimados com base na categoria do produto.
        /// </summary>
        FrontOfPackageOnly = 1,

        /// <summary>
        /// Análise completa com leitura da tabela nutricional e ingredientes.
        /// Valores nutricionais são extraídos diretamente da embalagem.
        /// </summary>
        FullNutritionLabel = 2
    }
}
