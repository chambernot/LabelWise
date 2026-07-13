# ⚡ SOLUÇÃO RÁPIDA - Baixar Tessdata Manualmente

## Execute estes comandos no PowerShell:

```powershell
# 1. Navegar para o diretório da API
cd "C:\Users\chamb\source\repos\LabelWise\LabelWise.Api"

# 2. Verificar se tessdata existe
if (Test-Path tessdata) { Write-Host "✅ Diretório tessdata existe" } else { mkdir tessdata }

# 3. Baixar por.traineddata (Português) - ~4 MB
Write-Host "📥 Baixando por.traineddata..."
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata/raw/main/por.traineddata" -OutFile "tessdata\por.traineddata" -UseBasicParsing

# 4. Baixar eng.traineddata (Inglês) - ~4 MB
Write-Host "📥 Baixando eng.traineddata..."
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata" -OutFile "tessdata\eng.traineddata" -UseBasicParsing

# 5. Verificar downloads
Write-Host "`n✅ Arquivos baixados:"
Get-ChildItem tessdata\*.traineddata | Format-Table Name, @{Label="Tamanho (MB)"; Expression={[math]::Round($_.Length / 1MB, 2)}}

# 6. Voltar para raiz e recompilar
cd ..
Write-Host "`n🔨 Recompilando projeto..."
dotnet clean --nologo --verbosity quiet
dotnet build --nologo --verbosity quiet

# 7. Verificar se arquivos foram copiados
Write-Host "`n✅ Verificando cópia para bin:"
Get-ChildItem "LabelWise.Api\bin\Debug\net10.0\tessdata\*.traineddata" -ErrorAction SilentlyContinue | Format-Table Name, @{Label="Tamanho (MB)"; Expression={[math]::Round($_.Length / 1MB, 2)}}

Write-Host "`n🎉 PRONTO! Agora reinicie a API:" -ForegroundColor Green
Write-Host "   dotnet run --project LabelWise.Api" -ForegroundColor Cyan
```

## ✅ Checklist após executar:

- [ ] Arquivos baixados em `LabelWise.Api\tessdata\`
- [ ] `por.traineddata` tem ~4 MB
- [ ] `eng.traineddata` tem ~4 MB  
- [ ] Projeto recompilado sem erros
- [ ] Arquivos copiados para `bin\Debug\net10.0\tessdata\`
- [ ] API reiniciada

## 🧪 Teste final:

```powershell
# Reiniciar API
dotnet run --project LabelWise.Api

# Em outro terminal, testar com curl
curl -X 'POST' 'https://localhost:7319/api/products/analyze-image' -H 'accept: text/plain' -H 'Content-Type: multipart/form-data' -F 'file=@arroz.jpg;type=image/jpeg'
```

**Resultado esperado:** Texto extraído com sucesso, sem erros sobre tessdata vazio.
