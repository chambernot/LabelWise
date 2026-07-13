# Copilot Instructions

## Diretrizes de projeto
- O pós-processamento nutricional deve ser calibrado de forma genérica, independente de produto específico, evitando heurísticas específicas por categoria quando a regra puder ser baseada em sinais nutricionais e classificações.
- Para a lógica de fallback nutricional, prefira regras genéricas, consistentes e escaláveis, impulsionadas pelo conhecimento do PostgreSQL e sinais nutricionais; evite correções específicas de produtos ou soluções pontuais quando uma regra genérica puder ser utilizada.
- Mantenha a compatibilidade dos endpoints existentes e reaproveite a arquitetura atual (.NET com Controllers, Services, Interfaces e Repositories). A implementação deve ser real, limpa e compilável, evitando pseudo-código.

## Instruções de Análise Nutricional
- Para textos da API de análise nutricional, use linguagem simples e direta em português.
- Explique o motivo do score, incluindo `resumoRapido`, `explicacaoScore` e `pontoPrincipal`.
- Evite termos técnicos e adicione um aviso leve quando a análise for baseada na categoria do produto.
- No projeto LabelWise, use exclusivamente `NutritionScoringServiceV2` como implementação de `INutritionScoringService`.
- No projeto LabelWise, o motor de score nutricional deve evitar supervalorizar produtos proteicos/fitness quando houver açúcar elevado, gordura saturada alta, excesso calórico ou ultraprocessamento; polióis devem ser tratados como parcialmente impactantes, não neutros; ultraprocessados e açúcar adicionado devem ter penalidade relevante; score deve evitar recomendações enganosas quando OCR tiver baixa confiabilidade.

## Mudanças no Motor Semântico de Ingredientes do LabelWise
- Implemente governança semântica estrutural/genérica em vez de correções pontuais de cenário: segmente OCR em regiões contextuais, apenas `IngredientList` alimenta ingredientes/perfil normalizado/semântico, valide alegações regulatórias contra lista branca/lista negra de contexto, limite a confiança pela hierarquia de evidência/confiança e previna que a inferência semântica se torne verdade regulatória.

## ⚠️ ARQUITETURA REFATORADA - FOOD INTELLIGENCE ENGINE V2

### Princípios Fundamentais (OBRIGATÓRIO)
1. **Hierarquia Absoluta de Evidências**: Claims regulatórios (prioridade 100) SEMPRE vencem inferências (prioridade 50)
2. **Separação de Camadas**: OCR (L1) → Semantic Extraction (L2) → Regulatory (L3) → Conflict Resolution (L3.5) → Inference (L4) → Decision (L5)
3. **Centralização de Decisões**: TODAS as decisões finais devem passar pelo `IDecisionEngine`
4. **Detecção de Conflitos**: Conflitos entre evidências devem ser detectados e resolvidos automaticamente
5. **Auditabilidade**: Toda decisão deve ter trail completo de evidências

### Enums Obrigatórios
```csharp
// Use SEMPRE estes enums da nova arquitetura:
EvidencePriority      // Hierarquia de evidências (0-100)
FoodCompatibilityStatus  // Estados bem definidos
RegulatoryClaimType   // Tipos de claims regulatórios
ProcessingLevel       // NOVA classification
AnalysisQuality       // Qualidade da análise
ConflictType          // Tipos de conflito
ConflictSeverity      // Severidade do conflito
```

### Regras de Implementação

#### 1. Claims Regulatórios (Prioridade Máxima)
```csharp
// ✅ CORRETO: Claim regulatório sempre vence
if (regulatoryClaim.Priority == EvidencePriority.RegulatoryClaimExplicit) {
    return regulatoryClaim.Decision;  // Prioridade 100
}

// ❌ ERRADO: Inferência nunca pode sobrescrever claim
if (openAiInference.Confidence > 0.9) {
    return openAiInference.Decision;  // NUNCA fazer isso
}
```

#### 2. Estados de Compatibilidade
```csharp
// ✅ CORRETO: Use FoodCompatibilityStatus
return new ProfileCompatibility {
    Status = FoodCompatibilityStatus.Incompatible,  // Estado claro
    Confidence = 1.0,
    Reasons = ["Claim regulatório: 'CONTÉM GLÚTEN'"]
};

// ❌ ERRADO: Estados contraditórios
return new DietCompatibilityDto {
    Compatible = false,
    CompatibilityStatus = "uncertain"  // Inconsistente!
};
```

#### 3. Processamento NOVA
```csharp
// ✅ CORRETO: Baseado em indicadores estruturais
if (HasUltraProcessedIndicators(ingredients)) {
    return ProcessingLevel.UltraProcessed;  // Baseado em aditivos, não em count
}

// ❌ ERRADO: Baseado apenas em quantidade
if (ingredients.Count > 10) {
    return ProcessingLevel.UltraProcessed;  // Muito simplista
}
```

#### 4. Detecção de Conflitos
```csharp
// ✅ CORRETO: Sempre detectar conflitos
var conflicts = _conflictEngine.DetectConflicts(claims, ingredients, inferences);
var resolutions = _conflictEngine.ResolveConflicts(conflicts);
var quality = _conflictEngine.EvaluateAnalysisQuality(conflicts);

// ❌ ERRADO: Ignorar conflitos
// Não fazer nada quando há contradições
```

#### 5. Threshold de Confiança
```csharp
// ✅ CORRETO: Validar threshold antes de inferir
const double MinimumConfidence = 0.6;
if (inference.Confidence >= MinimumConfidence) {
    // OK, pode adicionar
}

// ❌ ERRADO: Aceitar qualquer inferência
if (aiSuggests) {
    // Adicionar sem validar confiança
}
```

#### 6. Decisão Centralizada
```csharp
// ✅ CORRETO: Uma única fonte de verdade
var decision = await _decisionEngine.MakeDecisionAsync(input);
return new Response {
    Profiles = decision.ProfileCompatibilities,
    Score = decision.NutritionalScore,
    Cards = decision.SummaryCards,  // Tudo vem do mesmo lugar
    Insights = decision.QuickInsights,
    Summary = decision.AssistantSummary
};

// ❌ ERRADO: Múltiplas fontes de verdade
var profiles = _dietEngine.Evaluate(...);
var score = _scoringService.Calculate(...);
var cards = _summaryBuilder.Build(...);  // Podem ser inconsistentes!
```

### Engines Disponíveis

#### Nova Arquitetura (V2 - USAR)
- `IRegulatoryEngine` - Detecção de claims regulatórios
- `IConflictResolutionEngine` - Detecção e resolução de conflitos
- `IDecisionEngine` - Decisão final unificada
- `ProcessingLevelEngine` - Classificação NOVA
- `IngredientKnowledgeBase` - Base de conhecimento alimentar
- `DietProfileEngineV2` - Avaliação de perfis com hierarquia

#### Arquitetura Antiga (DEPRECAR)
- `DietProfileEngine` - Substituído por `DietProfileEngineV2`
- `FoodCompatibilityEngine` - Lógica movida para `DecisionEngine`
- Lógica de score espalhada - Centralizar em `DecisionEngine`

### Exemplo Completo
```csharp
// Fluxo correto end-to-end
public async Task<FoodDecision> AnalyzeFoodAsync(IFormFile image) {
    // L1: OCR
    var ocrResult = await _ocrService.ExtractTextAsync(image);

    // L2: Semantic Extraction
    var ingredients = await _semanticEngine.DetectIngredientsAsync(ocrResult.Text);

    // L3: Regulatory
    var claims = await _regulatoryEngine.DetectClaimsAsync(ocrResult.Text);

    // L4: Inference
    var inferences = await _inferenceEngine.InferIngredientsAsync(context);

    // L3.5: Conflicts
    var conflicts = _conflictEngine.DetectConflicts(claims, ingredients, inferences);
    var resolutions = _conflictEngine.ResolveConflicts(conflicts);
    var quality = _conflictEngine.EvaluateAnalysisQuality(conflicts);

    // L5: Decision (CENTRALIZADA)
    var input = new DecisionInput {
        RegulatoryInformation = claims,
        ExplicitIngredients = ingredients,
        SemanticInferences = inferences,
        Conflicts = conflicts,
        AnalysisQuality = quality
    };

    return await _decisionEngine.MakeDecisionAsync(input);
}
```

### Documentação
Ver documentação completa em:
- `docs/ARCHITECTURE_REFACTORING_FOOD_INTELLIGENCE.md` - Arquitetura detalhada
- `docs/REFACTORING_IMPLEMENTATION_CHECKLIST.md` - Checklist de implementação