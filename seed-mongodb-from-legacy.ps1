Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🌱 Bootstrap do MongoDB a partir dos seeds legados" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

& dotnet run --project LabelWise.Api\LabelWise.Api.csproj -- --seed-mongo-bootstrap

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Falha ao executar o bootstrap do MongoDB." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "✅ Bootstrap Mongo concluído." -ForegroundColor Green
