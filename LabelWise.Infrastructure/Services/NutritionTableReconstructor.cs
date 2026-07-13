using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Reconstroi a estrutura espacial da tabela nutricional a partir dos
/// <see cref="OcrTextBlock"/> retornados pelo OCR (com bounding boxes).
///
/// FLUXO:
///   1. Agrupa blocos por coordenada Y → linhas
///   2. Identifica a linha de cabeçalho (contém "100 g", "30 g", "%VD", "porção", etc.)
///   3. Captura as posições X das colunas
///   4. Para cada linha: mapeia rótulo (texto não-numérico) + valores por coluna mais próxima
///   5. Trata "linhas quebradas" (valores na linha de baixo sem rótulo) → anexa ao último rótulo
///   6. Descarta ruído (linhas sem rótulo + valor, ingredientes, marketing)
///   7. Mapeia rótulos → nutrientes canônicos
///   8. Lê SEMPRE da coluna "100 g" / "100 ml" (cai para porção apenas se 100g ausente)
///
/// Esta classe NÃO faz parsing sequencial linha-por-linha de texto bruto —
/// trabalha exclusivamente com a geometria 2D do OCR.
/// </summary>
public sealed class NutritionTableReconstructor
{
    private readonly ILogger _logger;

    // Tolerância vertical para considerar dois blocos na mesma linha.
    // Calculada dinamicamente como fração da altura média dos blocos.
    private const double DefaultRowToleranceFactor = 0.6;

    // Tolerância horizontal para atribuir um valor a uma coluna (px).
    private const double ColumnAssignTolerance = 80.0;

    public NutritionTableReconstructor(ILogger logger) => _logger = logger;

    // ─────────────────────────────────────────────────────────────────
    // Public entry point
    // ─────────────────────────────────────────────────────────────────

    public StructuredNutritionResult Reconstruct(IReadOnlyList<OcrTextBlock> blocks)
    {
        if (blocks == null || blocks.Count == 0)
            return Failed("Nenhum bloco de OCR fornecido.");

        // Filter blocks lacking geometry — engine requires bounding boxes
        var geo = blocks
            .Where(b => b.BoundingBox != null && !string.IsNullOrWhiteSpace(b.Text))
            .ToList();

        if (geo.Count < 4)
            return Failed("Poucos blocos com geometria para reconstruir tabela.");

        // 1. Group by Y → raw rows
        var rowTolerance = ComputeRowTolerance(geo);
        var rawRows = GroupByY(geo, rowTolerance);
        _logger.LogInformation(
            "[Reconstructor] {Rows} linhas brutas detectadas (tolY={Tol:F1})",
            rawRows.Count, rowTolerance);

        // 2. Identify header row + column positions
        var (headerIndex, columns) = DetectHeader(rawRows);
        if (headerIndex < 0 || columns.Count == 0)
        {
            _logger.LogWarning("[Reconstructor] Cabeçalho não localizado — abortando reconstrução");
            return Failed("Cabeçalho da tabela (100 g / porção / %VD) não localizado.");
        }

        _logger.LogInformation(
            "[Reconstructor] Cabeçalho na linha {Idx} | Colunas: {Cols}",
            headerIndex, string.Join(" | ", columns.Select(c => $"{c.Kind}@x={c.X:F0}")));

        // 3. Build structured rows after the header
        var dataRows = BuildStructuredRows(rawRows, headerIndex, columns);

        // 4. Attach broken rows (values without label) to previous labeled row
        AttachBrokenRows(dataRows);

        // 5. Drop noise (rows without label + ≥1 numeric value)
        var validRows = dataRows
            .Where(r => !string.IsNullOrWhiteSpace(r.Label) && r.ValuesByColumn.Count > 0)
            .ToList();

        _logger.LogInformation(
            "[Reconstructor] {Valid}/{Total} linhas válidas após filtragem de ruído",
            validRows.Count, dataRows.Count);

        // 🔍 DIAGNÓSTICO: dump completo das linhas válidas — útil para entender
        // por que nutrientes não estão sendo mapeados em uma imagem específica.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var r in validRows)
            {
                var vals = string.Join(", ", r.ValuesByColumn.Select(kv => $"{kv.Key}={kv.Value}"));
                _logger.LogDebug("[Reconstructor]   linha label=\"{Label}\" valores=[{Vals}]",
                    r.Label, vals);
            }
        }

        // 6. Pick the canonical column (prefer 100 g, then 100 ml, then portion)
        var canonical = PickCanonicalColumn(columns);
        var unit = canonical.Kind == ColumnKind.Per100Ml ? "ml" : "g";
        _logger.LogInformation(
            "[Reconstructor] Coluna canônica selecionada: {Kind} (unit={Unit})",
            canonical.Kind, unit);

        // 7. Map labels → nutrients and read from canonical column
        var result = new StructuredNutritionResult { Unit = unit };
        int extracted = 0;
        int unknownLabels = 0;
        int withoutValue  = 0;

        foreach (var row in validRows)
        {
            var nutrient = MapLabelToNutrient(row.Label);
            if (nutrient == Nutrient.Unknown)
            {
                unknownLabels++;
                _logger.LogDebug("[Reconstructor]   ❌ rótulo não mapeado: \"{Label}\"", row.Label);
                continue;
            }

            if (!TryGetValue(row, canonical, columns, out var value))
            {
                withoutValue++;
                _logger.LogDebug(
                    "[Reconstructor]   ❌ {Nutrient} sem valor na coluna canônica (\"{Label}\")",
                    nutrient, row.Label);
                continue;
            }

            switch (nutrient)
            {
                case Nutrient.Calories:     result.Calories     ??= value; extracted++; break;
                case Nutrient.Carbs:        result.Carbs        ??= value; extracted++; break;
                case Nutrient.Sugar:        result.Sugar        ??= value; extracted++; break;
                case Nutrient.AddedSugar:   result.AddedSugar   ??= value; extracted++; break;
                case Nutrient.Protein:      result.Protein      ??= value; extracted++; break;
                case Nutrient.Fat:          result.Fat          ??= value; extracted++; break;
                case Nutrient.SaturatedFat: result.SaturatedFat ??= value; extracted++; break;
                case Nutrient.Fiber:        result.Fiber        ??= value; extracted++; break;
                case Nutrient.Sodium:       result.Sodium       ??= value; extracted++; break;
            }

            _logger.LogDebug(
                "[Reconstructor]   ✅ {Label} → {Nutrient} = {Value} (col {Col})",
                row.Label, nutrient, value, canonical.Kind);
        }

        _logger.LogInformation(
            "[Reconstructor] Resumo: {Extracted} extraídos | {Unknown} rótulos não mapeados | {NoValue} sem valor",
            extracted, unknownLabels, withoutValue);

        result.Success = extracted >= 2;
        if (!result.Success)
            result.ErrorMessage = $"Apenas {extracted} nutrientes mapeados — insuficiente.";

        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    // 1. Group blocks by Y position
    // ─────────────────────────────────────────────────────────────────

    private static double ComputeRowTolerance(IReadOnlyList<OcrTextBlock> blocks)
    {
        var avgHeight = blocks.Average(b => b.BoundingBox!.Height);
        return Math.Max(8.0, avgHeight * DefaultRowToleranceFactor);
    }

    private static List<List<OcrTextBlock>> GroupByY(IReadOnlyList<OcrTextBlock> blocks, double tolerance)
    {
        var ordered = blocks.OrderBy(b => b.BoundingBox!.Y).ToList();
        var rows = new List<List<OcrTextBlock>>();
        List<OcrTextBlock>? current = null;
        double currentY = double.NaN;

        foreach (var b in ordered)
        {
            var y = b.BoundingBox!.Y;
            if (current == null || Math.Abs(y - currentY) > tolerance)
            {
                current = new List<OcrTextBlock>();
                rows.Add(current);
                currentY = y;
            }
            current.Add(b);
        }

        // Sort each row left-to-right
        foreach (var r in rows)
            r.Sort((a, b) => a.BoundingBox!.X.CompareTo(b.BoundingBox!.X));

        return rows;
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. Header detection
    // ─────────────────────────────────────────────────────────────────

    private static readonly Regex HeaderPer100G  = new(@"\b100\s*g\b",  RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HeaderPer100Ml = new(@"\b100\s*ml\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HeaderVd       = new(@"%\s*v\.?d\.?|valor\s*di[áa]rio", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HeaderPortion  = new(@"por[çc][ãa]o|\d+\s*(g|ml)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private (int rowIndex, List<Column> columns) DetectHeader(List<List<OcrTextBlock>> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var rowText = string.Join(" ", rows[i].Select(b => b.Text));
            bool has100   = HeaderPer100G.IsMatch(rowText) || HeaderPer100Ml.IsMatch(rowText);
            bool hasOther = HeaderVd.IsMatch(rowText) || HeaderPortion.IsMatch(rowText);

            if (!has100 && !hasOther) continue;

            _logger.LogDebug("[Reconstructor] Linha {Idx} candidata a cabeçalho: \"{Text}\"", i, rowText);

            var cols = ExtractColumnsFromRow(rows[i]);

            // Se a linha encontrada só tem %VD/Porção, varrer linhas vizinhas para
            // tentar achar a coluna 100 g / 100 ml (cabeçalhos podem estar quebrados em duas linhas).
            bool hasCanonical = cols.Any(c => c.Kind is ColumnKind.Per100G or ColumnKind.Per100Ml);
            if (!hasCanonical)
            {
                if (i - 1 >= 0)            cols.AddRange(ExtractColumnsFromRow(rows[i - 1]));
                if (i + 1 < rows.Count)    cols.AddRange(ExtractColumnsFromRow(rows[i + 1]));
            }

            // De-duplicate columns by kind, keep leftmost
            cols = cols
                .GroupBy(c => c.Kind)
                .Select(g => g.OrderBy(c => c.X).First())
                .OrderBy(c => c.X)
                .ToList();

            if (cols.Count > 0)
            {
                _logger.LogInformation(
                    "[Reconstructor] Cabeçalho aceito na linha {Idx} → colunas: {Cols}",
                    i, string.Join(" | ", cols.Select(c => $"{c.Kind}@x={c.X:F0}")));
                return (i, cols);
            }

            _logger.LogDebug(
                "[Reconstructor] Linha {Idx} casou regex mas não produziu colunas — continuando busca", i);
        }

        return (-1, new List<Column>());
    }

    /// <summary>
    /// Varre os blocos de uma linha extraindo posições X das colunas conhecidas.
    /// Lida com OCR fragmentado: combina blocos vizinhos quando "100" e "g" / "ml"
    /// chegam separados, ou quando "%" e "VD" estão em blocos diferentes.
    /// </summary>
    private static List<Column> ExtractColumnsFromRow(List<OcrTextBlock> row)
    {
        var cols = new List<Column>();

        for (int j = 0; j < row.Count; j++)
        {
            var t = row[j].Text.Trim();
            var x = row[j].BoundingBox!.X;

            // Caso 1: bloco completo "100 g", "100ml", "100  g"
            if (HeaderPer100G.IsMatch(t))   { cols.Add(new Column(ColumnKind.Per100G,  x)); continue; }
            if (HeaderPer100Ml.IsMatch(t))  { cols.Add(new Column(ColumnKind.Per100Ml, x)); continue; }

            // Caso 2: "100" sozinho + bloco vizinho "g" / "ml"
            if (Regex.IsMatch(t, @"^100$") && j + 1 < row.Count)
            {
                var next = row[j + 1].Text.Trim().ToLowerInvariant();
                if (next == "g")   { cols.Add(new Column(ColumnKind.Per100G,  x)); j++; continue; }
                if (next == "ml")  { cols.Add(new Column(ColumnKind.Per100Ml, x)); j++; continue; }
            }

            // Caso 3: %VD em vários formatos
            if (HeaderVd.IsMatch(t)) { cols.Add(new Column(ColumnKind.PercentVd, x)); continue; }

            // Caso 4: "%" sozinho + bloco vizinho "VD"
            if (t == "%" && j + 1 < row.Count &&
                Regex.IsMatch(row[j + 1].Text.Trim(), @"^v\.?d\.?$", RegexOptions.IgnoreCase))
            {
                cols.Add(new Column(ColumnKind.PercentVd, x));
                j++;
                continue;
            }

            // Caso 5: porção (ex.: "30 g", "porção", "Quantidade por porção")
            if (HeaderPortion.IsMatch(t)) { cols.Add(new Column(ColumnKind.Portion, x)); continue; }
        }

        return cols;
    }

    private static Column PickCanonicalColumn(List<Column> columns)
    {
        return columns.FirstOrDefault(c => c.Kind == ColumnKind.Per100G)
            ?? columns.FirstOrDefault(c => c.Kind == ColumnKind.Per100Ml)
            ?? columns.FirstOrDefault(c => c.Kind == ColumnKind.Portion)
            ?? columns.First();
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. Build structured rows (label + values by column)
    // ─────────────────────────────────────────────────────────────────

    private static List<ReconstructedRow> BuildStructuredRows(
        List<List<OcrTextBlock>> rawRows, int headerIndex, List<Column> columns)
    {
        var output = new List<ReconstructedRow>();

        for (int i = headerIndex + 1; i < rawRows.Count; i++)
        {
            var row = rawRows[i];

            // Label = leftmost non-numeric tokens concatenated
            var labelTokens  = new List<string>();
            var numericTokens = new List<(double Value, double X)>();

            foreach (var block in row)
            {
                if (TryParseNumber(block.Text, out var num))
                    numericTokens.Add((num, block.BoundingBox!.X));
                else if (LooksLikeLabel(block.Text))
                    labelTokens.Add(block.Text.Trim());
            }

            var label = string.Join(" ", labelTokens).Trim();
            var valuesByCol = AssignValuesToColumns(numericTokens, columns);

            output.Add(new ReconstructedRow
            {
                Label = label,
                ValuesByColumn = valuesByCol,
                Y = row[0].BoundingBox!.Y
            });
        }

        return output;
    }

    private static Dictionary<ColumnKind, double> AssignValuesToColumns(
        List<(double Value, double X)> numbers, List<Column> columns)
    {
        var result = new Dictionary<ColumnKind, double>();
        foreach (var (val, x) in numbers)
        {
            Column? best = null;
            double bestDist = double.MaxValue;
            foreach (var c in columns)
            {
                var d = Math.Abs(c.X - x);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            if (best != null && bestDist <= ColumnAssignTolerance && !result.ContainsKey(best.Kind))
                result[best.Kind] = val;
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    // 4. Broken rows: values that fall on a row without label
    //     are attached to the previous labeled row.
    // ─────────────────────────────────────────────────────────────────

    private static void AttachBrokenRows(List<ReconstructedRow> rows)
    {
        ReconstructedRow? lastLabeled = null;
        var toRemove = new List<ReconstructedRow>();

        foreach (var r in rows)
        {
            bool hasLabel  = !string.IsNullOrWhiteSpace(r.Label);
            bool hasValues = r.ValuesByColumn.Count > 0;

            if (hasLabel)
            {
                lastLabeled = r;
                continue;
            }

            // Orphan values → attach to previous labeled row (if it lacks values)
            if (hasValues && lastLabeled != null)
            {
                foreach (var kv in r.ValuesByColumn)
                {
                    if (!lastLabeled.ValuesByColumn.ContainsKey(kv.Key))
                        lastLabeled.ValuesByColumn[kv.Key] = kv.Value;
                }
                toRemove.Add(r);
            }
        }

        foreach (var r in toRemove) rows.Remove(r);
    }

    // ─────────────────────────────────────────────────────────────────
    // 5/6. Read a value from the canonical column (with fallback chain)
    // ─────────────────────────────────────────────────────────────────

    private static bool TryGetValue(
        ReconstructedRow row, Column canonical, List<Column> all, out double value)
    {
        if (row.ValuesByColumn.TryGetValue(canonical.Kind, out value)) return true;

        // Fallback: portion column (acceptable as a last resort), then any non-%VD value
        foreach (var c in all)
        {
            if (c.Kind == ColumnKind.PercentVd) continue;
            if (row.ValuesByColumn.TryGetValue(c.Kind, out value)) return true;
        }

        value = 0;
        return false;
    }

    // ─────────────────────────────────────────────────────────────────
    // Label → nutrient mapping
    // ─────────────────────────────────────────────────────────────────

    private enum Nutrient
    {
        Unknown, Calories, Carbs, Sugar, AddedSugar, Protein,
        Fat, SaturatedFat, Fiber, Sodium
    }

    private static Nutrient MapLabelToNutrient(string label)
    {
        var l = StripDiacritics(label).ToLowerInvariant();

        if (l.Contains("valor energetico") || l.Contains("energia") ||
            l.Contains("calorias")         || l.Contains("kcal"))           return Nutrient.Calories;

        if (l.Contains("acucar"))
        {
            if (l.Contains("adicion")) return Nutrient.AddedSugar;
            return Nutrient.Sugar;
        }

        if (l.Contains("carboidrato"))                                       return Nutrient.Carbs;
        if (l.Contains("proteina"))                                          return Nutrient.Protein;
        if (l.Contains("fibra"))                                             return Nutrient.Fiber;
        if (l.Contains("sodio"))                                             return Nutrient.Sodium;

        if (l.Contains("gordura"))
        {
            if (l.Contains("satur"))   return Nutrient.SaturatedFat;
            if (l.Contains("trans"))   return Nutrient.Unknown; // ignored on purpose
            return Nutrient.Fat;
        }

        return Nutrient.Unknown;
    }

    // ─────────────────────────────────────────────────────────────────
    // Token classification helpers
    // ─────────────────────────────────────────────────────────────────

    private static readonly Regex NumberRegex = new(
        @"^\s*[<>~]?\s*(\d+(?:[.,]\d+)?)\s*(kcal|kj|mg|g|ml|%)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool TryParseNumber(string text, out double value)
    {
        var m = NumberRegex.Match(text);
        if (!m.Success)
        {
            value = 0;
            return false;
        }
        var raw = m.Groups[1].Value.Replace(',', '.');
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool LooksLikeLabel(string text)
    {
        var t = text.Trim();
        if (t.Length < 3) return false;
        // At least 3 letters → likely a label, not noise
        int letters = t.Count(char.IsLetter);
        return letters >= 3;
    }

    private static string StripDiacritics(string text)
    {
        var n = text.Normalize(NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(n.Length);
        foreach (var c in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    // ─────────────────────────────────────────────────────────────────
    // Result helpers + internal types
    // ─────────────────────────────────────────────────────────────────

    private static StructuredNutritionResult Failed(string reason) =>
        new() { Success = false, ErrorMessage = reason };

    private enum ColumnKind { Unknown, Per100G, Per100Ml, Portion, PercentVd }

    private sealed record Column(ColumnKind Kind, double X);

    private sealed class ReconstructedRow
    {
        public string Label { get; set; } = string.Empty;
        public Dictionary<ColumnKind, double> ValuesByColumn { get; set; } = new();
        public double Y { get; set; }
    }
}
