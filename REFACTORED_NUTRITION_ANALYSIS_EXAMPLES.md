# EXEMPLOS PRÁTICOS - ANÁLISE NUTRICIONAL REFATORADA

## 📸 Exemplo 1: Achocolatado (Frente da Embalagem)

### Request:
```http
POST /api/RefactoredNutrition/analyze
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: multipart/form-data

image: chocolatto_frente.jpg
languageCode: pt
```

### Response:
```json
{
  "success": true,
  "productName": "Chocolatto",
  "brand": "3 Corações",
  "category": "alimento achocolatado em pó instantâneo",
  "packageWeight": "560 g",
  "analysisMode": "FrontOfPackageOnly",
  "visibleClaims": [
    "Não contém glúten",
    "Fonte de vitaminas e minerais",
    "Vitaminas D, B1, B2, B6 e B12",
    "Ferro e zinco",
    "Nova fórmula"
  ],
  "estimatedNutritionProfile": {
    "caloriesPer100g": 380,
    "estimatedPackageCalories": 2128,
    "estimatedSugarPer100g": 75.0,
    "estimatedProteinPer100g": 4.0,
    "estimatedSodiumPer100g": 150.0,
    "estimatedFiberPer100g": 3.0,
    "estimatedFatPer100g": 2.5,
    "basis": "Estimativa por categoria visual, sem leitura da tabela nutricional oficial"
  },
  "classification": {
    "diabetic": {
      "status": "consumo_moderado",
      "reason": "Produto achocolatado tende a conter açúcar relevante e baixa fibra"
    },
    "bloodPressure": {
      "status": "nao_recomendado",
      "reason": "Produto ultraprocessado pode apresentar teor relevante de sódio"
    },
    "weightLoss": {
      "status": "consumo_moderado",
      "reason": "Produto com densidade calórica moderada e provável adição de açúcar"
    },
    "muscleGain": {
      "status": "fraco",
      "reason": "Não é uma fonte relevante de proteína"
    }
  },
  "summary": "Achocolatado em pó ultraprocessado, com fortificação de vitaminas e minerais, sem indicação de alto teor proteico, com provável presença relevante de açúcar, baseado em análise visual da embalagem.",
  "confidenceDetails": {
    "productIdentification": 0.90,
    "visibleClaimsExtraction": 0.85,
    "estimatedNutritionProfile": 0.55,
    "classification": 0.70
  },
  "warnings": [
    "Análise estimada com base na imagem frontal do produto",
    "Valores nutricionais não foram extraídos da tabela nutricional oficial",
    "Para análise precisa, envie a parte traseira com tabela nutricional e ingredientes"
  ],
  "errorMessage": null,
  "processingTimeSeconds": 6.58
}
```

### 💡 Análise:
- ✅ Produto identificado corretamente
- ✅ Claims extraídos da frente da embalagem
- ⚠️ Valores nutricionais **estimados** (não reais)
- ⚠️ Confiança em nutrição: **0.55** (média-baixa)
- ⚠️ 3 warnings alertando sobre limitações

---

## 📸 Exemplo 2: Biscoito Recheado (Frente da Embalagem)

### Request:
```http
POST /api/RefactoredNutrition/analyze
Content-Type: multipart/form-data

image: oreo_frente.jpg
languageCode: pt
```

### Response:
```json
{
  "success": true,
  "productName": "Oreo",
  "brand": "Mondelez",
  "category": "biscoito recheado",
  "packageWeight": "144 g",
  "analysisMode": "FrontOfPackageOnly",
  "visibleClaims": [],
  "estimatedNutritionProfile": {
    "caloriesPer100g": 480,
    "estimatedPackageCalories": 691,
    "estimatedSugarPer100g": 35.0,
    "estimatedProteinPer100g": 5.0,
    "estimatedSodiumPer100g": 350.0,
    "estimatedFiberPer100g": 2.5,
    "estimatedFatPer100g": 20.0,
    "basis": "Estimativa por categoria visual, sem leitura da tabela nutricional oficial"
  },
  "classification": {
    "diabetic": {
      "status": "nao_recomendado",
      "reason": "Alto teor de açúcar e carboidratos refinados"
    },
    "bloodPressure": {
      "status": "nao_recomendado",
      "reason": "Teor moderado a alto de sódio"
    },
    "weightLoss": {
      "status": "nao_recomendado",
      "reason": "Alta densidade calórica e baixo poder sacietógeno"
    },
    "muscleGain": {
      "status": "fraco",
      "reason": "Baixo teor proteico"
    }
  },
  "summary": "Biscoito recheado ultraprocessado, sem indicação de alto teor proteico, com provável presença relevante de açúcar, baseado em análise visual da embalagem.",
  "confidenceDetails": {
    "productIdentification": 0.90,
    "visibleClaimsExtraction": 0.50,
    "estimatedNutritionProfile": 0.55,
    "classification": 0.70
  },
  "warnings": [
    "Análise estimada com base na imagem frontal do produto",
    "Valores nutricionais não foram extraídos da tabela nutricional oficial",
    "Para análise precisa, envie a parte traseira com tabela nutricional e ingredientes"
  ],
  "errorMessage": null,
  "processingTimeSeconds": 5.23
}
```

### 💡 Análise:
- ✅ Produto identificado
- ❌ Nenhum claim encontrado (lista vazia, não null)
- ⚠️ Confiança em claims: **0.50** (baixa)
- ⚠️ Valores nutricionais estimados
- 🚫 Produto não recomendado para todos os perfis

---

## 📸 Exemplo 3: Produto Não Identificado

### Request:
```http
POST /api/RefactoredNutrition/analyze
Content-Type: multipart/form-data

image: imagem_borrada.jpg
languageCode: pt
```

### Response:
```json
{
  "success": false,
  "productName": null,
  "brand": null,
  "category": null,
  "packageWeight": null,
  "analysisMode": "FrontOfPackageOnly",
  "visibleClaims": [],
  "estimatedNutritionProfile": null,
  "classification": null,
  "summary": null,
  "confidenceDetails": null,
  "warnings": [],
  "errorMessage": "Não foi possível interpretar a imagem",
  "processingTimeSeconds": 3.45
}
```

### 💡 Análise:
- ❌ Análise falhou
- ✅ `success: false`
- ✅ `errorMessage` explica o problema
- ✅ Todos os campos opcionais são `null`
- ✅ Listas vazias ao invés de null

---

## 📸 Exemplo 4: Categoria Desconhecida (Fallback para Genérico)

### Request:
```http
POST /api/RefactoredNutrition/analyze
Content-Type: multipart/form-data

image: produto_exotico.jpg
languageCode: pt
```

### Response:
```json
{
  "success": true,
  "productName": "Kombucha Orgânica",
  "brand": "Kefera",
  "category": "bebida fermentada probiótica",
  "packageWeight": "300 ml",
  "analysisMode": "FrontOfPackageOnly",
  "visibleClaims": [
    "100% orgânico",
    "Probiótico natural",
    "Sem conservantes"
  ],
  "estimatedNutritionProfile": {
    "caloriesPer100g": 250,
    "estimatedPackageCalories": 750,
    "estimatedSugarPer100g": 10.0,
    "estimatedProteinPer100g": 5.0,
    "estimatedSodiumPer100g": 300.0,
    "estimatedFiberPer100g": 2.0,
    "estimatedFatPer100g": 10.0,
    "basis": "Estimativa por categoria visual, sem leitura da tabela nutricional oficial"
  },
  "classification": {
    "diabetic": {
      "status": "consumo_moderado",
      "reason": "Categoria não identificada, assumindo perfil médio"
    },
    "bloodPressure": {
      "status": "consumo_moderado",
      "reason": "Categoria não identificada, assumindo perfil médio"
    },
    "weightLoss": {
      "status": "consumo_moderado",
      "reason": "Categoria não identificada, assumindo perfil médio"
    },
    "muscleGain": {
      "status": "moderado",
      "reason": "Categoria não identificada, assumindo perfil médio"
    }
  },
  "summary": "Alimento processado genérico, baseado em análise visual da embalagem.",
  "confidenceDetails": {
    "productIdentification": 0.90,
    "visibleClaimsExtraction": 0.85,
    "estimatedNutritionProfile": 0.55,
    "classification": 0.70
  },
  "warnings": [
    "Análise estimada com base na imagem frontal do produto",
    "Valores nutricionais não foram extraídos da tabela nutricional oficial",
    "Para análise precisa, envie a parte traseira com tabela nutricional e ingredientes"
  ],
  "errorMessage": null,
  "processingTimeSeconds": 7.12
}
```

### 💡 Análise:
- ✅ Produto identificado
- ✅ Claims extraídos
- ⚠️ Categoria não existe no banco de perfis
- ✅ Fallback para perfil genérico
- ⚠️ Razões das classificações indicam "categoria não identificada"

---

## 🧪 Script PowerShell de Teste

```powershell
# test-refactored-nutrition.ps1

$baseUrl = "https://localhost:7001"
$imagePath = "C:\images\chocolatto_frente.jpg"

# 1. Login
$loginResponse = Invoke-RestMethod `
    -Uri "$baseUrl/api/Auth/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body (@{
        email = "test@example.com"
        password = "Test@123"
    } | ConvertTo-Json)

$token = $loginResponse.token
Write-Host "✅ Token obtido: $($token.Substring(0, 20))..." -ForegroundColor Green

# 2. Análise Refatorada
Write-Host "`n📸 Enviando imagem para análise..." -ForegroundColor Cyan

$headers = @{
    "Authorization" = "Bearer $token"
}

$form = @{
    image = Get-Item -Path $imagePath
    languageCode = "pt"
}

$response = Invoke-RestMethod `
    -Uri "$baseUrl/api/RefactoredNutrition/analyze" `
    -Method Post `
    -Headers $headers `
    -Form $form

# 3. Exibir Resultado
Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Yellow
Write-Host "RESULTADO DA ANÁLISE REFATORADA" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Yellow

Write-Host "`n🏷️  IDENTIFICAÇÃO:" -ForegroundColor Cyan
Write-Host "   Produto: $($response.productName)"
Write-Host "   Marca: $($response.brand)"
Write-Host "   Categoria: $($response.category)"
Write-Host "   Peso: $($response.packageWeight)"

Write-Host "`n📋 MODO DE ANÁLISE:" -ForegroundColor Cyan
Write-Host "   $($response.analysisMode)" -ForegroundColor $(if ($response.analysisMode -eq "FrontOfPackageOnly") { "Yellow" } else { "Green" })

Write-Host "`n✨ CLAIMS VISÍVEIS:" -ForegroundColor Cyan
if ($response.visibleClaims.Count -gt 0) {
    foreach ($claim in $response.visibleClaims) {
        Write-Host "   • $claim" -ForegroundColor White
    }
} else {
    Write-Host "   (Nenhum claim encontrado)" -ForegroundColor DarkGray
}

Write-Host "`n🍎 PERFIL NUTRICIONAL ESTIMADO:" -ForegroundColor Cyan
$nutrition = $response.estimatedNutritionProfile
Write-Host "   Calorias/100g: $($nutrition.caloriesPer100g) kcal"
Write-Host "   Calorias/embalagem: $($nutrition.estimatedPackageCalories) kcal"
Write-Host "   Açúcar/100g: $($nutrition.estimatedSugarPer100g) g"
Write-Host "   Proteína/100g: $($nutrition.estimatedProteinPer100g) g"
Write-Host "   Sódio/100g: $($nutrition.estimatedSodiumPer100g) mg"
Write-Host "   Fibra/100g: $($nutrition.estimatedFiberPer100g) g"
Write-Host "   Gordura/100g: $($nutrition.estimatedFatPer100g) g"
Write-Host "   Base: $($nutrition.basis)" -ForegroundColor Yellow

Write-Host "`n👥 CLASSIFICAÇÃO POR PERFIL:" -ForegroundColor Cyan
Write-Host "   Diabético: $($response.classification.diabetic.status)" -ForegroundColor $(Get-StatusColor $response.classification.diabetic.status)
Write-Host "      → $($response.classification.diabetic.reason)"
Write-Host "   Pressão Alta: $($response.classification.bloodPressure.status)" -ForegroundColor $(Get-StatusColor $response.classification.bloodPressure.status)
Write-Host "      → $($response.classification.bloodPressure.reason)"
Write-Host "   Perda de Peso: $($response.classification.weightLoss.status)" -ForegroundColor $(Get-StatusColor $response.classification.weightLoss.status)
Write-Host "      → $($response.classification.weightLoss.reason)"
Write-Host "   Ganho Muscular: $($response.classification.muscleGain.status)" -ForegroundColor $(Get-StatusColor $response.classification.muscleGain.status)
Write-Host "      → $($response.classification.muscleGain.reason)"

Write-Host "`n📊 CONFIANÇA DETALHADA:" -ForegroundColor Cyan
$confidence = $response.confidenceDetails
Write-Host "   Identificação do Produto: $($confidence.productIdentification * 100)%"
Write-Host "   Extração de Claims: $($confidence.visibleClaimsExtraction * 100)%"
Write-Host "   Perfil Nutricional: $($confidence.estimatedNutritionProfile * 100)%" -ForegroundColor $(if ($confidence.estimatedNutritionProfile -lt 0.7) { "Yellow" } else { "Green" })
Write-Host "   Classificação: $($confidence.classification * 100)%"

Write-Host "`n⚠️  AVISOS:" -ForegroundColor Yellow
if ($response.warnings.Count -gt 0) {
    foreach ($warning in $response.warnings) {
        Write-Host "   • $warning" -ForegroundColor Yellow
    }
} else {
    Write-Host "   (Nenhum aviso)" -ForegroundColor Green
}

Write-Host "`n📝 RESUMO:" -ForegroundColor Cyan
Write-Host "   $($response.summary)"

Write-Host "`n⏱️  PROCESSAMENTO:" -ForegroundColor Cyan
Write-Host "   $($response.processingTimeSeconds) segundos"

Write-Host "`n═══════════════════════════════════════════════════════════`n" -ForegroundColor Yellow

# Helper function
function Get-StatusColor($status) {
    switch ($status) {
        "mais_adequado" { return "Green" }
        "bom" { return "Green" }
        "consumo_moderado" { return "Yellow" }
        "moderado" { return "Yellow" }
        "fraco" { return "DarkYellow" }
        "nao_recomendado" { return "Red" }
        "nao_indicado" { return "Red" }
        default { return "White" }
    }
}
```

### Como usar:
```powershell
.\test-refactored-nutrition.ps1
```

---

## 🔄 Comparação: Antes vs Depois

### ANTES (Endpoint Legado):
```json
{
  "success": true,
  "productName": "Chocolatto",
  "estimatedNutrition": {
    "caloriesPer100g": 380
  },
  "confidence": 0.75
}
```
❌ Não fica claro se 380 kcal é estimado ou real  
❌ Sem modo de análise  
❌ Sem claims visíveis  
❌ Confiança única (genérica)  
❌ Sem avisos automáticos  

### DEPOIS (Endpoint Refatorado):
```json
{
  "success": true,
  "productName": "Chocolatto",
  "analysisMode": "FrontOfPackageOnly",
  "visibleClaims": ["Não contém glúten"],
  "estimatedNutritionProfile": {
    "caloriesPer100g": 380,
    "basis": "Estimativa por categoria visual, sem leitura da tabela nutricional oficial"
  },
  "confidenceDetails": {
    "productIdentification": 0.90,
    "estimatedNutritionProfile": 0.55
  },
  "warnings": [
    "Valores nutricionais não foram extraídos da tabela nutricional oficial"
  ]
}
```
✅ Explícito que 380 kcal é estimado  
✅ Modo de análise claro  
✅ Claims extraídos  
✅ Confiança detalhada  
✅ Avisos automáticos  

---

**Desenvolvido para produção ✅**
