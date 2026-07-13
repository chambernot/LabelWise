using System.Text.RegularExpressions;

namespace LabelWise.Infrastructure.Services.NutritionPipeline;

/// <summary>
/// Valida se uma tabela nutricional detectada pelo OCR contém valores reais
/// ou é apenas um template vazio / formulário não preenchido.
///
/// Problema a resolver: imagens de embalagens onde a tabela nutricional existe
/// estruturalmente (palavras-chave presentes) mas os campos numéricos estão
/// em branco — como templates de rótulo, artes sem impressão final ou fotos
/// da frente do produto que capturam apenas o layout da tabela.
///
/// Exemplos de falsos positivos sem esta validação:
///   • "Porção de ..... g ou ml"
///   • "Valor Energético kcal e kJ"
///   • Tabela com apenas unidades (g, mg, %) mas sem números
///   • Formulário com linhas pontilhadas (......)
/// </summary>
public static class NutritionTableValidator
{
    // ── Limites ──────────────────────────────────────────────────────────────

    /// <summary>Mínimo de valores numéricos distintos para considerar a tabela preenchida.</summary>
    private const int MinNumericValues = 3;

    /// <summary>Mínimo de valores numéricos com magnitude nutricional plausível (> 0.1).</summary>
    private const int MinPlausibleNutrientValues = 2;

    // ── Padrões de template ───────────────────────────────────────────────────

    private static readonly string[] TemplatePhrases =
    [
        ".....",          // linhas pontilhadas = campo não preenchido
        "g ou ml",        // rótulo genérico de unidade sem valor
        "kcal e kj",      // cabeçalho de unidade sem valor correspondente
        "__ g",           // campo em branco com underline
        "___ g",
        "( ) g",          // parênteses vazios de template
    ];

    private static readonly Regex[] TemplatePatterns =
    [
        new(@"\.{3,}", RegexOptions.Compiled),                          // 3+ pontos consecutivos
        new(@"\b_+\b", RegexOptions.Compiled),                          // underlines em branco
        new(@"quantidade\s+por\s+por[çc][aã]o\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\(\s*\)", RegexOptions.Compiled),                         // parênteses vazios
        new(@"[\*\#]{3,}", RegexOptions.Compiled),                      // placeholders repetidos
    ];

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>
    /// Retorna true apenas quando a tabela nutricional parece ter sido preenchida
    /// com valores reais — i.e., não é um template vazio ou formulário em branco.
    /// </summary>
    /// <param name="ocrText">Texto bruto retornado pelo OCR.</param>
    /// <param name="extractedNumbers">Números extraídos do mesmo texto via <see cref="ExtractNumbers"/>.</param>
    public static bool IsValidTable(string ocrText, IReadOnlyList<double> extractedNumbers)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return false;

        // Regra 1: Quantidade mínima de valores numéricos
        if ((extractedNumbers?.Count ?? 0) < MinNumericValues)
            return false;

        // Regra 2: Pelo menos N valores com magnitude nutricionalmente plausível
        // Isso filtra tabelas que têm apenas "0", "1" ou números de página/código de barras
        int plausibleCount = extractedNumbers!.Count(n => n >= 0.1 && n <= 10_000);
        if (plausibleCount < MinPlausibleNutrientValues)
            return false;

        // Regra 3: Padrões textuais de template
        var text = ocrText.ToLowerInvariant();

        foreach (var phrase in TemplatePhrases)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (var pattern in TemplatePatterns)
        {
            if (pattern.IsMatch(ocrText))
                return false;
        }

        // Regra 4: Rejeitar "quantidade por porção" isolado sem nenhum valor numérico real
        // (cabeçalho de template sem corpo preenchido)
        bool hasOnlyHeader = text.Contains("quantidade por porção") && plausibleCount < 3;
        if (hasOnlyHeader)
            return false;

        return true;
    }

    /// <summary>
    /// Extrai todos os valores numéricos do texto OCR para análise de completude.
    /// Trata tanto ponto quanto vírgula como separador decimal.
    /// </summary>
    public static List<double> ExtractNumbers(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var results = new List<double>();
        foreach (Match m in Regex.Matches(text, @"\d+[.,]?\d*"))
        {
            var raw = m.Value.Replace(',', '.');
            if (double.TryParse(raw,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
            {
                results.Add(value);
            }
        }

        return results;
    }
}
