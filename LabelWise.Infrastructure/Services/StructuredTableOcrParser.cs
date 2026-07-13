using System.Text.RegularExpressions;
using LabelWise.Application.DTOs;
using LabelWise.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Parser estruturado de tabelas nutricionais usando coordenadas espaciais do OCR.
/// 
/// PROBLEMA RESOLVIDO:
/// - OCR retorna texto fragmentado (ex: "12\n4\n15" para carboidratos)
/// - Sem coordenadas X/Y, não sabemos qual número pertence a qual coluna
/// 
/// SOLUÇÃO:
/// 1. Usa TextBlocks com bounding boxes (coordenadas X, Y, Width, Height)
/// 2. Detecta colunas por clustering de coordenadas X
/// 3. Detecta linhas (nutrientes) por clustering de coordenadas Y
/// 4. Valida estrutura da tabela (título, cabeçalho, valores)
/// 5. Extrai valores da coluna correta (100g/ml vs porção vs %VD)
/// 
/// EXEMPLO:
/// ```
/// TextBlock: "Carboidratos" (X=50, Y=200, W=150, H=20)
/// TextBlock: "12"           (X=220, Y=200, W=30, H=20)  ← Coluna "100ml"
/// TextBlock: "15"           (X=270, Y=200, W=30, H=20)  ← Coluna "20g"
/// TextBlock: "5"            (X=320, Y=200, W=20, H=20)  ← Coluna "%VD"
/// ```
/// Com coordenadas Y similares (~200), sabemos que estão na mesma linha.
/// Com coordenadas X diferentes, sabemos qual valor pertence a qual coluna.
/// </summary>
public sealed class StructuredTableOcrParser
{
    private readonly ILogger<StructuredTableOcrParser> _logger;

    // Tolerâncias para agrupamento espacial
    private const double Y_TOLERANCE = 10.0; // pixels - linhas na mesma altura
    private const double X_TOLERANCE = 15.0; // pixels - textos na mesma coluna
    private const double MIN_COLUMN_WIDTH = 30.0; // largura mínima de uma coluna
    // Confiança mínima: 0.45 (antes 0.65) — imagens escuras/baixo contraste retornam
    // confidence menor mesmo com leitura correta; 0.45 evita falsos-negativos.
    private const double MIN_OCR_CONFIDENCE = 0.45;

    // Ranges de validação de domínio (valores por 100g/ml)
    private const double MAX_CALORIES = 900.0;
    private const double MAX_PROTEIN = 100.0;
    private const double MAX_CARBS = 100.0;
    private const double MAX_SUGAR = 100.0;
    private const double MAX_FAT = 100.0;
    private const double MAX_FIBER = 100.0;
    private const double MAX_SODIUM = 5000.0; // mg

    public StructuredTableOcrParser(ILogger<StructuredTableOcrParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extrai dados nutricionais usando estrutura espacial do OCR
    /// </summary>
    public StructuredNutritionResult ParseStructured(List<OcrTextBlock> textBlocks, string rawText)
    {
        if (textBlocks == null || textBlocks.Count == 0)
        {
            _logger.LogWarning("[StructuredParser] Nenhum TextBlock fornecido, fallback para parser simples");
            return FallbackToSimpleParser(rawText);
        }

        _logger.LogInformation("[StructuredParser] ┌─────────────────────────────────────────────┐");
        _logger.LogInformation("[StructuredParser] │  ANÁLISE ESTRUTURADA DE TABELA NUTRICIONAL │");
        _logger.LogInformation("[StructuredParser] └─────────────────────────────────────────────┘");

        // ═══════════════════════════════════════════════════════════════════
        // 🆕 RECONSTRUÇÃO ESPACIAL (caminho primário)
        // Usa bounding boxes para reconstruir a tabela: agrupa por Y,
        // detecta cabeçalho ("100 g" / "%VD" / porção), captura X de cada
        // coluna, mapeia rótulo → linha, atribui valores por proximidade
        // de X, anexa linhas quebradas, descarta ruído e lê SEMPRE da
        // coluna 100 g/ml. Veja NutritionTableReconstructor.
        // ═══════════════════════════════════════════════════════════════════
        try
        {
            var reconstructor = new NutritionTableReconstructor(_logger);
            var reconstructed = reconstructor.Reconstruct(textBlocks);
            if (reconstructed.Success)
            {
                _logger.LogInformation(
                    "[StructuredParser] ✅ Reconstrução espacial bem-sucedida — pulando pipeline legado");
                return reconstructed;
            }

            _logger.LogInformation(
                "[StructuredParser] ⏭️ Reconstrução não conclusiva ({Reason}) — caindo no pipeline legado",
                reconstructed.ErrorMessage ?? "sem detalhe");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[StructuredParser] ⚠️ Erro na reconstrução espacial — caindo no pipeline legado");
        }

        // ═══════════════════════════════════════════════════════════════════
        // 🔥 ETAPA 0: PRÉ-FILTRAGEM DE RUÍDO (NOVO)
        // ═══════════════════════════════════════════════════════════════════
        var filteredBlocks = PreFilterNoiseBlocks(textBlocks);

        if (filteredBlocks.Count < 5)
        {
            _logger.LogWarning("[StructuredParser] ❌ Muitos blocos filtrados como ruído ({Filtered}/{Total}), usando fallback",
                textBlocks.Count - filteredBlocks.Count, textBlocks.Count);
            return FallbackToSimpleParser(rawText);
        }

        // ETAPA 1: Validar se é uma tabela nutricional
        if (!ValidateNutritionTableStructure(filteredBlocks, rawText))
        {
            _logger.LogWarning("[StructuredParser] ❌ Estrutura de tabela nutricional não detectada");
            return new StructuredNutritionResult 
            { 
                Success = false, 
                ErrorMessage = "Estrutura de tabela nutricional não detectada" 
            };
        }

        // ETAPA 2: Detectar colunas por coordenadas X
        var columns = DetectColumns(filteredBlocks);
        LogColumns(columns);

        // ETAPA 3: Detectar linhas (nutrientes) por coordenadas Y
        var rows = DetectRows(filteredBlocks);
        _logger.LogInformation("[StructuredParser] 📋 Detectadas {Count} linhas (nutrientes)", rows.Count);

        // ETAPA 4: Identificar qual coluna é "100g/ml" vs "porção" vs "%VD"
        var columnMapping = IdentifyColumnTypes(filteredBlocks, columns);
        LogColumnMapping(columnMapping);

        // ETAPA 5: Extrair valores de cada nutriente da coluna correta
        var extractedData = ExtractNutrientValues(rows, columns, columnMapping);

        // ETAPA 6: Validar consistência dos dados extraídos
        var validationResult = ValidateExtractedData(extractedData);

        if (!validationResult.IsValid)
        {
            _logger.LogWarning("[StructuredParser] ⚠️ Validação falhou: {Reason}", validationResult.Reason);

            // Tentar autocorreção
            var corrected = AutoCorrectData(extractedData, validationResult);
            if (corrected.IsValid)
            {
                _logger.LogInformation("[StructuredParser] ✅ Dados corrigidos automaticamente");
                extractedData = corrected.Data;
            }
            else
            {
                // Validação crítica falhou, usar fallback
                _logger.LogError("[StructuredParser] ❌ Autocorreção falhou, usando fallback");
                return FallbackToSimpleParser(rawText);
            }
        }

        // ETAPA 7: Construir resultado final
        var result = BuildResult(extractedData);
        LogExtractedValues(result);

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    // ETAPA 0: Pré-Filtragem de Ruído (NOVO)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Remove blocos de OCR que são provavelmente ruído/lixo.
    /// 
    /// CRITÉRIOS DE FILTRAGEM:
    /// 1. Confiança do OCR < 0.65
    /// 2. Texto muito curto com baixa confiança (ex: "1", "4" com conf < 0.8)
    /// 3. Caracteres especiais mal detectados
    /// 4. Valores numéricos absurdos (ex: "99999", "0.0001")
    /// 5. Blocos duplicados (mesmo texto, mesma posição)
    /// </summary>
    private List<OcrTextBlock> PreFilterNoiseBlocks(List<OcrTextBlock> textBlocks)
    {
        _logger.LogInformation("[StructuredParser] 🧹 Iniciando pré-filtragem de ruído...");
        _logger.LogInformation("[StructuredParser] Total de blocos originais: {Count}", textBlocks.Count);

        var filtered = new List<OcrTextBlock>();
        var removed = 0;

        foreach (var block in textBlocks)
        {
            var text = block.Text?.Trim() ?? "";

            // Filtro 1: Confiança muito baixa (threshold reduzido para 0.45)
            if (block.Confidence < MIN_OCR_CONFIDENCE)
            {
                _logger.LogDebug("[StructuredParser] ❌ Removido (confiança baixa): '{Text}' (conf={Conf:F2})",
                    text, block.Confidence);
                removed++;
                continue;
            }

            // Filtro 2: Texto vazio ou apenas espaços
            if (string.IsNullOrWhiteSpace(text))
            {
                removed++;
                continue;
            }

            // Filtro 3: Apenas caracteres especiais sem valor semântico
            if (ContainsOnlySpecialChars(text))
            {
                _logger.LogDebug("[StructuredParser] ❌ Removido (caracteres especiais): '{Text}'", text);
                removed++;
                continue;
            }

            // Filtro 4: Valores numéricos visivelmente absurdos
            if (IsNumeric(text))
            {
                if (double.TryParse(text.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var numValue))
                {
                    if (numValue > 10000 || (numValue > 0 && numValue < 0.001))
                    {
                        _logger.LogDebug("[StructuredParser] ❌ Removido (valor absurdo): '{Text}' ({Value})",
                            text, numValue);
                        removed++;
                        continue;
                    }
                }
            }

            // Filtro 5: Blocos duplicados (mesmo texto, mesma posição)
            if (filtered.Any(f => f.Text == block.Text &&
                                  Math.Abs(f.BoundingBox.X - block.BoundingBox.X) < 5 &&
                                  Math.Abs(f.BoundingBox.Y - block.BoundingBox.Y) < 5))
            {
                _logger.LogDebug("[StructuredParser] ❌ Removido (duplicado): '{Text}'", text);
                removed++;
                continue;
            }

            filtered.Add(block);
        }

        _logger.LogInformation("[StructuredParser] 🧹 Filtragem concluída:");
        _logger.LogInformation("[StructuredParser]    • Blocos originais: {Total}", textBlocks.Count);
        _logger.LogInformation("[StructuredParser]    • Blocos removidos: {Removed}", removed);
        _logger.LogInformation("[StructuredParser]    • Blocos mantidos: {Kept}", filtered.Count);
        _logger.LogInformation("[StructuredParser]    • Taxa de filtragem: {Rate:P1}",
            removed / (double)textBlocks.Count);

        return filtered;
    }

    private bool IsDigit(string text)
    {
        return text.Length == 1 && char.IsDigit(text[0]);
    }

    private bool ContainsOnlySpecialChars(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        // Permitir letras, dígitos, vírgula, ponto, parênteses
        var allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,()%- ";
        return text.All(c => !allowedChars.Contains(c));
    }

    // ═══════════════════════════════════════════════════════════════════
    // ETAPA 1: Validação de Estrutura
    // ═══════════════════════════════════════════════════════════════════

    private bool ValidateNutritionTableStructure(List<OcrTextBlock> textBlocks, string rawText)
    {
        var allText = string.Join(" ", textBlocks.Select(b => b.Text)).ToLowerInvariant();

        bool hasTitle = allText.Contains("informa\u00e7\u00e3o nutricional") ||
                       allText.Contains("informacao nutricional") ||
                       allText.Contains("tabela nutricional") ||
                       allText.Contains("nutrition facts");

        bool hasEnergyValue = allText.Contains("valor energ\u00e9tico") ||
                             allText.Contains("valor energetico") ||
                             allText.Contains("energia") ||
                             allText.Contains("calorias") ||
                             allText.Contains("kcal");

        bool hasNutrients = allText.Contains("carboidrato") ||
                           allText.Contains("prote\u00edna") ||
                           allText.Contains("proteina") ||
                           allText.Contains("gordura");

        // hasBasis: aceitar 100g/ml OU indicativos de por\u00e7\u00e3o (tabelas per-por\u00e7\u00e3o tamb\u00e9m s\u00e3o v\u00e1lidas)
        bool hasBasis = (allText.Contains("100") && (allText.Contains("ml") || allText.Contains("g")))
                     || allText.Contains("por por\u00e7\u00e3o")
                     || allText.Contains("por porcao")
                     || allText.Contains("quantidade por")
                     || Regex.IsMatch(allText, @"por\u00e7\u00e3o\s+de\s+\d+");

        bool isValid = (hasTitle || hasEnergyValue) && hasNutrients && hasBasis;

        _logger.LogInformation("[StructuredParser] \ud83d\udd0d Valida\u00e7\u00e3o de estrutura:");
        _logger.LogInformation("[StructuredParser]    \u2022 T\u00edtulo: {HasTitle}", hasTitle ? "\u2705" : "\u274c");
        _logger.LogInformation("[StructuredParser]    \u2022 Valor energ\u00e9tico: {HasEnergy}", hasEnergyValue ? "\u2705" : "\u274c");
        _logger.LogInformation("[StructuredParser]    \u2022 Nutrientes: {HasNutrients}", hasNutrients ? "\u2705" : "\u274c");
        _logger.LogInformation("[StructuredParser]    \u2022 Base (100g/ml ou por\u00e7\u00e3o): {HasBasis}", hasBasis ? "\u2705" : "\u274c");
        _logger.LogInformation("[StructuredParser]    \u2022 Resultado: {IsValid}", isValid ? "\u2705 V\u00c1LIDA" : "\u274c INV\u00c1LIDA");

        return isValid;
    }

    // ═══════════════════════════════════════════════════════════════════
    // ETAPA 2: Detecção de Colunas por Coordenadas X
    // ═══════════════════════════════════════════════════════════════════

    private List<ColumnCluster> DetectColumns(List<OcrTextBlock> textBlocks)
    {
        // Agrupar blocos de texto por coordenada X (clustering)
        var sortedByX = textBlocks
            .Where(b => IsNumericOrUnit(b.Text)) // Focar em números e unidades
            .OrderBy(b => b.BoundingBox.X)
            .ToList();

        var columns = new List<ColumnCluster>();
        
        foreach (var block in sortedByX)
        {
            var existingColumn = columns.FirstOrDefault(c => 
                Math.Abs(c.AverageX - block.BoundingBox.X) < X_TOLERANCE);

            if (existingColumn != null)
            {
                // Adicionar à coluna existente
                existingColumn.Blocks.Add(block);
                existingColumn.AverageX = existingColumn.Blocks.Average(b => b.BoundingBox.X);
            }
            else
            {
                // Criar nova coluna
                columns.Add(new ColumnCluster
                {
                    Blocks = new List<OcrTextBlock> { block },
                    AverageX = block.BoundingBox.X
                });
            }
        }

        // Ordenar colunas por posição X (esquerda para direita)
        return columns.OrderBy(c => c.AverageX).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ETAPA 3: Detecção de Linhas (Nutrientes) por Coordenadas Y
    // ═══════════════════════════════════════════════════════════════════

    private List<RowCluster> DetectRows(List<OcrTextBlock> textBlocks)
    {
        var sortedByY = textBlocks.OrderBy(b => b.BoundingBox.Y).ToList();
        var rows = new List<RowCluster>();

        foreach (var block in sortedByY)
        {
            var existingRow = rows.FirstOrDefault(r => 
                Math.Abs(r.AverageY - block.BoundingBox.Y) < Y_TOLERANCE);

            if (existingRow != null)
            {
                existingRow.Blocks.Add(block);
                existingRow.AverageY = existingRow.Blocks.Average(b => b.BoundingBox.Y);
            }
            else
            {
                rows.Add(new RowCluster
                {
                    Blocks = new List<OcrTextBlock> { block },
                    AverageY = block.BoundingBox.Y
                });
            }
        }

        // Ordenar linhas por posição Y (topo para baixo)
        return rows.OrderBy(r => r.AverageY).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ETAPA 4: Identificação de Tipos de Colunas
    // ═══════════════════════════════════════════════════════════════════

    private ColumnMapping IdentifyColumnTypes(List<OcrTextBlock> textBlocks, List<ColumnCluster> columns)
    {
        var mapping = new ColumnMapping();

        // ── Threshold dinâmico: top 30% do range vertical da tabela ──────────
        double minY = textBlocks.Min(b => b.BoundingBox.Y);
        double maxY = textBlocks.Max(b => b.BoundingBox.Y);
        double headerYThreshold = minY + (maxY - minY) * 0.30;

        var allTextLower = string.Join(" ", textBlocks.Select(b => b.Text)).ToLowerInvariant();
        var headerBlocks = textBlocks.Where(b => b.BoundingBox.Y <= headerYThreshold).ToList();

        _logger.LogDebug("[StructuredParser] Header threshold: Y≤{Threshold:F0} (range {Min:F0}–{Max:F0})",
            headerYThreshold, minY, maxY);

        // ── DETECÇÃO DO TIPO DE TABELA ───────────────────────────────────────
        bool hasPer100Column  = Regex.IsMatch(allTextLower, @"100\s*(g|ml)");
        bool hasPortionHeader = allTextLower.Contains("por porção") ||
                                allTextLower.Contains("por porcao") ||
                                allTextLower.Contains("quantidade por");

        // Extrair tamanho da porção do cabeçalho.
        // Regex correto: "porção" = por + ção (\u00e7\u00e3o) ou variantes sem acento.
        // Exemplos: "PORÇÃO DE 200ml", "porção: 30g", "porcao de 50g"
        var portionMatch = Regex.Match(allTextLower,
            @"por(?:\u00e7\u00e3o|\u00e7ao|cao)\s*(?:de\s*)?(\d+(?:[.,]\d+)?)\s*(ml|g)",
            RegexOptions.IgnoreCase);
        if (!portionMatch.Success)
        {
            // Segunda tentativa: busca direta por "\d+ml" ou "\d+g" próximo a palavras-chave
            portionMatch = Regex.Match(allTextLower,
                @"(?:quantidade|porcao|por[çc][aã]o)[^\d]{0,30}?(\d+(?:[.,]\d+)?)\s*(ml|g)",
                RegexOptions.IgnoreCase);
        }
        if (portionMatch.Success)
        {
            mapping.PortionSizeValue = double.Parse(
                portionMatch.Groups[1].Value.Replace(",", "."),
                System.Globalization.CultureInfo.InvariantCulture);
            mapping.PortionSizeUnit = portionMatch.Groups[2].Value.ToLower();
            _logger.LogInformation("[StructuredParser] Porção detectada: {Size}{Unit}",
                mapping.PortionSizeValue, mapping.PortionSizeUnit);
        }

        // ── Mapear colunas por cabeçalho ───────────────────────────────────────
        var per100Candidates  = new List<(int index, string unit)>();
        var portionCandidates = new List<int>();
        var vdCandidates      = new List<int>();

        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var headerText = string.Join(" ", headerBlocks
                .Where(h => Math.Abs(h.BoundingBox.X - column.AverageX) < X_TOLERANCE * 2)
                .Select(h => h.Text.ToLowerInvariant()));

            if (headerText.Contains("100") && (headerText.Contains("ml") || headerText.Contains("g")))
            {
                per100Candidates.Add((i, headerText.Contains("ml") ? "ml" : "g"));
            }
            else if (headerText.Contains("porção") || headerText.Contains("porcao") ||
                     headerText.Contains("quantidade") ||
                     Regex.IsMatch(headerText, @"\d+\s*(g|ml)"))
            {
                portionCandidates.Add(i);
            }
            else if (headerText.Contains("%") || headerText.Contains("vd") ||
                     headerText.Contains("valor diário"))
            {
                vdCandidates.Add(i);
            }
        }

        if (vdCandidates.Count > 0) mapping.VdIndex = vdCandidates[0];

        if (per100Candidates.Count > 0)
        {
            // TIPO 1 ou 3: tabela com coluna 100g/ml (pode também ter porção)
            mapping.Per100Index = per100Candidates[0].index;
            mapping.Unit        = per100Candidates[0].unit;
            if (portionCandidates.Count > 0) mapping.PortionIndex = portionCandidates[0];
            mapping.IsPerPortionOnly = false;

            _logger.LogInformation("[StructuredParser] 📊 Tipo de tabela: Per-100{Unit} (col {Col})",
                mapping.Unit, mapping.Per100Index);
        }
        else if (portionCandidates.Count > 0 || hasPortionHeader)
        {
            // TIPO 2: apenas coluna per-porção — converter para per-100
            mapping.IsPerPortionOnly = true;
            mapping.PortionIndex     = portionCandidates.Count > 0 ? portionCandidates[0] : (int?)null;
            mapping.Unit             = mapping.PortionSizeUnit;

            // Se não detectou coluna por índice, usar a primeira coluna numérica (geralmente col 0)
            if (!mapping.PortionIndex.HasValue && columns.Count >= 1)
                mapping.PortionIndex = 0;

            // Usar PortionIndex como Per100Index para que ExtractNutrientValues extraia os valores
            mapping.Per100Index = mapping.PortionIndex;

            _logger.LogInformation(
                "[StructuredParser] 📊 Tipo de tabela: Per-Porção ({Size}{Unit}) — será convertido para per-100{Unit}",
                mapping.PortionSizeValue, mapping.PortionSizeUnit, mapping.PortionSizeUnit);
        }
        else
        {
            // FALLBACK: assumir primeira coluna numérica = 100g
            mapping.Per100Index      = 0;
            mapping.Unit             = "g";
            mapping.IsPerPortionOnly = false;

            if (columns.Count >= 2) mapping.PortionIndex = 1;
            if (columns.Count >= 3) mapping.VdIndex      = 2;

            _logger.LogWarning(
                "[StructuredParser] Tipo de tabela não detectado — fallback posicional (col 0 = 100g)");
        }

        return mapping;
    }

    // ═══════════════════════════════════════════════════════════════════
    // ETAPA 5: Extração de Valores de Nutrientes
    // ═══════════════════════════════════════════════════════════════════

    private ExtractedNutritionData ExtractNutrientValues(
        List<RowCluster> rows,
        List<ColumnCluster> columns,
        ColumnMapping mapping)
    {
        var data = new ExtractedNutritionData { Unit = mapping.Unit ?? "g" };

        // Fator de conversão per-porção → per-100: ex. porção=200ml → fator=2,0
        double conversionFactor = mapping.IsPerPortionOnly && mapping.PortionSizeValue > 0
            ? mapping.PortionSizeValue / 100.0
            : 1.0;

        if (mapping.IsPerPortionOnly)
            _logger.LogInformation(
                "[StructuredParser] Porção únicamente: fator de conversão = {Factor:F2} (porção={Size}{Unit})",
                conversionFactor, mapping.PortionSizeValue, mapping.PortionSizeUnit);

        _logger.LogInformation("[StructuredParser] \ud83d\udd0d Extraindo valores de nutrientes...");

        foreach (var row in rows)
        {
            var nutrientBlock = row.Blocks.FirstOrDefault(b => IsNutrientName(b.Text));
            if (nutrientBlock == null) continue;

            var nutrientName = NormalizeNutrientName(nutrientBlock.Text);
            if (nutrientName == "ignorar") continue;

            _logger.LogDebug("[StructuredParser] \ud83d\udccb Linha: {Nutrient}", nutrientName);

            foreach (var block in row.Blocks.OrderBy(b => b.BoundingBox.X))
                _logger.LogDebug("[StructuredParser]    Block: '{Text}' @ X={X:F1} (conf={Conf:F2})",
                    block.Text, block.BoundingBox.X, block.Confidence);

            double? value = null;

            if (mapping.Per100Index.HasValue && mapping.Per100Index.Value < columns.Count)
            {
                var targetColumn = columns[mapping.Per100Index.Value];

                // Para valor energético: tentar parsing especial ("48kcal", "48kcal = 202kJ")
                if (nutrientName == "energia")
                {
                    value = TryExtractEnergyFromRow(row, targetColumn);
                }

                if (!value.HasValue)
                {
                    var numericBlocks = row.Blocks
                        .Where(b => IsNumeric(b.Text))
                        .Where(b => !IsPercentageVD(b.Text))
                        .Where(b => Math.Abs(b.BoundingBox.X - targetColumn.AverageX) < X_TOLERANCE * 1.5)
                        .OrderBy(b => Math.Abs(b.BoundingBox.X - targetColumn.AverageX))
                        .ToList();

                    foreach (var block in numericBlocks)
                    {
                        if (double.TryParse(block.Text.Replace(",", "."),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var parsed)
                            && IsValidValueForNutrient(nutrientName, parsed))
                        {
                            value = parsed;
                            _logger.LogDebug("[StructuredParser]    \u2705 Valor: {Val} ('{Text}' X={X:F1})",
                                value, block.Text, block.BoundingBox.X);
                            break;
                        }
                    }
                }
            }

            if (value.HasValue)
            {
                // Aplicar conversão per-porção → per-100 quando necessário
                var converted = conversionFactor > 1.0
                    ? Math.Round(value.Value / conversionFactor, 2)
                    : value.Value;

                MapNutrientValue(data, nutrientName, converted);
                _logger.LogInformation("[StructuredParser]    \u2192 {Nutrient}: {Raw}{Conv}",
                    nutrientName, value.Value,
                    conversionFactor > 1.0 ? $" ÷ {conversionFactor:F1} = {converted}" : "");
            }
        }

        return data;
    }

    /// <summary>
    /// Extrai calorias de linhas com formato misto como "48kcal", "48kcal = 202kJ" ou "360 kcal".
    /// Azure OCR pode retornar o valor + unidade como um único bloco.
    /// </summary>
    private double? TryExtractEnergyFromRow(RowCluster row, ColumnCluster targetColumn)
    {
        foreach (var block in row.Blocks
            .Where(b => Math.Abs(b.BoundingBox.X - targetColumn.AverageX) < X_TOLERANCE * 2)
            .OrderBy(b => Math.Abs(b.BoundingBox.X - targetColumn.AverageX)))
        {
            var text = block.Text.Replace(",", ".");

            // Formato "48kcal" ou "48kcal = 202kJ"
            var kcalMatch = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*kcal", RegexOptions.IgnoreCase);
            if (kcalMatch.Success &&
                double.TryParse(kcalMatch.Groups[1].Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var kcal)
                && kcal > 0 && kcal <= MAX_CALORIES)
                return kcal;

            // Formato numérico simples
            if (double.TryParse(text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var plain)
                && plain > 0 && plain <= MAX_CALORIES)
                return plain;
        }
        return null;
    }

    private void MapNutrientValue(ExtractedNutritionData data, string nutrient, double? value)
    {
        if (!value.HasValue) return;

        switch (nutrient)
        {
            case "energia":
            case "calorias":
            case "valor_energetico":
                data.Calories = value;
                break;
            case "carboidratos":
                data.Carbs = value;
                break;
            case "acucares":
            case "acucares_totais":
                data.Sugar = value;
                break;
            case "acucares_adicionados":
                data.AddedSugar = value;
                break;
            case "proteinas":
                data.Protein = value;
                break;
            case "gorduras":
            case "gorduras_totais":
                data.Fat = value;
                break;
            case "gorduras_saturadas":
                data.SaturatedFat = value;
                break;
            case "fibras":
                data.Fiber = value;
                break;
            case "sodio":
                data.Sodium = value;
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // ETAPA 6: Validação de Dados Extraídos
    // ═══════════════════════════════════════════════════════════════════

    private ValidationResult ValidateExtractedData(ExtractedNutritionData data)
    {
        var errors = new List<string>();

        // Regra 1: Calorias devem estar presentes
        if (!data.Calories.HasValue || data.Calories.Value <= 0)
        {
            errors.Add("Calorias não detectadas ou inválidas");
        }

        // 🔥 NOVO: Validação de DOMÍNIO rigorosa
        // Regra 1.5: Valores devem estar em ranges REALISTAS
        if (data.Calories.HasValue)
        {
            if (data.Calories.Value > MAX_CALORIES)
            {
                errors.Add($"Calorias fora do range realista (0-{MAX_CALORIES}): {data.Calories}kcal");
            }
        }

        if (data.Protein.HasValue && (data.Protein.Value < 0 || data.Protein.Value > MAX_PROTEIN))
        {
            errors.Add($"Proteína fora do range (0-{MAX_PROTEIN}g): {data.Protein}g");
        }

        if (data.Carbs.HasValue && (data.Carbs.Value < 0 || data.Carbs.Value > MAX_CARBS))
        {
            errors.Add($"Carboidratos fora do range (0-{MAX_CARBS}g): {data.Carbs}g");
        }

        if (data.Fat.HasValue && (data.Fat.Value < 0 || data.Fat.Value > MAX_FAT))
        {
            errors.Add($"Gordura fora do range (0-{MAX_FAT}g): {data.Fat}g");
        }

        if (data.Sugar.HasValue && (data.Sugar.Value < 0 || data.Sugar.Value > MAX_SUGAR))
        {
            errors.Add($"Açúcar fora do range (0-{MAX_SUGAR}g): {data.Sugar}g");
        }

        if (data.Fiber.HasValue && (data.Fiber.Value < 0 || data.Fiber.Value > MAX_FIBER))
        {
            errors.Add($"Fibra fora do range (0-{MAX_FIBER}g): {data.Fiber}g");
        }

        if (data.Sodium.HasValue && (data.Sodium.Value < 0 || data.Sodium.Value > MAX_SODIUM))
        {
            errors.Add($"Sódio fora do range (0-{MAX_SODIUM}mg): {data.Sodium}mg");
        }

        // Regra 2: Calorias devem ser coerentes com macros
        if (data.Calories.HasValue && data.Protein.HasValue && data.Carbs.HasValue && data.Fat.HasValue)
        {
            var expectedCalories = (data.Protein.Value * 4) + (data.Carbs.Value * 4) + (data.Fat.Value * 9);
            var delta = Math.Abs(data.Calories.Value - expectedCalories) / Math.Max(data.Calories.Value, expectedCalories);

            if (delta > 0.30) // Tolerância de 30%
            {
                errors.Add($"Inconsistência calórica: {data.Calories}kcal vs {expectedCalories:F1}kcal esperado (delta: {delta:P0})");
            }
        }

        // Regra 3: Açúcar não pode ser maior que carboidratos
        if (data.Sugar.HasValue && data.Carbs.HasValue && data.Sugar.Value > data.Carbs.Value)
        {
            errors.Add($"Açúcar ({data.Sugar}g) > Carboidratos ({data.Carbs}g)");
        }

        // Regra 4: Gordura saturada não pode ser maior que gordura total
        if (data.SaturatedFat.HasValue && data.Fat.HasValue && data.SaturatedFat.Value > data.Fat.Value)
        {
            errors.Add($"Gordura saturada ({data.SaturatedFat}g) > Gordura total ({data.Fat}g)");
        }

        // 🔥 NOVO: Regra 5 - Açúcar adicionado não pode ser maior que açúcar total
        if (data.AddedSugar.HasValue && data.Sugar.HasValue && data.AddedSugar.Value > data.Sugar.Value)
        {
            errors.Add($"Açúcar adicionado ({data.AddedSugar}g) > Açúcar total ({data.Sugar}g)");
        }

        // 🔥 NOVO: Regra 6 - Soma de macros não pode exceder 100g
        if (data.Protein.HasValue && data.Carbs.HasValue && data.Fat.HasValue && data.Fiber.HasValue)
        {
            var totalMacros = data.Protein.Value + data.Carbs.Value + data.Fat.Value + data.Fiber.Value;
            if (totalMacros > 110) // Tolerância de 10g para água/cinzas
            {
                errors.Add($"Soma de macros excede 100g: {totalMacros:F1}g (Prot+Carbs+Fat+Fiber)");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Reason = errors.Count > 0 ? string.Join("; ", errors) : "OK",
            Errors = errors
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // ETAPA 7: Autocorreção de Dados (FORTALECIDA)
    // ═══════════════════════════════════════════════════════════════════

    private CorrectionResult AutoCorrectData(ExtractedNutritionData data, ValidationResult validation)
    {
        var corrected = new ExtractedNutritionData
        {
            Calories = data.Calories,
            Carbs = data.Carbs,
            Sugar = data.Sugar,
            AddedSugar = data.AddedSugar,
            Protein = data.Protein,
            Fat = data.Fat,
            SaturatedFat = data.SaturatedFat,
            Fiber = data.Fiber,
            Sodium = data.Sodium,
            Unit = data.Unit
        };

        var corrections = new List<string>();

        // 🔥 CORREÇÃO 1: Remover valores fora do domínio
        if (corrected.Calories.HasValue && corrected.Calories.Value > MAX_CALORIES)
        {
            _logger.LogWarning("[StructuredParser] 🔧 Removendo calorias inválidas: {Val}kcal", corrected.Calories);
            corrected.Calories = null;
            corrections.Add("Calorias removidas (fora do range)");
        }

        if (corrected.Protein.HasValue && (corrected.Protein.Value < 0 || corrected.Protein.Value > MAX_PROTEIN))
        {
            _logger.LogWarning("[StructuredParser] 🔧 Removendo proteína inválida: {Val}g", corrected.Protein);
            corrected.Protein = null;
            corrections.Add("Proteína removida (fora do range)");
        }

        if (corrected.Carbs.HasValue && (corrected.Carbs.Value < 0 || corrected.Carbs.Value > MAX_CARBS))
        {
            _logger.LogWarning("[StructuredParser] 🔧 Removendo carboidratos inválidos: {Val}g", corrected.Carbs);
            corrected.Carbs = null;
            corrections.Add("Carboidratos removidos (fora do range)");
        }

        if (corrected.Fat.HasValue && (corrected.Fat.Value < 0 || corrected.Fat.Value > MAX_FAT))
        {
            _logger.LogWarning("[StructuredParser] 🔧 Removendo gordura inválida: {Val}g", corrected.Fat);
            corrected.Fat = null;
            corrections.Add("Gordura removida (fora do range)");
        }

        if (corrected.Sugar.HasValue && (corrected.Sugar.Value < 0 || corrected.Sugar.Value > MAX_SUGAR))
        {
            _logger.LogWarning("[StructuredParser] 🔧 Removendo açúcar inválido: {Val}g", corrected.Sugar);
            corrected.Sugar = null;
            corrections.Add("Açúcar removido (fora do range)");
        }

        if (corrected.Fiber.HasValue && (corrected.Fiber.Value < 0 || corrected.Fiber.Value > MAX_FIBER))
        {
            _logger.LogWarning("[StructuredParser] 🔧 Removendo fibra inválida: {Val}g", corrected.Fiber);
            corrected.Fiber = null;
            corrections.Add("Fibra removida (fora do range)");
        }

        if (corrected.Sodium.HasValue && (corrected.Sodium.Value < 0 || corrected.Sodium.Value > MAX_SODIUM))
        {
            _logger.LogWarning("[StructuredParser] 🔧 Removendo sódio inválido: {Val}mg", corrected.Sodium);
            corrected.Sodium = null;
            corrections.Add("Sódio removido (fora do range)");
        }

        // 🔥 CORREÇÃO 2: Inferir carboidratos a partir de calorias (se possível)
        if (corrected.Calories.HasValue && corrected.Protein.HasValue && corrected.Fat.HasValue)
        {
            var expectedCalories = (corrected.Protein.Value * 4) + (corrected.Fat.Value * 9);
            var remainingCalories = corrected.Calories.Value - expectedCalories;
            var inferredCarbs = remainingCalories / 4;

            if (inferredCarbs > 0 && inferredCarbs <= MAX_CARBS)
            {
                var originalCarbs = corrected.Carbs ?? 0;
                var delta = corrected.Carbs.HasValue 
                    ? Math.Abs(inferredCarbs - originalCarbs) / Math.Max(inferredCarbs, originalCarbs)
                    : 1.0;

                // Se não há carbs OU há mas com grande diferença (>30%), corrigir
                if (!corrected.Carbs.HasValue || delta > 0.30)
                {
                    _logger.LogWarning("[StructuredParser] 🔧 Corrigindo carboidratos: {Old}g → {New}g (inferido)",
                        corrected.Carbs, Math.Round(inferredCarbs, 1));
                    corrected.Carbs = Math.Round(inferredCarbs, 1);
                    corrections.Add($"Carboidratos inferidos por calorias: {corrected.Carbs}g");
                }
            }
        }

        // 🔥 CORREÇÃO 3: Limitar açúcar a carboidratos
        if (corrected.Sugar.HasValue && corrected.Carbs.HasValue && corrected.Sugar.Value > corrected.Carbs.Value)
        {
            _logger.LogWarning("[StructuredParser] 🔧 Limitando açúcar ao total de carboidratos: {Old}g → {New}g",
                corrected.Sugar, corrected.Carbs);
            corrected.Sugar = corrected.Carbs;
            corrections.Add($"Açúcar limitado a carboidratos: {corrected.Sugar}g");
        }

        // 🔥 CORREÇÃO 4: Limitar açúcar adicionado a açúcar total
        if (corrected.AddedSugar.HasValue && corrected.Sugar.HasValue && corrected.AddedSugar.Value > corrected.Sugar.Value)
        {
            _logger.LogWarning("[StructuredParser] 🔧 Limitando açúcar adicionado ao açúcar total: {Old}g → {New}g",
                corrected.AddedSugar, corrected.Sugar);
            corrected.AddedSugar = corrected.Sugar;
            corrections.Add($"Açúcar adicionado limitado: {corrected.AddedSugar}g");
        }

        // 🔥 CORREÇÃO 5: Limitar gordura saturada a gordura total
        if (corrected.SaturatedFat.HasValue && corrected.Fat.HasValue && corrected.SaturatedFat.Value > corrected.Fat.Value)
        {
            _logger.LogWarning("[StructuredParser] 🔧 Limitando gordura saturada à gordura total: {Old}g → {New}g",
                corrected.SaturatedFat, corrected.Fat);
            corrected.SaturatedFat = corrected.Fat;
            corrections.Add($"Gordura saturada limitada: {corrected.SaturatedFat}g");
        }

        // Validar novamente
        var revalidation = ValidateExtractedData(corrected);

        if (corrections.Any())
        {
            _logger.LogInformation("[StructuredParser] 🔧 Correções aplicadas:");
            foreach (var correction in corrections)
            {
                _logger.LogInformation("[StructuredParser]    • {Correction}", correction);
            }
        }

        return new CorrectionResult
        {
            IsValid = revalidation.IsValid,
            Data = corrected,
            Errors = revalidation.Errors
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Fallback para Parser Simples
    // ═══════════════════════════════════════════════════════════════════

    private StructuredNutritionResult FallbackToSimpleParser(string rawText)
    {
        _logger.LogWarning("[StructuredParser] Usando fallback: parser simples baseado em texto");

        var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        var simpleParser = new NutritionTableParser();
        var parsed = simpleParser.Parse(lines);

        return new StructuredNutritionResult
        {
            Success = parsed.HasAnyValue,
            Calories = parsed.Calories,
            Protein = parsed.Protein,
            Fat = parsed.Fat,
            SaturatedFat = parsed.SaturatedFat,
            Carbs = parsed.Carbs,
            Sugar = parsed.Sugar,
            AddedSugar = parsed.AddedSugar,
            Fiber = parsed.Fiber,
            Sodium = parsed.Sodium,
            Unit = parsed.Unit,
            ErrorMessage = parsed.HasAnyValue ? null : "Parser simples não conseguiu extrair dados"
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private StructuredNutritionResult BuildResult(ExtractedNutritionData data)
    {
        return new StructuredNutritionResult
        {
            Success = data.Calories.HasValue || data.Carbs.HasValue,
            Calories = data.Calories,
            Protein = data.Protein,
            Fat = data.Fat,
            SaturatedFat = data.SaturatedFat,
            Carbs = data.Carbs,
            Sugar = data.Sugar,
            AddedSugar = data.AddedSugar,
            Fiber = data.Fiber,
            Sodium = data.Sodium,
            Unit = data.Unit
        };
    }

    /// <summary>
    /// Verifica se o texto é %VD (percentual de Valor Diário).
    ///
    /// Regra: SOMENTE filtrar quando houver marcador explícito no texto (%,vd,*).
    /// Não usar heurística de "inteiro 0-100 = %VD" — essa regra destrói valores
    /// reais como 32g (carbs), 33g (proteína), 73mg (sódio), etc.
    /// A exclusão espacial da coluna %VD é feita em <see cref="IdentifyColumnTypes"/>.
    /// </summary>
    private static bool IsPercentageVD(string text)
    {
        var normalized = text.ToLowerInvariant().Replace(" ", "");
        return normalized.Contains("%") || normalized.Contains("vd") || normalized.Contains("*");
    }

    /// <summary>
    /// Valida se o valor está em um range realista para o nutriente
    /// </summary>
    private bool IsValidValueForNutrient(string nutrientName, double value)
    {
        return nutrientName switch
        {
            "energia" => value >= 0 && value <= MAX_CALORIES, // 0-900 kcal
            "carboidratos" => value >= 0 && value <= MAX_CARBS, // 0-100 g
            "acucares" => value >= 0 && value <= MAX_SUGAR, // 0-100 g
            "acucares_adicionados" => value >= 0 && value <= MAX_SUGAR, // 0-100 g
            "proteinas" => value >= 0 && value <= MAX_PROTEIN, // 0-100 g
            "gorduras" => value >= 0 && value <= MAX_FAT, // 0-100 g
            "gorduras_saturadas" => value >= 0 && value <= MAX_FAT, // 0-100 g
            "fibras" => value >= 0 && value <= MAX_FIBER, // 0-100 g
            "sodio" => value >= 0 && value <= MAX_SODIUM, // 0-5000 mg
            _ => true // Nutriente desconhecido, aceitar por padrão
        };
    }

    private bool IsNumericOrUnit(string text)
    {
        return IsNumeric(text) || text.ToLowerInvariant() is "g" or "ml" or "mg" or "kcal";
    }

    private bool IsNumeric(string text)
    {
        var cleaned = text.Replace(",", ".").Replace(" ", "");
        return double.TryParse(cleaned, out _);
    }

    private bool IsNutrientName(string text)
    {
        var normalized = text.ToLowerInvariant();
        return normalized.Contains("energia") || normalized.Contains("caloria") ||
               normalized.Contains("carboidrato") || normalized.Contains("açúcar") || normalized.Contains("acucar") ||
               normalized.Contains("proteína") || normalized.Contains("proteina") ||
               normalized.Contains("gordura") || normalized.Contains("fibra") ||
               normalized.Contains("sódio") || normalized.Contains("sodio");
    }

    private string NormalizeNutrientName(string text)
    {
        var normalized = text.ToLowerInvariant()
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ç", "c")
            .Replace("-", " ").Trim();

        if (normalized.Contains("energia") || normalized.Contains("energetico") || normalized.Contains("caloria"))
            return "energia";
        if (normalized.Contains("carboidrato"))
            return "carboidratos";
        if (normalized.Contains("acucar") && (normalized.Contains("adicionado") || normalized.Contains("adiciona")))
            return "acucares_adicionados";
        if (normalized.Contains("acucar"))
            return "acucares";
        if (normalized.Contains("proteina"))
            return "proteinas";
        if (normalized.Contains("gordura") && normalized.Contains("saturada"))
            return "gorduras_saturadas";
        if (normalized.Contains("gordura") && normalized.Contains("trans"))
            return "ignorar";  // gordura trans geralmente 0, não impacta score
        if (normalized.Contains("gordura") && (normalized.Contains("mono") || normalized.Contains("poli")))
            return "ignorar";  // monoinsaturada/poli-insaturada não são campos padrão ANVISA
        if (normalized.Contains("gordura"))
            return "gorduras";
        if (normalized.Contains("fibra"))   // cobre "fibra", "fibra alimentar"
            return "fibras";
        if (normalized.Contains("sodio"))
            return "sodio";
        if (normalized.Contains("colesterol") || normalized.Contains("calcio") ||
            normalized.Contains("vitamina")   || normalized.Contains("zinco")  ||
            normalized.Contains("ferro")       || normalized.Contains("poliol")  ||
            normalized.Contains("magnesio"))
            return "ignorar";  // micronutrientes e polióis: não são campos do perfil nutricional base

        return normalized;
    }

    private void LogColumns(List<ColumnCluster> columns)
    {
        _logger.LogInformation("[StructuredParser] 📊 Detectadas {Count} colunas:", columns.Count);
        for (int i = 0; i < columns.Count; i++)
        {
            _logger.LogInformation("[StructuredParser]    Col {Index}: X={X:F1}, Blocks={Count}",
                i, columns[i].AverageX, columns[i].Blocks.Count);
        }
    }

    private void LogColumnMapping(ColumnMapping mapping)
    {
        _logger.LogInformation("[StructuredParser] 🗂️ Mapeamento de colunas:");
        _logger.LogInformation("[StructuredParser]    • 100{Unit}: Col {Index}",
            mapping.Unit ?? "?", mapping.Per100Index?.ToString() ?? "não detectada");
        _logger.LogInformation("[StructuredParser]    • Porção: Col {Index}",
            mapping.PortionIndex?.ToString() ?? "não detectada");
        _logger.LogInformation("[StructuredParser]    • %VD: Col {Index}",
            mapping.VdIndex?.ToString() ?? "não detectada");
    }

    private void LogExtractedValues(StructuredNutritionResult result)
    {
        _logger.LogInformation("[StructuredParser] ✅ Valores extraídos:");
        _logger.LogInformation("[StructuredParser]    • Calorias: {Val} kcal", result.Calories?.ToString("F1") ?? "N/A");
        _logger.LogInformation("[StructuredParser]    • Proteína: {Val} g", result.Protein?.ToString("F1") ?? "N/A");
        _logger.LogInformation("[StructuredParser]    • Gordura: {Val} g", result.Fat?.ToString("F1") ?? "N/A");
        _logger.LogInformation("[StructuredParser]    • Carboidratos: {Val} g", result.Carbs?.ToString("F1") ?? "N/A");
        _logger.LogInformation("[StructuredParser]    • Açúcar: {Val} g", result.Sugar?.ToString("F1") ?? "N/A");
        _logger.LogInformation("[StructuredParser]    • Sódio: {Val} mg", result.Sodium?.ToString("F1") ?? "N/A");
    }
}

// ═══════════════════════════════════════════════════════════════════
// DTOs Internos
// ═══════════════════════════════════════════════════════════════════

internal class ColumnCluster
{
    public List<OcrTextBlock> Blocks { get; set; } = new();
    public double AverageX { get; set; }
}

internal class RowCluster
{
    public List<OcrTextBlock> Blocks { get; set; } = new();
    public double AverageY { get; set; }
}

internal class ColumnMapping
{
    public int? Per100Index   { get; set; }
    public int? PortionIndex  { get; set; }
    public int? VdIndex       { get; set; }
    public string? Unit       { get; set; }

    // Suporte a tabelas onde só existe coluna per-porção (sem 100g/ml)
    // Nesse caso, os valores serão extraídos da coluna de porção e convertidos
    // dividindo pelo fator: PortionSizeValue / 100
    public bool   IsPerPortionOnly   { get; set; }
    public double PortionSizeValue   { get; set; } = 100.0;  // ex.: 200 (ml ou g)
    public string PortionSizeUnit    { get; set; } = "g";
}

internal class ExtractedNutritionData
{
    public double? Calories { get; set; }
    public double? Carbs { get; set; }
    public double? Sugar { get; set; }
    public double? AddedSugar { get; set; }
    public double? Protein { get; set; }
    public double? Fat { get; set; }
    public double? SaturatedFat { get; set; }
    public double? Fiber { get; set; }
    public double? Sodium { get; set; }
    public string Unit { get; set; } = "g";
}

internal class ValidationResult
{
    public bool IsValid { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}

internal class CorrectionResult
{
    public bool IsValid { get; set; }
    public ExtractedNutritionData Data { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Resultado da extração estruturada de dados nutricionais.
/// </summary>
public class StructuredNutritionResult
{
    public bool Success { get; set; }
    public double? Calories { get; set; }
    public double? Protein { get; set; }
    public double? Fat { get; set; }
    public double? SaturatedFat { get; set; }
    public double? Carbs { get; set; }
    public double? Sugar { get; set; }
    public double? AddedSugar { get; set; }
    public double? Fiber { get; set; }
    public double? Sodium { get; set; }
    public string Unit { get; set; } = "g";
    public string? ErrorMessage { get; set; }
}
