# 🚀 Quick Start - Dev Full Guided Analysis Endpoint

## 📝 TL;DR

Endpoint de desenvolvimento que processa múltiplas imagens de produtos em uma única chamada para testes rápidos do fluxo de captura guiada.

```
POST /api/dev/full-guided-analysis-test
```

⚠️ **Development Only** - Bloqueado em Production

---

## 🏃 Start em 3 Passos

### 1. Prepare as Imagens

Crie uma pasta com imagens de teste:

```
C:\temp\test-images\
├── front.jpg          (opcional)
├── ingredients.jpg    (recomendado)
├── nutrition.jpg      (recomendado)
└── allergen.jpg       (opcional)
```

### 2. Execute o Script

```powershell
.\test-dev-full-guided-analysis.ps1 `
    -ApiBaseUrl "https://localhost:7319" `
    -Username "test@example.com" `
    -Password "Test@123" `
    -ImagesPath "C:\temp\test-images" `
    -SkipCertificateCheck
```

### 3. Veja o Resultado

O script mostra:
- ✅ Status do processamento
- 🏷️  Identificação do produto
- 🥗 Ingredientes detectados
- ⚠️  Alérgenos identificados
- 📊 Informações nutricionais
- 🎯 Análise final com score
- 📈 Metadados de cada etapa

---

## 💻 Teste Manual (PowerShell)

```powershell
# 1. Login
$login = Invoke-RestMethod `
    -Uri "https://localhost:7319/api/auth/login" `
    -Method Post `
    -Body (@{email="test@example.com"; password="Test@123"} | ConvertTo-Json) `
    -ContentType "application/json" `
    -SkipCertificateCheck

$token = $login.token

# 2. Análise
$form = @{
    ingredientsImage = Get-Item "C:\temp\ingredients.jpg"
    nutritionImage = Get-Item "C:\temp\nutrition.jpg"
    languageCode = "pt-BR"
}

$result = Invoke-RestMethod `
    -Uri "https://localhost:7319/api/dev/full-guided-analysis-test" `
    -Method Post `
    -Headers @{Authorization="Bearer $token"} `
    -Form $form `
    -SkipCertificateCheck

# 3. Ver resultado
$result | ConvertTo-Json -Depth 10
```

---

## 🧪 Teste via Swagger

1. Abra: `https://localhost:7319/swagger`
2. Procure: **DevGuidedAnalysis**
3. Clique em **Authorize** e cole seu token JWT
4. Expanda `POST /api/dev/full-guided-analysis-test`
5. Clique **Try it out**
6. Selecione imagens
7. **Execute**

---

## 🐛 Troubleshooting Rápido

### "Endpoint disponível apenas em Development"
**Solução**: Defina `ASPNETCORE_ENVIRONMENT=Development`

### "Não autenticado"
**Solução**: Faça login em `/api/auth/login` primeiro

### "Nenhuma entrada fornecida"
**Solução**: Envie pelo menos uma imagem ou barcode

### OCR retorna texto vazio
**Soluções**:
- Use imagem com boa iluminação
- Mantenha câmera paralela ao rótulo
- Evite reflexos e sombras

---

## 📊 O Que Esperar

### Tempo de Processamento
- 1 imagem: ~2-3 segundos
- 4 imagens: ~8-12 segundos

### Confidence Esperado
- **OCR**: > 0.85 (boas imagens)
- **Parsing**: > 0.80 (dados estruturados)
- **Overall**: > 0.75 (análise completa)

### Success Criteria
- `success: true`
- `processedSteps.length > 0`
- `errors.length == 0`
- `confidence.overall > 0.70`

---

## 🎯 Casos de Uso Rápidos

### Teste Completo
```powershell
# Todas as 4 imagens
-FrontImage, -IngredientsImage, -NutritionImage, -AllergenImage
```

### Teste Mínimo
```powershell
# Apenas as obrigatórias
-IngredientsImage, -NutritionImage
```

### Teste de OCR
```powershell
# Uma imagem por vez para validar OCR
-IngredientsImage
```

### Teste de Barcode
```powershell
# Apenas barcode (sem imagens)
-Barcode "7891234567890"
```

---

## 📚 Documentação Completa

- **`DEV_FULL_GUIDED_ANALYSIS_DOCUMENTATION.md`** - Guia completo
- **`DEV_FULL_GUIDED_ANALYSIS_EXAMPLES.cs`** - Exemplos C#
- **`IMPLEMENTATION_COMPLETE_DEV_ENDPOINT.md`** - Status da implementação

---

## ✅ Checklist Pré-Teste

- [ ] API rodando em Development
- [ ] PostgreSQL rodando
- [ ] OCR configurado (Tesseract ou Azure)
- [ ] Usuário de teste criado
- [ ] Imagens preparadas
- [ ] Script baixado

---

## 🆘 Precisa de Ajuda?

1. **Health Check**: `GET /api/dev/full-guided-analysis-test/health`
2. **Logs**: Verifique logs da API
3. **Documentação**: Leia `DEV_FULL_GUIDED_ANALYSIS_DOCUMENTATION.md`
4. **Swagger**: Use Swagger UI para testar interativamente

---

**Pronto para testar!** 🚀

Execute o script e veja o fluxo completo em ação!
