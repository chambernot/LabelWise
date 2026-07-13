# ═══════════════════════════════════════════════════════════════════════════
# SCRIPT DE SETUP AUTOMÁTICO DO AZURE COMPUTER VISION OCR
# ═══════════════════════════════════════════════════════════════════════════
# Este script automatiza:
# 1. Verificação de pré-requisitos (Azure CLI)
# 2. Criação do recurso Azure Computer Vision
# 3. Obtenção de endpoint e API key
# 4. Configuração no appsettings.json
# 5. Instalação do pacote NuGet
# 6. Validação da configuração
# ═══════════════════════════════════════════════════════════════════════════

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceName = "labelwise-ocr-cv",
    
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "rg-labelwise",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "brazilsouth",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("F0", "S1")]
    [string]$Sku = "F0",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipAzureCreation,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipPackageInstall
)

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🚀 AZURE COMPUTER VISION OCR - SETUP AUTOMÁTICO" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# FUNÇÃO: Verificar se comando existe
# ═══════════════════════════════════════════════════════════════════════════
function Test-CommandExists {
    param($Command)
    $null = Get-Command $Command -ErrorAction SilentlyContinue
    return $?
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 1: Verificar Pré-requisitos
# ═══════════════════════════════════════════════════════════════════════════
Write-Host "📋 ETAPA 1: Verificando pré-requisitos..." -ForegroundColor Yellow
Write-Host ""

# Verificar Azure CLI
if (!(Test-CommandExists "az")) {
    Write-Host "❌ Azure CLI não encontrado!" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 Instale o Azure CLI:" -ForegroundColor Yellow
    Write-Host "   Windows: winget install Microsoft.AzureCLI" -ForegroundColor White
    Write-Host "   Ou: https://aka.ms/installazurecliwindows" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Host "✅ Azure CLI encontrado" -ForegroundColor Green

# Verificar .NET
if (!(Test-CommandExists "dotnet")) {
    Write-Host "❌ .NET CLI não encontrado!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ .NET CLI encontrado" -ForegroundColor Green
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 2: Criar Recurso Azure (ou pular se já existir)
# ═══════════════════════════════════════════════════════════════════════════
$endpoint = ""
$apiKey = ""

if ($SkipAzureCreation) {
    Write-Host "⏭️  Pulando criação do recurso Azure (--SkipAzureCreation)" -ForegroundColor Yellow
    Write-Host ""
    
    Write-Host "Digite o endpoint do seu recurso Azure:" -ForegroundColor Yellow
    $endpoint = Read-Host "Endpoint"
    
    Write-Host "Digite a API Key do seu recurso Azure:" -ForegroundColor Yellow
    $apiKey = Read-Host "API Key"
    
    Write-Host ""
} else {
    Write-Host "☁️  ETAPA 2: Criando recurso Azure Computer Vision..." -ForegroundColor Yellow
    Write-Host ""
    
    Write-Host "📝 Configuração:" -ForegroundColor Cyan
    Write-Host "   Nome do recurso:    $ResourceName" -ForegroundColor White
    Write-Host "   Grupo de recursos:  $ResourceGroup" -ForegroundColor White
    Write-Host "   Região:             $Location" -ForegroundColor White
    Write-Host "   SKU:                $Sku" -ForegroundColor White
    Write-Host ""
    
    if ($Sku -eq "F0") {
        Write-Host "💰 SKU Selecionado: FREE TIER" -ForegroundColor Green
        Write-Host "   • 5.000 transações/mês GRÁTIS" -ForegroundColor White
        Write-Host "   • 20 chamadas/minuto" -ForegroundColor White
        Write-Host ""
    } else {
        Write-Host "💰 SKU Selecionado: STANDARD (S1)" -ForegroundColor Yellow
        Write-Host "   • $1 USD por 1.000 transações" -ForegroundColor White
        Write-Host "   • 10 chamadas/segundo" -ForegroundColor White
        Write-Host ""
    }
    
    Write-Host "🔐 Fazendo login no Azure..." -ForegroundColor Cyan
    az login --output none
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Falha no login do Azure" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Login realizado com sucesso" -ForegroundColor Green
    Write-Host ""
    
    # Criar grupo de recursos (se não existir)
    Write-Host "📦 Verificando grupo de recursos..." -ForegroundColor Cyan
    $rgExists = az group exists --name $ResourceGroup
    
    if ($rgExists -eq "false") {
        Write-Host "   Criando grupo de recursos: $ResourceGroup" -ForegroundColor Yellow
        az group create --name $ResourceGroup --location $Location --output none
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Falha ao criar grupo de recursos" -ForegroundColor Red
            exit 1
        }
        
        Write-Host "   ✅ Grupo de recursos criado" -ForegroundColor Green
    } else {
        Write-Host "   ✅ Grupo de recursos já existe" -ForegroundColor Green
    }
    
    Write-Host ""
    
    # Verificar se recurso já existe
    Write-Host "🔍 Verificando se recurso já existe..." -ForegroundColor Cyan
    $resourceExists = az cognitiveservices account show `
        --name $ResourceName `
        --resource-group $ResourceGroup `
        --query "name" `
        --output tsv 2>$null
    
    if ($resourceExists) {
        Write-Host "   ℹ️  Recurso já existe: $ResourceName" -ForegroundColor Yellow
        Write-Host "   Usando recurso existente..." -ForegroundColor White
    } else {
        Write-Host "   Criando novo recurso Computer Vision..." -ForegroundColor Yellow
        Write-Host "   ⏱️  Isso pode levar 1-2 minutos..." -ForegroundColor White
        Write-Host ""
        
        az cognitiveservices account create `
            --name $ResourceName `
            --resource-group $ResourceGroup `
            --kind ComputerVision `
            --sku $Sku `
            --location $Location `
            --yes `
            --output none
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Falha ao criar recurso Computer Vision" -ForegroundColor Red
            Write-Host ""
            Write-Host "💡 Possíveis causas:" -ForegroundColor Yellow
            Write-Host "   • Nome do recurso já existe (deve ser único globalmente)" -ForegroundColor White
            Write-Host "   • SKU não disponível na região" -ForegroundColor White
            Write-Host "   • Quota excedida na assinatura" -ForegroundColor White
            Write-Host ""
            exit 1
        }
        
        Write-Host "   ✅ Recurso criado com sucesso!" -ForegroundColor Green
    }
    
    Write-Host ""
    
    # Obter endpoint
    Write-Host "🌐 Obtendo endpoint..." -ForegroundColor Cyan
    $endpoint = az cognitiveservices account show `
        --name $ResourceName `
        --resource-group $ResourceGroup `
        --query "properties.endpoint" `
        --output tsv
    
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($endpoint)) {
        Write-Host "❌ Falha ao obter endpoint" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "   ✅ Endpoint: $endpoint" -ForegroundColor Green
    Write-Host ""
    
    # Obter API key
    Write-Host "🔑 Obtendo API Key..." -ForegroundColor Cyan
    $apiKey = az cognitiveservices account keys list `
        --name $ResourceName `
        --resource-group $ResourceGroup `
        --query "key1" `
        --output tsv
    
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($apiKey)) {
        Write-Host "❌ Falha ao obter API key" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "   ✅ API Key obtida (oculta por segurança)" -ForegroundColor Green
    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 3: Instalar Pacote NuGet
# ═══════════════════════════════════════════════════════════════════════════
if ($SkipPackageInstall) {
    Write-Host "⏭️  Pulando instalação do pacote NuGet (--SkipPackageInstall)" -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Host "📦 ETAPA 3: Instalando pacote NuGet Azure.AI.Vision.ImageAnalysis..." -ForegroundColor Yellow
    Write-Host ""
    
    $projectPath = "LabelWise.Infrastructure\LabelWise.Infrastructure.csproj"
    
    if (!(Test-Path $projectPath)) {
        Write-Host "❌ Projeto não encontrado: $projectPath" -ForegroundColor Red
        Write-Host "   Execute este script na raiz do repositório LabelWise" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "   Instalando Azure.AI.Vision.ImageAnalysis..." -ForegroundColor Cyan
    dotnet add $projectPath package Azure.AI.Vision.ImageAnalysis --version 1.0.0
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Falha ao instalar pacote" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "   ✅ Pacote instalado com sucesso" -ForegroundColor Green
    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 4: Configurar appsettings.json
# ═══════════════════════════════════════════════════════════════════════════
Write-Host "⚙️  ETAPA 4: Configurando appsettings.json..." -ForegroundColor Yellow
Write-Host ""

$appsettingsPath = "LabelWise.Api\appsettings.json"

if (!(Test-Path $appsettingsPath)) {
    Write-Host "❌ appsettings.json não encontrado: $appsettingsPath" -ForegroundColor Red
    exit 1
}

# Ler appsettings.json
$appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json

# Atualizar configuração OCR
$appsettings.OCR.Provider = "Composite"
$appsettings.OCR.Azure.Endpoint = $endpoint
$appsettings.OCR.Azure.ApiKey = $apiKey
$appsettings.OCR.Azure.ValidateOnStartup = $false
$appsettings.OCR.Composite.PrimaryProvider = "AzureComputerVision"
$appsettings.OCR.Composite.FallbackProvider = "Tesseract"
$appsettings.OCR.Composite.ConfidenceThreshold = 0.85

# Salvar appsettings.json
$appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath

Write-Host "✅ appsettings.json atualizado com sucesso" -ForegroundColor Green
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# ETAPA 5: Validar Configuração
# ═══════════════════════════════════════════════════════════════════════════
Write-Host "✔️  ETAPA 5: Validando configuração..." -ForegroundColor Yellow
Write-Host ""

Write-Host "📋 Configuração Final:" -ForegroundColor Cyan
Write-Host "   Provider:             Composite (Azure + Tesseract)" -ForegroundColor White
Write-Host "   Primary Provider:     Azure Computer Vision" -ForegroundColor White
Write-Host "   Fallback Provider:    Tesseract" -ForegroundColor White
Write-Host "   Confidence Threshold: 85%" -ForegroundColor White
Write-Host "   Azure Endpoint:       $endpoint" -ForegroundColor White
Write-Host "   Azure API Key:        $(if ($apiKey) { '✅ Configurada' } else { '❌ Não configurada' })" -ForegroundColor White
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# CONCLUSÃO
# ═══════════════════════════════════════════════════════════════════════════
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "✅ SETUP CONCLUÍDO COM SUCESSO!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""

Write-Host "📚 PRÓXIMOS PASSOS:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1️⃣  Compilar o projeto:" -ForegroundColor Cyan
Write-Host "   dotnet build" -ForegroundColor White
Write-Host ""
Write-Host "2️⃣  Iniciar a API:" -ForegroundColor Cyan
Write-Host "   dotnet run --project LabelWise.Api" -ForegroundColor White
Write-Host ""
Write-Host "3️⃣  Testar o OCR:" -ForegroundColor Cyan
Write-Host "   # Via cURL (Windows PowerShell)" -ForegroundColor White
Write-Host "   curl.exe -X POST http://localhost:5000/api/pipeline/upload `" -ForegroundColor White
Write-Host "     -F 'file=@C:\temp\rotulo.jpg'" -ForegroundColor White
Write-Host ""
Write-Host "4️⃣  Verificar logs:" -ForegroundColor Cyan
Write-Host "   Os logs mostrarão qual provider foi usado (Azure ou Tesseract)" -ForegroundColor White
Write-Host ""

Write-Host "📖 DOCUMENTAÇÃO:" -ForegroundColor Yellow
Write-Host "   Leia: AZURE_OCR_IMPLEMENTATION_GUIDE.md" -ForegroundColor White
Write-Host "   Exemplos: AZURE_OCR_USAGE_EXAMPLES.cs" -ForegroundColor White
Write-Host ""

Write-Host "💰 CUSTOS (SKU: $Sku):" -ForegroundColor Yellow
if ($Sku -eq "F0") {
    Write-Host "   • FREE TIER: 5.000 transações/mês GRÁTIS" -ForegroundColor Green
    Write-Host "   • Após exceder: $1/1.000 transações" -ForegroundColor White
} else {
    Write-Host "   • STANDARD: $1 USD por 1.000 transações" -ForegroundColor White
}
Write-Host ""

Write-Host "🔗 LINKS ÚTEIS:" -ForegroundColor Yellow
Write-Host "   Portal Azure:  https://portal.azure.com" -ForegroundColor White
Write-Host "   Documentação:  https://learn.microsoft.com/azure/ai-services/computer-vision/" -ForegroundColor White
Write-Host "   Pricing:       https://azure.microsoft.com/pricing/details/cognitive-services/computer-vision/" -ForegroundColor White
Write-Host ""

Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🎉 Azure Computer Vision OCR está pronto para uso!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
