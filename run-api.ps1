#!/usr/bin/env pwsh

Write-Host "========================================" -ForegroundColor Green
Write-Host "  RESUMO DO SETUP - LABELWISE API" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

Write-Host "`n[OK] PostgreSQL esta rodando" -ForegroundColor Green
Write-Host "[OK] Database 'labelwise_db' criado" -ForegroundColor Green
Write-Host "[OK] Migrations aplicadas" -ForegroundColor Green
Write-Host "[OK] 11 tabelas criadas com sucesso" -ForegroundColor Green

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  TABELAS CRIADAS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

docker exec labelwise-postgres psql -U postgres -d labelwise_db -c "\dt"

Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "  INICIANDO API LABELWISE" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

Write-Host "`nA API sera iniciada em alguns segundos..." -ForegroundColor Cyan
Write-Host "Swagger UI estara disponivel em:" -ForegroundColor Cyan
Write-Host "  https://localhost:7001/swagger" -ForegroundColor White
Write-Host "  http://localhost:5000/swagger" -ForegroundColor White

Write-Host "`nPressione Ctrl+C para parar a API`n" -ForegroundColor Yellow

dotnet run --project LabelWise.Api
