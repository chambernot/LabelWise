# ============================================================================
# Script para Listar Deployments do Azure OpenAI
# ============================================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Azure OpenAI - Listar Deployments" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configurações (PREENCHA ESTES VALORES)
$endpoint = "https://aihca.openai.azure.com"
$apiKey = "COLE_SUA_API_KEY_AQUI"

Write-Host "📋 Configuração:" -ForegroundColor Yellow
Write-Host "   Endpoint: $endpoint" -ForegroundColor White
Write-Host "   API Key: $($apiKey.Substring(0, [Math]::Min(10, $apiKey.Length)))..." -ForegroundColor White
Write-Host ""

if ($apiKey -eq "COLE_SUA_API_KEY_AQUI") {
    Write-Host "❌ ERRO: Você precisa configurar a API Key no script!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Edite este arquivo e substitua 'COLE_SUA_API_KEY_AQUI' pela sua API Key real." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Para obter a API Key:" -ForegroundColor Cyan
    Write-Host "1. Acesse: https://portal.azure.com" -ForegroundColor White
    Write-Host "2. Vá para o recurso AIHCA" -ForegroundColor White
    Write-Host "3. Clique em 'Keys and Endpoint'" -ForegroundColor White
    Write-Host "4. Copie KEY 1" -ForegroundColor White
    exit 1
}

# API version
$apiVersion = "2023-05-15"

# URL para listar deployments
$url = "$endpoint/openai/deployments?api-version=$apiVersion"

Write-Host "🌐 Consultando deployments..." -ForegroundColor Yellow
Write-Host "   URL: $url" -ForegroundColor Gray
Write-Host ""

try {
    $headers = @{
        "api-key" = $apiKey
    }
    
    $response = Invoke-RestMethod -Uri $url -Headers $headers -Method GET -ErrorAction Stop
    
    Write-Host "✅ Conexão bem-sucedida!" -ForegroundColor Green
    Write-Host ""
    
    if ($response.data -and $response.data.Count -gt 0) {
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "📋 DEPLOYMENTS DISPONÍVEIS" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        
        $index = 1
        foreach ($deployment in $response.data) {
            $deploymentName = $deployment.id
            $model = $deployment.model
            $status = $deployment.status
            
            Write-Host "[$index] Nome do Deployment: $deploymentName" -ForegroundColor Green
            Write-Host "    Modelo: $model" -ForegroundColor White
            Write-Host "    Status: $status" -ForegroundColor White
            
            # Verificar se é um modelo com visão
            $hasVision = $model -match "gpt-4o|gpt-4-turbo|gpt-4.*vision"
            
            if ($hasVision) {
                Write-Host "    ✅ SUPORTA VISÃO (Multimodal)" -ForegroundColor Green
                Write-Host ""
                Write-Host "    💡 USE ESTE DEPLOYMENT NO appsettings.json:" -ForegroundColor Cyan
                Write-Host '    "VisionDeployment": "' -NoNewline -ForegroundColor Yellow
                Write-Host $deploymentName -NoNewline -ForegroundColor White
                Write-Host '"' -ForegroundColor Yellow
            } else {
                Write-Host "    ⚠️  NÃO suporta visão" -ForegroundColor Yellow
            }
            
            Write-Host ""
            $index++
        }
        
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "📝 RESUMO" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "Total de deployments: $($response.data.Count)" -ForegroundColor White
        
        $visionModels = $response.data | Where-Object { $_.model -match "gpt-4o|gpt-4-turbo|gpt-4.*vision" }
        
        if ($visionModels.Count -gt 0) {
            Write-Host "Deployments com visão: $($visionModels.Count)" -ForegroundColor Green
            Write-Host ""
            Write-Host "✅ Você TEM deployments que suportam visão!" -ForegroundColor Green
            Write-Host ""
            Write-Host "🎯 PRÓXIMO PASSO:" -ForegroundColor Cyan
            Write-Host "   Atualize o appsettings.Development.json com um dos nomes acima." -ForegroundColor White
            Write-Host ""
            Write-Host "   Exemplo:" -ForegroundColor Yellow
            Write-Host '   "VisionDeployment": "' -NoNewline -ForegroundColor Gray
            Write-Host $visionModels[0].id -NoNewline -ForegroundColor White
            Write-Host '"' -ForegroundColor Gray
        } else {
            Write-Host "Deployments com visão: 0" -ForegroundColor Red
            Write-Host ""
            Write-Host "❌ PROBLEMA: Nenhum deployment com visão encontrado!" -ForegroundColor Red
            Write-Host ""
            Write-Host "💡 SOLUÇÃO:" -ForegroundColor Yellow
            Write-Host "   Você precisa criar um deployment com um modelo que suporte visão." -ForegroundColor White
            Write-Host ""
            Write-Host "   Modelos suportados:" -ForegroundColor Cyan
            Write-Host "   - gpt-4o" -ForegroundColor White
            Write-Host "   - gpt-4-turbo" -ForegroundColor White
            Write-Host "   - gpt-4-vision-preview" -ForegroundColor White
            Write-Host ""
            Write-Host "   Como criar:" -ForegroundColor Cyan
            Write-Host "   1. Acesse: https://oai.azure.com" -ForegroundColor White
            Write-Host "   2. Selecione o recurso AIHCA" -ForegroundColor White
            Write-Host "   3. Vá para 'Deployments'" -ForegroundColor White
            Write-Host "   4. Clique em '+ Create new deployment'" -ForegroundColor White
            Write-Host "   5. Escolha gpt-4o e dê um nome (ex: gpt-4o)" -ForegroundColor White
        }
        
    } else {
        Write-Host "⚠️  Nenhum deployment encontrado!" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Isso significa que você precisa criar um deployment primeiro." -ForegroundColor White
        Write-Host ""
        Write-Host "Como criar um deployment:" -ForegroundColor Cyan
        Write-Host "1. Acesse: https://oai.azure.com" -ForegroundColor White
        Write-Host "2. Selecione o recurso AIHCA" -ForegroundColor White
        Write-Host "3. Vá para 'Deployments'" -ForegroundColor White
        Write-Host "4. Clique em '+ Create new deployment'" -ForegroundColor White
        Write-Host "5. Escolha gpt-4o e dê um nome" -ForegroundColor White
    }
    
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    
    Write-Host "❌ ERRO ao consultar deployments" -ForegroundColor Red
    Write-Host ""
    
    if ($statusCode -eq 404) {
        Write-Host "Status: 404 - Not Found" -ForegroundColor Red
        Write-Host ""
        Write-Host "Possíveis causas:" -ForegroundColor Yellow
        Write-Host "1. Endpoint incorreto" -ForegroundColor White
        Write-Host "2. Recurso Azure OpenAI não existe" -ForegroundColor White
        Write-Host "3. API version não suportada" -ForegroundColor White
        Write-Host ""
        Write-Host "Verifique se o endpoint está correto:" -ForegroundColor Cyan
        Write-Host "   Endpoint atual: $endpoint" -ForegroundColor White
        Write-Host "   Formato esperado: https://[resource-name].openai.azure.com" -ForegroundColor Gray
        
    } elseif ($statusCode -eq 401) {
        Write-Host "Status: 401 - Unauthorized" -ForegroundColor Red
        Write-Host ""
        Write-Host "A API Key está INCORRETA ou EXPIROU!" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Como obter a API Key correta:" -ForegroundColor Cyan
        Write-Host "1. Acesse: https://portal.azure.com" -ForegroundColor White
        Write-Host "2. Vá para o recurso AIHCA" -ForegroundColor White
        Write-Host "3. Clique em 'Keys and Endpoint'" -ForegroundColor White
        Write-Host "4. Copie KEY 1 ou KEY 2" -ForegroundColor White
        
    } elseif ($statusCode -eq 403) {
        Write-Host "Status: 403 - Forbidden" -ForegroundColor Red
        Write-Host ""
        Write-Host "Sem permissões para acessar o recurso!" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Verifique:" -ForegroundColor Cyan
        Write-Host "1. Se você tem permissões no recurso Azure" -ForegroundColor White
        Write-Host "2. Se há firewall bloqueando o acesso" -ForegroundColor White
        
    } else {
        Write-Host "Status: $statusCode" -ForegroundColor Red
        Write-Host "Mensagem: $($_.Exception.Message)" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Script concluído" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
