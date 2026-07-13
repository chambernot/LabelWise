# ═══════════════════════════════════════════════════════════════════════════
# AZURE VISION OCR - SETUP SCRIPT
# ═══════════════════════════════════════════════════════════════════════════
#
# Este script auxilia na configuração do Azure Vision Read OCR no projeto.
#
# USO:
#   .\setup-azure-vision-ocr.ps1
#
# ═══════════════════════════════════════════════════════════════════════════

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  AZURE VISION READ OCR - SETUP WIZARD" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Função para solicitar input do usuário
function Get-UserInput {
    param(
        [string]$Prompt,
        [string]$Default = ""
    )
    
    if ($Default) {
        $input = Read-Host "$Prompt (default: $Default)"
        if ([string]::IsNullOrWhiteSpace($input)) {
            return $Default
        }
        return $input
    }
    else {
        return Read-Host $Prompt
    }
}

# Função para validar URL
function Test-Url {
    param([string]$Url)
    return $Url -match '^https?://.+\.cognitiveservices\.azure\.com/?$'
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 1: VERIFICAR PACOTE NUGET
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "📦 ETAPA 1: Verificando pacotes NuGet..." -ForegroundColor Yellow
Write-Host ""

$infraProject = "LabelWise.Infrastructure\LabelWise.Infrastructure.csproj"

if (-not (Test-Path $infraProject)) {
    Write-Host "❌ Erro: Projeto Infrastructure não encontrado em: $infraProject" -ForegroundColor Red
    Write-Host "   Execute este script na raiz do projeto LabelWise" -ForegroundColor Red
    exit 1
}

# Verificar se o pacote já está instalado
$packagesInstalled = dotnet list $infraProject package | Select-String "Azure.AI.Vision.ImageAnalysis"

if ($packagesInstalled) {
    Write-Host "✅ Pacote Azure.AI.Vision.ImageAnalysis já está instalado" -ForegroundColor Green
}
else {
    Write-Host "⚠️  Pacote Azure.AI.Vision.ImageAnalysis não encontrado" -ForegroundColor Yellow
    $installPackage = Get-UserInput "Deseja instalar o pacote agora? (S/N)" "S"
    
    if ($installPackage -eq "S" -or $installPackage -eq "s") {
        Write-Host "   Instalando pacote..." -ForegroundColor Cyan
        dotnet add $infraProject package Azure.AI.Vision.ImageAnalysis
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Pacote instalado com sucesso!" -ForegroundColor Green
        }
        else {
            Write-Host "❌ Erro ao instalar pacote" -ForegroundColor Red
            exit 1
        }
    }
    else {
        Write-Host "⚠️  Pacote não instalado. Você precisará instalá-lo manualmente:" -ForegroundColor Yellow
        Write-Host "   dotnet add $infraProject package Azure.AI.Vision.ImageAnalysis" -ForegroundColor White
    }
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 2: ESCOLHER PROVIDER
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "🎯 ETAPA 2: Escolher Provider OCR" -ForegroundColor Yellow
Write-Host ""
Write-Host "Escolha o provider que deseja configurar:" -ForegroundColor White
Write-Host "   1. Selector (RECOMENDADO) - Tesseract primeiro, Azure se confiança baixa" -ForegroundColor Cyan
Write-Host "   2. AzureVision - Apenas Azure Vision (máxima qualidade)" -ForegroundColor Cyan
Write-Host "   3. Tesseract - Apenas Tesseract (grátis, local)" -ForegroundColor Cyan
Write-Host ""

$providerChoice = Get-UserInput "Digite sua escolha (1-3)" "1"

$selectedProvider = switch ($providerChoice) {
    "1" { "Selector" }
    "2" { "AzureVision" }
    "3" { "Tesseract" }
    default { "Selector" }
}

Write-Host "✅ Provider selecionado: $selectedProvider" -ForegroundColor Green
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 3: COLETAR CREDENCIAIS AZURE (se necessário)
# ═══════════════════════════════════════════════════════════════════════════

$needsAzure = $selectedProvider -eq "Selector" -or $selectedProvider -eq "AzureVision"
$azureEndpoint = ""
$azureApiKey = ""

if ($needsAzure) {
    Write-Host "☁️  ETAPA 3: Configurar Azure Vision" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Você precisará das credenciais do Azure AI Vision." -ForegroundColor White
    Write-Host "Se ainda não tem um recurso criado:" -ForegroundColor White
    Write-Host "   1. Acesse: https://portal.azure.com" -ForegroundColor Cyan
    Write-Host "   2. Crie um recurso 'Azure AI Vision'" -ForegroundColor Cyan
    Write-Host "   3. Vá em 'Keys and Endpoint' no recurso criado" -ForegroundColor Cyan
    Write-Host ""
    
    $hasAzure = Get-UserInput "Você já tem as credenciais do Azure? (S/N)" "S"
    
    if ($hasAzure -eq "S" -or $hasAzure -eq "s") {
        do {
            $azureEndpoint = Get-UserInput "Azure Endpoint (ex: https://seu-recurso.cognitiveservices.azure.com/)"
            
            if (-not (Test-Url $azureEndpoint)) {
                Write-Host "❌ Endpoint inválido. Deve ser no formato: https://....cognitiveservices.azure.com/" -ForegroundColor Red
            }
        } while (-not (Test-Url $azureEndpoint))
        
        $azureApiKey = Get-UserInput "Azure API Key"
        
        if ([string]::IsNullOrWhiteSpace($azureApiKey)) {
            Write-Host "❌ API Key não pode ser vazia" -ForegroundColor Red
            exit 1
        }
        
        Write-Host "✅ Credenciais Azure configuradas" -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "⚠️  ATENÇÃO: Você precisará configurar as credenciais manualmente" -ForegroundColor Yellow
        Write-Host "   Edite o arquivo: LabelWise.Api\appsettings.json" -ForegroundColor White
        Write-Host "   Seção: OCR:AzureVision:Endpoint e OCR:AzureVision:ApiKey" -ForegroundColor White
        Write-Host ""
        
        $azureEndpoint = "https://YOUR-RESOURCE-NAME.cognitiveservices.azure.com/"
        $azureApiKey = "YOUR-API-KEY-HERE"
    }
    
    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 4: CONFIGURAR THRESHOLD (se Selector)
# ═══════════════════════════════════════════════════════════════════════════

$confidenceThreshold = 0.85

if ($selectedProvider -eq "Selector") {
    Write-Host "🎚️  ETAPA 4: Configurar Threshold de Confiança" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "O threshold define quando usar Azure como fallback." -ForegroundColor White
    Write-Host "Se Tesseract retornar confiança < threshold, usa Azure." -ForegroundColor White
    Write-Host ""
    Write-Host "Valores recomendados:" -ForegroundColor White
    Write-Host "   0.90 (90%) - Usa Azure com mais frequência (maior custo, maior qualidade)" -ForegroundColor Cyan
    Write-Host "   0.85 (85%) - Balanceado (RECOMENDADO)" -ForegroundColor Green
    Write-Host "   0.75 (75%) - Usa Azure apenas em casos críticos (menor custo)" -ForegroundColor Cyan
    Write-Host ""
    
    $thresholdInput = Get-UserInput "Threshold (0.0 - 1.0)" "0.85"
    
    try {
        $confidenceThreshold = [double]$thresholdInput
        
        if ($confidenceThreshold -lt 0 -or $confidenceThreshold -gt 1) {
            Write-Host "⚠️  Valor fora do range. Usando default: 0.85" -ForegroundColor Yellow
            $confidenceThreshold = 0.85
        }
        else {
            Write-Host "✅ Threshold configurado: $($confidenceThreshold.ToString("P0"))" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "⚠️  Valor inválido. Usando default: 0.85" -ForegroundColor Yellow
        $confidenceThreshold = 0.85
    }
    
    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 5: GERAR CONFIGURAÇÃO JSON
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "📝 ETAPA 5: Gerando configuração..." -ForegroundColor Yellow
Write-Host ""

$ocrConfig = @{
    OCR = @{
        Provider = $selectedProvider
        UseMockProvider = $false
        TessdataPath = $null
        Language = "por+eng"
        ValidateOnStartup = $true
    }
}

if ($needsAzure) {
    $ocrConfig.OCR.AzureVision = @{
        Endpoint = $azureEndpoint
        ApiKey = $azureApiKey
        Language = "pt"
        TimeoutSeconds = 30
        EnableDetailedLogging = $false
        ValidateOnStartup = $false
    }
}

if ($selectedProvider -eq "Selector") {
    $ocrConfig.OCR.Selector = @{
        UseAzureWhenTesseractConfidenceBelow = $confidenceThreshold
        AlwaysExecuteBoth = $false
    }
}

# Converter para JSON
$jsonConfig = $ocrConfig | ConvertTo-Json -Depth 10

# Salvar em arquivo temporário
$tempConfigFile = "ocr-config-temp.json"
$jsonConfig | Out-File -FilePath $tempConfigFile -Encoding UTF8

Write-Host "✅ Configuração gerada!" -ForegroundColor Green
Write-Host ""
Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "CONFIGURAÇÃO GERADA:" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host $jsonConfig -ForegroundColor White
Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 6: ATUALIZAR appsettings.json
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "💾 ETAPA 6: Atualizar appsettings.json" -ForegroundColor Yellow
Write-Host ""

$appsettingsPath = "LabelWise.Api\appsettings.json"

if (-not (Test-Path $appsettingsPath)) {
    Write-Host "❌ Erro: appsettings.json não encontrado em: $appsettingsPath" -ForegroundColor Red
    Write-Host "   Execute este script na raiz do projeto" -ForegroundColor Red
    exit 1
}

$updateAppsettings = Get-UserInput "Deseja atualizar automaticamente o appsettings.json? (S/N)" "S"

if ($updateAppsettings -eq "S" -or $updateAppsettings -eq "s") {
    try {
        # Ler appsettings existente
        $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
        
        # Atualizar seção OCR
        $appsettings.OCR = $ocrConfig.OCR
        
        # Salvar (com backup)
        $backupPath = "$appsettingsPath.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        Copy-Item $appsettingsPath $backupPath
        
        $appsettings | ConvertTo-Json -Depth 10 | Out-File -FilePath $appsettingsPath -Encoding UTF8
        
        Write-Host "✅ appsettings.json atualizado com sucesso!" -ForegroundColor Green
        Write-Host "   Backup salvo em: $backupPath" -ForegroundColor Cyan
    }
    catch {
        Write-Host "❌ Erro ao atualizar appsettings.json: $_" -ForegroundColor Red
        Write-Host "   Você precisará atualizar manualmente" -ForegroundColor Yellow
    }
}
else {
    Write-Host "⚠️  appsettings.json não foi atualizado automaticamente" -ForegroundColor Yellow
    Write-Host "   Copie a configuração acima e cole na seção OCR do arquivo:" -ForegroundColor White
    Write-Host "   $appsettingsPath" -ForegroundColor Cyan
}

Write-Host ""

# Limpar arquivo temporário
if (Test-Path $tempConfigFile) {
    Remove-Item $tempConfigFile
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 7: VALIDAR SETUP
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "✅ ETAPA 7: Validar Setup" -ForegroundColor Yellow
Write-Host ""

$validate = Get-UserInput "Deseja validar o setup agora (executar a API)? (S/N)" "S"

if ($validate -eq "S" -or $validate -eq "s") {
    Write-Host "🚀 Iniciando API..." -ForegroundColor Cyan
    Write-Host ""
    
    Push-Location "LabelWise.Api"
    
    try {
        # Compilar
        Write-Host "Compilando projeto..." -ForegroundColor Cyan
        dotnet build --configuration Debug
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Compilação bem-sucedida" -ForegroundColor Green
            Write-Host ""
            Write-Host "Iniciando API (pressione Ctrl+C para parar)..." -ForegroundColor Cyan
            Write-Host ""
            
            # Executar API
            dotnet run
        }
        else {
            Write-Host "❌ Erro na compilação" -ForegroundColor Red
        }
    }
    finally {
        Pop-Location
    }
}

# ═══════════════════════════════════════════════════════════════════════════
# FINALIZAÇÃO
# ═══════════════════════════════════════════════════════════════════════════

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SETUP COMPLETO!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 RESUMO DA CONFIGURAÇÃO:" -ForegroundColor Yellow
Write-Host "   Provider: $selectedProvider" -ForegroundColor White

if ($selectedProvider -eq "Selector") {
    Write-Host "   Threshold: $($confidenceThreshold.ToString("P0"))" -ForegroundColor White
}

if ($needsAzure) {
    Write-Host "   Azure Endpoint: $azureEndpoint" -ForegroundColor White
    Write-Host "   Azure API Key: ****$(if($azureApiKey.Length -gt 4){$azureApiKey.Substring($azureApiKey.Length - 4)})" -ForegroundColor White
}

Write-Host ""
Write-Host "📚 PRÓXIMOS PASSOS:" -ForegroundColor Yellow
Write-Host "   1. Inicie a API: cd LabelWise.Api && dotnet run" -ForegroundColor Cyan
Write-Host "   2. Faça upload de uma imagem via Swagger: http://localhost:5000/swagger" -ForegroundColor Cyan
Write-Host "   3. Verifique os logs para ver qual provider foi usado" -ForegroundColor Cyan
Write-Host ""
Write-Host "📖 DOCUMENTAÇÃO:" -ForegroundColor Yellow
Write-Host "   - AZURE_VISION_READ_IMPLEMENTATION.md" -ForegroundColor Cyan
Write-Host "   - AZURE_VISION_USAGE_EXAMPLES.cs" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ Setup concluído com sucesso!" -ForegroundColor Green
Write-Host ""
