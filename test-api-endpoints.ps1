#!/usr/bin/env pwsh

# Script de Teste Rápido dos Endpoints da API LabelWise

$baseUrl = "http://localhost:5000"
$apiUrl = "$baseUrl/api"

Write-Host @"
╔══════════════════════════════════════════════════════════════╗
║          TESTE AUTOMATIZADO DA API LABELWISE                 ║
╚══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Função para exibir resultados
function Show-Result {
    param(
        [string]$Title,
        [object]$Response,
        [bool]$Success
    )
    
    if ($Success) {
        Write-Host "`n✅ $Title" -ForegroundColor Green
        $Response | ConvertTo-Json -Depth 3 | Write-Host -ForegroundColor White
    } else {
        Write-Host "`n❌ $Title" -ForegroundColor Red
        Write-Host $Response -ForegroundColor Red
    }
}

# ========================================
# TESTE 1: Health Check
# ========================================
Write-Host "`n[1/6] 🏥 Verificando Health Check..." -ForegroundColor Yellow

try {
    $healthResponse = Invoke-RestMethod -Uri "$baseUrl/health" -Method GET -TimeoutSec 5
    Show-Result "Health Check OK" $healthResponse $true
} catch {
    Write-Host "⚠️ Endpoint /health não configurado (opcional)" -ForegroundColor Yellow
}

# ========================================
# TESTE 2: Swagger
# ========================================
Write-Host "`n[2/6] 📚 Verificando Swagger..." -ForegroundColor Yellow

try {
    $swaggerResponse = Invoke-WebRequest -Uri "$baseUrl/swagger/index.html" -Method GET -TimeoutSec 5 -UseBasicParsing
    if ($swaggerResponse.StatusCode -eq 200) {
        Write-Host "✅ Swagger disponível em: $baseUrl/swagger" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ Swagger não está acessível" -ForegroundColor Red
}

# ========================================
# TESTE 3: Registrar Usuário
# ========================================
Write-Host "`n[3/6] 👤 Registrando novo usuário..." -ForegroundColor Yellow

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$registerBody = @{
    email = "teste$timestamp@labelwise.com"
    password = "Senha@123"
    name = "Usuario Teste $timestamp"
} | ConvertTo-Json

try {
    $registerResponse = Invoke-RestMethod -Uri "$apiUrl/auth/register" `
        -Method POST `
        -ContentType "application/json" `
        -Body $registerBody `
        -TimeoutSec 10
    
    Show-Result "Usuário Registrado" $registerResponse $true
    $token = $registerResponse.token
    $userId = $registerResponse.userId
    
} catch {
    Show-Result "Erro ao Registrar Usuário" $_.Exception.Message $false
    Write-Host "⚠️ Continuando com usuário existente..." -ForegroundColor Yellow
    
    # Tentar login com usuário teste padrão
    $loginBody = @{
        email = "teste@labelwise.com"
        password = "Senha@123"
    } | ConvertTo-Json
    
    try {
        $loginResponse = Invoke-RestMethod -Uri "$apiUrl/auth/login" `
            -Method POST `
            -ContentType "application/json" `
            -Body $loginBody
        
        $token = $loginResponse.token
        $userId = $loginResponse.userId
    } catch {
        Write-Host "❌ Não foi possível obter token de autenticação" -ForegroundColor Red
        exit 1
    }
}

# ========================================
# TESTE 4: Login
# ========================================
Write-Host "`n[4/6] 🔐 Fazendo Login..." -ForegroundColor Yellow

$loginBody = @{
    email = "teste$timestamp@labelwise.com"
    password = "Senha@123"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$apiUrl/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $loginBody `
        -TimeoutSec 10
    
    Show-Result "Login Realizado" $loginResponse $true
    $token = $loginResponse.token
    
} catch {
    Write-Host "⚠️ Usando token do registro anterior" -ForegroundColor Yellow
}

if (-not $token) {
    Write-Host "❌ Não foi possível obter token JWT" -ForegroundColor Red
    exit 1
}

Write-Host "`n🔑 Token JWT obtido: $($token.Substring(0, 50))..." -ForegroundColor Green

# ========================================
# TESTE 5: Perfil do Usuário
# ========================================
Write-Host "`n[5/6] 👤 Buscando Perfil do Usuário..." -ForegroundColor Yellow

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

try {
    $profileResponse = Invoke-RestMethod -Uri "$apiUrl/profile" `
        -Method GET `
        -Headers $headers `
        -TimeoutSec 10
    
    Show-Result "Perfil do Usuário" $profileResponse $true
    
} catch {
    Show-Result "Erro ao buscar perfil" $_.Exception.Message $false
}

# ========================================
# TESTE 6: Atualizar Perfil
# ========================================
Write-Host "`n[6/6] ✏️ Atualizando Perfil do Usuário..." -ForegroundColor Yellow

$updateProfileBody = @{
    name = "Usuário Atualizado"
    age = 30
    sex = "Male"
    activityLevel = "Moderate"
    dietaryGoal = "WeightLoss"
    healthGoals = @("WeightLoss", "MuscleGain")
    restrictions = @("Lactose")
    allergies = @("Milk")
} | ConvertTo-Json

try {
    $updateProfileResponse = Invoke-RestMethod -Uri "$apiUrl/profile" `
        -Method PUT `
        -Headers $headers `
        -Body $updateProfileBody `
        -TimeoutSec 10
    
    Show-Result "Perfil Atualizado" $updateProfileResponse $true
    
} catch {
    Show-Result "Erro ao atualizar perfil" $_.Exception.Message $false
}

# ========================================
# RESUMO FINAL
# ========================================
Write-Host @"

╔══════════════════════════════════════════════════════════════╗
║                    TESTES CONCLUÍDOS!                        ║
╚══════════════════════════════════════════════════════════════╝

📊 PRÓXIMOS TESTES RECOMENDADOS:

1️⃣  TESTE DE ANÁLISE DE PRODUTO:
   - Upload de imagem de rótulo
   - Processamento via pipeline OCR
   - Análise de ingredientes e nutrientes

2️⃣  TESTE DE HISTÓRICO:
   - Buscar histórico de análises
   - Ver detalhes de análise específica

3️⃣  TESTE DE PERFORMANCE:
   - Tempo de resposta dos endpoints
   - Carga simultânea de requisições

📚 DOCUMENTAÇÃO:
   Swagger UI: $baseUrl/swagger

🔧 FERRAMENTAS RECOMENDADAS:
   - Postman: https://www.postman.com/
   - Insomnia: https://insomnia.rest/
   - REST Client (VS Code Extension)

"@ -ForegroundColor Cyan

Write-Host "✨ Token para uso futuro:" -ForegroundColor Green
Write-Host $token -ForegroundColor White
Write-Host ""
