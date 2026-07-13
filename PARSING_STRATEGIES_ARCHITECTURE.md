# 📋 Arquitetura de Parsing Estratégico por Tipo de Captura

## 🎯 Objetivo

Refatoração do sistema de parsing de OCR para trabalhar com **estratégias específicas por tipo de captura**, eliminando a abordagem genérica que tentava extrair tudo de qualquer imagem.

---

## 🏗️ Arquitetura

### **Estratégia: Strategy Pattern**

Cada tipo de captura de imagem tem seu próprio parser especializado:

```
┌──────────────────────────────────────────────────────────────┐
│                   OCR Text (Raw)                             │
└──────────────────────┬───────────────────────────────────────┘
                       │
                       ▼
        ┌──────────────┴──────────────┐
        │   Capture Type Detection    │
        │   (Nutrition Table, etc.)   │
        └──────────────┬──────────────┘
                       │
         ┌─────────────┴─────────────┐
         │                           │
         ▼                           ▼
┌─────────────────┐         ┌─────────────────┐
│  Nutrition      │         │  Ingredients    │
│  Table Parser   │         │  Parser         │
└─────────────────┘         └─────────────────┘
         │                           │
         ▼                           ▼
┌─────────────────┐         ┌─────────────────┐
│  Allergen       │         │  Front Packaging│
│  Parser         │         │  Parser         │
└─────────────────┘         └─────────────────┘
```

---

## 📦 Componentes Criados

### **1️⃣ Interfaces de Parsing**

#### `INutritionTableParser`
**Foco:** Extrair valores nutricionais (calorias, carboidratos, açúcares, proteínas, gorduras, sódio, porção)

**Responsabilidades:**
- ✅ Extrair dados da tabela nutricional
- ❌ Ignorar tentativa de achar marca/produto
- ✅ Validar valores (suspeitos como 15000 kcal)

#### `IIngredientsParser`
**Foco:** Extrair ingredientes após "INGREDIENTES"

**Responsabilidades:**
- ✅ Encontrar seção "INGREDIENTES:"
- ✅ Limpar ruídos de OCR (`|`, `[`, `]`, `{`, `}`)
- ✅ Normalizar delimitadores (`,`, `;`, ` e `, ` ou `)
- ✅ Filtrar valores numéricos e keywords inválidas

#### `IAllergenParser`
**Foco:** Separar `containsAllergens` e `mayContainAllergens`

**Responsabilidades:**
- ✅ Reconhecer frases:
  - `"contém"` → ConfirmedAllergens
  - `"contém derivados de"` → ConfirmedAllergens
  - `"pode conter"` → MayContainAllergens
  - `"não contém"` → DoesNotContainAllergens
- ✅ Extrair frases completas
- ✅ Normalizar nomes de alérgenos (gluten → glúten)

#### `IFrontPackagingParser`
**Foco:** Tentar extrair nome do produto e marca

**Responsabilidades:**
- ✅ Extrair nome do produto (primeira linha válida)
- ✅ Extrair marca (segunda linha, se válida)
- ✅ Detectar sabor (`"sabor"`, `"flavor"`)
- ❌ Ignorar linhas que pareçam tabela nutricional
- ❌ Ignorar keywords inválidas (`INGREDIENTES`, `CONTÉM`, `CNPJ`, etc.)

---

## 📊 DTOs de Resultado

### `NutritionTableParseResult`
```csharp
public class NutritionTableParseResult
{
    public string? ServingSize { get; set; }
    public double? Calories { get; set; }
    public double? TotalCarbohydrate { get; set; }
    public double? Sugars { get; set; }
    public double? Protein { get; set; }
    public double? TotalFat { get; set; }
    public double? SaturatedFat { get; set; }
    public double? TransFat { get; set; }
    public double? DietaryFiber { get; set; }
    public double? Sodium { get; set; }
    public double? Cholesterol { get; set; }
    
    public ConfidenceLevel Confidence { get; set; }
    public List<string> ValidationWarnings { get; set; }
    public bool HasNutritionData { get; }
}
```

### `IngredientsParseResult`
```csharp
public class IngredientsParseResult
{
    public List<string> Ingredients { get; set; }
    public string? RawIngredientsSection { get; set; }
    
    public ConfidenceLevel Confidence { get; set; }
    public List<string> ValidationWarnings { get; set; }
    public bool HasIngredients { get; }
}
```

### `AllergenParseResult`
```csharp
public class AllergenParseResult
{
    public List<string> ConfirmedAllergens { get; set; }      // "contém"
    public List<string> MayContainAllergens { get; set; }     // "pode conter"
    public List<string> DoesNotContainAllergens { get; set; } // "não contém"
    public List<string> ExtractedPhrases { get; set; }
    
    public ConfidenceLevel Confidence { get; set; }
    public List<string> ValidationWarnings { get; set; }
    public bool HasAllergenInfo { get; }
}
```

### `FrontPackagingParseResult`
```csharp
public class FrontPackagingParseResult
{
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public string? SubBrand { get; set; }
    public string? Flavor { get; set; }
    
    public ConfidenceLevel Confidence { get; set; }
    public List<string> ValidationWarnings { get; set; }
    public bool IsProductNameValidated { get; set; }
    public bool IsBrandValidated { get; set; }
    public bool HasProductInfo { get; }
}
```

---

## ✅ Validações Implementadas

### **NutritionTableParser**
- ✅ Mínimo de 6 valores nutricionais para `High Confidence`
- ✅ Detecta valores suspeitos (calorias > 9000, sódio > 50000mg)
- ✅ Suporta diferentes formatos (`123kcal`, `123 kcal`, `123,5g`, `123.5 g`)

### **IngredientsParser**
- ✅ Mínimo de 3 ingredientes para `High Confidence`
- ✅ Filtra linhas apenas numéricas
- ✅ Filtra keywords inválidas (`INFORMAÇÃO NUTRICIONAL`, `%VD`)
- ✅ Remove caracteres especiais de OCR (`|`, `[`, `]`, `{`, `}`)
- ✅ Remove duplicatas

### **AllergenParser**
- ✅ Separa alérgenos confirmados, potenciais e negados
- ✅ Normaliza nomes de alérgenos (gluten → glúten)
- ✅ Extrai frases completas (até ponto ou fim do texto)
- ✅ Remove duplicatas

### **FrontPackagingParser**
- ✅ Filtra linhas de tabela nutricional
- ✅ Filtra linhas com >60% de números
- ✅ Filtra linhas com >33% de caracteres especiais
- ✅ Valida tamanho mínimo (2 caracteres) e máximo (100 caracteres)
- ✅ Deve conter pelo menos uma letra

---

## 🧪 Testes Unitários

### **Cobertura de Testes**

✅ **NutritionTableParserTests** (7 testes)
- Parsing completo de tabela nutricional
- Parsing parcial
- Texto vazio
- Sem dados nutricionais
- Valores suspeitos
- Diferentes formatos

✅ **IngredientsParserTests** (10 testes)
- Lista de ingredientes válida
- Diferentes delimitadores (`,`, `;`, `e`)
- Texto vazio
- Seção não encontrada
- Apenas 1 ingrediente
- Remoção de caracteres inválidos
- Filtro de valores numéricos
- Filtro de keywords de tabela nutricional
- Remoção de duplicatas

✅ **AllergenParserTests** (11 testes)
- Alérgenos confirmados
- Alérgenos potenciais
- Alérgenos negados
- Declarações mistas
- Derivados
- Texto vazio
- Normalização de nomes
- Extração de frases
- Remoção de duplicatas
- Múltiplos alérgenos na mesma frase

✅ **FrontPackagingParserTests** (12 testes)
- Nome e marca válidos
- Produto com sabor
- Ignora linhas de tabela nutricional
- Ignora keywords inválidas
- Texto vazio
- Sem linhas válidas
- Apenas nome de produto
- Filtra linhas com muitos números
- Filtra linhas com muitos caracteres especiais
- Nome muito curto
- Múltiplas linhas válidas
- Identificação de marca

---

## 📁 Estrutura de Arquivos

```
LabelWise.Application/
├── Parsing/
│   └── Strategies/
│       ├── INutritionTableParser.cs
│       ├── IIngredientsParser.cs
│       ├── IAllergenParser.cs
│       ├── IFrontPackagingParser.cs
│       ├── NutritionTableParser.cs
│       ├── IngredientsParser.cs
│       ├── AllergenParser.cs
│       ├── FrontPackagingParser.cs
│       ├── NutritionTableParseResult.cs
│       ├── IngredientsParseResult.cs
│       ├── AllergenParseResult.cs
│       └── FrontPackagingParseResult.cs

LabelWise.Application.Tests/
└── Parsing/
    └── Strategies/
        ├── NutritionTableParserTests.cs
        ├── IngredientsParserTests.cs
        ├── AllergenParserTests.cs
        └── FrontPackagingParserTests.cs
```

---

## 🚀 Como Usar

### **Exemplo 1: Parsing de Tabela Nutricional**
```csharp
var parser = new NutritionTableParser();
var result = parser.Parse(ocrText);

if (result.HasNutritionData && result.Confidence == ConfidenceLevel.High)
{
    Console.WriteLine($"Calorias: {result.Calories} kcal");
    Console.WriteLine($"Carboidratos: {result.TotalCarbohydrate}g");
    Console.WriteLine($"Proteínas: {result.Protein}g");
}
```

### **Exemplo 2: Parsing de Ingredientes**
```csharp
var parser = new IngredientsParser();
var result = parser.Parse(ocrText);

if (result.HasIngredients)
{
    foreach (var ingredient in result.Ingredients)
    {
        Console.WriteLine($"- {ingredient}");
    }
}
```

### **Exemplo 3: Parsing de Alérgenos**
```csharp
var parser = new AllergenParser();
var result = parser.Parse(ocrText);

Console.WriteLine("Contém:");
foreach (var allergen in result.ConfirmedAllergens)
{
    Console.WriteLine($"  - {allergen}");
}

Console.WriteLine("Pode conter:");
foreach (var allergen in result.MayContainAllergens)
{
    Console.WriteLine($"  - {allergen}");
}
```

### **Exemplo 4: Parsing de Embalagem Frontal**
```csharp
var parser = new FrontPackagingParser();
var result = parser.Parse(ocrText);

if (result.HasProductInfo)
{
    Console.WriteLine($"Produto: {result.ProductName}");
    Console.WriteLine($"Marca: {result.Brand}");
    Console.WriteLine($"Sabor: {result.Flavor}");
}
```

---

## 🎯 Benefícios da Arquitetura

### ✅ **Separação de Responsabilidades**
Cada parser tem uma única responsabilidade, facilitando manutenção e evolução.

### ✅ **Testabilidade**
Cada parser pode ser testado isoladamente com casos específicos.

### ✅ **Reutilização**
Parsers podem ser usados em diferentes contextos (pipeline, API direta, batch processing).

### ✅ **Extensibilidade**
Fácil adicionar novos parsers (ex: `IBarcodeParser`, `IExpirationDateParser`).

### ✅ **Validação Específica**
Cada parser tem suas próprias regras de validação adequadas ao tipo de dado.

### ✅ **Confiança Granular**
Confiança calculada especificamente para cada tipo de parsing.

---

## 🔄 Próximos Passos

1. **Integração com Pipeline**
   - Modificar `LabelReadingService` para usar os novos parsers
   - Mapear `CaptureType` para o parser correto

2. **Registro de Dependências**
   - Adicionar parsers no `ServiceCollectionExtensions`

3. **Testes de Integração**
   - Criar testes end-to-end do pipeline completo

4. **Documentação de API**
   - Swagger docs para endpoints que usam os parsers

5. **Métricas e Telemetria**
   - Adicionar logging para cada parser
   - Coletar métricas de confiança

---

## 📚 Referências

- **Design Patterns:** Strategy Pattern, Factory Pattern
- **SOLID Principles:** Single Responsibility, Open/Closed, Dependency Inversion
- **.NET Best Practices:** Async/await, nullable reference types, records

---

**✅ IMPLEMENTAÇÃO COMPLETA - Parsing Estratégico por Tipo de Captura**

Data: 2025-01-XX
Versão: 1.0
