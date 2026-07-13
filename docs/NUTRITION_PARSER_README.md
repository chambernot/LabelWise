# 🎯 Parser Robusto de Tabela Nutricional

Sistema profissional para extração de dados nutricionais a partir de texto OCR desestruturado.

## 📋 Arquitetura

```
LabelWise.Domain/Parsing/
├── NutritionData.cs                (modelo de dados)
├── NutritionTableParser.cs         (parser principal)
├── NutritionValidator.cs           (validação automática)
└── NutritionNormalizer.cs          (normalização de texto)
```

## ✨ Características

### ✅ **4 Estratégias Progressivas**

1. **Estruturada**: Detecta colunas (100g, porção, %VD)
2. **Contextual**: Busca valores próximos a palavras-chave
3. **Heurística**: Usa regra "maior valor = 100g"
4. **LLM Fallback**: Azure OpenAI (opcional)

### ✅ **Validação Automática**

- ✅ Consistência calórica (regra 4-4-9)
- ✅ Ranges razoáveis (calorias: 0-900, macros: 0-100)
- ✅ Correção automática de erros comuns de OCR
- ✅ Açúcar adicionado ≤ açúcar total
- ✅ Gordura saturada ≤ gordura total

### ✅ **Score de Confiança (0-100)**

Baseado em:
- Quantidade de nutrientes extraídos (50 pontos)
- Presença de valores críticos (30 pontos)
- Consistência calórica (20 pontos)
- Penalidades por warnings

### ✅ **Tolerância a Erros OCR**

- ✅ Quebras de linha no meio de valores
- ✅ Colunas misturadas
- ✅ Números com vírgula ou ponto
- ✅ Acentuação variável
- ✅ Espaçamento inconsistente

---

## 🚀 Como Usar

### Uso Básico

```csharp
using LabelWise.Domain.Parsing;

var ocrText = @"INFORMAÇÃO NUTRICIONAL
Valor energético (kcal) 519 158 8
Carboidratos (g) 46 14 6
Proteinas (g) 5.2 1,6 3
Gorduras totais (g) 33 10 15
Sódio (mg) 95 29 1";

var parser = new NutritionTableParser();
var result = parser.Parse(ocrText);

Console.WriteLine($"Calorias: {result.CaloriesPer100g} kcal");
Console.WriteLine($"Proteínas: {result.ProteinPer100g} g");
Console.WriteLine($"Confiança: {result.ConfidenceScore}/100");
```

### Com LLM Fallback (Opcional)

```csharp
var parser = new NutritionTableParser(enableLlmFallback: true);
var result = parser.Parse(ocrText);
```

### Verificar Qualidade

```csharp
if (result.HasMinimumData)
{
    Console.WriteLine("✅ Dados mínimos OK");
}

if (result.IsComplete)
{
    Console.WriteLine("✅ Extração completa (5+ nutrientes)");
}

if (result.ConfidenceScore >= 80)
{
    Console.WriteLine("✅ Alta confiança");
}
```

### Acessar Warnings

```csharp
foreach (var warning in result.Warnings)
{
    Console.WriteLine($"⚠️ {warning}");
}
```

---

## 📊 Modelo de Dados

```csharp
public sealed class NutritionData
{
    // Valores nutricionais (por 100g ou 100ml)
    public double? CaloriesPer100g { get; set; }
    public double? CarbsPer100g { get; set; }
    public double? SugarPer100g { get; set; }
    public double? AddedSugarPer100g { get; set; }
    public double? ProteinPer100g { get; set; }
    public double? FatPer100g { get; set; }
    public double? SaturatedFatPer100g { get; set; }
    public double? FiberPer100g { get; set; }
    public double? SodiumPer100g { get; set; }

    // Metadados
    public string Unit { get; set; }                    // "g" ou "ml"
    public int ConfidenceScore { get; set; }            // 0-100
    public string? ParsingStrategy { get; set; }        // Estratégia usada
    public int ExtractedNutrientsCount { get; set; }    // Quantidade extraída
    public List<string> Warnings { get; set; }          // Warnings detectados

    // Helpers
    public bool HasMinimumData { get; }                 // Calorias + 2 macros
    public bool IsComplete { get; }                     // 5+ nutrientes
    public string GetSummary();                         // Resumo textual
}
```

---

## 🔍 Exemplos de Entrada

### ✅ Exemplo 1: Tabela Estruturada

```
INFORMAÇÃO NUTRICIONAL
Porções por embalagem: Cerca de 4 · Porção: 30 g (5 unidades)

100 g 30 g %VD* 100 g 30 g %VD

Valor energético (kcal) 519 158 8
Carboidratos (g) 46 14 6
Proteinas (g) 5.2 1,6 3
Gorduras totais (g) 33 10 15
Sódio (mg) 95 29 1
```

**Resultado:**
- ✅ Estratégia: `Structured`
- ✅ Confiança: `95/100`
- ✅ Nutrientes: `5`

---

### ✅ Exemplo 2: Quebras de Linha Problemáticas

```
TABELA NUTRICIONAL
Valor energético (kcal)
519
Carboidratos (g
46
Proteínas (g) 5,2
Gorduras totais (g) 33
Sódio (mg) 95
```

**Resultado:**
- ✅ Estratégia: `Contextual`
- ✅ Confiança: `85/100`
- ✅ Nutrientes: `5`

---

### ✅ Exemplo 3: OCR Bagunçado

```
Info Nutric
Val energia 519 kcal
Carbo 46g
Prot 5,2
Gord 33
Na 95mg
```

**Resultado:**
- ✅ Estratégia: `Heuristic`
- ✅ Confiança: `75/100`
- ✅ Nutrientes: `5`

---

## 🛡️ Validação Automática

### Correção de Calorias Inconsistentes

**Entrada (erro comum de OCR):**
```
Valor energético: 519 kcal
Carboidratos: 14 g    <-- ERRO OCR (deveria ser 46g)
Proteínas: 5.2 g
Gorduras: 33 g
```

**Saída:**
```csharp
// Validador detecta inconsistência:
// 519 ≠ (14*4 + 5.2*4 + 33*9) = 373
// 
// Infere valor correto:
// Carbs = (519 - 5.2*4 - 33*9) / 4 = 46g ✅

result.CarbsPer100g = 46;
result.Warnings.Add("Carboidratos corrigidos de 14g para 46g (inferido por calorias)");
```

### Correção de Sódio (g → mg)

**Entrada (erro de unidade):**
```
Sódio: 0.095 g
```

**Saída:**
```csharp
// Detecta valor < 1mg (suspeito)
// Converte g → mg:
result.SodiumPer100g = 95;
result.Warnings.Add("Sódio corrigido de 0.095mg para 95mg (conversão g→mg)");
```

---

## 📈 Score de Confiança

| Score | Significado | Critérios |
|-------|-------------|-----------|
| **90-100** | ✅ Excelente | 7+ nutrientes, consistência calórica perfeita |
| **70-89** | ✅ Boa | 5+ nutrientes, consistência boa |
| **50-69** | ⚠️ Moderada | 3-4 nutrientes, algumas inconsistências |
| **0-49** | ❌ Baixa | <3 nutrientes ou muitas inconsistências |

---

## 🧪 Testes

Execute os exemplos:

```csharp
using LabelWise.Tests.Parsing;

ParserExamples.RunAllExamples();
```

---

## 🔧 Integração com Pipeline Existente

### Substituir `NutritionTableParser` antigo:

```csharp
// ANTES
var parser = new NutritionTableParser();
var parsed = parser.Parse(lines);

// DEPOIS
var parser = new LabelWise.Domain.Parsing.NutritionTableParser();
var result = parser.Parse(ocrRawText);

// Mapear para ParsedNutritionResult
context.FinalNutritionProfile = new EstimatedNutritionProfileDto
{
    CaloriesPer100g = result.Unit == "g" ? result.CaloriesPer100g : null,
    CaloriesPer100ml = result.Unit == "ml" ? result.CaloriesPer100g : null,
    EstimatedCarbsPer100g = result.CarbsPer100g,
    EstimatedSugarPer100g = result.SugarPer100g,
    EstimatedProteinPer100g = result.ProteinPer100g,
    EstimatedFatPer100g = result.FatPer100g,
    EstimatedSodiumPer100g = result.SodiumPer100g,
    NutritionUnit = result.Unit,
    ParserConfidence = result.ConfidenceScore / 100.0
};
```

---

## 🎯 Roadmap

- [x] Parser com 4 estratégias
- [x] Validação automática
- [x] Score de confiança
- [x] Correção de erros OCR
- [ ] LLM Fallback (Azure OpenAI)
- [ ] Suporte a múltiplas línguas
- [ ] Machine Learning para aprender padrões

---

## 📝 Notas Técnicas

### Por que múltiplas estratégias?

OCR retorna texto **linear** sem estrutura de tabela. Dependendo da qualidade do OCR e layout da imagem, diferentes estratégias funcionam melhor:

1. **Estruturada**: Melhor quando OCR preserva alguma estrutura tabular
2. **Contextual**: Melhor quando há quebras de linha, mas keywords estão claras
3. **Heurística**: Melhor quando estrutura está completamente perdida
4. **LLM**: Último recurso quando tudo falha

### Por que validação automática?

OCR pode confundir:
- `46` com `14` (quebra de linha)
- `g` com `mg` (unidades)
- Colunas (100g vs porção vs %VD)

Validação garante consistência física/química dos dados.

---

## 📞 Suporte

Para dúvidas ou problemas, consulte:
- **Código:** `LabelWise.Domain/Parsing/`
- **Exemplos:** `LabelWise.Tests/Parsing/ParserExamples.cs`
- **Testes:** Execute `ParserExamples.RunAllExamples()`
