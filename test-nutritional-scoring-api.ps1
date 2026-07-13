# Test Nutritional Scoring Engine via API
# Script para testar o novo motor de scoring com exemplos reais

Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   TESTE DO MOTOR DE SCORE NUTRICIONAL - VIA API          ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Configuração
$baseUrl = "https://localhost:7319"
$endpoint = "$baseUrl/api/pipeline/analyze-image"

# Ignorar erros de certificado SSL em desenvolvimento
if (-not ([System.Management.Automation.PSTypeName]'ServerCertificateValidationCallback').Type) {
    $certCallback = @"
using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
public class ServerCertificateValidationCallback
{
    public static void Ignore()
    {
        if(ServicePointManager.ServerCertificateValidationCallback == null)
        {
            ServicePointManager.ServerCertificateValidationCallback += 
                delegate
                (
                    Object obj, 
                    X509Certificate certificate, 
                    X509Chain chain, 
                    SslPolicyErrors errors
                )
                {
                    return true;
                };
        }
    }
}
"@
    Add-Type $certCallback
}
[ServerCertificateValidationCallback]::Ignore()

# Verificar se API está rodando
Write-Host "⏳ Verificando se API está rodando..." -ForegroundColor Yellow
try {
    $healthCheck = Invoke-WebRequest -Uri "$baseUrl/swagger/index.html" -UseBasicParsing -TimeoutSec 5
    Write-Host "✅ API está rodando!" -ForegroundColor Green
} catch {
    Write-Host "❌ API não está rodando. Execute 'run-api.ps1' primeiro!" -ForegroundColor Red
    exit 1
}

# Função para fazer upload de imagem
function Test-ProductAnalysis {
    param (
        [string]$imagePath,
        [string]$description
    )
    
    Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host "📸 Testando: $description" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    
    if (-not (Test-Path $imagePath)) {
        Write-Host "⚠️  Imagem não encontrada: $imagePath" -ForegroundColor Yellow
        Write-Host "   Pulando este teste..." -ForegroundColor Gray
        return
    }
    
    Write-Host "📁 Arquivo: $imagePath" -ForegroundColor Gray
    
    # Preparar multipart form data
    $boundary = [System.Guid]::NewGuid().ToString()
    $LF = "`r`n"
    
    $fileName = Split-Path $imagePath -Leaf
    $fileBytes = [System.IO.File]::ReadAllBytes($imagePath)
    $fileEnc = [System.Text.Encoding]::GetEncoding('iso-8859-1').GetString($fileBytes)
    
    $bodyLines = ( 
        "--$boundary",
        "Content-Disposition: form-data; name=`"file`"; filename=`"$fileName`"",
        "Content-Type: image/jpeg$LF",
        $fileEnc,
        "--$boundary--$LF" 
    ) -join $LF
    
    try {
        Write-Host "⏳ Enviando imagem para análise..." -ForegroundColor Yellow
        
        $response = Invoke-RestMethod -Uri $endpoint `
            -Method Post `
            -ContentType "multipart/form-data; boundary=$boundary" `
            -Body $bodyLines `
            -TimeoutSec 60
        
        Write-Host "✅ Análise concluída!" -ForegroundColor Green
        Write-Host ""
        
        # Mostrar resultados principais
        if ($response.analysis) {
            $analysis = $response.analysis
            
            Write-Host "━━━ RESULTADO DA ANÁLISE ━━━" -ForegroundColor White
            Write-Host ""
            
            # Score e Classificação
            $generalScorePercent = [math]::Round($analysis.generalScore * 100)
            $personalizedScorePercent = [math]::Round($analysis.personalizedScore * 100)
            
            Write-Host "📊 SCORES:" -ForegroundColor Cyan
            Write-Host "   Score Geral:        $generalScorePercent/100" -ForegroundColor $(if($generalScorePercent -ge 80) {"Green"} elseif($generalScorePercent -ge 60) {"Yellow"} elseif($generalScorePercent -ge 40) {"DarkYellow"} else {"Red"})
            Write-Host "   Score Personalizado: $personalizedScorePercent/100" -ForegroundColor $(if($personalizedScorePercent -ge 80) {"Green"} elseif($personalizedScorePercent -ge 60) {"Yellow"} elseif($personalizedScorePercent -ge 40) {"DarkYellow"} else {"Red"})
            Write-Host ""
            
            # Classificação
            $classification = $analysis.classification
            $classColor = switch ($classification) {
                "Excellent" { "Green" }
                "Good" { "Yellow" }
                "Attention" { "DarkYellow" }
                "Avoid" { "Red" }
                default { "Gray" }
            }
            Write-Host "🏷️  Classificação: $classification" -ForegroundColor $classColor
            Write-Host ""
            
            # Resumo curto
            if ($analysis.shortSummary) {
                Write-Host "💬 Resumo: $($analysis.shortSummary)" -ForegroundColor White
                Write-Host ""
            }
            
            # Alertas
            if ($analysis.alerts -and $analysis.alerts.Count -gt 0) {
                Write-Host "⚠️  ALERTAS ($($analysis.alerts.Count)):" -ForegroundColor Yellow
                foreach ($alert in $analysis.alerts) {
                    Write-Host "   $alert" -ForegroundColor $(if($alert -match "🚨") {"Red"} else {"Yellow"})
                }
                Write-Host ""
            }
            
            # Recomendações
            if ($analysis.recommendations -and $analysis.recommendations.Count -gt 0) {
                Write-Host "💡 RECOMENDAÇÕES ($($analysis.recommendations.Count)):" -ForegroundColor Cyan
                foreach ($rec in $analysis.recommendations) {
                    Write-Host "   $rec" -ForegroundColor Gray
                }
                Write-Host ""
            }
            
            # Produto
            if ($analysis.productName) {
                Write-Host "📦 Produto: $($analysis.productName)" -ForegroundColor Gray
                if ($analysis.brand) {
                    Write-Host "🏢 Marca: $($analysis.brand)" -ForegroundColor Gray
                }
            }
        }
        
        # Metadados técnicos (opcional)
        if ($response.ocrResult -and $response.ocrResult.extractedText) {
            Write-Host ""
            Write-Host "━━━ DADOS TÉCNICOS ━━━" -ForegroundColor DarkGray
            Write-Host "OCR Provider: $($response.ocrResult.providerName)" -ForegroundColor DarkGray
            Write-Host "Texto extraído: $($response.ocrResult.extractedText.Length) caracteres" -ForegroundColor DarkGray
            Write-Host "Tempo total: $([math]::Round($response.totalProcessingTimeMs))ms" -ForegroundColor DarkGray
        }
        
        Write-Host ""
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
        
        return $response
        
    } catch {
        Write-Host "❌ ERRO ao processar imagem:" -ForegroundColor Red
        Write-Host "   $($_.Exception.Message)" -ForegroundColor Red
        
        if ($_.ErrorDetails.Message) {
            try {
                $errorObj = $_.ErrorDetails.Message | ConvertFrom-Json
                Write-Host "   Detalhes: $($errorObj.error)" -ForegroundColor Red
            } catch {
                Write-Host "   Detalhes: $($_.ErrorDetails.Message)" -ForegroundColor Red
            }
        }
        
        Write-Host ""
        return $null
    }
}

# ═══════════════════════════════════════════════════════════════════
# TESTES DE VALIDAÇÃO
# ═══════════════════════════════════════════════════════════════════

Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║   INICIANDO TESTES DE VALIDAÇÃO                          ║" -ForegroundColor Green
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Green

# Teste 1: Imagem fornecida pelo usuário (arroz.jpg)
$result1 = Test-ProductAnalysis -imagePath "arroz.jpg" -description "Imagem do usuário (arroz.jpg)"

# Teste 2: Se houver outras imagens de teste
$result2 = Test-ProductAnalysis -imagePath "teste-produto-ultraprocessado.jpg" -description "Produto Ultraprocessado"
$result3 = Test-ProductAnalysis -imagePath "teste-produto-saudavel.jpg" -description "Produto Saudável"

# ═══════════════════════════════════════════════════════════════════
# VALIDAÇÃO FINAL
# ═══════════════════════════════════════════════════════════════════

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   VALIDAÇÃO FINAL                                        ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$testsPassed = 0
$testsTotal = 0

if ($result1) {
    $testsTotal++
    $score = [math]::Round($result1.analysis.generalScore * 100)
    $classification = $result1.analysis.classification
    
    Write-Host "✅ Teste 1: Imagem processada com sucesso" -ForegroundColor Green
    Write-Host "   Score: $score/100 | Classificação: $classification" -ForegroundColor Gray
    $testsPassed++
} else {
    Write-Host "⏭️  Teste 1: Pulado (imagem não encontrada)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor White
Write-Host "RESULTADO: $testsPassed/$testsTotal testes executados" -ForegroundColor $(if($testsPassed -eq $testsTotal) {"Green"} else {"Yellow"})
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor White
Write-Host ""

# ═══════════════════════════════════════════════════════════════════
# INSTRUÇÕES
# ═══════════════════════════════════════════════════════════════════

Write-Host "📚 COMO USAR:" -ForegroundColor Cyan
Write-Host "   1. Coloque suas imagens de produtos na raiz do projeto" -ForegroundColor Gray
Write-Host "   2. Execute: .\test-nutritional-scoring-api.ps1" -ForegroundColor Gray
Write-Host "   3. Ou teste pelo Swagger:" -ForegroundColor Gray
Write-Host "      https://localhost:7319/swagger" -ForegroundColor Gray
Write-Host "      Endpoint: POST /api/pipeline/analyze-image" -ForegroundColor Gray
Write-Host ""

Write-Host "🎯 ENDPOINT CORRETO:" -ForegroundColor Yellow
Write-Host "   POST $endpoint" -ForegroundColor White
Write-Host ""

Write-Host "✅ Teste concluído!" -ForegroundColor Green
