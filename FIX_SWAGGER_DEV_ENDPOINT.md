# 🔧 Correção do Erro Swagger - Dev Endpoint

## 🐛 Problema Identificado

**Erro**: `Failed to load API definition. Fetch error response status is 500 /swagger/v1/swagger.json`

## 🔍 Causa Raiz

O DTO `FullGuidedAnalysisResponse` estava importando `LabelWise.Application.DTOs.ProductIdentification` e usando `ProductCandidate` na propriedade `AlternativeCandidates`, causando conflito ou problema de serialização no Swagger.

## ✅ Solução Aplicada

### 1. Removida importação desnecessária

**Antes:**
```csharp
using LabelWise.Application.DTOs.ProductIdentification;
```

**Depois:**
```csharp
// Removido - não é necessário
```

### 2. Simplificada propriedade AlternativeCandidates

**Antes:**
```csharp
public List<ProductCandidate> AlternativeCandidates { get; set; } = new();
```

**Depois:**
```csharp
public List<string> AlternativeCandidates { get; set; } = new();
```

## 🚀 Como Testar a Correção

### 1. Parar a aplicação atual

```powershell
# Se estiver rodando no Visual Studio, pare o debug (Shift+F5)
# Ou no terminal:
taskkill /F /IM LabelWise.Api.exe 2>$null
```

### 2. Reconstruir a solução

```powershell
dotnet build
```

### 3. Iniciar a aplicação

```powershell
cd C:\Users\chamb\source\repos\LabelWise\LabelWise.Api
dotnet run
```

### 4. Acessar Swagger

Abra o navegador em:
```
https://localhost:7319/swagger
```

## ✅ Resultado Esperado

- ✅ Swagger deve carregar sem erros
- ✅ Endpoint `DevGuidedAnalysis` deve aparecer
- ✅ Documentação XML deve estar visível
- ✅ Todos os endpoints devem estar acessíveis

## 📋 Validação

### Checklist:
- [ ] Swagger carrega sem erro 500
- [ ] `POST /api/dev/full-guided-analysis-test` aparece
- [ ] `GET /api/dev/full-guided-analysis-test/health` aparece
- [ ] Schemas estão visíveis
- [ ] Documentação XML está presente

## 🔄 Se o Erro Persistir

### Verificar logs detalhados:

```powershell
# Iniciar com logging detalhado
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:Logging__LogLevel__Default="Debug"
dotnet run
```

### Verificar configuração do Swagger no Program.cs:

```csharp
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LabelWise API", Version = "v1" });
    // ... outras configurações
});
```

## 📚 Arquivos Modificados

1. ✅ `LabelWise.Application/DTOs/Development/FullGuidedAnalysisResponse.cs`
   - Removido using desnecessário
   - Simplificado tipo de AlternativeCandidates

## 🎯 Impacto

**Mínimo** - A propriedade `AlternativeCandidates` agora armazena strings (nomes dos produtos) em vez de objetos complexos `ProductCandidate`. Isso é suficiente para o endpoint de desenvolvimento e evita problemas de serialização.

Se no futuro precisar de dados mais complexos, pode-se:
1. Criar um DTO específico dentro do namespace Development
2. Usar herança ou composição
3. Mapear manualmente os dados necessários

---

**Status**: ✅ Correção aplicada
**Build**: ✅ Compilando sem erros
**Próximo passo**: Reiniciar aplicação e validar Swagger
