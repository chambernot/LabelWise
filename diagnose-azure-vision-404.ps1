# ============================================================================
# Script de Diagnóstico: Azure OpenAI Vision 404 Error
# ============================================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Azure OpenAI Vision - Diagnóstico" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Função para ler configuração do appsettings.json
function Get-AzureOpenAiConfig {
    $appsettingsPath = ".\LabelWise.Api\appsettings.Development.json"
    
    if (-not (Test-Path $appsettingsPath)) {
        $appsettingsPath = ".\LabelWise.Api\appsettings.json"
    }
    
    if (-not (Test-Path $appsettingsPath)) {
        Write-Host "❌ Arquivo appsettings.json não encontrado!" -ForegroundColor Red
        return $null
    }
    
    $config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
    return $config.AzureOpenAiVision
}

# Ler configuração
Write-Host "📋 Lendo configuração do appsettings.json..." -ForegroundColor Yellow
$config = Get-AzureOpenAiConfig

if ($null -eq $config) {
    Write-Host "❌ Não foi possível ler a configuração!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Configuração encontrada:" -ForegroundColor Green
Write-Host "   Endpoint: $($config.Endpoint)" -ForegroundColor White
Write-Host "   Deployment: $($config.VisionDeployment)" -ForegroundColor White
Write-Host "   API Key: $($config.ApiKey.Substring(0, 10))..." -ForegroundColor White
Write-Host ""

# Validar Endpoint
Write-Host "🔍 Validando Endpoint..." -ForegroundColor Yellow
$endpoint = $config.Endpoint

if ($endpoint -match "openai\.azure\.com/$") {
    Write-Host "   ✅ Formato do endpoint está correto" -ForegroundColor Green
} elseif ($endpoint -match "openai\.azure\.com$") {
    Write-Host "   ⚠️  Endpoint sem '/' no final (será corrigido automaticamente)" -ForegroundColor Yellow
} elseif ($endpoint -match "/openai/") {
    Write-Host "   ❌ Endpoint contém path '/openai/' - REMOVA ISSO!" -ForegroundColor Red
    Write-Host "      Formato correto: https://seu-recurso.openai.azure.com/" -ForegroundColor White
} else {
    Write-Host "   ❌ Formato do endpoint parece incorreto" -ForegroundColor Red
    Write-Host "      Esperado: https://[resource-name].openai.azure.com/" -ForegroundColor White
}

# Validar Deployment Name
Write-Host ""
Write-Host "🔍 Validando Deployment Name..." -ForegroundColor Yellow
$deployment = $config.VisionDeployment

if ([string]::IsNullOrWhiteSpace($deployment)) {
    Write-Host "   ❌ Deployment name está vazio!" -ForegroundColor Red
} else {
    Write-Host "   ✅ Deployment name: '$deployment'" -ForegroundColor Green
    
    # Verificar modelos conhecidos que suportam visão
    $visionModels = @("gpt-4o", "gpt-4-turbo", "gpt-4-vision-preview", "gpt-4-turbo-vision")
    
    $isKnownVisionModel = $false
    foreach ($model in $visionModels) {
        if ($deployment -like "*$model*") {
            $isKnownVisionModel = $true
            break
        }
    }
    
    if ($isKnownVisionModel) {
        Write-Host "   ✅ O nome do deployment sugere suporte a visão" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  O nome do deployment não corresponde aos modelos conhecidos com visão" -ForegroundColor Yellow
        Write-Host "      Modelos que suportam visão: gpt-4o, gpt-4-turbo, gpt-4-vision-preview" -ForegroundColor White
    }
}

# Validar API Key
Write-Host ""
Write-Host "🔍 Validando API Key..." -ForegroundColor Yellow
$apiKey = $config.ApiKey

if ([string]::IsNullOrWhiteSpace($apiKey)) {
    Write-Host "   ❌ API Key está vazia!" -ForegroundColor Red
} elseif ($apiKey.Length -lt 20) {
    Write-Host "   ❌ API Key parece muito curta (pode estar incompleta)" -ForegroundColor Red
} else {
    Write-Host "   ✅ API Key parece válida (comprimento: $($apiKey.Length) caracteres)" -ForegroundColor Green
}

# Testar conectividade
Write-Host ""
Write-Host "🌐 Testando conectividade com o endpoint..." -ForegroundColor Yellow

$baseEndpoint = $endpoint.TrimEnd('/')
$testUrl = "$baseEndpoint/openai/deployments?api-version=2023-05-15"

try {
    $headers = @{
        "api-key" = $config.ApiKey
    }
    
    $response = Invoke-WebRequest -Uri $testUrl -Headers $headers -Method GET -ErrorAction Stop
    Write-Host "   ✅ Conectividade OK (Status: $($response.StatusCode))" -ForegroundColor Green
    
    # Tentar listar deployments
    $deployments = ($response.Content | ConvertFrom-Json).data
    
    if ($deployments.Count -gt 0) {
        Write-Host ""
        Write-Host "📋 Deployments disponíveis no seu recurso Azure OpenAI:" -ForegroundColor Cyan
        foreach ($dep in $deployments) {
            $depName = $dep.id
            if ($depName -eq $config.VisionDeployment) {
                Write-Host "   ✅ $depName (CONFIGURADO)" -ForegroundColor Green
            } else {
                Write-Host "   - $depName" -ForegroundColor White
            }
        }
        
        # Verificar se o deployment configurado existe
        $deploymentExists = $deployments.id -contains $config.VisionDeployment
        
        if (-not $deploymentExists) {
            Write-Host ""
            Write-Host "   ❌ O deployment '$($config.VisionDeployment)' NÃO FOI ENCONTRADO!" -ForegroundColor Red
            Write-Host "      Deployments disponíveis: $($deployments.id -join ', ')" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "   💡 SOLUÇÃO: Atualize o appsettings.json com um dos deployments acima" -ForegroundColor Cyan
        }
    }
    
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    
    if ($statusCode -eq 404) {
        Write-Host "   ❌ Erro 404 - Recurso não encontrado" -ForegroundColor Red
        Write-Host "      Possíveis causas:" -ForegroundColor Yellow
        Write-Host "      1. Endpoint está incorreto" -ForegroundColor White
        Write-Host "      2. Recurso Azure OpenAI não existe ou foi deletado" -ForegroundColor White
        Write-Host "      3. API version não suportada" -ForegroundColor White
    } elseif ($statusCode -eq 401) {
        Write-Host "   ❌ Erro 401 - Não autorizado" -ForegroundColor Red
        Write-Host "      Possíveis causas:" -ForegroundColor Yellow
        Write-Host "      1. API Key está incorreta" -ForegroundColor White
        Write-Host "      2. API Key expirou" -ForegroundColor White
    } elseif ($statusCode -eq 403) {
        Write-Host "   ❌ Erro 403 - Proibido" -ForegroundColor Red
        Write-Host "      Possíveis causas:" -ForegroundColor Yellow
        Write-Host "      1. Sem permissões no recurso" -ForegroundColor White
        Write-Host "      2. Firewall bloqueando o acesso" -ForegroundColor White
    } else {
        Write-Host "   ❌ Erro: $statusCode - $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Resumo e Recomendações
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "📊 RESUMO DO DIAGNÓSTICO" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Para corrigir o erro 404, verifique:" -ForegroundColor Yellow
Write-Host "1. ✅ O endpoint está no formato correto: https://[resource].openai.azure.com/" -ForegroundColor White
Write-Host "2. ✅ O deployment name corresponde exatamente ao nome no Azure Portal" -ForegroundColor White
Write-Host "3. ✅ A API Key está correta e ativa" -ForegroundColor White
Write-Host "4. ✅ O deployment usa um modelo que suporta visão (gpt-4o, gpt-4-turbo)" -ForegroundColor White
Write-Host ""

Write-Host "🔗 Links úteis:" -ForegroundColor Cyan
Write-Host "   - Azure Portal: https://portal.azure.com" -ForegroundColor White
Write-Host "   - Azure OpenAI Studio: https://oai.azure.com" -ForegroundColor White
Write-Host "   - Documentação: FIX_AZURE_OPENAI_VISION_404_ERROR.md" -ForegroundColor White
Write-Host ""

Write-Host "💡 Próximos passos:" -ForegroundColor Yellow
Write-Host "   1. Acesse o Azure Portal" -ForegroundColor White
Write-Host "   2. Vá para seu recurso Azure OpenAI" -ForegroundColor White
Write-Host "   3. Clique em 'Model deployments'" -ForegroundColor White
Write-Host "   4. Confirme que existe um deployment com o nome que você configurou" -ForegroundColor White
Write-Host "   5. Se não existir, crie um ou atualize o appsettings.json" -ForegroundColor White
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Diagnóstico concluído!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
