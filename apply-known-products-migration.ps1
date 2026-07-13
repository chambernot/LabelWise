# ═══════════════════════════════════════════════════════════════════════
# APPLY KNOWN PRODUCTS MIGRATION
# ═══════════════════════════════════════════════════════════════════════
# 
# Este script aplica a migration para criar a tabela known_products
# no banco de dados PostgreSQL.
#
# PRÉ-REQUISITOS:
# - PostgreSQL rodando (porta 5432)
# - Migration criada (execute create-known-products-migration.ps1 primeiro)
# - Banco labelwise_db existente
#
# ═══════════════════════════════════════════════════════════════════════

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🚀 Aplicando migration Known Products Catalog" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Verificar PostgreSQL
Write-Host "🔍 Verificando PostgreSQL..." -ForegroundColor Yellow

$pgRunning = $false
try {
    $pgProcess = Get-Process postgres -ErrorAction SilentlyContinue
    if ($pgProcess) {
        $pgRunning = $true
        Write-Host "✅ PostgreSQL está rodando (PID: $($pgProcess[0].Id))" -ForegroundColor Green
    }
}
catch {
    Write-Host "⚠️  Não foi possível verificar status do PostgreSQL" -ForegroundColor Yellow
}

Write-Host ""

# Aplicar migration
Write-Host "📦 Aplicando migration ao banco de dados..." -ForegroundColor Cyan
Write-Host ""

try {
    dotnet ef database update `
        --project LabelWise.Infrastructure\LabelWise.Infrastructure.csproj `
        --startup-project LabelWise.Api\LabelWise.Api.csproj `
        --context ApplicationDbContext `
        --verbose

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
        Write-Host "✅ Migration aplicada com sucesso!" -ForegroundColor Green
        Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
        Write-Host ""
        Write-Host "🎉 Tabela created:" -ForegroundColor Yellow
        Write-Host "   • known_products" -ForegroundColor Gray
        Write-Host ""
        Write-Host "📊 Índices criados:" -ForegroundColor Yellow
        Write-Host "   • idx_known_products_barcode (UNIQUE)" -ForegroundColor Gray
        Write-Host "   • idx_known_products_name_brand (BTREE)" -ForegroundColor Gray
        Write-Host "   • idx_known_products_category (BTREE)" -ForegroundColor Gray
        Write-Host "   • idx_known_products_validated (BTREE)" -ForegroundColor Gray
        Write-Host "   • idx_known_products_identification_count (BTREE)" -ForegroundColor Gray
        Write-Host "   • idx_known_products_search_text_gin (GIN)" -ForegroundColor Gray
        Write-Host "   • idx_known_products_search_text_like (BTREE)" -ForegroundColor Gray
        Write-Host ""
        Write-Host "🚀 Próximos passos:" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "1. Popular catálogo com produtos conhecidos:" -ForegroundColor Yellow
        Write-Host "   .\seed-known-products.ps1" -ForegroundColor White
        Write-Host ""
        Write-Host "2. Testar busca de produtos:" -ForegroundColor Yellow
        Write-Host "   .\test-known-products-search.ps1" -ForegroundColor White
        Write-Host ""
        Write-Host "3. Iniciar API e testar integração:" -ForegroundColor Yellow
        Write-Host "   .\run-api.ps1" -ForegroundColor White
        Write-Host ""
    } else {
        Write-Host ""
        Write-Host "❌ Erro ao aplicar migration!" -ForegroundColor Red
        Write-Host ""
        Write-Host "💡 Possíveis causas:" -ForegroundColor Yellow
        Write-Host "   • PostgreSQL não está rodando" -ForegroundColor Gray
        Write-Host "   • String de conexão incorreta" -ForegroundColor Gray
        Write-Host "   • Banco labelwise_db não existe" -ForegroundColor Gray
        Write-Host "   • Migration não foi criada" -ForegroundColor Gray
        Write-Host ""
        Write-Host "🔧 Para resolver:" -ForegroundColor Yellow
        Write-Host "   1. Inicie o PostgreSQL: .\start-postgres.bat" -ForegroundColor White
        Write-Host "   2. Crie a migration: .\create-known-products-migration.ps1" -ForegroundColor White
        Write-Host "   3. Tente novamente: .\apply-known-products-migration.ps1" -ForegroundColor White
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

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ Script concluído!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
