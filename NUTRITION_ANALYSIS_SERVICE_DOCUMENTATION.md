# 🍎 Nutrition Analysis Service - Documentação

## 📋 Visão Geral

O **Nutrition Analysis Service** é um novo endpoint independente que fornece análise nutricional simplificada de produtos alimentícios a partir de imagens. Ele foi projetado para ser **completamente separado** do pipeline de OCR completo existente, oferecendo uma abordagem mais rápida e direta para avaliação nutricional básica.

---

## 🎯 Objetivo

Identificar produtos alimentícios em imagens e fornecer:
- **Estimativa nutricional** baseada em categoria
- **Classificação para perfis de saúde** (diabético, pressão alta, perda de peso, ganho de massa)
- **Resumo rápido e objetivo** sobre o produto

---

## 🏗️ Arquitetura

### Estrutura de Componentes

```
LabelWise.Api/
└── Controllers/
    └── NutritionController.cs .................. Controller HTTP

LabelWise.Application/
├── Interfaces/
│   └── INutritionAnalysisService.cs ........... Interface do serviço
├── DTOs/Nutrition/
│   ├── SimpleNutritionAnalysisRequest.cs ...... Request DTO
│   ├── SimpleNutritionAnalysisResponse.cs ..... Response DTO
│   ├── EstimatedNutritionDto.cs ............... Dados nutricionais
│   ├── ProfileClassificationDto.cs ............ Classificações
│   └── ProfileStatusDto.cs .................... Status individual
└── Models/Nutrition/
    └── ProductCategoryNutritionProfile.cs ..... Base de conhecimento

LabelWise.Infrastructure/
├── Services/
│   └── NutritionAnalysisService.cs ............ Implementação
└── Extensions/
    └── ServiceCollectionExtensions.cs ......... Registro DI
```

---

## 🚀 Endpoint

### `POST /api/nutrition/analyze-simple-image`

#### Request

**Content-Type:** `multipart/form-data`

**Parâmetros:**
- `file` (obrigatório): Imagem do produto (JPG, PNG, WEBP)
- `languageCode` (opcional): Código do idioma (padrão: "pt")
- `profiles` (opcional): Perfis específicos, separados por vírgula

**Exemplo com cURL:**

```bash
curl -X POST "https://localhost:7223/api/nutrition/analyze-simple-image" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -F "file=@produto.jpg" \
  -F "languageCode=pt"
```

#### Response

```json
{
  "success": true,
  "productName": "Biscoito Amanteigado Sabor Leite",
  "brand": "Fortaleza",
  "category": "Biscoito Amanteigado",
  "packageWeight": "350g",
  "estimatedNutrition": {
    "caloriesPer100g": 450,
    "estimatedPackageCalories": 1575,
    "estimatedSugarPer100g": 18.0,
    "estimatedProteinPer100g": 6.0,
    "estimatedSodiumPer100g": 350.0,
    "estimatedFiberPer100g": 2.0,
    "estimatedFatPer100g": 18.0
  },
  "classification": {
    "diabetic": {
      "status": "nao_recomendado",
      "reason": "Alto teor estimado de açúcar e carboidratos refinados"
    },
    "bloodPressure": {
      "status": "consumo_moderado",
      "reason": "Produto ultraprocessado com possível presença elevada de sódio e gordura saturada"
    },
    "weightLoss": {
      "status": "nao_indicado",
      "reason": "Alta densidade calórica e baixa saciedade"
    },
    "muscleGain": {
      "status": "fraco",
      "reason": "Baixa proteína e calorias de baixa qualidade nutricional"
    }
  },
  "summary": "Produto com produto ultraprocessado, alta densidade calórica, alto teor de açúcar, baixa proteína.",
  "confidence": 0.85,
  "warnings": [
    "Análise estimada com base na categoria do produto",
    "Valores nutricionais são aproximações baseadas em dados médios da categoria"
  ],
  "processingTimeSeconds": 2.45
}
```

---

## 🔧 Fluxo de Processamento

### Etapas do Serviço

```
┌─────────────────────────────────────────────────────────────┐
│  1. IDENTIFICAÇÃO DO PRODUTO                                │
│     • Usa Azure OpenAI Vision (IVisualInterpreter)         │
│     • Extrai: nome, marca, categoria, peso da embalagem     │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  2. BUSCA DE PERFIL NUTRICIONAL                             │
│     • Compara categoria com base de conhecimento            │
│     • Keywords matching (biscoito, salgadinho, etc.)        │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  3. ESTIMATIVA NUTRICIONAL                                  │
│     • Aplica valores médios da categoria                    │
│     • Calcula calorias totais da embalagem                  │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  4. CLASSIFICAÇÃO PARA PERFIS                               │
│     • Diabético (açúcar, fibra, densidade calórica)        │
│     • Pressão Alta (sódio, ultraprocessamento)             │
│     • Perda de Peso (densidade calórica, saciedade)        │
│     • Ganho de Massa (proteína, qualidade calórica)        │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  5. GERAÇÃO DE RESUMO E WARNINGS                            │
│     • Resumo das características principais                 │
│     • Avisos sobre limitações da análise                    │
└─────────────────────────────────────────────────────────────┘
```

---

## 📊 Base de Conhecimento Nutricional

O serviço mantém perfis nutricionais para categorias comuns:

| Categoria            | Cal/100g | Açúcar | Proteína | Sódio | Densidade Calórica |
|----------------------|----------|--------|----------|-------|--------------------|
| Biscoito Amanteigado | 450      | Alto   | Baixo    | Mod   | Alta               |
| Biscoito Recheado    | 480      | Alto   | Baixo    | Mod   | Alta               |
| Salgadinho           | 520      | Baixo  | Baixo    | Alto  | Alta               |
| Refrigerante         | 42       | Alto   | Baixo    | Baixo | Moderada           |
| Barra Proteica       | 380      | Mod    | Alto     | Mod   | Moderada           |
| Iogurte Proteico     | 90       | Baixo  | Alto     | Baixo | Baixa              |
| Chocolate            | 540      | Alto   | Baixo    | Baixo | Alta               |
| Pão de Forma         | 265      | Baixo  | Mod      | Alto  | Moderada           |

**Keywords de Matching:**
- Biscoito: `biscoito, amanteigado, manteiga, cookie`
- Salgadinho: `salgadinho, chips, doritos, ruffles, cheetos`
- Refrigerante: `refrigerante, coca, pepsi, guaraná, fanta`
- E mais...

---

## 🎯 Regras de Classificação

### Diabético
- **Não Recomendado:** Alto açúcar + alta densidade calórica
- **Consumo Moderado:** Açúcar moderado + baixa fibra
- **Mais Adequado:** Perfil favorável de açúcar/carboidratos

### Pressão Alta
- **Não Recomendado:** Alto sódio + ultraprocessado
- **Consumo Moderado:** Sódio moderado
- **Mais Adequado:** Baixo sódio

### Perda de Peso
- **Não Indicado:** Alta densidade calórica + baixa fibra
- **Consumo Moderado:** Densidade moderada
- **Mais Adequado:** Densidade favorável + boa saciedade

### Ganho de Massa Muscular
- **Fraco:** Baixa proteína (< 8g/100g)
- **Moderado:** Proteína moderada (8-15g/100g)
- **Bom:** Alta proteína (≥ 15g/100g)

---

## 🔐 Autenticação

O endpoint requer autenticação JWT:

```
Authorization: Bearer <token>
```

Para obter o token:
1. Registre um usuário: `POST /api/auth/register`
2. Faça login: `POST /api/auth/login`
3. Use o token retornado no header `Authorization`

---

## ⚙️ Configuração

### Dependências Necessárias

O serviço reutiliza a infraestrutura existente:

- **IVisualInterpreter** (Azure OpenAI Vision)
  - Configurado em `appsettings.json`:
    ```json
    "AzureOpenAiVision": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "your-api-key",
      "VisionDeployment": "gpt-4-vision"
    }
    ```

### Registro de Dependências

Já configurado automaticamente em `ServiceCollectionExtensions.cs`:

```csharp
services.AddScoped<INutritionAnalysisService, NutritionAnalysisService>();
```

---

## 🧪 Como Testar

### Opção 1: PowerShell Script

```powershell
.\test-nutrition-endpoint.ps1
```

### Opção 2: Swagger UI

1. Inicie a API: `.\run-api.ps1`
2. Acesse: `https://localhost:7223/swagger`
3. Autentique com JWT
4. Teste o endpoint `/api/nutrition/analyze-simple-image`

### Opção 3: Postman

1. **Register:** `POST /api/auth/register`
   ```json
   {
     "email": "test@example.com",
     "password": "Test123!@#",
     "name": "Test User"
   }
   ```

2. **Login:** `POST /api/auth/login`
   ```json
   {
     "email": "test@example.com",
     "password": "Test123!@#"
   }
   ```

3. **Analyze:** `POST /api/nutrition/analyze-simple-image`
   - Headers: `Authorization: Bearer <token>`
   - Body: `form-data`
     - `file`: (upload image)
     - `languageCode`: `pt`

---

## 📝 Exemplos de Uso

### Exemplo 1: Análise Básica

```bash
curl -X POST "https://localhost:7223/api/nutrition/analyze-simple-image" \
  -H "Authorization: Bearer eyJhbGc..." \
  -F "file=@biscoito.jpg"
```

### Exemplo 2: Com Idioma Específico

```bash
curl -X POST "https://localhost:7223/api/nutrition/analyze-simple-image" \
  -H "Authorization: Bearer eyJhbGc..." \
  -F "file=@produto.jpg" \
  -F "languageCode=en"
```

### Exemplo 3: Filtrar Perfis

```bash
curl -X POST "https://localhost:7223/api/nutrition/analyze-simple-image" \
  -H "Authorization: Bearer eyJhbGc..." \
  -F "file=@produto.jpg" \
  -F "profiles=diabetic,weightLoss"
```

---

## 🚨 Tratamento de Erros

### Erro 400 - Bad Request
```json
{
  "success": false,
  "error": "Arquivo de imagem é obrigatório"
}
```

### Erro 401 - Unauthorized
```json
{
  "success": false,
  "error": "Token inválido ou expirado"
}
```

### Erro 500 - Internal Server Error
```json
{
  "success": false,
  "error": "Erro interno ao processar análise",
  "details": "..."
}
```

---

## 📈 Métricas e Logs

O serviço gera logs detalhados:

```
═══════════════════════════════════════════════════════════
🍎 Iniciando análise nutricional simplificada
   FileName: biscoito.jpg
   ImageSize: 245678 bytes
   Language: pt
═══════════════════════════════════════════════════════════
✅ Produto identificado: Biscoito Amanteigado
   Marca: Fortaleza
   Categoria: Biscoito Amanteigado
✅ Perfil nutricional: Biscoito Amanteigado
═══════════════════════════════════════════════════════════
✅ Análise nutricional concluída
   Confidence: 85.00%
   ProcessingTime: 2.45s
═══════════════════════════════════════════════════════════
```

---

## 🔄 Diferenças do Pipeline Completo

| Aspecto                  | Pipeline Completo        | Nutrition Analysis        |
|--------------------------|--------------------------|---------------------------|
| **Objetivo**             | Análise detalhada        | Avaliação rápida          |
| **OCR**                  | Completo (tabela, etc.)  | Apenas identificação      |
| **Tempo**                | 5-15s                    | 2-5s                      |
| **Precisão Nutricional** | Alta (leitura exata)     | Estimativa (categoria)    |
| **Uso**                  | Análise completa         | Screening rápido          |
| **Endpoint**             | `/api/guided-capture`    | `/api/nutrition/analyze`  |

---

## 🎯 Casos de Uso

### ✅ Ideal Para:
- Screening rápido de produtos
- Identificação preliminar
- Apps de scanning em tempo real
- Feedback imediato para usuário

### ❌ Não Ideal Para:
- Valores nutricionais exatos
- Análise detalhada de ingredientes
- Identificação de alérgenos específicos
- Conformidade regulatória

---

## 🛠️ Extensões Futuras

### Melhorias Planejadas:
1. **Base de conhecimento expandida**
   - Mais categorias de produtos
   - Perfis regionais/locais

2. **Machine Learning**
   - Predição baseada em histórico
   - Refinamento de estimativas

3. **Integração com catálogo de produtos conhecidos**
   - Busca por nome/marca identificados
   - Dados exatos quando disponíveis

4. **Personalização**
   - Ajuste de thresholds por perfil de usuário
   - Histórico de preferências

---

## 📚 Referências

- **Azure OpenAI Vision:** Usado para identificação de produtos
- **IVisualInterpreter:** Interface de integração com Vision
- **ProductIdentificationService:** Serviço existente de identificação

---

## ✅ Checklist de Validação

- [x] Endpoint funcional
- [x] Autenticação JWT
- [x] Validação de arquivo
- [x] Identificação com Vision
- [x] Base de conhecimento nutricional
- [x] Classificação de perfis
- [x] Logs detalhados
- [x] Tratamento de erros
- [x] Documentação completa
- [x] Script de teste

---

## 📞 Suporte

Para questões ou problemas:
1. Verifique os logs em `LabelWise.Infrastructure.Services.NutritionAnalysisService`
2. Confirme configuração de `AzureOpenAiVision` em `appsettings.json`
3. Valide token JWT com `/api/auth/validate`

---

**Versão:** 1.0.0  
**Data:** 2024  
**Status:** ✅ Implementado e Testado
