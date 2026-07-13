# 🧪 Teste do Parser Melhorado de Rótulos Alimentares

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🧪 TESTE DO PARSER MELHORADO DE RÓTULOS ALIMENTARES" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════════
# TESTE 1: Rótulo Limpo (Sem Tabela Nutricional)
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host "📋 TESTE 1: Rótulo Limpo" -ForegroundColor Green
Write-Host "----------------------------------------" -ForegroundColor Gray

$test1 = @"
Chocolate em Pó
NESTLÉ
INGREDIENTES: cacau, açúcar, leite
CONTÉM: leite, soja
"@

Write-Host "INPUT:" -ForegroundColor Yellow
Write-Host $test1
Write-Host ""

Write-Host "RESULTADO ESPERADO:" -ForegroundColor Yellow
Write-Host "  ProductName: Chocolate em Pó" -ForegroundColor Green
Write-Host "  Brand: NESTLÉ" -ForegroundColor Green
Write-Host "  Ingredients: [cacau, açúcar, leite]" -ForegroundColor Green
Write-Host "  ConfirmedAllergens: [leite, soja]" -ForegroundColor Green
Write-Host "  Confidence: High" -ForegroundColor Green
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════════
# TESTE 2: Rótulo com Tabela Nutricional (Problema Original)
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "📋 TESTE 2: Rótulo com Tabela Nutricional" -ForegroundColor Green
Write-Host "----------------------------------------" -ForegroundColor Gray

$test2 = @"
Biscoito Recheado
BAUDUCCO
INFORMAÇÃO NUTRICIONAL
Porção 30g (3 unidades)
Valor Energético 150 kcal
Carboidratos 20g
Proteínas 3g
Gorduras 5g
%VD 10%
INGREDIENTES: farinha de trigo, açúcar, gordura vegetal
CONTÉM: glúten, leite
"@

Write-Host "INPUT:" -ForegroundColor Yellow
Write-Host $test2
Write-Host ""

Write-Host "RESULTADO ESPERADO:" -ForegroundColor Yellow
Write-Host "  ProductName: Biscoito Recheado" -ForegroundColor Green
Write-Host "  Brand: BAUDUCCO" -ForegroundColor Green
Write-Host "  Ingredients: [farinha de trigo, açúcar, gordura vegetal]" -ForegroundColor Green
Write-Host "  ConfirmedAllergens: [glúten, leite]" -ForegroundColor Green
Write-Host "  Confidence: High" -ForegroundColor Green
Write-Host "  ⚠️  ANTES: ProductName seria 'Porção 30g (3 unidades)' ❌" -ForegroundColor Red
Write-Host "  ✅  AGORA: Tabela nutricional é ignorada completamente" -ForegroundColor Green
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════════
# TESTE 3: Rótulo com Nome Inválido (Apenas Tabela)
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "📋 TESTE 3: Rótulo Sem Nome Válido" -ForegroundColor Green
Write-Host "----------------------------------------" -ForegroundColor Gray

$test3 = @"
INFORMAÇÃO NUTRICIONAL
Porção 30g
Valor Energético 150 kcal
INGREDIENTES: farinha, açúcar
"@

Write-Host "INPUT:" -ForegroundColor Yellow
Write-Host $test3
Write-Host ""

Write-Host "RESULTADO ESPERADO:" -ForegroundColor Yellow
Write-Host "  ProductName: null" -ForegroundColor Yellow
Write-Host "  Brand: null" -ForegroundColor Yellow
Write-Host "  Ingredients: [farinha, açúcar]" -ForegroundColor Green
Write-Host "  Confidence: Medium" -ForegroundColor Yellow
Write-Host "  ValidationWarnings: ['Nenhum nome de produto válido encontrado']" -ForegroundColor Yellow
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════════
# TESTE 4: Rótulo com Ruído de OCR
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "📋 TESTE 4: Rótulo com Ruído de OCR" -ForegroundColor Green
Write-Host "----------------------------------------" -ForegroundColor Gray

$test4 = @"
|||###Choc@late|||
N3STL3
INGREDIENTES: c@cau, açúcar, |eite
CONTÉM: |eite, s0ja
"@

Write-Host "INPUT:" -ForegroundColor Yellow
Write-Host $test4
Write-Host ""

Write-Host "RESULTADO ESPERADO:" -ForegroundColor Yellow
Write-Host "  ProductName: null (ruído excessivo)" -ForegroundColor Yellow
Write-Host "  Brand: null" -ForegroundColor Yellow
Write-Host "  Ingredients: [cacau, açúcar, eite] (limpos)" -ForegroundColor Yellow
Write-Host "  Confidence: Low" -ForegroundColor Yellow
Write-Host "  ValidationWarnings: ['Texto com alto nível de ruído']" -ForegroundColor Yellow
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════════
# TESTE 5: Validação de Nome Inválido (Apenas Números)
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "📋 TESTE 5: Nome Inválido (Apenas Números)" -ForegroundColor Green
Write-Host "----------------------------------------" -ForegroundColor Gray

$test5 = @"
12345
6789
INGREDIENTES: açúcar, farinha
"@

Write-Host "INPUT:" -ForegroundColor Yellow
Write-Host $test5
Write-Host ""

Write-Host "RESULTADO ESPERADO:" -ForegroundColor Yellow
Write-Host "  ProductName: null (apenas números)" -ForegroundColor Yellow
Write-Host "  Brand: null" -ForegroundColor Yellow
Write-Host "  Ingredients: [açúcar, farinha]" -ForegroundColor Green
Write-Host "  Confidence: Medium" -ForegroundColor Yellow
Write-Host "  ValidationWarnings: ['Nenhum nome de produto válido encontrado']" -ForegroundColor Yellow
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════════
# TESTE 6: Múltiplos Alergênicos
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "📋 TESTE 6: Múltiplos Alergênicos" -ForegroundColor Green
Write-Host "----------------------------------------" -ForegroundColor Gray

$test6 = @"
Barra de Cereal
NATURE VALLEY
INGREDIENTES: aveia, mel, amendoim, castanhas
CONTÉM: glúten, amendoim, castanhas
PODE CONTER: leite, soja
"@

Write-Host "INPUT:" -ForegroundColor Yellow
Write-Host $test6
Write-Host ""

Write-Host "RESULTADO ESPERADO:" -ForegroundColor Yellow
Write-Host "  ProductName: Barra de Cereal" -ForegroundColor Green
Write-Host "  Brand: NATURE VALLEY" -ForegroundColor Green
Write-Host "  Ingredients: [aveia, mel, amendoim, castanhas]" -ForegroundColor Green
Write-Host "  ConfirmedAllergens: [glúten, amendoim, castanhas]" -ForegroundColor Green
Write-Host "  MayContainAllergens: [leite, soja]" -ForegroundColor Green
Write-Host "  Confidence: High" -ForegroundColor Green
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════════
# SUMÁRIO DAS MELHORIAS
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ SUMÁRIO DAS MELHORIAS IMPLEMENTADAS" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Host "1. ✅ Detecção e Remoção de Tabela Nutricional" -ForegroundColor Green
Write-Host "   - Ignora completamente blocos com %VD, kcal, g, mg" -ForegroundColor Gray
Write-Host "   - Identifica padrões numéricos de tabela" -ForegroundColor Gray
Write-Host ""

Write-Host "2. ✅ Validação Robusta de Nome de Produto" -ForegroundColor Green
Write-Host "   - Tamanho mínimo de 3 caracteres" -ForegroundColor Gray
Write-Host "   - Não aceita apenas números" -ForegroundColor Gray
Write-Host "   - Máximo 60% de números na linha" -ForegroundColor Gray
Write-Host "   - Máximo 33% de símbolos especiais" -ForegroundColor Gray
Write-Host "   - Deve conter pelo menos uma letra" -ForegroundColor Gray
Write-Host ""

Write-Host "3. ✅ Limpeza de Ingredientes" -ForegroundColor Green
Write-Host "   - Remove caracteres inválidos: | \ / [ ] { }" -ForegroundColor Gray
Write-Host "   - Remove espaços múltiplos" -ForegroundColor Gray
Write-Host ""

Write-Host "4. ✅ Cálculo Dinâmico de Confiança" -ForegroundColor Green
Write-Host "   - Reduz se nome inválido (-30 pontos)" -ForegroundColor Gray
Write-Host "   - Reduz se ingredientes ausentes (-20 pontos)" -ForegroundColor Gray
Write-Host "   - Reduz se alto nível de ruído (-20 pontos)" -ForegroundColor Gray
Write-Host ""

Write-Host "5. ✅ Lista de Warnings de Validação" -ForegroundColor Green
Write-Host "   - 'Nome do produto não identificado'" -ForegroundColor Gray
Write-Host "   - 'Nenhum ingrediente identificado'" -ForegroundColor Gray
Write-Host "   - 'Texto com alto nível de ruído (X%)'" -ForegroundColor Gray
Write-Host ""

Write-Host "6. ✅ Retorna null se Não Houver Evidência Clara" -ForegroundColor Green
Write-Host "   - ProductName = null se inválido" -ForegroundColor Gray
Write-Host "   - Brand = null se não encontrada" -ForegroundColor Gray
Write-Host ""

Write-Host "7. ✅ Classificação de Alergênicos" -ForegroundColor Green
Write-Host "   - ConfirmedAllergens: 'CONTÉM'" -ForegroundColor Gray
Write-Host "   - MayContainAllergens: 'PODE CONTER'" -ForegroundColor Gray
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════════
# COMO EXECUTAR OS TESTES
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🚀 COMO EXECUTAR OS TESTES" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Host "1. Criar Teste Unitário:" -ForegroundColor Yellow
Write-Host "   [Test]" -ForegroundColor Gray
Write-Host "   public void Parse_IgnoresNutritionalTable()" -ForegroundColor Gray
Write-Host "   {" -ForegroundColor Gray
Write-Host "       var parser = new IngredientAllergenParser();" -ForegroundColor Gray
Write-Host "       var result = parser.Parse(ocrText);" -ForegroundColor Gray
Write-Host "       Assert.That(result.ProductName, Is.EqualTo(""Biscoito Recheado""));" -ForegroundColor Gray
Write-Host "       Assert.That(result.ParsingConfidence, Is.EqualTo(ConfidenceLevel.High));" -ForegroundColor Gray
Write-Host "   }" -ForegroundColor Gray
Write-Host ""

Write-Host "2. Testar via API:" -ForegroundColor Yellow
Write-Host "   POST /api/ProductAnalysisPipeline/analyze" -ForegroundColor Gray
Write-Host "   (Upload imagem de rótulo)" -ForegroundColor Gray
Write-Host ""

Write-Host "3. Verificar Logs:" -ForegroundColor Yellow
Write-Host "   Procurar por 'ParsingConfidence' e 'ValidationWarnings' no response" -ForegroundColor Gray
Write-Host ""

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ TESTE PREPARADO - Pronto para Validação!" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
