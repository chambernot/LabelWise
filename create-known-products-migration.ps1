# ═══════════════════════════════════════════════════════════════════════
# CREATE KNOWN PRODUCTS MIGRATION
# ═══════════════════════════════════════════════════════════════════════
# 
# Este script cria a migration para adicionar a tabela known_products
# ao banco de dados PostgreSQL.
#
# IMPORTANTE:
# - Execute este script a partir da raiz do projeto
# - Certifique-se de que o PostgreSQL está rodando
# - A migration será criada no projeto Infrastructure
#
# ═══════════════════════════════════════════════════════════════════════

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🚀 Criando migration para Known Products Catalog" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Caminho do projeto Infrastructure
$infrastructureProject = "LabelWise.Infrastructure\LabelWise.Infrastructure.csproj"

# Nome da migration
$migrationName = "AddKnownProductsCatalog"

Write-Host "📋 Informações:" -ForegroundColor Yellow
Write-Host "   Projeto: $infrastructureProject" -ForegroundColor Gray
Write-Host "   Migration: $migrationName" -ForegroundColor Gray
Write-Host ""

# Verificar se o projeto existe
if (-not (Test-Path $infrastructureProject)) {
    Write-Host "❌ Erro: Projeto Infrastructure não encontrado!" -ForegroundColor Red
    Write-Host "   Caminho esperado: $infrastructureProject" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 Certifique-se de executar este script da raiz do projeto" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Projeto Infrastructure encontrado" -ForegroundColor Green
Write-Host ""

# Criar migration
Write-Host "📦 Criando migration..." -ForegroundColor Cyan
Write-Host ""

try {
    dotnet ef migrations add $migrationName `
        --project $infrastructureProject `
        --startup-project LabelWise.Api\LabelWise.Api.csproj `
        --context ApplicationDbContext `
        --verbose

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
        Write-Host "✅ Migration criada com sucesso!" -ForegroundColor Green
        Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
        Write-Host ""
        Write-Host "📁 Arquivos gerados em:" -ForegroundColor Yellow
        Write-Host "   LabelWise.Infrastructure\Migrations\" -ForegroundColor Gray
        Write-Host ""
        Write-Host "🔍 A migration inclui:" -ForegroundColor Yellow
        Write-Host "   • Tabela: known_products" -ForegroundColor Gray
        Write-Host "   • Índice UNIQUE: barcode" -ForegroundColor Gray
        Write-Host "   • Índice composto: name + brand" -ForegroundColor Gray
        Write-Host "   • Índice GIN: search_text (full-text)" -ForegroundColor Gray
        Write-Host "   • Índice BTREE: search_text (LIKE)" -ForegroundColor Gray
        Write-Host ""
        Write-Host "📊 Estrutura da tabela:" -ForegroundColor Yellow
        Write-Host "   • id (UUID)" -ForegroundColor Gray
        Write-Host "   • name (VARCHAR 200)" -ForegroundColor Gray
        Write-Host "   • brand (VARCHAR 100)" -ForegroundColor Gray
        Write-Host "   • category (VARCHAR 100)" -ForegroundColor Gray
        Write-Host "   • barcode (VARCHAR 50, NULLABLE, UNIQUE)" -ForegroundColor Gray
        Write-Host "   • known_front_text (VARCHAR 1000)" -ForegroundColor Gray
        Write-Host "   • known_ingredients (VARCHAR 2000)" -ForegroundColor Gray
        Write-Host "   • known_allergens (VARCHAR 500)" -ForegroundColor Gray
        Write-Host "   • keywords (VARCHAR 500)" -ForegroundColor Gray
        Write-Host "   • is_validated (BOOLEAN)" -ForegroundColor Gray
        Write-Host "   • identification_count (INTEGER)" -ForegroundColor Gray
        Write-Host "   • last_identified_at (TIMESTAMPTZ)" -ForegroundColor Gray
        Write-Host "   • search_text (VARCHAR 1000)" -ForegroundColor Gray
        Write-Host "   • created_at (TIMESTAMPTZ)" -ForegroundColor Gray
        Write-Host "   • updated_at (TIMESTAMPTZ)" -ForegroundColor Gray
        Write-Host ""
        Write-Host "🚀 Próximo passo: Aplicar a migration" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Execute:" -ForegroundColor Yellow
        Write-Host "   .\apply-known-products-migration.ps1" -ForegroundColor White
        Write-Host ""
    } else {
        Write-Host ""
        Write-Host "❌ Erro ao criar migration!" -ForegroundColor Red
        Write-Host "   Verifique os logs acima para mais detalhes" -ForegroundColor Red
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
