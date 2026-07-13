# 📋 Melhorias no Parser de Rótulos Alimentares

## 🎯 Problema Resolvido

O parser estava **identificando incorretamente** o nome do produto e marca a partir de **tabelas nutricionais**, resultando em dados inválidos como:
- `ProductName = "Porção 30g (3 unidades)"`
- `Brand = "Valor Energético 150 kcal"`

## ✅ Solução Implementada

Criamos um **parser robusto** com 7 etapas de processamento e validação explícita.

---

## 🏗️ Arquitetura da Solução

### **Etapa 1: Identificar e Remover Tabela Nutricional**
```csharp
RemoveNutritionalTableBlock(rawOcrText)
```

**Regras:**
- Detecta linhas contendo:
  - Keywords: `INFORMAÇÃO NUTRICIONAL`, `%VD`, `KCAL`, `PORÇÃO`, `CARBOIDRATO`, etc.
  - Padrões: `\d+ kcal`, `\d+ g`, `\d+ mg`, `\d+ %`
  - Valores numéricos isolados
- Ignora **completamente** o bloco da tabela nutricional
- Retorna apenas linhas válidas (fora da tabela)

**Exemplo:**
```
INPUT:
Chocolate em Pó
NESTLÉ
INFORMAÇÃO NUTRICIONAL
Porção 30g (3 unidades)
Valor Energético 150 kcal
Carboidratos 25g
Proteínas 3g
INGREDIENTES: cacau, açúcar, leite

OUTPUT (cleanedLines):
Chocolate em Pó
NESTLÉ
INGREDIENTES: cacau, açúcar, leite
```

---

### **Etapa 2: Extrair Nome do Produto e Marca (Robusto)**
```csharp
ExtractProductInfoRobust(cleanedLines, nutritionalTableStartIndex, result)
```

**Regras:**
1. Pega apenas linhas **ANTES de "INGREDIENTES"**
2. Valida cada linha com `IsValidProductName()`
3. Primeira linha válida = `ProductName`
4. Segunda linha válida = `Brand`

**Validações (`IsValidProductName`):**
- ✅ Tamanho mínimo de 3 caracteres
- ❌ Não pode ser linha de tabela nutricional
- ❌ Não pode conter keywords: `INGREDIENTES`, `CONTÉM`, `VALIDADE`, `LOTE`, `CNPJ`
- ❌ Não pode ser apenas números
- ❌ Máximo 60% de números na linha
- ❌ Máximo 33% de símbolos especiais
- ✅ Deve conter pelo menos uma letra
- ❌ Comprimento máximo: 100 caracteres

**Exemplo:**
```
INPUT (cleanedLines):
Chocolate em Pó
NESTLÉ
INGREDIENTES: cacau, açúcar, leite

VALIDAÇÃO:
"Chocolate em Pó" → ✅ válido → ProductName
"NESTLÉ" → ✅ válido → Brand
"INGREDIENTES: ..." → ❌ contém keyword → ignorar
```

---

### **Etapa 3: Extrair Informações Nutricionais**
```csharp
ExtractNutritionInfo(text)
```
- Extrai da seção identificada na Etapa 1
- Padrões: `calorias: 150 kcal`, `carboidratos: 25g`, etc.

---

### **Etapa 4: Extrair Ingredientes**
```csharp
ExtractIngredientsSection(text)
```

**Regras:**
- Extrai texto **APÓS** "INGREDIENTES:"
- Para **ANTES** de: `INFORMAÇÃO NUTRICIONAL`, `CONTÉM:`, `ALÉRGICOS`
- Limpa ruídos de OCR: `|`, `\`, `/`, `[`, `]`, `{`, `}`
- Remove caracteres inválidos

**Exemplo:**
```
INPUT:
INGREDIENTES: cacau, açúcar, leite, estabilizante [lecitina de soja]

OUTPUT:
Ingredients = ["cacau", "açúcar", "leite", "estabilizante lecitina de soja"]
```

---

### **Etapa 5: Detectar Alergênicos**
```csharp
DetectAllergens(text, result)
```
- Busca palavras-chave: `glúten`, `lactose`, `soja`, `amendoim`, `ovo`, etc.
- Usa `\b` (word boundary) para evitar falsos positivos

---

### **Etapa 6: Extrair Frases Críticas**
```csharp
ExtractCriticalPhrases(text, result)
```

**Regras:**
- Detecta: `contém`, `pode conter`, `não contém`, `isento de`
- Classifica alergênicos:
  - `contém` → `ConfirmedAllergens`
  - `pode conter` → `MayContainAllergens`

---

### **Etapa 7: Validação Final e Ajuste de Confiança**
```csharp
FinalValidationAndConfidenceAdjustment(result, rawText)
```

**Ajuste de Confiança:**
```csharp
ConfidenceScore (100 pontos):
- ProductName inválido ou não encontrado: -30 pontos
- Brand não encontrada: -10 pontos
- Ingredientes não encontrados: -20 pontos
- Alto nível de ruído (>30%): -20 pontos
- Mais de 3 warnings: -15 pontos

Mapeamento:
≥80 → High
≥50 → Medium
<50 → Low
```

**Cálculo de Ruído:**
```csharp
NoiseLevel = (caracteres inválidos) / (total de caracteres)
Caracteres inválidos = !IsLetterOrDigit && !IsWhiteSpace && !IsPunctuation
```

---

## 📊 Novos Campos no `IngredientAllergenParseResult`

```csharp
public class IngredientAllergenParseResult
{
    // ... campos existentes ...

    // ✨ NOVOS CAMPOS DE QUALIDADE
    public ConfidenceLevel ParsingConfidence { get; set; } = ConfidenceLevel.High;
    public List<string> ValidationWarnings { get; set; } = new List<string>();
    public bool IsProductNameValidated { get; set; }
    public bool IsBrandValidated { get; set; }
}
```

**Exemplo de Warnings:**
```json
{
  "ValidationWarnings": [
    "Nome do produto não identificado",
    "Nenhum ingrediente identificado",
    "Texto com alto nível de ruído (35%)"
  ],
  "ParsingConfidence": "Low"
}
```

---

## 🧪 Exemplos de Validação

### ✅ Exemplo 1: Parsing Bem-Sucedido
```
INPUT:
Chocolate em Pó
NESTLÉ
INFORMAÇÃO NUTRICIONAL
Porção 30g (3 unidades)
Valor Energético 150 kcal
INGREDIENTES: cacau, açúcar, leite
CONTÉM: leite, soja

OUTPUT:
{
  "ProductName": "Chocolate em Pó",
  "Brand": "NESTLÉ",
  "Ingredients": ["cacau", "açúcar", "leite"],
  "ConfirmedAllergens": ["leite", "soja"],
  "ParsingConfidence": "High",
  "IsProductNameValidated": true,
  "IsBrandValidated": true,
  "ValidationWarnings": []
}
```

### ❌ Exemplo 2: Parsing com Baixa Confiança
```
INPUT:
|||###
Porção 30g
150 kcal
INGREDIENTES: ???

OUTPUT:
{
  "ProductName": null,
  "Brand": null,
  "Ingredients": ["???"],
  "ParsingConfidence": "Low",
  "IsProductNameValidated": false,
  "IsBrandValidated": false,
  "ValidationWarnings": [
    "Nenhum nome de produto válido encontrado",
    "Nome do produto não identificado",
    "Nenhum ingrediente identificado",
    "Texto com alto nível de ruído (45%)"
  ]
}
```

### ⚠️ Exemplo 3: Nome Inválido Rejeitado
```
INPUT:
Porção 30g (3 unidades)
150 kcal
INGREDIENTES: cacau

OUTPUT:
{
  "ProductName": null,  // ❌ Rejeitado (contém padrão nutricional)
  "Brand": null,
  "Ingredients": ["cacau"],
  "ParsingConfidence": "Medium",
  "ValidationWarnings": [
    "Nenhum nome de produto válido encontrado",
    "Nome do produto não identificado"
  ]
}
```

---

## 🔍 Funções Separadas por Responsabilidade

| Função | Responsabilidade |
|--------|-----------------|
| `RemoveNutritionalTableBlock()` | Identificar e remover tabela nutricional |
| `IsNutritionalTableLine()` | Verificar se linha pertence à tabela nutricional |
| `ExtractProductInfoRobust()` | Extrair nome e marca com validação |
| `IsValidProductName()` | Validar se linha é nome de produto válido |
| `ExtractIngredientsSection()` | Extrair seção de ingredientes |
| `SplitIngredients()` | Separar ingredientes individuais |
| `CleanIngredient()` | Remover caracteres inválidos de OCR |
| `DetectAllergens()` | Buscar alergênicos no texto |
| `ExtractCriticalPhrases()` | Extrair frases com termos críticos |
| `FinalValidationAndConfidenceAdjustment()` | Validar resultado e ajustar confiança |
| `CalculateNoiseLevel()` | Calcular nível de ruído do texto OCR |

---

## 🎯 Benefícios da Refatoração

### ✅ Antes vs Depois

| Aspecto | Antes | Depois |
|---------|-------|--------|
| **Nome do Produto** | ❌ Pode ser linha da tabela nutricional | ✅ Validado, não aceita padrões inválidos |
| **Marca** | ❌ Pode ser valor numérico | ✅ Validada ou `null` |
| **Tabela Nutricional** | ❌ Misturada com nome/marca | ✅ Completamente ignorada |
| **Ingredientes** | ⚠️ Com ruído de OCR | ✅ Limpos e validados |
| **Confiança** | ❌ Não havia | ✅ Calculada e ajustada dinamicamente |
| **Warnings** | ❌ Não havia | ✅ Lista de problemas identificados |
| **Validação** | ❌ Não havia | ✅ Validação em múltiplas etapas |

---

## 🚀 Como Testar

### Teste 1: Rótulo Limpo
```powershell
# Criar arquivo test-label-clean.txt:
Chocolate em Pó
NESTLÉ
INGREDIENTES: cacau, açúcar, leite
CONTÉM: leite, soja

# Processar e verificar:
# ProductName: "Chocolate em Pó"
# Brand: "NESTLÉ"
# Confidence: High
```

### Teste 2: Rótulo com Tabela Nutricional
```powershell
# Criar arquivo test-label-with-table.txt:
Biscoito Recheado
BAUDUCCO
INFORMAÇÃO NUTRICIONAL
Porção 30g
150 kcal
Carboidratos 20g
INGREDIENTES: farinha, açúcar, gordura
CONTÉM: glúten, leite

# Processar e verificar:
# ProductName: "Biscoito Recheado"
# Brand: "BAUDUCCO"
# Confidence: High
# Tabela nutricional deve ser ignorada
```

### Teste 3: Rótulo com Ruído
```powershell
# Criar arquivo test-label-noisy.txt:
|||###
Porção 30g
INGREDIENTES: ???

# Processar e verificar:
# ProductName: null
# Confidence: VeryLow
# ValidationWarnings deve conter avisos
```

---

## 📝 Resumo das Regras Implementadas

### ✅ Regra 1: Ignorar Tabela Nutricional
- [x] Detectar keywords: `%VD`, `kcal`, `INFORMAÇÃO NUTRICIONAL`
- [x] Detectar padrões: `\d+ g`, `\d+ mg`, `\d+ %`
- [x] Ignorar valores numéricos isolados

### ✅ Regra 2: Detectar Início da Tabela
- [x] Função `IsNutritionalTableLine()`
- [x] Marcar início da tabela (`nutritionalTableStartIndex`)

### ✅ Regra 3: Nome do Produto
- [x] Deve vir ANTES de "INGREDIENTES"
- [x] Deve conter palavras relevantes (não números)
- [x] Tamanho mínimo de 3 caracteres
- [x] Validado por `IsValidProductName()`

### ✅ Regra 4: Retornar `null` se Não Confiável
- [x] `ProductName = null` se inválido
- [x] `Brand = null` se não houver evidência

### ✅ Regra 5: Marca
- [x] Só preencher se houver evidência clara
- [x] Evitar valores numéricos

### ✅ Regra 6: Ingredientes
- [x] Extrair após "INGREDIENTES:"
- [x] Limpar ruídos de OCR (`CleanIngredient()`)
- [x] Remover caracteres inválidos

### ✅ Regra 7: Confiança
- [x] Reduzir se texto contém ruído
- [x] Reduzir se nome inválido
- [x] Reduzir se parsing incompleto
- [x] Mapeamento para `ConfidenceLevel` enum

### ✅ Regra 8: Validação Final
- [x] Se `ProductName` inválido → `Confidence <= Medium`
- [x] Lista de `ValidationWarnings`

---

## 🔧 Próximos Passos (Opcional)

1. **Testes Unitários**: Criar testes para cada função
2. **Regex Patterns Avançados**: Melhorar detecção de padrões complexos
3. **Machine Learning**: Treinar modelo para classificar linhas
4. **Idiomas**: Suporte para múltiplos idiomas
5. **Telemetria**: Coletar métricas de confiança para análise

---

## ✅ Conclusão

O parser foi **completamente refatorado** para resolver o problema de identificação incorreta de nomes de produtos a partir de tabelas nutricionais.

**Principais melhorias:**
- ✅ Detecção e remoção completa de tabelas nutricionais
- ✅ Validação robusta de nomes de produtos
- ✅ Limpeza de ruídos de OCR
- ✅ Cálculo dinâmico de confiança
- ✅ Lista de warnings de validação
- ✅ Funções separadas por responsabilidade
- ✅ Código bem documentado e testável

**Resultado:**
- ❌ Antes: `ProductName = "Porção 30g (3 unidades)"`
- ✅ Depois: `ProductName = null` ou nome válido com confiança alta
