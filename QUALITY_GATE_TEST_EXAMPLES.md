# Quality Gate - Exemplos de Teste e Validação

## 🎯 Casos de Teste

### Caso 1: Imagem de Baixíssima Qualidade
**Cenário:** Foto tremida, fora de foco, ângulo ruim, baixa iluminação

**OCR Result:**
```
Texto extraído: "~~@ Pr0du†0 Al¡m3nt1c10 ... §§§ 1ngr3d13nt3§ ??? ... "
Confidence: 0.35
```

**Parser Result:**
```csharp
{
    ProductName = "Produto Desconhecido",
    Brand = null,
    Ingredients = [],
    Allergens = [],
    Nutrition = null
}
```

**Resultado ANTES do Quality Gate:**
```json
{
  "confidenceLevel": "Alto",
  "classification": "Safe",
  "generalScore": 0.70,
  "personalizedScore": 0.70,
  "summary": "Boa Escolha (70/100). Pode consumir regularmente.",
  "shortSummary": "Análise incompleta"
}
```

**Resultado DEPOIS do Quality Gate:**
```json
{
  "confidenceLevel": "Baixo",
  "classification": "Incomplete",
  "generalScore": 0.21,
  "personalizedScore": 0.21,
  "summary": "Análise Parcial - Não foi possível ler o rótulo adequadamente. Tire uma nova foto mais próxima, com boa iluminação e sem reflexo.",
  "shortSummary": "Produto não identificado (21/100). Tire outra foto do rótulo.",
  "alerts": [
    "⚠️ OCR: VeryLow (Não foi possível ler o rótulo adequadamente) | Parsing: Incomplete (Leitura incompleta)"
  ]
}
```

**Ajustes aplicados:**
- ✅ Confidence: Alto → Baixo (OCR quality = VeryLow)
- ✅ Classification: Safe → Incomplete (produto não identificado)
- ✅ Score: 0.70 → 0.21 (penalização de 70%: -30% confidence + -20% OCR + -25% parsing)
- ✅ Summary: Mensagem adequada pedindo nova foto
- ✅ Alert: Explicação técnica do problema

---

### Caso 2: OCR OK mas Parser Não Identificou Produto
**Cenário:** Texto foi extraído mas não contém nome/marca identificável

**OCR Result:**
```
Texto extraído: "Lista de ingredientes: farinha de trigo, açúcar, óleo vegetal..."
Confidence: 0.88
```

**Parser Result:**
```csharp
{
    ProductName = "Produto Desconhecido",
    Brand = null,
    Ingredients = ["farinha de trigo", "açúcar", "óleo vegetal", "sal"],
    Allergens = ["glúten"],
    Nutrition = { Calories = 450, TotalFat = 18, ... }
}
```

**Resultado ANTES do Quality Gate:**
```json
{
  "confidenceLevel": "Alto",
  "classification": "Safe",
  "generalScore": 0.75,
  "personalizedScore": 0.78,
  "summary": "Boa Escolha (78/100). Pode consumir regularmente com moderação.",
  "shortSummary": "Boa escolha (78/100). Pode consumir com tranquilidade."
}
```

**Resultado DEPOIS do Quality Gate:**
```json
{
  "confidenceLevel": "Médio",
  "classification": "Incomplete",
  "generalScore": 0.64,
  "personalizedScore": 0.66,
  "summary": "Análise com Ressalvas - Algumas informações foram identificadas, mas a leitura não está completa. Leitura incompleta. Tire outra foto mais próxima do rótulo nutricional.",
  "shortSummary": "Produto não identificado (66/100). Tire outra foto do rótulo.",
  "alerts": [
    "⚠️ OCR: High (Leitura clara) | Parsing: Partial (Leitura incompleta. Tire outra foto mais próxima do rótulo nutricional.)"
  ]
}
```

**Ajustes aplicados:**
- ✅ Confidence: Alto → Médio (produto não identificado, mas parsing parcial ok)
- ✅ Classification: Safe → Incomplete (REGRA: produto desconhecido → Incomplete)
- ✅ Score: 0.78 → 0.66 (penalização de 15%: -15% confidence médio)
- ✅ Summary: Indica que identificou informações mas está incompleto
- ✅ ShortSummary: Pede nova foto

---

### Caso 3: Parsing Parcial com Ingredientes Inválidos
**Cenário:** OCR extraiu texto com ruído, parser capturou alguns ingredientes mas com tokens inválidos

**OCR Result:**
```
Texto extraído: "Biscoito Chocolate Brand XYZ Ingredientes: farinha ??? açúcar ... sal óleo ?? corante"
Confidence: 0.65
```

**Parser Result:**
```csharp
{
    ProductName = "Biscoito Chocolate",
    Brand = "Brand XYZ",
    Ingredients = ["farinha", "???", "açúcar", "...", "sal", "óleo", "??", "corante"],
    Allergens = ["glúten"],
    Nutrition = { Calories = 480, Sugars = 22, ... }
}
```

**Resultado ANTES do Quality Gate:**
```json
{
  "confidenceLevel": "Alto",
  "classification": "Safe",
  "generalScore": 0.62,
  "personalizedScore": 0.65,
  "summary": "Boa Escolha (65/100). Pode consumir regularmente com moderação. Alto teor de açúcar (22g), Baixo teor de fibras.",
  "shortSummary": "Boa escolha (65/100). Pode consumir com tranquilidade."
}
```

**Resultado DEPOIS do Quality Gate:**
```json
{
  "confidenceLevel": "Médio",
  "classification": "Caution",
  "generalScore": 0.53,
  "personalizedScore": 0.55,
  "summary": "Opção Aceitável (55/100). Pode consumir ocasionalmente. Alto teor de açúcar (22g), Baixo teor de fibras. • ⚠️ Análise baseada em leitura parcial do rótulo.",
  "shortSummary": "Consumir com atenção (55/100). Informações parciais identificadas.",
  "alerts": [
    "⚠️ OCR: Medium (Leitura parcial. Análise pode estar incompleta.) | Parsing: Partial (Análise parcial. Algumas informações não foram identificadas.)"
  ]
}
```

**Ajustes aplicados:**
- ✅ Confidence: Alto → Médio (OCR quality = Medium, parsing = Partial)
- ✅ Classification: Safe → Caution (confidence não é alta)
- ✅ Score: 0.65 → 0.55 (penalização de 15%: -15% confidence + -10% parsing partial)
- ✅ Summary: Termos otimistas removidos + disclaimer sobre parsing parcial
- ✅ ShortSummary: Coerente com análise parcial

---

### Caso 4: Análise Completa e de Alta Qualidade
**Cenário:** Foto de qualidade, OCR preciso, parser completo

**OCR Result:**
```
Texto extraído: "Bolacha Integral Maria Marca XYZ Ingredientes: farinha de trigo integral, açúcar, óleo vegetal, sal, fermentos químicos. Contém glúten. Tabela Nutricional: Porção 30g (3 unidades) Valor Energético 120kcal Carboidratos 20g Proteínas 3g Gorduras Totais 4g Fibra Alimentar 2g Sódio 150mg"
Confidence: 0.95
```

**Parser Result:**
```csharp
{
    ProductName = "Bolacha Integral Maria",
    Brand = "Marca XYZ",
    Ingredients = ["farinha de trigo integral", "açúcar", "óleo vegetal", "sal", "fermentos químicos"],
    Allergens = ["glúten"],
    Nutrition = {
        ServingSize = "30g (3 unidades)",
        Calories = 120,
        TotalCarbohydrate = 20,
        Protein = 3,
        TotalFat = 4,
        DietaryFiber = 2,
        Sodium = 150
    }
}
```

**Resultado ANTES do Quality Gate:**
```json
{
  "confidenceLevel": "Alto",
  "classification": "Safe",
  "generalScore": 0.82,
  "personalizedScore": 0.85,
  "summary": "Boa Escolha (85/100). Produto adequado para consumo regular. Baixo teor de sódio (150mg), Baixo teor de fibras (2g).",
  "shortSummary": "Boa escolha (85/100). Pode consumir com tranquilidade."
}
```

**Resultado DEPOIS do Quality Gate (SEM MUDANÇAS):**
```json
{
  "confidenceLevel": "Alto",
  "classification": "Safe",
  "generalScore": 0.82,
  "personalizedScore": 0.85,
  "summary": "Boa Escolha (85/100). Produto adequado para consumo regular. Baixo teor de sódio (150mg), Baixo teor de fibras (2g).",
  "shortSummary": "Boa escolha (85/100). Pode consumir com tranquilidade."
}
```

**Ajustes aplicados:**
- ✅ NENHUM - Quality Gate só interfere quando detecta problemas
- ✅ OCR Quality: High
- ✅ Parsing Completeness: Complete
- ✅ Confidence: Alto (sem alteração)
- ✅ Scores mantidos

---

### Caso 5: Alérgenos Declarados mas Parsing Incompleto
**Cenário:** Parser identificou alérgenos mas ingredientes estão incompletos

**OCR Result:**
```
Texto extraído: "Produto alimentício. Contém: leite, soja, glúten. Ingredientes: ??? ... ..."
Confidence: 0.55
```

**Parser Result:**
```csharp
{
    ProductName = "Produto Alimentício",
    Brand = null,
    Ingredients = ["???", "..."],
    Allergens = ["leite", "soja", "glúten"],
    Nutrition = null
}
```

**Resultado ANTES do Quality Gate:**
```json
{
  "confidenceLevel": "Médio",
  "classification": "Safe",
  "generalScore": 0.68,
  "personalizedScore": 0.70,
  "summary": "Boa Escolha (70/100). Pode consumir regularmente.",
  "shortSummary": "Boa escolha (70/100). Pode consumir com tranquilidade."
}
```

**Resultado DEPOIS do Quality Gate:**
```json
{
  "confidenceLevel": "Baixo",
  "classification": "Caution",
  "generalScore": 0.34,
  "personalizedScore": 0.35,
  "summary": "Análise com Ressalvas - Algumas informações foram identificadas, mas a leitura não está completa. Leitura incompleta. Tire outra foto mais próxima do rótulo nutricional.",
  "shortSummary": "Consumir com atenção (35/100). Informações parciais identificadas.",
  "alerts": [
    "⚠️ OCR: Low (Leitura com dificuldades. Tire outra foto com melhor iluminação e foco.) | Parsing: Partial (Análise parcial. Algumas informações não foram identificadas.)",
    "🚨 Produto contém glúten!",
    "🚨 Produto contém leite!",
    "🚨 Produto contém soja!"
  ]
}
```

**Ajustes aplicados:**
- ✅ Confidence: Médio → Baixo (OCR quality = Low)
- ✅ Classification: Safe → Caution (REGRA: alérgenos + parsing incompleto → conservador)
- ✅ Score: 0.70 → 0.35 (penalização de 50%: -30% confidence + -10% OCR + -10% parsing)
- ✅ Summary: Indica problema + pede nova foto
- ✅ Mantém alertas de alérgenos (importantes!)

---

## 🧪 Script PowerShell para Testes

```powershell
# test-quality-gate.ps1

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Quality Gate - Testes de Validação" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

$apiUrl = "http://localhost:5000/api/pipeline/analyze"

# Função auxiliar para testar imagem
function Test-ImageUpload {
    param(
        [string]$ImagePath,
        [string]$TestName
    )

    Write-Host "`n🧪 Teste: $TestName" -ForegroundColor Yellow
    Write-Host "   Arquivo: $ImagePath"

    if (-not (Test-Path $ImagePath)) {
        Write-Host "   ❌ Arquivo não encontrado!" -ForegroundColor Red
        return
    }

    try {
        $form = @{
            image = Get-Item -Path $ImagePath
        }

        $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Form $form -ContentType "multipart/form-data"

        Write-Host "   ✅ Upload realizado com sucesso" -ForegroundColor Green
        Write-Host "   📊 Resultado:"
        Write-Host "      • Produto: $($response.analysisResult.productName)"
        Write-Host "      • Confiança: $($response.analysisResult.confidenceLevel)"
        Write-Host "      • Classificação: $($response.analysisResult.classification)"
        Write-Host "      • Score Geral: $($response.analysisResult.generalScore.ToString('F2'))"
        Write-Host "      • Score Personalizado: $($response.analysisResult.personalizedScore.ToString('F2'))"
        Write-Host "      • Summary: $($response.analysisResult.shortSummary)"

        if ($response.analysisResult.alerts.Count -gt 0) {
            Write-Host "      • Alerts:" -ForegroundColor Yellow
            foreach ($alert in $response.analysisResult.alerts) {
                Write-Host "        - $alert" -ForegroundColor Yellow
            }
        }
    }
    catch {
        Write-Host "   ❌ Erro: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# ═══════════════════════════════════════════════════════════════════
# Testes
# ═══════════════════════════════════════════════════════════════════

# Teste 1: Imagem de baixa qualidade
Test-ImageUpload -ImagePath "test-images/low-quality.jpg" -TestName "Imagem de Baixa Qualidade"

# Teste 2: Imagem sem nome de produto visível
Test-ImageUpload -ImagePath "test-images/no-product-name.jpg" -TestName "Imagem Sem Nome do Produto"

# Teste 3: Imagem com parsing parcial
Test-ImageUpload -ImagePath "test-images/partial-parsing.jpg" -TestName "Parsing Parcial"

# Teste 4: Imagem de alta qualidade
Test-ImageUpload -ImagePath "test-images/high-quality.jpg" -TestName "Imagem de Alta Qualidade"

# Teste 5: Alérgenos com parsing incompleto
Test-ImageUpload -ImagePath "test-images/allergens-incomplete.jpg" -TestName "Alérgenos + Parsing Incompleto"

Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Testes Concluídos" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
```

---

## ✅ Checklist de Validação

### Validação de OCR Quality
- [ ] Texto vazio → `OcrQuality = VeryLow`
- [ ] Texto com >15% ruído → `HasSignificantNoise = true`
- [ ] Palavras válidas <35% → `Quality = Low or VeryLow`
- [ ] Fragmentação alta (>30% palavras de 1 char) → `IsHighlyFragmented = true`
- [ ] Confiança OCR <0.60 → `Quality <= Medium`

### Validação de Parsing Quality
- [ ] ProductName = "Produto Desconhecido" → `HasProductName = false`
- [ ] Brand = null → `HasBrand = false`
- [ ] Ingredientes = [] → `HasIngredients = false`
- [ ] >50% ingredientes inválidos → `InvalidIngredientsRatio > 0.5`
- [ ] Nutrition null → `HasNutritionalInfo = false`
- [ ] <3 campos nutricionais → `HasMinimalNutritionalData = false`

### Validação de Quality Gate
- [ ] OCR Low + Parsing Partial → `FinalConfidence = Baixo`
- [ ] ProductName desconhecido → `Classification = Incomplete`
- [ ] Confidence != Alto + Classification = Safe → `Classification = Caution`
- [ ] Penalização no score aplicada corretamente
- [ ] Summary coerente com confiança e classificação
- [ ] ShortSummary coerente com summary
- [ ] Alert adicionado quando quality gate falha

### Validação de Persistência
- [ ] Valores ajustados salvos no banco
- [ ] Enum `AnalysisClassification.Incomplete` funciona
- [ ] Alerts incluem mensagem do quality gate

---

## 📝 Resultados Esperados

| Cenário | OCR Quality | Parsing Completeness | Final Confidence | Final Classification | Score Penalty |
|---------|-------------|----------------------|------------------|---------------------|---------------|
| Baixa qualidade | VeryLow | Incomplete | Baixo | Incomplete | ~70% |
| Sem nome produto | High | Partial | Médio | Incomplete | ~15% |
| Parsing parcial | Medium | Partial | Médio | Caution | ~25% |
| Alta qualidade | High | Complete | Alto | Safe/Excellent | 0% |
| Alérgenos incompleto | Low | Partial | Baixo | Caution | ~50% |

---

## 🐛 Troubleshooting

### "Classification enum not recognized"
**Solução:** Certifique-se que os novos valores (`Incomplete`, `Moderate`, `Avoid`, `Excellent`) foram adicionados ao enum.

### "Quality Gate não está sendo aplicado"
**Solução:** Verifique os logs do console. Deve aparecer:
```
🎯 [QUALITY GATE] Aplicando Quality Gate...
```

### "Score não está sendo penalizado"
**Solução:** Adicione breakpoint em `AdjustScores` e verifique os valores de penalty calculados.

### "Summary ainda aparece otimista"
**Solução:** Verifique se `GenerateCoherentSummary` está sendo chamado e os termos estão sendo substituídos.

---

**Última Atualização:** 2025-01-XX  
**Versão:** 1.0
