using System.Globalization;
using System.Text;

namespace LabelWise.Domain.Parsing;

/// <summary>
/// Normalizador de texto OCR para facilitar parsing.
/// Remove ruídos, padroniza formatos e corrige erros comuns.
/// </summary>
public static class NutritionNormalizer
{
    /// <summary>
    /// Normaliza texto OCR para facilitar extração de dados
    /// </summary>
    public static string NormalizeOcrText(string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return string.Empty;

        var text = ocrText;

        // Remover caracteres especiais problemáticos
        text = text.Replace("•", " ");
        text = text.Replace("*", " ");
        text = text.Replace("|", " ");
        text = text.Replace("**", " ");

        // Normalizar quebras de linha
        text = text.Replace("\r\n", "\n");
        text = text.Replace("\r", "\n");

        // Remover múltiplas quebras de linha
        while (text.Contains("\n\n\n"))
        {
            text = text.Replace("\n\n\n", "\n\n");
        }

        // Normalizar espaços
        text = System.Text.RegularExpressions.Regex.Replace(text, @" +", " ");

        return text.Trim();
    }

    /// <summary>
    /// Extrai todos os números de uma linha, já convertidos para double
    /// </summary>
    public static List<double> ExtractNumbers(string line)
    {
        var numbers = new List<double>();
        
        if (string.IsNullOrWhiteSpace(line))
            return numbers;

        // Regex para capturar números (incluindo decimais com vírgula ou ponto)
        var regex = new System.Text.RegularExpressions.Regex(@"\d+(?:[,\.]\d+)?");
        var matches = regex.Matches(line);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var numberStr = match.Value.Replace(',', '.');
            if (double.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    /// <summary>
    /// Extrai números próximos a uma palavra-chave (mesma linha + próximas linhas)
    /// </summary>
    public static List<double> ExtractNumbersNearKeyword(string text, string keyword, int maxLinesToSearch = 3)
    {
        var numbers = new List<double>();
        var lines = text.Split('\n');

        // Encontrar linha que contém a keyword
        int keywordLineIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (ContainsKeyword(lines[i], keyword))
            {
                keywordLineIndex = i;
                break;
            }
        }

        if (keywordLineIndex == -1)
            return numbers;

        // Extrair números da linha da keyword + próximas linhas
        for (int i = keywordLineIndex; i < Math.Min(keywordLineIndex + maxLinesToSearch, lines.Length); i++)
        {
            numbers.AddRange(ExtractNumbers(lines[i]));
        }

        return numbers;
    }

    /// <summary>
    /// Verifica se linha contém keyword (ignorando acentos e case)
    /// </summary>
    public static bool ContainsKeyword(string line, string keyword)
    {
        var normalizedLine = RemoveDiacritics(line.ToLowerInvariant());
        var normalizedKeyword = RemoveDiacritics(keyword.ToLowerInvariant());
        return normalizedLine.Contains(normalizedKeyword);
    }

    /// <summary>
    /// Remove acentos de uma string para facilitar comparação
    /// </summary>
    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Detecta unidade da tabela (g ou ml)
    /// </summary>
    public static string DetectUnit(string text)
    {
        var normalized = text.ToLowerInvariant();

        // Procurar por "100 ml" ou "100ml"
        if (normalized.Contains("100 ml") || normalized.Contains("100ml"))
            return "ml";

        // Procurar por "100 g" ou "100g"
        if (normalized.Contains("100 g") || normalized.Contains("100g"))
            return "g";

        // Default: gramas
        return "g";
    }

    /// <summary>
    /// Seleciona o valor mais provável para 100g a partir de uma lista de números
    /// HEURÍSTICA: O maior valor costuma ser o de 100g
    /// </summary>
    public static double? SelectMostLikelyValue100g(List<double> numbers, string nutrientType)
    {
        if (numbers.Count == 0)
            return null;

        // Remover valores claramente inválidos
        var validNumbers = numbers
            .Where(n => n > 0 && IsReasonableValue(n, nutrientType))
            .ToList();

        if (validNumbers.Count == 0)
            return null;

        // Se há apenas 1 valor, retornar
        if (validNumbers.Count == 1)
            return validNumbers[0];

        // Se há 2 valores, retornar o maior (heurística)
        if (validNumbers.Count == 2)
            return validNumbers.Max();

        // Se há 3+ valores:
        // - Geralmente: [100g] [porção] [%VD]
        // - Pegar o primeiro (100g)
        return validNumbers[0];
    }

    /// <summary>
    /// Verifica se um valor é razoável para um tipo de nutriente
    /// </summary>
    private static bool IsReasonableValue(double value, string nutrientType)
    {
        return nutrientType.ToLowerInvariant() switch
        {
            "calorias" or "energia" => value >= 0 && value <= 900,
            "carboidratos" or "carbs" => value >= 0 && value <= 100,
            "acucar" or "sugar" => value >= 0 && value <= 100,
            "proteina" or "protein" => value >= 0 && value <= 100,
            "gordura" or "fat" => value >= 0 && value <= 100,
            "fibra" or "fiber" => value >= 0 && value <= 100,
            "sodio" or "sodium" => value >= 0 && value <= 5000,
            _ => value >= 0 && value <= 100
        };
    }

    /// <summary>
    /// Tenta corrigir quebra de linha no meio de um valor
    /// Ex: "Carboidratos (g\n14" → "Carboidratos (g) 14"
    /// </summary>
    public static string FixLineBreaksInValues(string text)
    {
        // Procurar por padrões: "(unidade\n" seguido de número
        var regex = new System.Text.RegularExpressions.Regex(@"\(([gm][l]?)\s*\n\s*(\d+)");
        return regex.Replace(text, "($1) $2");
    }
}
