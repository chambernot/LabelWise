# 🔧 Troubleshooting - Parser Estruturado OCR

## 🚨 PROBLEMAS COMUNS E SOLUÇÕES

### Problema 1: Parser Sempre Usa Fallback Simples

**Sintoma:**
```
[StructuredParser] ⚠️ TextBlocks não disponíveis, usando parser simples (fallback)
```

**Causa:**
- Azure Vision OCR não está retornando `TextBlocks`
- `OcrResultDto.TextBlocks` está nulo ou vazio

**Diagnóstico:**
```csharp
// Adicionar log no AzureVisionReadOcrProvider
_logger.LogInformation("TextBlocks count: {Count}", result.TextBlocks?.Count ?? 0);

// Verificar se BoundingBox está sendo populado
foreach (var block in result.TextBlocks)
{
    _logger.LogDebug("Block: {Text}, BoundingBox: {Box}", 
        block.Text, block.BoundingBox != null ? "✅" : "❌");
}
```

**Solução:**
1. Verificar se `AzureVisionReadOcrProvider.ProcessAzureVisionResult` está criando `TextBlocks`
2. Confirmar que `BoundingPolygon` do Azure tem pelo menos 4 pontos
3. Verificar se coordenadas estão sendo calculadas corretamente:
   ```csharp
   var minX = points.Min(p => p.X);
   var minY = points.Min(p => p.Y);
   var maxX = points.Max(p => p.X);
   var maxY = points.Max(p => p.Y);
   ```

---

### Problema 2: Parser Não Detecta Tabela Nutricional

**Sintoma:**
```
[StructuredParser] ❌ Estrutura de tabela nutricional não detectada
```

**Causa:**
- Texto OCR não contém indicadores obrigatórios
- Imagem não é de uma tabela nutricional

**Diagnóstico:**
```csharp
// Logar texto extraído para análise
var allText = string.Join(" ", textBlocks.Select(b => b.Text)).ToLowerInvariant();
_logger.LogWarning("Texto completo: {Text}", allText);

// Verificar indicadores individuais
_logger.LogInformation("hasTitle: {Val}", allText.Contains("informação nutricional"));
_logger.LogInformation("hasEnergy: {Val}", allText.Contains("valor energético"));
_logger.LogInformation("hasNutrients: {Val}", allText.Contains("carboidrato"));
_logger.LogInformation("hasBasis: {Val}", allText.Contains("100"));
```

**Solução:**
1. Adicionar mais indicadores alternativos:
   ```csharp
   bool hasEnergyValue = 
       allText.Contains("valor energético") ||
       allText.Contains("valor energetico") ||
       allText.Contains("energia") ||
       allText.Contains("calorias") ||
       allText.Contains("kcal");
   ```
2. Ajustar regra de validação para ser mais permissiva:
   ```csharp
   // Antes: (hasTitle || hasEnergyValue) && hasNutrients && hasBasis
   // Depois: hasEnergyValue && hasNutrients
   ```

---

### Problema 3: Clustering Detecta Colunas Erradas

**Sintoma:**
```
[StructuredParser] 📊 Detectadas 5 colunas (esperado 3)
```

**Causa:**
- Tolerância X muito alta ou muito baixa
- Texto fragmentado em múltiplos blocos

**Diagnóstico:**
```csharp
// Logar coordenadas X de todos os blocos numéricos
var sortedByX = textBlocks
    .Where(b => IsNumeric(b.Text))
    .OrderBy(b => b.BoundingBox.X)
    .ToList();

foreach (var block in sortedByX)
{
    _logger.LogDebug("Block '{Text}' at X={X}", block.Text, block.BoundingBox.X);
}

// Verificar distribuição de coordenadas X
var xCoords = sortedByX.Select(b => b.BoundingBox.X).Distinct().ToList();
_logger.LogInformation("Coordenadas X únicas: {Coords}", string.Join(", ", xCoords));
```

**Solução:**
1. Ajustar tolerância X:
   ```csharp
   // De: const double X_TOLERANCE = 15.0;
   // Para: const double X_TOLERANCE = 20.0; // Aumentar se colunas estão se dividindo
   // Para: const double X_TOLERANCE = 10.0; // Diminuir se colunas estão se mesclando
   ```
2. Implementar agrupamento adaptativo:
   ```csharp
   // Calcular gap médio entre coordenadas X e usar como tolerância
   var gaps = xCoords.Zip(xCoords.Skip(1), (a, b) => b - a).ToList();
   var avgGap = gaps.Average();
   var adaptiveTolerance = avgGap * 0.3; // 30% do gap médio
   ```

---

### Problema 4: Valores Extraídos Incorretos

**Sintoma:**
```
[StructuredParser] ✅ Valores extraídos:
   • Carboidratos: 15g (esperado 12g)
```

**Causa:**
- Pega valor da coluna errada (porção ou %VD ao invés de 100ml)
- Mapeamento de colunas incorreto

**Diagnóstico:**
```csharp
// Logar mapeamento de colunas
_logger.LogInformation("Mapeamento de colunas:");
_logger.LogInformation("   Per100Index: {Index} (X={X})", 
    mapping.Per100Index, 
    mapping.Per100Index.HasValue ? columns[mapping.Per100Index.Value].AverageX : null);
_logger.LogInformation("   PortionIndex: {Index}", mapping.PortionIndex);
_logger.LogInformation("   VdIndex: {Index}", mapping.VdIndex);

// Logar blocos em cada linha
foreach (var row in rows)
{
    var nutrientBlock = row.Blocks.FirstOrDefault(b => IsNutrientName(b.Text));
    if (nutrientBlock != null)
    {
        _logger.LogDebug("Linha {Nutrient}:", nutrientBlock.Text);
        foreach (var block in row.Blocks.Where(b => IsNumeric(b.Text)))
        {
            _logger.LogDebug("   - Valor: {Val} (X={X})", block.Text, block.BoundingBox.X);
        }
    }
}
```

**Solução:**
1. Verificar se cabeçalhos estão sendo detectados:
   ```csharp
   var headerBlocks = textBlocks
       .Where(b => b.BoundingBox.Y < 200) // Ajustar threshold de Y
       .ToList();
   
   _logger.LogInformation("Cabeçalhos detectados: {Headers}", 
       string.Join(", ", headerBlocks.Select(h => $"{h.Text}@X={h.BoundingBox.X}")));
   ```
2. Melhorar detecção de cabeçalhos:
   ```csharp
   // Usar Y mais flexível (top 30% da imagem)
   var maxY = textBlocks.Max(b => b.BoundingBox.Y);
   var headerThreshold = maxY * 0.3;
   var headerBlocks = textBlocks.Where(b => b.BoundingBox.Y < headerThreshold);
   ```
3. Fallback heurístico se cabeçalho não detectado:
   ```csharp
   if (!mapping.Per100Index.HasValue && columns.Count >= 2)
   {
       // Primeira coluna numérica geralmente é 100g/ml
       mapping.Per100Index = 0;
       _logger.LogWarning("Usando heurística: primeira coluna = 100g/ml");
   }
   ```

---

### Problema 5: Validação Falha Mesmo Com Dados Corretos

**Sintoma:**
```
[StructuredParser] ⚠️ Validação falhou: Inconsistência calórica: 100kcal vs 102kcal esperado (delta: 2%)
```

**Causa:**
- Tolerância de validação muito restrita (< 30%)
- Arredondamentos causam pequenas diferenças

**Diagnóstico:**
```csharp
// Logar cálculo detalhado
var expectedCalories = (protein * 4) + (carbs * 4) + (fat * 9);
var delta = Math.Abs(calories - expectedCalories) / Math.Max(calories, expectedCalories);

_logger.LogDebug("Validação calórica:");
_logger.LogDebug("   Proteína: {P}g × 4 = {Cal}kcal", protein, protein * 4);
_logger.LogDebug("   Carbs:    {C}g × 4 = {Cal}kcal", carbs, carbs * 4);
_logger.LogDebug("   Gordura:  {F}g × 9 = {Cal}kcal", fat, fat * 9);
_logger.LogDebug("   Esperado: {Expected}kcal", expectedCalories);
_logger.LogDebug("   Real:     {Real}kcal", calories);
_logger.LogDebug("   Delta:    {Delta:P2}", delta);
```

**Solução:**
1. Ajustar tolerância:
   ```csharp
   // De: if (delta > 0.30) // 30%
   // Para: if (delta > 0.35) // 35% (mais permissivo)
   ```
2. Permitir pequenas diferenças sem erro:
   ```csharp
   if (delta > 0.05 && delta <= 0.30) // 5-30%
   {
       // Warning mas não erro
       _logger.LogWarning("Pequena inconsistência calórica: {Delta:P0}", delta);
   }
   else if (delta > 0.30)
   {
       // Erro crítico
       errors.Add($"Inconsistência calórica crítica: {delta:P0}");
   }
   ```

---

### Problema 6: Autocorreção Não Funciona

**Sintoma:**
```
[StructuredParser] ❌ Autocorreção falhou, usando fallback
```

**Causa:**
- Valores inferidos estão fora do range válido
- Autocorreção não consegue resolver todas as inconsistências

**Diagnóstico:**
```csharp
// Logar tentativa de autocorreção
var inferredCarbs = (calories - (protein * 4) - (fat * 9)) / 4;

_logger.LogDebug("Autocorreção:");
_logger.LogDebug("   Calorias:       {Cal}kcal", calories);
_logger.LogDebug("   Proteína:       {P}g ({Cal}kcal)", protein, protein * 4);
_logger.LogDebug("   Gordura:        {F}g ({Cal}kcal)", fat, fat * 9);
_logger.LogDebug("   Carbs inferido: {C}g", inferredCarbs);
_logger.LogDebug("   Carbs original: {C}g", carbs);
_logger.LogDebug("   Range válido:   0-100g");
```

**Solução:**
1. Ampliar range válido:
   ```csharp
   // De: if (inferredCarbs > 0 && inferredCarbs <= 100)
   // Para: if (inferredCarbs >= 0 && inferredCarbs <= 150) // Permitir concentrados
   ```
2. Corrigir múltiplos campos se necessário:
   ```csharp
   // Se inferir carbs falhar, tentar inferir gordura
   if (inferredCarbs < 0 || inferredCarbs > 100)
   {
       var inferredFat = (calories - (protein * 4) - (carbs * 4)) / 9;
       if (inferredFat >= 0 && inferredFat <= 100)
       {
           corrected.Fat = Math.Round(inferredFat, 1);
       }
   }
   ```

---

### Problema 7: Performance Lenta

**Sintoma:**
- Parser demora > 2 segundos para processar uma imagem

**Diagnóstico:**
```csharp
var sw = Stopwatch.StartNew();

// ETAPA 1
var result1 = ValidateNutritionTableStructure(textBlocks, rawText);
_logger.LogDebug("ETAPA 1: {Ms}ms", sw.ElapsedMilliseconds);
sw.Restart();

// ETAPA 2
var columns = DetectColumns(textBlocks);
_logger.LogDebug("ETAPA 2: {Ms}ms", sw.ElapsedMilliseconds);
// ... repetir para cada etapa
```

**Solução:**
1. Cachear resultados de regex:
   ```csharp
   private static readonly Regex NumericRegex = new(@"^\d+([.,]\d+)?$", RegexOptions.Compiled);
   
   private bool IsNumeric(string text)
   {
       return NumericRegex.IsMatch(text.Replace(",", ".").Replace(" ", ""));
   }
   ```
2. Limitar blocos processados:
   ```csharp
   // Só processar blocos relevantes (númericos e nutrientes)
   var relevantBlocks = textBlocks
       .Where(b => IsNumericOrUnit(b.Text) || IsNutrientName(b.Text))
       .ToList();
   ```
3. Early exit em validações:
   ```csharp
   if (!hasTitle && !hasEnergyValue)
       return false; // Não continuar se não tem indicadores básicos
   ```

---

## 📊 LOGS ÚTEIS PARA DEBUG

### Log Completo de Debugging
Adicionar no início do `ParseStructured`:

```csharp
if (_logger.IsEnabled(LogLevel.Debug))
{
    _logger.LogDebug("════════════════════════════════════════════");
    _logger.LogDebug("INÍCIO DEBUG - StructuredTableOcrParser");
    _logger.LogDebug("════════════════════════════════════════════");
    
    _logger.LogDebug("TextBlocks count: {Count}", textBlocks?.Count ?? 0);
    _logger.LogDebug("RawText length: {Length}", rawText?.Length ?? 0);
    
    if (textBlocks != null)
    {
        foreach (var block in textBlocks.Take(20)) // Primeiros 20 blocos
        {
            _logger.LogDebug("Block: '{Text}' | X={X:F1} Y={Y:F1} W={W:F1} H={H:F1} | Conf={Conf:P0}",
                block.Text,
                block.BoundingBox?.X ?? 0,
                block.BoundingBox?.Y ?? 0,
                block.BoundingBox?.Width ?? 0,
                block.BoundingBox?.Height ?? 0,
                block.Confidence);
        }
    }
    
    _logger.LogDebug("════════════════════════════════════════════");
}
```

### Verificar Integridade de Dados
```csharp
public void ValidateIntegrity(List<OcrTextBlock> textBlocks)
{
    var issues = new List<string>();
    
    if (textBlocks == null || textBlocks.Count == 0)
        issues.Add("TextBlocks nulo ou vazio");
    
    var blocksWithoutBoundingBox = textBlocks?.Count(b => b.BoundingBox == null) ?? 0;
    if (blocksWithoutBoundingBox > 0)
        issues.Add($"{blocksWithoutBoundingBox} blocos sem BoundingBox");
    
    var blocksWithInvalidCoords = textBlocks?
        .Count(b => b.BoundingBox != null && 
                   (b.BoundingBox.X < 0 || b.BoundingBox.Y < 0 || 
                    b.BoundingBox.Width <= 0 || b.BoundingBox.Height <= 0)) ?? 0;
    if (blocksWithInvalidCoords > 0)
        issues.Add($"{blocksWithInvalidCoords} blocos com coordenadas inválidas");
    
    if (issues.Any())
    {
        _logger.LogWarning("Problemas de integridade detectados:");
        foreach (var issue in issues)
        {
            _logger.LogWarning("   - {Issue}", issue);
        }
    }
    else
    {
        _logger.LogInformation("✅ Integridade dos dados OK");
    }
}
```

---

## 🧪 TESTES RECOMENDADOS

### Teste de Regressão
```csharp
[Fact]
public void ParseStructured_WithRealWorldExample_ShouldExtractCorrectValues()
{
    // ARRANGE
    var textBlocks = new List<OcrTextBlock>
    {
        new() { Text = "Informação Nutricional", BoundingBox = new() { X = 100, Y = 50 } },
        new() { Text = "Carboidratos", BoundingBox = new() { X = 50, Y = 200 } },
        new() { Text = "12", BoundingBox = new() { X = 220, Y = 200 } }, // 100ml
        new() { Text = "15", BoundingBox = new() { X = 270, Y = 200 } }, // 20g
        new() { Text = "5", BoundingBox = new() { X = 320, Y = 200 } },  // %VD
        new() { Text = "Proteínas", BoundingBox = new() { X = 50, Y = 230 } },
        new() { Text = "3.6", BoundingBox = new() { X = 220, Y = 230 } }, // 100ml
    };
    var rawText = string.Join("\n", textBlocks.Select(b => b.Text));
    
    // ACT
    var result = _parser.ParseStructured(textBlocks, rawText);
    
    // ASSERT
    Assert.True(result.Success);
    Assert.Equal(12.0, result.Carbs);
    Assert.Equal(3.6, result.Protein);
    Assert.Equal("ml", result.Unit);
}
```

---

## 📞 SUPORTE

Se nenhuma solução acima resolver o problema:

1. **Coletar logs completos** com `LogLevel.Debug` ativado
2. **Salvar imagem problemática** para reprodução
3. **Exportar TextBlocks como JSON** para análise offline:
   ```csharp
   var json = JsonSerializer.Serialize(textBlocks, new JsonSerializerOptions { WriteIndented = true });
   File.WriteAllText("debug_textblocks.json", json);
   ```
4. **Abrir issue** com logs + imagem + JSON anexados

---

**📚 Este guia cobre 95% dos problemas comuns. Para casos específicos, consulte os logs detalhados.**
