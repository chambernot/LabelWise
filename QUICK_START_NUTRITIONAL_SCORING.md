# 🚀 QUICK START - Motor de Score Nutricional

## ⚡ Início Rápido em 5 Minutos

### 1️⃣ Compilar o Projeto

```powershell
cd C:\Users\chamb\source\repos\LabelWise
dotnet build
```

**Esperado:** ✅ Build succeeded

---

### 2️⃣ Testar Manualmente (Console)

Criar arquivo `TestScoring.cs`:

```csharp
using System;
using System.Collections.Generic;
using LabelWise.Application.Scoring;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;

class Program
{
    static void Main()
    {
        var engine = new NutritionalScoringEngine();
        
        // Teste 1: Produto Ultraprocessado
        Console.WriteLine("=== Biscoito Recheado ===");
        var badProduct = CreateBadProduct();
        var badScore = engine.CalculateGeneralScore(badProduct.nutrition, badProduct.ingredients);
        Console.WriteLine($"Score: {badScore:F1}/100");
        Console.WriteLine($"Classificação: {engine.DetermineClassification(badScore)}");
        Console.WriteLine($"Esperado: < 40 (Avoid)");
        Console.WriteLine($"Resultado: {(badScore < 40 ? "✅ PASSOU" : "❌ FALHOU")}\n");
        
        // Teste 2: Produto Saudável
        Console.WriteLine("=== Iogurte Natural ===");
        var goodProduct = CreateGoodProduct();
        var goodScore = engine.CalculateGeneralScore(goodProduct.nutrition, goodProduct.ingredients);
        Console.WriteLine($"Score: {goodScore:F1}/100");
        Console.WriteLine($"Classificação: {engine.DetermineClassification(goodScore)}");
        Console.WriteLine($"Esperado: > 70 (Good ou Excellent)");
        Console.WriteLine($"Resultado: {(goodScore >= 70 ? "✅ PASSOU" : "❌ FALHOU")}\n");
        
        Console.WriteLine("Pressione qualquer tecla para sair...");
        Console.ReadKey();
    }
    
    static (NutritionalInfo nutrition, List<ProductIngredient> ingredients) CreateBadProduct()
    {
        var nutrition = new NutritionalInfo(Guid.NewGuid());
        nutrition.UpdateMacros(
            calories: 480m,
            totalFat: 22m,
            satFat: 9m,
            transFat: 0.5m,
            sodium: 420m,
            carbs: 66m,
            fiber: 1m,
            sugars: 28m,
            protein: 4m
        );
        
        var ingredients = new List<ProductIngredient>();
        for (int i = 0; i < 22; i++)
        {
            ingredients.Add(new ProductIngredient(Guid.NewGuid(), $"Ingrediente {i}", i));
        }
        ingredients[2] = new ProductIngredient(Guid.NewGuid(), "Gordura vegetal hidrogenada", 2);
        ingredients[5] = new ProductIngredient(Guid.NewGuid(), "Aromatizante", 5);
        ingredients[6] = new ProductIngredient(Guid.NewGuid(), "Corante", 6);
        
        return (nutrition, ingredients);
    }
    
    static (NutritionalInfo nutrition, List<ProductIngredient> ingredients) CreateGoodProduct()
    {
        var nutrition = new NutritionalInfo(Guid.NewGuid());
        nutrition.UpdateMacros(
            calories: 60m,
            totalFat: 3m,
            satFat: 2m,
            transFat: 0m,
            sodium: 50m,
            carbs: 4.5m,
            fiber: 0m,
            sugars: 4m,
            protein: 6m
        );
        
        var ingredients = new List<ProductIngredient>
        {
            new ProductIngredient(Guid.NewGuid(), "Leite integral", 0),
            new ProductIngredient(Guid.NewGuid(), "Fermento lácteo", 1)
        };
        
        return (nutrition, ingredients);
    }
}
```

---

### 3️⃣ Testar via API

#### Iniciar a API

```powershell
.\run-api.ps1
```

#### Fazer Request

```powershell
# PowerShell
$userId = "00000000-0000-0000-0000-000000000001" # Use um ID válido do banco

$headers = @{
    "Content-Type" = "multipart/form-data"
}

# Upload de imagem
$imagePath = "C:\path\to\product-image.jpg"
$form = @{
    file = Get-Item -Path $imagePath
    userId = $userId
}

Invoke-RestMethod -Uri "http://localhost:5000/api/pipeline/analyze" `
    -Method POST `
    -Form $form
```

#### Verificar Response

```json
{
  "productName": "Biscoito Recheado XYZ",
  "generalScore": 0.165,        // 16.5/100 convertido para 0-1
  "personalizedScore": 0.165,
  "classification": "Avoid",
  "shortSummary": "Não recomendado (17/100). Evitar este produto.",
  "alerts": [
    "🚨 CONTÉM GORDURA TRANS - Evite este produto!",
    "🚨 Contém gordura hidrogenada - associada a riscos cardiovasculares",
    "🚨 Teor de açúcar muito elevado: 28g por porção",
    "🚨 Produto altamente processado: 5 tipos de aditivos químicos",
    "🚨 PRODUTO ULTRAPROCESSADO (Grau 4 - NOVA) - Evitar consumo regular"
  ],
  "recommendations": [
    "Prefira alimentos in natura ou minimamente processados",
    "Consulte as informações nutricionais e lista de ingredientes",
    "Ultraprocessados podem contribuir para obesidade, diabetes e doenças cardiovasculares"
  ]
}
```

---

### 4️⃣ Usar no Código

#### Exemplo Básico

```csharp
using LabelWise.Application.Scoring;

// Instanciar o motor
var scoringEngine = new NutritionalScoringEngine();

// Calcular score geral
double generalScore = scoringEngine.CalculateGeneralScore(nutrition, ingredients);
// Resultado: 0-100

// Calcular score personalizado
double personalizedScore = scoringEngine.CalculatePersonalizedScore(
    nutrition, 
    ingredients, 
    allergens, 
    userProfile
);
// Resultado: 0-100 (com ajustes)

// Determinar classificação
string classification = scoringEngine.DetermineClassification(
    Math.Min(generalScore, personalizedScore)
);
// Resultado: "Excellent", "Good", "Attention", "Avoid"
```

#### Exemplo com Breakdown (Debug)

```csharp
#if DEBUG
string breakdown = scoringEngine.GenerateScoreBreakdown(nutrition, ingredients, userProfile);
Console.WriteLine(breakdown);
#endif
```

**Saída:**
```
Açúcar: 5.0/100 (peso 25%)
Gordura Ruim: 0.0/100 (peso 20%)
Fibra: 25.0/100 (peso 15%)
Proteína: 40.0/100 (peso 10%)
Sódio: 55.0/100 (peso 10%)
Ultraprocessamento: 10.0/100 (peso 10%)
Aditivos: 10.0/100 (peso 10%)
```

#### Exemplo Completo no Pipeline

```csharp
using LabelWise.Application.Rules;
using LabelWise.Application.Scoring;

// 1. NutrientScoringRule usa o motor automaticamente
var scoringRule = new NutrientScoringRule();
var result = new ProductAnalysisResultDto();

// 2. Aplica a regra
scoringRule.Evaluate(product, nutrition, ingredients, allergens, userProfile, result);

// 3. Resultado contém scores em escala 0-1
Console.WriteLine($"Score Geral: {result.GeneralScore:P0}");        // Ex: 17%
Console.WriteLine($"Score Personalizado: {result.PersonalizedScore:P0}"); // Ex: 3%
Console.WriteLine($"Classificação: {result.Classification}");       // Ex: Avoid
```

---

### 5️⃣ Entender os Scores

#### Tabela de Referência

| Score (0-100) | Score (0-1) | Classificação | Ação |
|---------------|-------------|--------------|------|
| 80-100 | 0.80-1.00 | **Excellent** 🟢 | Consumo regular |
| 60-79 | 0.60-0.79 | **Good** 🟡 | Consumo regular com moderação |
| 40-59 | 0.40-0.59 | **Attention** 🟠 | Consumo esporádico |
| 0-39 | 0.00-0.39 | **Avoid** 🔴 | Evitar |

#### Exemplos Práticos

```
Iogurte Natural:
- Açúcar: 4g → 80 pts
- Sem trans → 80 pts
- Proteína: 6g → 55 pts
- Score: 74.5/100 → Good ✅

Biscoito Recheado:
- Açúcar: 28g → 5 pts
- Trans: 0.5g → 0 pts
- 22 ingredientes → 10 pts
- Score: 16.5/100 → Avoid ✅
```

---

## 🔧 Troubleshooting

### Problema 1: Score muito alto para produto ruim

**Diagnóstico:**
```csharp
var breakdown = engine.GenerateScoreBreakdown(nutrition, ingredients);
Console.WriteLine(breakdown);
```

**Soluções:**
- Verificar dados nutricionais (valores corretos?)
- Verificar lista de ingredientes (completa?)
- Ajustar limiares em `NutritionalScoringEngine.cs`

### Problema 2: Compilação falha

**Erro comum:**
```
CS1501: Nenhuma sobrecarga para o método...
```

**Solução:**
```powershell
# Limpar e recompilar
dotnet clean
dotnet build
```

### Problema 3: Score personalizado igual ao geral

**Causa:** Perfil do usuário não tem restrições/objetivos específicos

**Verificar:**
```csharp
Console.WriteLine($"Perfil: {userProfile.Goal}");
Console.WriteLine($"Diabetes: {userProfile.Diabetes}");
Console.WriteLine($"Lactose: {userProfile.LactoseIntolerance}");
```

---

## 📊 Validar Resultados

### Checklist de Validação

- [ ] Produto com gordura trans recebe score < 20
- [ ] Produto com 22 ingredientes recebe score < 40
- [ ] Produto com açúcar > 20g recebe score < 30
- [ ] Iogurte natural recebe score > 70
- [ ] Diabético com açúcar alto recebe penalização (-25 pts)
- [ ] Intolerante à lactose com leite recebe score = 0

### Executar Validações Automáticas

```csharp
var validator = new ScoringValidationExamples();
validator.RunAllValidations();
```

**Esperado:** Todos os testes passam ✅

---

## 📚 Recursos Adicionais

### Documentação Completa
- `NUTRITIONAL_SCORING_ENGINE_DOCUMENTATION.md`

### Exemplos de Validação
- `SCORING_VALIDATION_EXAMPLES.cs`

### Resumo da Implementação
- `IMPLEMENTATION_SUMMARY_NUTRITIONAL_SCORING.md`

---

## 🎯 Casos de Uso Comuns

### 1. Adicionar Nova Categoria de Score

```csharp
// Em NutritionalScoringEngine.cs

private const double WEIGHT_VITAMINS = 5.0; // Novo peso

private double CalculateVitaminScore(NutritionalInfo nutrition)
{
    // Implementar lógica
    return 100.0;
}

// Adicionar ao cálculo geral
totalScore += CalculateVitaminScore(nutrition) * WEIGHT_VITAMINS / 100.0;
```

### 2. Ajustar Limiar de Açúcar

```csharp
// Em CalculateSugarScore()

if (sugar <= 2.0) return 100.0;  // Mais rigoroso (era 1.0)
if (sugar <= 5.0) return 70.0;   // Reduzir score (era 80)
```

### 3. Adicionar Novo Perfil

```csharp
// Em AdjustForNewProfile()

private double AdjustForLowCarb(double baseScore, NutritionalInfo nutrition, UserProfile profile)
{
    if (profile.Goal != GoalType.LowCarb)
        return baseScore;
    
    double penalty = 0.0;
    
    if (nutrition.TotalCarbohydratesGrams.HasValue)
    {
        var carbs = (double)nutrition.TotalCarbohydratesGrams.Value;
        if (carbs >= 20.0) penalty += 30.0;
        else if (carbs >= 15.0) penalty += 20.0;
    }
    
    return baseScore - penalty;
}

// Chamar no CalculatePersonalizedScore
adjustedScore = AdjustForLowCarb(adjustedScore, nutrition, profile);
```

---

## ✅ Checklist Final

Antes de considerar completo:

- [ ] ✅ Compilação bem-sucedida
- [ ] ✅ Produto ultraprocessado recebe score < 40
- [ ] ✅ Produto saudável recebe score > 70
- [ ] ✅ Alertas críticos aparecem com 🚨
- [ ] ✅ Classificação em "Excellent", "Good", "Attention", "Avoid"
- [ ] ✅ Personalização funciona (diabético, lactose, etc.)
- [ ] ✅ API retorna scores corretos
- [ ] ✅ Breakdown de debug funciona

---

## 🎉 Pronto!

Você agora tem um motor de score nutricional completo, baseado em evidências científicas e transparente.

**Score = Saúde Baseada em Evidências** 🎯

---

**Dúvidas?** Consulte a documentação completa em `NUTRITIONAL_SCORING_ENGINE_DOCUMENTATION.md`
