# ═══════════════════════════════════════════════════════════════════════
# SEED KNOWN PRODUCTS CATALOG
# ═══════════════════════════════════════════════════════════════════════
# 
# Este script popula o catálogo de produtos conhecidos com dados iniciais
# para testes e demonstração.
#
# PRODUTOS INCLUÍDOS:
# - Bebidas (refrigerantes, sucos)
# - Alimentos (biscoitos, achocolatados)
# - Laticínios (iogurtes, leites)
#
# PRÉ-REQUISITOS:
# - Migration aplicada (apply-known-products-migration.ps1)
# - API rodando ou acesso direto ao banco
#
# ═══════════════════════════════════════════════════════════════════════

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🌱 Populando catálogo de produtos conhecidos" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# SQL para inserir produtos conhecidos
$seedSql = @"
-- ═══════════════════════════════════════════════════════════════════════
-- KNOWN PRODUCTS CATALOG - SEED DATA
-- ═══════════════════════════════════════════════════════════════════════

-- Limpar dados existentes (cuidado em produção!)
-- DELETE FROM known_products;

-- ───────────────────────────────────────────────────────────────────────
-- CATEGORIA: Refrigerantes
-- ───────────────────────────────────────────────────────────────────────

INSERT INTO known_products (id, name, brand, category, barcode, keywords, search_text, is_validated, identification_count, created_at)
VALUES 
(gen_random_uuid(), 'Coca-Cola Original', 'Coca-Cola', 'Refrigerante', '7894900011517', 'coca cola refrigerante lata pet garrafa', 'coca-cola original coca-cola refrigerante coca cola refrigerante lata pet garrafa', true, 0, NOW()),
(gen_random_uuid(), 'Coca-Cola Zero', 'Coca-Cola', 'Refrigerante', '7894900530001', 'coca cola zero refrigerante diet sem açúcar', 'coca-cola zero coca-cola refrigerante coca cola zero refrigerante diet sem açúcar', true, 0, NOW()),
(gen_random_uuid(), 'Pepsi Cola', 'Pepsi', 'Refrigerante', '7894900530018', 'pepsi cola refrigerante', 'pepsi cola pepsi refrigerante pepsi cola refrigerante', true, 0, NOW()),
(gen_random_uuid(), 'Guaraná Antarctica', 'Antarctica', 'Refrigerante', '7894900012108', 'guarana antarctica refrigerante', 'guaraná antarctica antarctica refrigerante guarana antarctica refrigerante', true, 0, NOW()),
(gen_random_uuid(), 'Sprite', 'Coca-Cola', 'Refrigerante', '7894900011524', 'sprite refrigerante limão', 'sprite coca-cola refrigerante sprite refrigerante limão', true, 0, NOW());

-- ───────────────────────────────────────────────────────────────────────
-- CATEGORIA: Achocolatados
-- ───────────────────────────────────────────────────────────────────────

INSERT INTO known_products (id, name, brand, category, barcode, keywords, search_text, is_validated, identification_count, created_at)
VALUES 
(gen_random_uuid(), 'Nescau Achocolatado em Pó', 'Nestlé', 'Achocolatado', '7891000100103', 'nescau nestle achocolatado chocolate pó', 'nescau achocolatado em pó nestlé achocolatado nescau nestle achocolatado chocolate pó', true, 0, NOW()),
(gen_random_uuid(), 'Toddy Original', 'Pepsico', 'Achocolatado', '7896007800469', 'toddy achocolatado chocolate pó pepsico', 'toddy original pepsico achocolatado toddy achocolatado chocolate pó pepsico', true, 0, NOW()),
(gen_random_uuid(), 'Chocolate em Pó Italac', 'Italac', 'Achocolatado', '7896051115014', 'italac chocolate pó achocolatado', 'chocolate em pó italac italac achocolatado italac chocolate pó achocolatado', true, 0, NOW());

-- ───────────────────────────────────────────────────────────────────────
-- CATEGORIA: Biscoitos
-- ───────────────────────────────────────────────────────────────────────

INSERT INTO known_products (id, name, brand, category, barcode, keywords, search_text, is_validated, identification_count, created_at)
VALUES 
(gen_random_uuid(), 'Bis Xtra Chocolate', 'Lacta', 'Biscoito', '7622300990060', 'bis biscoito chocolate lacta wafer', 'bis xtra chocolate lacta biscoito bis biscoito chocolate lacta wafer', true, 0, NOW()),
(gen_random_uuid(), 'Oreo Original', 'Mondelez', 'Biscoito', '7622210341112', 'oreo biscoito recheado chocolate', 'oreo original mondelez biscoito oreo biscoito recheado chocolate', true, 0, NOW()),
(gen_random_uuid(), 'Trakinas Morango', 'Mondelez', 'Biscoito', '7622300985929', 'trakinas biscoito morango', 'trakinas morango mondelez biscoito trakinas biscoito morango', true, 0, NOW()),
(gen_random_uuid(), 'Club Social Original', 'Mondelez', 'Biscoito', '7622300991005', 'club social biscoito cream cracker salgado', 'club social original mondelez biscoito club social biscoito cream cracker salgado', true, 0, NOW()),
(gen_random_uuid(), 'Passatempo Recheado Chocolate', 'Nestlé', 'Biscoito', '7891000246313', 'passatempo biscoito recheado chocolate', 'passatempo recheado chocolate nestlé biscoito passatempo biscoito recheado chocolate', true, 0, NOW());

-- ───────────────────────────────────────────────────────────────────────
-- CATEGORIA: Sucos
-- ───────────────────────────────────────────────────────────────────────

INSERT INTO known_products (id, name, brand, category, barcode, keywords, search_text, is_validated, identification_count, created_at)
VALUES 
(gen_random_uuid(), 'Del Valle Laranja', 'Coca-Cola', 'Suco', '7894900011531', 'del valle suco laranja', 'del valle laranja coca-cola suco del valle suco laranja', true, 0, NOW()),
(gen_random_uuid(), 'Ades Original', 'Coca-Cola', 'Bebida de Soja', '7894900011548', 'ades bebida soja', 'ades original coca-cola bebida de soja ades bebida soja', true, 0, NOW()),
(gen_random_uuid(), 'Suco Maguary Laranja', 'Maguary', 'Suco', '7896005800014', 'maguary suco laranja', 'suco maguary laranja maguary suco maguary suco laranja', true, 0, NOW());

-- ───────────────────────────────────────────────────────────────────────
-- CATEGORIA: Laticínios
-- ───────────────────────────────────────────────────────────────────────

INSERT INTO known_products (id, name, brand, category, barcode, keywords, search_text, is_validated, identification_count, created_at)
VALUES 
(gen_random_uuid(), 'Leite Ninho Integral', 'Nestlé', 'Leite', '7891000100110', 'ninho leite integral nestle', 'leite ninho integral nestlé leite ninho leite integral nestle', true, 0, NOW()),
(gen_random_uuid(), 'Leite Condensado Moça', 'Nestlé', 'Leite Condensado', '7891000100127', 'moça leite condensado nestle', 'leite condensado moça nestlé leite condensado moça leite condensado nestle', true, 0, NOW()),
(gen_random_uuid(), 'Iogurte Danone Morango', 'Danone', 'Iogurte', '7891025100515', 'danone iogurte morango', 'iogurte danone morango danone iogurte danone iogurte morango', true, 0, NOW()),
(gen_random_uuid(), 'Yakult Original', 'Yakult', 'Leite Fermentado', '7898919510012', 'yakult leite fermentado probiótico', 'yakult original yakult leite fermentado yakult leite fermentado probiótico', true, 0, NOW());

-- ───────────────────────────────────────────────────────────────────────
-- CATEGORIA: Snacks
-- ───────────────────────────────────────────────────────────────────────

INSERT INTO known_products (id, name, brand, category, barcode, keywords, search_text, is_validated, identification_count, created_at)
VALUES 
(gen_random_uuid(), 'Doritos Queijo', 'Pepsico', 'Salgadinho', '7896028022053', 'doritos salgadinho queijo nacho', 'doritos queijo pepsico salgadinho doritos salgadinho queijo nacho', true, 0, NOW()),
(gen_random_uuid(), 'Ruffles Queijo', 'Pepsico', 'Salgadinho', '7896028013587', 'ruffles salgadinho batata queijo', 'ruffles queijo pepsico salgadinho ruffles salgadinho batata queijo', true, 0, NOW()),
(gen_random_uuid(), 'Cheetos Lua Parmesão', 'Pepsico', 'Salgadinho', '7896028022077', 'cheetos salgadinho parmesão', 'cheetos lua parmesão pepsico salgadinho cheetos salgadinho parmesão', true, 0, NOW());

-- ───────────────────────────────────────────────────────────────────────
-- ESTATÍSTICAS
-- ───────────────────────────────────────────────────────────────────────

SELECT 
    'Total de produtos inseridos: ' || COUNT(*)::TEXT AS summary
FROM known_products;

SELECT 
    category AS categoria,
    COUNT(*) AS quantidade
FROM known_products
GROUP BY category
ORDER BY quantidade DESC;
"@

# Salvar SQL em arquivo temporário
$tempSqlFile = "seed-known-products.sql"
$seedSql | Out-File -FilePath $tempSqlFile -Encoding UTF8

Write-Host "📝 Script SQL gerado: $tempSqlFile" -ForegroundColor Green
Write-Host ""

# Configuração do banco
$dbHost = "localhost"
$dbPort = "5432"
$dbName = "labelwise_db"
$dbUser = "postgres"
$dbPassword = "postgres"

Write-Host "🔌 Conectando ao PostgreSQL..." -ForegroundColor Yellow
Write-Host "   Host: $dbHost`:$dbPort" -ForegroundColor Gray
Write-Host "   Database: $dbName" -ForegroundColor Gray
Write-Host "   User: $dbUser" -ForegroundColor Gray
Write-Host ""

# Executar SQL
Write-Host "🌱 Executando seed..." -ForegroundColor Cyan
Write-Host ""

$env:PGPASSWORD = $dbPassword

try {
    psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -f $tempSqlFile

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
        Write-Host "✅ Seed concluído com sucesso!" -ForegroundColor Green
        Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
        Write-Host ""
        Write-Host "📊 Produtos adicionados:" -ForegroundColor Yellow
        Write-Host "   • 5 Refrigerantes" -ForegroundColor Gray
        Write-Host "   • 3 Achocolatados" -ForegroundColor Gray
        Write-Host "   • 5 Biscoitos" -ForegroundColor Gray
        Write-Host "   • 3 Sucos" -ForegroundColor Gray
        Write-Host "   • 4 Laticínios" -ForegroundColor Gray
        Write-Host "   • 3 Snacks" -ForegroundColor Gray
        Write-Host "   ─────────────────" -ForegroundColor Gray
        Write-Host "   • 23 produtos no total" -ForegroundColor White
        Write-Host ""
        Write-Host "🔍 Produtos disponíveis para busca:" -ForegroundColor Cyan
        Write-Host "   • Coca-Cola, Pepsi, Guaraná Antarctica, Sprite" -ForegroundColor Gray
        Write-Host "   • Nescau, Toddy" -ForegroundColor Gray
        Write-Host "   • Bis, Oreo, Trakinas, Club Social, Passatempo" -ForegroundColor Gray
        Write-Host "   • Del Valle, Ades, Maguary" -ForegroundColor Gray
        Write-Host "   • Leite Ninho, Leite Moça, Danone, Yakult" -ForegroundColor Gray
        Write-Host "   • Doritos, Ruffles, Cheetos" -ForegroundColor Gray
        Write-Host ""
        Write-Host "🚀 Próximo passo: Testar busca" -ForegroundColor Cyan
        Write-Host "   .\test-known-products-search.ps1" -ForegroundColor White
        Write-Host ""
    } else {
        Write-Host ""
        Write-Host "❌ Erro ao executar seed!" -ForegroundColor Red
        Write-Host ""
        Write-Host "💡 Possíveis causas:" -ForegroundColor Yellow
        Write-Host "   • psql não está instalado ou não está no PATH" -ForegroundColor Gray
        Write-Host "   • PostgreSQL não está rodando" -ForegroundColor Gray
        Write-Host "   • Credenciais incorretas" -ForegroundColor Gray
        Write-Host "   • Tabela known_products não existe (migration não aplicada)" -ForegroundColor Gray
        Write-Host ""
        exit 1
    }
}
catch {
    Write-Host ""
    Write-Host "❌ Erro ao executar comando!" -ForegroundColor Red
    Write-Host "   Mensagem: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    exit 1
}
finally {
    # Limpar variável de senha
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    
    # Remover arquivo temporário
    if (Test-Path $tempSqlFile) {
        Remove-Item $tempSqlFile -Force
    }
}

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ Script concluído!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
