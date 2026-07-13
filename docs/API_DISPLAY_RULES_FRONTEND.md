# Regras de Exibição de Resultados — Frontend

## Visão Geral

O app realiza **duas análises independentes** ao escanear um produto:

| API | Endpoint | O que analisa |
|---|---|---|
| **Ingredientes** | `POST /food/ingredient-analysis` | Lista de ingredientes, compatibilidade dietética, alergênicos |
| **Nutrição** | `POST /api/nutrition/nutricao-analise-inteligente` | Tabela nutricional, calorias, macros, score |

Ambas são chamadas em paralelo. As regras abaixo definem **quando exibir, ocultar ou alertar** cada resultado.

---

## Regra Geral — Decisão de Exibição

```
┌─────────────────────────────────────────────────────────┐
│              Ingredientes OK?   Nutrição OK?            │
│                                                         │
│  Sim + Sim  →  Exibir ambos os cards completos         │
│  Sim + Não  →  Exibir só ingredientes + aviso nutrição │
│  Não + Sim  →  Exibir só nutrição + aviso ingredientes │
│  Não + Não  →  Não exibir resultado — pedir nova foto  │
└─────────────────────────────────────────────────────────┘
```

---

## Como Determinar "OK" para Cada API

### API de Ingredientes — `food/ingredient-analysis`

A análise é considerada **bem-sucedida** quando **todos** os critérios abaixo são atendidos:

```dart
bool ingredientAnalysisOk(IngredientAnalysisResponse r) {
  // 1. HTTP 200 foi recebido (não 400, 403, 500)
  // 2. Campo success = true
  if (!r.success) return false;

  // 3. Pelo menos um ingrediente OU um claim foi detectado
  final hasIngredients = r.ingredients.raw.isNotEmpty;
  final hasClaims = r.compatibility.allergies.isNotEmpty ||
                    r.alerts.contains.isNotEmpty ||
                    r.alerts.mayContain.isNotEmpty;
  if (!hasIngredients && !hasClaims) return false;

  // 4. Qualidade de imagem não é crítica
  if (r.analysis.imageQuality == 'poor') return false;

  return true;
}
```

**Campos determinantes:**

| Campo | Tipo | Valor que indica insucesso |
|---|---|---|
| HTTP status | `int` | `400`, `403`, `500` |
| `success` | `bool` | `false` |
| `ingredients.raw` | `string[]` | vazio E `alerts` também vazio |
| `analysis.imageQuality` | `string` | `"poor"` ou `"unknown"` |
| `analysis.safeMode` | `bool` | `true` → exibir, mas com aviso de baixa qualidade |
| `analysis.partialReading` | `bool` | `true` → exibir, mas com banner de leitura parcial |

---

### API de Nutrição — `api/nutrition/nutricao-analise-inteligente`

A análise é considerada **bem-sucedida** quando **todos** os critérios abaixo são atendidos:

```dart
bool nutritionAnalysisOk(IntelligentAnalysisResponse r) {
  // 1. HTTP 200 foi recebido
  // 2. Campo success = true
  if (!r.success) return false;

  // 3. Confiança não é criticamente baixa
  if (r.analysisQuality.confidence == 'low') return false;

  // 4. Tabela nutricional estava visível na imagem
  if (!r.imageQuality.tableVisible) return false;

  // 5. Não foi pedido retry por falta de dados
  if (r.imageQuality.retryRequested) return false;

  return true;
}
```

**Campos determinantes:**

| Campo | Tipo | Valor que indica insucesso |
|---|---|---|
| HTTP status | `int` | `400`, `403`, `500` |
| `success` | `bool` | `false` |
| `analysisQuality.confidence` | `string` | `"low"` |
| `imageQuality.tableVisible` | `bool` | `false` |
| `imageQuality.retryRequested` | `bool` | `true` |
| `imageQuality.blurDetected` | `bool` | `true` → exibir com aviso |
| `imageQuality.reflectionDetected` | `bool` | `true` → exibir com aviso |
| `imageQuality.safeForPreciseNutritionAnalysis` | `bool` | `false` → exibir com aviso de estimativa |

---

## Matriz de Decisão Completa

```
ingredientOk | nutritionOk | Ação
─────────────────────────────────────────────────────────────────────────────
    true     |    true     | Exibir ambos os cards normalmente
    true     |    false    | Exibir card de ingredientes + ocultar card de nutrição
             |             | Mostrar mensagem: "Não foi possível ler a tabela nutricional.
             |             | Tente uma foto mais próxima da tabela."
    false    |    true     | Ocultar card de ingredientes + exibir card de nutrição
             |             | Mostrar mensagem: "Não foi possível ler os ingredientes.
             |             | Tente uma foto do verso da embalagem."
    false    |    false    | Não exibir nenhum resultado
             |             | Mostrar tela de retry: "Não conseguimos analisar este produto.
             |             | Tire uma nova foto com boa iluminação e sem reflexo."
```

---

## Comportamentos de Exibição Parcial

Mesmo quando `OK = true`, alguns campos indicam que o resultado deve ser exibido com ressalvas:

### Ingredientes — avisos contextuais

| Condição | UI recomendada |
|---|---|
| `analysis.safeMode == true` | Banner amarelo: "Foto com baixa qualidade. Resultados podem ser imprecisos." + botão "Tirar nova foto" |
| `analysis.partialReading == true` | Aviso sutil: "Lista de ingredientes pode estar incompleta." |
| `analysis.imageQuality == 'fair'` | Ícone de aviso ⚠ ao lado do título do card |
| `analysis.ingredientBlockConfidence < 60` | Texto em cinza nos ingredientes: "Leitura com baixa confiança" |
| `compatibility.*.status == 'attention'` | Badge ⚠ amarelo (compatível com ressalva) |
| `compatibility.*.status == 'unknown'` | Badge ⬜ cinza: "Sem informação suficiente" |

### Nutrição — avisos contextuais

| Condição | UI recomendada |
|---|---|
| `imageQuality.blurDetected == true` | Banner: "Imagem desfocada. Valores podem ser imprecisos." |
| `imageQuality.reflectionDetected == true` | Banner: "Reflexo detectado. Valores podem ser imprecisos." |
| `imageQuality.safeForPreciseNutritionAnalysis == false` | Prefixar valores com "~" (estimado): "~450 kcal" |
| `imageQuality.tablePartiallyObstructed == true` | Aviso: "Tabela parcialmente obstruída. Alguns valores podem estar faltando." |
| `analysisQuality.confidence == 'medium'` | Ícone de aviso ⚠ ao lado do score nutricional |

---

## Tratamento de Erros HTTP

### HTTP 400 — Arquivo inválido (ambas as APIs)

```dart
// Exibir inline, não como tela de erro
showInlineError(r.error);
// Exemplo: "Imagem muito pequena. Envie uma foto mais nítida da embalagem."
```

Não contar como tentativa falha — é erro do usuário, não do servidor.

### HTTP 403 — Acesso negado

```dart
if (r.reason == 'trial_expired') {
  // Redirecionar para tela de upgrade
  navigateTo('/upgrade');
} else {
  showAccessDeniedDialog();
}
```

### HTTP 500 — Erro interno

```dart
// Exibir tela de retry genérica
showRetryScreen("Ocorreu um erro inesperado. Tente novamente.");
// Logar o erro para diagnóstico
logError('api_error_500', endpoint: endpoint);
```

---

## Fluxo Recomendado no App

```dart
Future<void> scanProduct(File image) async {
  showLoadingScreen();

  // Chamar ambas as APIs em paralelo
  final results = await Future.wait([
    ingredientApi.analyze(image),
    nutritionApi.analyze(image),
  ]);

  final ingredientResult = results[0];
  final nutritionResult  = results[1];

  final ingredientOk = ingredientAnalysisOk(ingredientResult);
  final nutritionOk  = nutritionAnalysisOk(nutritionResult);

  hideLoadingScreen();

  if (!ingredientOk && !nutritionOk) {
    // Ambas falharam
    showRetryScreen(
      message: "Não conseguimos analisar este produto.",
      hint: "Tire uma nova foto com boa iluminação, sem reflexos e "
            "com a embalagem plana.",
    );
    return;
  }

  // Montar tela de resultados
  showResultsScreen(
    ingredients: ingredientOk ? ingredientResult : null,
    nutrition:   nutritionOk  ? nutritionResult  : null,
    warnings:    buildWarnings(ingredientResult, nutritionResult),
  );
}

List<String> buildWarnings(
  IngredientAnalysisResponse? ing,
  IntelligentAnalysisResponse? nut,
) {
  final warnings = <String>[];

  if (ing == null)
    warnings.add("Ingredientes não disponíveis. Tente uma foto do verso da embalagem.");

  if (nut == null)
    warnings.add("Tabela nutricional não disponível. Tente uma foto da tabela.");

  if (ing != null && ing.analysis.safeMode)
    warnings.add("Leitura de ingredientes com baixa qualidade. Resultados podem ser imprecisos.");

  if (nut != null && nut.imageQuality.blurDetected)
    warnings.add("Imagem desfocada. Valores nutricionais podem ser imprecisos.");

  return warnings;
}
```

---

## Resumo Visual — Card de Resultado

```
┌─────────────────────────────────────────────────────────┐
│  INGREDIENTES               NUTRIÇÃO                    │
│                                                         │
│  ✅ OK → Exibir card       ✅ OK → Exibir card          │
│  ⚠  safeMode → Banner      ⚠  blur → Banner            │
│  ❌ Falhou → Ocultar card  ❌ Falhou → Ocultar card     │
│             + msg de retry              + msg de retry  │
│                                                         │
│  SE AMBOS FALHAREM:                                     │
│  ┌─────────────────────────────────────────────────┐   │
│  │  Tela de retry com instruções de nova foto      │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

---

## Changelog

| Versão | Data | Mudança |
|---|---|---|
| `1.0` | 2025-07 | Documento inicial — regras de exibição dual-API |
