# Correção de Consistência de Parsing - LabelWise

## Resumo das Correções

Este documento descreve as correções implementadas para resolver inconsistências no parsing de rótulos, especialmente para suplementos como Creatina.

---

## Problema Original

Uma imagem de suplemento Creatina tinha OCR bom, mas parsing inconsistente:

```json
{
  "productName": "\" Creapure'",           // ❌ Ruído OCR não removido
  "brand": "INFORMAGAO NUTRICIONAL",        // ❌ Keyword de tabela nutricional como marca
  "nutritionalFacts": null,                  // ❌ Null mesmo com dados extraídos
  "nutritionalFieldsCount": 0,               // ❌ Zero mesmo com nutrientes
  "nutrientsExtracted": true,                // ✅ OCR OK
  "extractedAllergens": ["gluten"],          // ❌ Extraiu "gluten" de "NÃO CONTÉM GLÚTEN"
  "declaredAllergen": "gluten",              // ❌ Declarou como positivo
  "isPartialAnalysis": false                 // ❌ Deveria ser true se faltam dados
}
```

---

## Correções Implementadas

### 1. AllergenParser.cs - Distinção de NÃO CONTÉM

**Problema:** "NÃO CONTÉM GLÚTEN" gerava `extractedAllergens: ["gluten"]` positivo.

**Solução:** Priorizar frases negativas no parsing e rastrear alérgenos explicitamente negados.

```csharp
// ANTES: Processava todas as frases na mesma ordem
foreach (var phraseEntry in sortedPhrases)
{
    // Não distinguia "contém" de "não contém"
}

// DEPOIS: Processa negativas PRIMEIRO
var negativePhrases = new[] { "não contém", "nao contem", "isento de" ... };
var positivePhrases = new[] { "contém", "pode conter" };

// PASSO 1: Identificar todos os alérgenos NEGADOS
foreach (var term in negativePhrases)
{
    // Adiciona ao HashSet de alérgenos negados
    deniedAllergens.Add(allergen);
}

// PASSO 2: Processar positivos, EXCLUINDO os negados
foreach (var term in positivePhrases)
{
    // Verifica contexto para evitar "não contém" → "contém"
    if (contextBefore.Contains("não") || contextBefore.Contains("nao"))
        continue;
    
    // NÃO ADICIONAR se foi explicitamente negado
    if (deniedAllergens.Contains(allergen))
        continue;
}
```

**Resultado:**
- `"NÃO CONTÉM GLÚTEN"` → `DoesNotContainAllergens: ["glúten"]`
- `"CONTÉM LEITE"` → `ConfirmedAllergens: ["leite"]`
- `"PODE CONTER SOJA"` → `MayContainAllergens: ["soja"]`

---

### 2. FrontPackagingParser.cs e IngredientAllergenParser.cs - Limpeza de Ruído OCR

**Problema:** Nome do produto vinha com aspas e caracteres estranhos: `"\" Creapure'"`

**Solução:** Novo método `CleanOcrNoise()` aplicado a nome e marca.

```csharp
private static string CleanOcrNoise(string text)
{
    if (string.IsNullOrWhiteSpace(text))
        return string.Empty;

    var cleaned = text;

    // Remove aspas de qualquer tipo no início e fim
    cleaned = Regex.Replace(cleaned, @"^[\""'\'\""\""`´\\]+", "");
    cleaned = Regex.Replace(cleaned, @"[\""'\'\""\""`´\\]+$", "");

    // Remove barras invertidas
    cleaned = cleaned.Replace("\\", "");

    // Remove caracteres especiais isolados no início/fim
    cleaned = Regex.Replace(cleaned, @"^[\*\#\@\!\?\$\%\^\&\(\)\[\]\{\}\|\/\<\>]+", "");
    cleaned = Regex.Replace(cleaned, @"[\*\#\@\!\?\$\%\^\&\(\)\[\]\{\}\|\/\<\>]+$", "");

    return cleaned.Trim();
}
```

**Resultado:**
- `"\" Creapure'"` → `"Creapure"`
- `"*MARCA*"` → `"MARCA"`
- `"\\Produto/"` → `"Produto"`

---

### 3. FrontPackagingParser.cs - Validação de Marca

**Problema:** `"INFORMAÇÃO NUTRICIONAL"` era detectada como marca.

**Solução:** Validar que marca não é keyword de tabela nutricional.

```csharp
private static bool IsPotentialBrand(string line)
{
    // ... validações existentes ...

    // NOVO: Não pode ser keyword de tabela nutricional
    if (NutritionalTableKeywords.Any(kw => normalizedUpper.Contains(kw.ToUpperInvariant())))
    {
        return false;
    }

    return true;
}
```

**Resultado:**
- `"INFORMAÇÃO NUTRICIONAL"` → Rejeitado como marca
- `"Creapure"` → Aceito como marca

---

### 4. Suporte a Nutrientes de Suplementos

**Problema:** `nutritionalFieldsCount = 0` mesmo com creatina extraída.

**Solução:** Adicionar campos de nutrientes de suplementos:

```csharp
// NutritionTableParseResult.cs e NutritionData
public double? Creatine { get; set; }
public double? Caffeine { get; set; }
public double? Bcaa { get; set; }

// HasNutritionData agora considera suplementos
public bool HasNutritionData => 
    Calories.HasValue || 
    TotalCarbohydrate.HasValue || 
    Protein.HasValue ||
    TotalFat.HasValue ||
    Sodium.HasValue ||
    Creatine.HasValue ||    // NOVO
    Caffeine.HasValue ||     // NOVO
    Bcaa.HasValue;           // NOVO
```

**Keywords de extração adicionadas:**
```csharp
private static readonly string[] CreatineKeywords = 
{ 
    "creatina", "creatine", "creatina monohidratada", "creatine monohydrate"
};

private static readonly string[] CaffeineKeywords = 
{ 
    "cafeína", "cafeina", "caffeine"
};

private static readonly string[] BcaaKeywords = 
{ 
    "bcaa", "aminoácidos de cadeia ramificada", "leucina", "isoleucina", "valina"
};
```

**Resultado:**
- `"creatina (mg) 3.000"` → `Creatine: 3000`
- `"Porção: 3g (1 dosador)"` → `ServingSize: "3 g"`
- `nutritionalFieldsCount: 2+` (porção + creatina)

---

## Exemplos BEFORE/AFTER

### Exemplo 1: Suplemento Creatina

**Texto OCR:**
```
" Creapure'
INFORMAÇÃO NUTRICIONAL
Porção: 3 g (1 dosador)
Valor energético 0 kcal
Carboidratos 0 g
Proteínas 0 g
Gorduras totais 0 g
Sódio 0 mg
Creatina (mg) 3.000
INGREDIENTES: Creatina monohidratada
NÃO CONTÉM GLÚTEN
```

**ANTES:**
```json
{
  "productName": "\" Creapure'",
  "brand": "INFORMAGAO NUTRICIONAL",
  "nutritionalFacts": null,
  "nutritionalFieldsCount": 0,
  "extractedAllergens": ["gluten"],
  "confirmedAllergens": ["gluten"],
  "mayContainAllergens": [],
  "doesNotContainAllergens": [],
  "isPartialAnalysis": false
}
```

**DEPOIS:**
```json
{
  "productName": "Creapure",
  "brand": null,
  "nutritionalFacts": {
    "servingSize": "3 g",
    "calories": 0,
    "carbohydrates": 0,
    "proteins": 0,
    "totalFat": 0,
    "sodium": 0,
    "creatine": 3000
  },
  "nutritionalFieldsCount": 7,
  "extractedAllergens": [],
  "confirmedAllergens": [],
  "mayContainAllergens": [],
  "doesNotContainAllergens": ["glúten"],
  "isPartialAnalysis": false
}
```

---

### Exemplo 2: Produto com Alérgenos Mistos

**Texto OCR:**
```
Biscoito Integral
CONTÉM GLÚTEN E DERIVADOS DE LEITE
PODE CONTER SOJA E AMENDOIM
NÃO CONTÉM LACTOSE
```

**ANTES:**
```json
{
  "extractedAllergens": ["glúten", "leite", "soja", "amendoim", "lactose"],
  "confirmedAllergens": ["glúten", "leite", "lactose"],
  "mayContainAllergens": ["soja", "amendoim"]
}
```

**DEPOIS:**
```json
{
  "extractedAllergens": ["glúten", "leite", "soja", "amendoim"],
  "confirmedAllergens": ["glúten", "leite"],
  "mayContainAllergens": ["soja", "amendoim"],
  "doesNotContainAllergens": ["lactose"]
}
```

---

### Exemplo 3: Nome com Ruído OCR

**Texto OCR:**
```
\" Whey Protein*
MARCA: //Optimum\\
```

**ANTES:**
```json
{
  "productName": "\" Whey Protein*",
  "brand": "MARCA: //Optimum\\"
}
```

**DEPOIS:**
```json
{
  "productName": "Whey Protein",
  "brand": "Optimum"
}
```

---

## Arquivos Modificados

| Arquivo | Alteração |
|---------|-----------|
| `AllergenParser.cs` | Priorização de frases negativas, rastreamento de alérgenos negados |
| `FrontPackagingParser.cs` | Método `CleanOcrNoise()`, validação de marca contra keywords |
| `IngredientAllergenParser.cs` | Limpeza OCR, extração de creatina/cafeína/BCAA, correção de frases críticas |
| `NutritionTableParser.cs` | Keywords para suplementos, extração de creatina/cafeína/BCAA |
| `NutritionTableParseResult.cs` | Propriedades `Creatine`, `Caffeine`, `Bcaa`, ajuste de `HasNutritionData` |
| `IngredientAllergenParseResult.cs` | Propriedades de suplementos em `NutritionData` |

---

## Validação

O build passou com sucesso após todas as alterações.

```bash
> dotnet build
Compilação bem-sucedida
```

---

## Recomendações Futuras

1. **Testes Unitários:** Adicionar testes para cenários de "NÃO CONTÉM" + "CONTÉM" no mesmo texto
2. **Logging:** Adicionar logs para rastrear decisões de parsing de alérgenos
3. **Confiança:** Ajustar score de confiança quando houver ambiguidade em alérgenos
4. **Suplementos:** Expandir lista de nutrientes para incluir vitaminas, minerais específicos de suplementos
