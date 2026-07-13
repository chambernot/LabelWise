# Script de diagnóstico completo do Swagger

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  DIAGNÓSTICO SWAGGER" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar arquivos necessários
Write-Host "[1/5] Verificando arquivos..." -ForegroundColor Yellow
$filterFile = "LabelWise.Api\Swagger\FileUploadOperationFilter.cs"
if (Test-Path $filterFile) {
    Write-Host "   ✓ FileUploadOperationFilter.cs existe" -ForegroundColor Green
} else {
    Write-Host "   ✗ FileUploadOperationFilter.cs NÃO EXISTE!" -ForegroundColor Red
    Write-Host "     Caminho esperado: $filterFile" -ForegroundColor Gray
}
Write-Host ""

# 2. Verificar Program.cs
Write-Host "[2/5] Verificando Program.cs..." -ForegroundColor Yellow
$programFile = "LabelWise.Api\Program.cs"
$programContent = Get-Content $programFile -Raw
if ($programContent -match "FileUploadOperationFilter") {
    Write-Host "   ✓ FileUploadOperationFilter está registrado" -ForegroundColor Green
} else {
    Write-Host "   ✗ FileUploadOperationFilter NÃO está registrado!" -ForegroundColor Red
}
if ($programContent -match "using LabelWise.Api.Swagger") {
    Write-Host "   ✓ using LabelWise.Api.Swagger presente" -ForegroundColor Green
} else {
    Write-Host "   ✗ using LabelWise.Api.Swagger AUSENTE!" -ForegroundColor Red
}
Write-Host ""

# 3. Parar processos
Write-Host "[3/5] Parando processos..." -ForegroundColor Yellow
Get-Process -Name "LabelWise.Api","LabelWise","dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Write-Host "   ✓ Processos parados" -ForegroundColor Green
Write-Host ""

# 4. Build limpo
Write-Host "[4/5] Fazendo build limpo..." -ForegroundColor Yellow
$apiPath = "LabelWise.Api"
Push-Location $apiPath
dotnet clean --verbosity quiet
Remove-Item -Recurse -Force bin,obj -ErrorAction SilentlyContinue
dotnet build --no-incremental --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✓ Build OK" -ForegroundColor Green
} else {
    Write-Host "   ✗ Build FALHOU!" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host ""

# 5. Iniciar e testar
Write-Host "[5/5] Iniciando API..." -ForegroundColor Yellow
Push-Location $apiPath
Write-Host ""
Write-Host "   Iniciando... aguarde 'Application started'" -ForegroundColor Gray
Write-Host ""

$process = Start-Process -FilePath "dotnet" -ArgumentList "run" -PassThru -NoNewWindow

# Aguardar
Start-Sleep -Seconds 10

Write-Host ""
Write-Host "   Testando Swagger..." -ForegroundColor Yellow

$ports = @(7319, 7001, 7018, 5001)
$success = $false

foreach ($port in $ports) {
    try {
        Write-Host "   Tentando porta $port..." -ForegroundColor Gray
        $response = Invoke-WebRequest -Uri "https://localhost:$port/swagger/v1/swagger.json" -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host ""
            Write-Host "========================================" -ForegroundColor Green
            Write-Host "  ✅ SWAGGER FUNCIONANDO!" -ForegroundColor Green
            Write-Host "========================================" -ForegroundColor Green
            Write-Host ""
            Write-Host "Acesse: https://localhost:$port/swagger" -ForegroundColor Cyan
            Start-Process "https://localhost:$port/swagger"
            $success = $true
            break
        }
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 500) {
            Write-Host ""
            Write-Host "========================================" -ForegroundColor Red
            Write-Host "  ✗ ERRO 500 PERSISTE" -ForegroundColor Red
            Write-Host "========================================" -ForegroundColor Red
            Write-Host ""
            Write-Host "Erro detectado na porta $port" -ForegroundColor Gray
            Write-Host ""
            Write-Host "POSSÍVEIS CAUSAS:" -ForegroundColor Yellow
            Write-Host "1. FileUploadOperationFilter não foi compilado corretamente" -ForegroundColor Gray
            Write-Host "2. Program.cs não está usando o filtro" -ForegroundColor Gray
            Write-Host "3. Controller tem anotação incompatível" -ForegroundColor Gray
            Write-Host ""
            Write-Host "SOLUÇÃO:" -ForegroundColor Yellow
            Write-Host "Execute os comandos abaixo para diagnóstico detalhado:" -ForegroundColor Gray
            Write-Host ""
            Write-Host "  Get-Content LabelWise.Api\Program.cs | Select-String 'FileUploadOperationFilter'" -ForegroundColor Cyan
            Write-Host "  Get-Content LabelWise.Api\Swagger\FileUploadOperationFilter.cs | Select-Object -First 20" -ForegroundColor Cyan
            Write-Host ""
            break
        }
    }
}

if (-not $success) {
    Write-Host ""
    Write-Host "Não foi possível detectar a porta automaticamente." -ForegroundColor Yellow
    Write-Host "Verifique os logs acima para encontrar a porta." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Pressione Ctrl+C para parar a API" -ForegroundColor Gray
Write-Host ""

Pop-Location
Wait-Process -Id $process.Id
