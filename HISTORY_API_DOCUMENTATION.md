# Histórico de Análises - LabelWise

## Endpoints Implementados

### 1. Listar Histórico de Análises
**GET** `/api/history`

Lista todas as análises realizadas pelo usuário autenticado, ordenadas por data decrescente.

**Headers:**
```
Authorization: Bearer {token}
```

**Resposta de Sucesso (200 OK):**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "productName": "Iogurte Natural",
    "brand": "Danone",
    "analyzedAt": "2024-01-15T10:30:00Z",
    "classification": "Healthy",
    "confidenceLevel": "High",
    "alertsCount": 0,
    "recommendationsCount": 3
  },
  {
    "id": "2fb85f64-5717-4562-b3fc-2c963f66afa5",
    "productName": "Refrigerante Cola",
    "brand": "Coca-Cola",
    "analyzedAt": "2024-01-14T15:20:00Z",
    "classification": "Unhealthy",
    "confidenceLevel": "High",
    "alertsCount": 5,
    "recommendationsCount": 4
  }
]
```

**Resposta de Erro (401 Unauthorized):**
```json
{
  "error": "Não foi possível determinar o ID do usuário a partir do token."
}
```

---

### 2. Obter Detalhes da Análise
**GET** `/api/history/{id}`

Retorna os detalhes completos de uma análise específica do usuário autenticado.

**Headers:**
```
Authorization: Bearer {token}
```

**Parâmetros:**
- `id` (Guid): ID da análise

**Resposta de Sucesso (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "analyzedAt": "2024-01-15T10:30:00Z",
  "classification": "Healthy",
  "confidenceLevel": "High",
  "summary": "Produto com bom perfil nutricional, adequado para consumo regular.",
  "product": {
    "id": "4fa85f64-5717-4562-b3fc-2c963f66afa7",
    "name": "Iogurte Natural",
    "brand": "Danone",
    "barcode": "7891234567890"
  },
  "alerts": [],
  "recommendations": [
    {
      "id": "5fa85f64-5717-4562-b3fc-2c963f66afa8",
      "recommendation": "Consumir preferencialmente pela manhã",
      "reason": "Ótima fonte de proteínas para o café da manhã",
      "explanationLevel": "Detailed"
    },
    {
      "id": "6fa85f64-5717-4562-b3fc-2c963f66afa9",
      "recommendation": "Combinar com frutas frescas",
      "reason": "Aumenta o teor de fibras e vitaminas",
      "explanationLevel": "Brief"
    }
  ]
}
```

**Resposta de Erro (404 Not Found):**
```json
{
  "error": "Análise não encontrada ou não pertence ao usuário."
}
```

**Resposta de Erro (401 Unauthorized):**
```json
{
  "error": "Não foi possível determinar o ID do usuário a partir do token."
}
```

---

## Segurança

- Ambos os endpoints requerem autenticação via JWT Bearer Token
- Usuários só podem acessar suas próprias análises
- Tentativa de acessar análise de outro usuário retorna 404

---

## Características Técnicas

### Otimizações Implementadas:
1. **AsNoTracking()**: Queries otimizadas sem tracking do EF Core
2. **Include()**: Eager loading para evitar N+1 queries
3. **Select() direto para DTOs**: Reduz transferência de dados
4. **OrderByDescending()**: Ordenação por data no banco de dados
5. **Filtro por UserId**: Garantia de segurança ao nível de query

### DTOs:
- **AnalysisHistorySummaryDto**: Resumo para listagem
- **AnalysisHistoryDetailDto**: Detalhes completos da análise
- **ProductDetailsDto**: Informações do produto
- **AlertDetailsDto**: Detalhes de alertas
- **RecommendationDetailsDto**: Detalhes de recomendações

### Service:
- **IAnalysisHistoryService**: Interface na camada Application
- **AnalysisHistoryService**: Implementação na camada Infrastructure
- Injeção de dependência configurada

### Controller:
- **HistoryController**: Endpoints RESTful
- Documentação com XML comments
- Tratamento de erros padronizado
- Extração segura de UserId dos Claims JWT

---

## Exemplo de Uso (cURL)

### Listar histórico:
```bash
curl -X GET "https://localhost:7001/api/history" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

### Obter detalhes:
```bash
curl -X GET "https://localhost:7001/api/history/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

---

## Estrutura de Arquivos Criados

```
LabelWise.Application/
├── DTOs/
│   ├── AnalysisHistorySummaryDto.cs
│   └── AnalysisHistoryDetailDto.cs
└── Interfaces/
    └── IAnalysisHistoryService.cs

LabelWise.Infrastructure/
├── Services/
│   └── AnalysisHistoryService.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs (atualizado)

LabelWise.Api/
└── Controllers/
    └── HistoryController.cs
```
