# Candidate Suggestion Fallback - Documentação

## Visão Geral

O sistema de sugestão de candidatos é um **fallback inteligente** para quando a identificação primária de produtos falha ou tem baixa confiança. Em vez de inventar um nome de produto, o sistema retorna `ProductUnknown` acompanhado de uma lista de candidatos sugeridos (`topCandidates`).

## Arquitetura

```
┌─────────────────────────────────────────────────────────────────┐
│                 ProductIdentificationService                     │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────────────┐│
│  │   Barcode     │  │  OCR Frontal  │  │ Candidate Suggestion ││
│  │  (alta conf.) │─▶│  (média conf.)│─▶│     (fallback)       ││
│  └───────────────┘  └───────────────┘  └───────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
                                                     │
                                                     ▼
┌─────────────────────────────────────────────────────────────────┐
│               CandidateSuggestionService                         │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────────────┐│
│  │    Texto      │  │ Ingredientes  │  │    Categoria         ││
│  │  Similaridade │  │    Match      │  │     Match            ││
│  └───────────────┘  └───────────────┘  └───────────────────────┘│
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────────────┐│
│  │   Histórico   │  │   Visual      │  │    Combined          ││
│  │   Usuário     │  │ Similarity*   │  │    Ranking           ││
│  └───────────────┘  └───────────────┘  └───────────────────────┘│
│                                       * Preparado para futuro   │
└─────────────────────────────────────────────────────────────────┘
```

## Componentes

### 1. Interface `ICandidateSuggestionService`
**Localização**: `LabelWise.Application/Interfaces/ICandidateSuggestionService.cs`

```csharp
public interface ICandidateSuggestionService
{
    Task<CandidateSuggestionResult> SuggestCandidatesAsync(CandidateSuggestionRequest request);
    Task<List<SuggestedCandidate>> SearchByTextAsync(string text, int maxResults = 5);
    Task<List<SuggestedCandidate>> SearchByIngredientsAsync(List<string> ingredients, int maxResults = 5);
    Task<List<SuggestedCandidate>> SearchByCategoryAsync(string category, int maxResults = 5);
    Task<List<SuggestedCandidate>> SearchByVisualSimilarityAsync(double[] visualFeatures, int maxResults = 5);
    List<SuggestedCandidate> CombineAndRankCandidates(IEnumerable<SuggestedCandidate> candidates, int maxResults = 5);
}
```

### 2. DTOs

#### `CandidateSuggestionRequest`
**Entrada** para o serviço de sugestão:

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `ExtractedText` | `string?` | Texto extraído do OCR frontal |
| `PartialIngredients` | `List<string>` | Ingredientes parciais identificados |
| `Allergens` | `List<string>` | Alergênicos identificados |
| `InferredCategory` | `string?` | Categoria inferida do produto |
| `MaxCandidates` | `int` | Número máximo de candidatos (default: 5) |
| `MinConfidence` | `double` | Confiança mínima (default: 0.30) |
| `ImageData` | `byte[]?` | Dados de imagem (para futura similaridade visual) |
| `VisualFeatures` | `double[]?` | Features visuais (para futura similaridade visual) |

#### `CandidateSuggestionResult`
**Saída** do serviço:

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `TopCandidates` | `List<SuggestedCandidate>` | Lista ordenada de candidatos |
| `IsProductUnknown` | `bool` | Se o produto é desconhecido |
| `FallbackReason` | `string?` | Razão do fallback |
| `StrategiesUsed` | `List<string>` | Estratégias utilizadas |
| `ProcessingTimeSeconds` | `double` | Tempo de processamento |

#### `SuggestedCandidate`
Representa um candidato sugerido:

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `CandidateName` | `string` | Nome do produto candidato |
| `CandidateBrand` | `string?` | Marca do candidato |
| `Category` | `string?` | Categoria do candidato |
| `CandidateConfidence` | `double` | Confiança (0.0 a 1.0) |
| `MatchStrategy` | `CandidateMatchStrategy` | Estratégia que gerou o match |
| `TextSimilarityScore` | `double?` | Score de similaridade textual |
| `IngredientSimilarityScore` | `double?` | Score de similaridade de ingredientes |
| `VisualSimilarityScore` | `double?` | Score de similaridade visual (futuro) |

### 3. Helpers

#### `TextSimilarityCalculator`
Calcula similaridade entre strings usando:
- **Levenshtein Distance**: Para similaridade de caracteres
- **Token Similarity (Jaccard)**: Para similaridade de palavras
- **Combined Similarity**: Combinação ponderada

```csharp
// Exemplos de uso
var similarity = TextSimilarityCalculator.CalculateSimilarity("Coca-Cola", "coca cola");
// Resultado: ~0.85

var tokenSim = TextSimilarityCalculator.CalculateTokenSimilarity("refrigerante cola", "cola refrigerante");
// Resultado: 1.0 (mesmas palavras, ordem diferente)

var (bestMatch, score) = TextSimilarityCalculator.FindBestMatch("Guaraná", products);
// Encontra melhor correspondência na lista
```

#### `CategoryInferenceHelper`
Infere categorias baseadas em texto e ingredientes:

```csharp
var category = CategoryInferenceHelper.InferCategory("Refrigerante de cola com cafeína");
// Resultado: "Bebida"

var probabilities = CategoryInferenceHelper.GetCategoryProbabilities(text, ingredients);
// Retorna lista de categorias com probabilidades
```

## Estratégias de Matching

### 1. TextSimilarity
- Compara texto extraído com nomes de produtos conhecidos
- Usa Levenshtein + Token similarity combinados
- Peso: **máximo** (score direto)

### 2. IngredientMatch
- Compara ingredientes identificados com ingredientes de produtos conhecidos
- Usa `CalculateListSimilarity`
- Peso: **0.85** (ligeiramente menor que texto)

### 3. CategoryMatch
- Filtra produtos pela categoria inferida
- Confiança base: **0.40**
- Usado para aumentar cobertura

### 4. UserHistory
- Busca em produtos validados pelo usuário
- Usa o `IValidatedProductRepository`
- Peso: **0.90** (desconto pequeno)

### 5. VisualSimilarity (Futuro)
- **Arquitetura preparada** para embeddings de imagem
- Interface já aceita `visualFeatures: double[]`
- Implementação retorna lista vazia por enquanto

## Regra Principal

```
SE confiança_identificação < 0.60:
    RETORNAR ProductUnknown + topCandidates
SENÃO:
    RETORNAR resultado_identificação
FIM
```

## Fluxo de Integração

1. `ProductIdentificationService.IdentifyProductAsync` é chamado
2. Tenta identificar por **Barcode** (se fornecido)
3. Tenta identificar por **OCR Frontal** (se CaptureType = FrontPackaging)
4. Se confiança < 0.60, chama `CandidateSuggestionService.SuggestCandidatesAsync`
5. Retorna `ProductIdentificationResult` com `TopCandidates` preenchido

## Exemplo de Resposta

```json
{
  "success": false,
  "method": "Composite",
  "matchSource": "Unknown",
  "confidence": 0.0,
  "matchConfidence": 0.0,
  "isReliableMatch": false,
  "errorMessage": "Encontramos 3 produto(s) similares. Por favor, confirme ou selecione o correto.",
  "topCandidates": [
    {
      "productName": "Coca-Cola Original",
      "brand": "Coca-Cola",
      "category": "Bebida",
      "confidenceScore": 0.78,
      "matchSource": "FrontOcr",
      "matchReason": "Similaridade textual: 78%"
    },
    {
      "productName": "Coca-Cola Zero",
      "brand": "Coca-Cola",
      "category": "Bebida",
      "confidenceScore": 0.65,
      "matchSource": "FrontOcr",
      "matchReason": "Similaridade textual: 65%"
    }
  ],
  "details": [
    "Produto não identificado com confiança suficiente. Candidatos sugeridos disponíveis.",
    "Encontrados 3 candidato(s) sugerido(s)",
    "",
    "Próximos passos:",
    "- Selecione um dos produtos sugeridos",
    "- Ou capture o código de barras para identificação precisa"
  ],
  "metadata": {
    "HasCandidates": "True",
    "CandidateCount": "3",
    "StrategiesUsed": "TextSimilarity,CategoryMatch"
  }
}
```

## Extensões Futuras

### Similaridade Visual
A arquitetura está preparada para receber embeddings de imagem:

1. Extrair features visuais com CNN (ex: ResNet, EfficientNet)
2. Armazenar embeddings na base de produtos
3. Implementar busca por cosine similarity (ou usar FAISS)
4. Ativar `SearchByVisualSimilarityAsync`

### Base de Dados Externa
- Integrar com Open Food Facts
- Integrar com bases de produtos brasileiros
- Cache de produtos identificados

### Machine Learning
- Ranking de candidatos com modelo treinado
- Personalização por usuário
- Aprendizado contínuo com feedback

## Dependências

```xml
<!-- Nenhuma dependência adicional necessária -->
<!-- Usa apenas System.Text (NormalizationForm) e coleções padrão -->
```

## Registro de Serviços

```csharp
// Em ServiceCollectionExtensions.cs (Infrastructure)
services.AddScoped<ICandidateSuggestionService, CandidateSuggestionService>();
services.AddScoped<IProductIdentificationService, ProductIdentificationService>();
```

## Testes

Veja `LabelWise.Application.Tests/ProductIdentification/CandidateSuggestionTests.cs` para:
- Testes de `TextSimilarityCalculator`
- Testes de `CategoryInferenceHelper`
- Testes de `CandidateSuggestionResult`
