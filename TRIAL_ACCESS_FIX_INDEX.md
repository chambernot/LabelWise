# 📚 Índice: Correção Trial de 15 Dias

## 🎯 Visão Geral

Este conjunto de documentos fornece uma solução completa para corrigir o bug onde o trial de 15 dias não é reconhecido na tela de análise do app mobile .NET MAUI.

---

## 📄 Documentos

### 1. **TRIAL_ACCESS_FIX_SUMMARY.md** ⭐ COMEÇAR AQUI
**Resumo Executivo**

- 📊 Visão geral do problema e solução
- 🔍 Diagnóstico resumido
- 📈 Impacto e métricas
- ⏱️ Estimativa de tempo
- ✅ Critérios de aceitação

**Quando usar:** Para entender o problema em 5 minutos.

---

### 2. **TRIAL_ACCESS_FIX_QUICK_START.md** ⚡ IMPLEMENTAÇÃO RÁPIDA
**Guia de Implementação Rápida**

- 🚀 Solução em 5 passos
- 💻 Código mínimo funcional
- 🧪 Testes básicos
- 🐛 Troubleshooting rápido

**Quando usar:** Para implementar a correção rapidamente (30-60 min).

---

### 3. **TRIAL_ACCESS_FIX_DIAGNOSTIC.md** 🔬 ANÁLISE DETALHADA
**Diagnóstico Completo**

- 🎯 Problema identificado
- 🏗️ Arquitetura backend (funcional)
- 🐛 Cenários de erro no mobile
- ✅ Solução detalhada com código completo
- 🧪 Exemplos de testes
- 📋 Checklist de verificação

**Quando usar:** Para entender em profundidade como o sistema funciona e onde está o problema.

---

### 4. **TRIAL_ACCESS_FIX_CHECKLIST.md** ✅ PASSO A PASSO
**Checklist Completo de Implementação**

- 📋 Verificação do código atual
- 💻 Implementação do AppAccessManager (código completo)
- 🔧 Atualização dos ViewModels
- 🧪 Guia de testes
- ✅ Checklist final

**Quando usar:** Durante a implementação, como guia passo a passo.

---

### 5. **TRIAL_ACCESS_FIX_EXAMPLES.cs** 📖 EXEMPLOS PRÁTICOS
**Código Antes e Depois**

- 7 exemplos comparativos:
  1. Obtenção do DeviceId
  2. Verificação de acesso
  3. Inicialização do trial
  4. Envio do DeviceId na análise
  5. Tratamento de erro 403
  6. Logs de diagnóstico
  7. Fluxo completo

**Quando usar:** Para comparar código antigo com o código corrigido.

---

### 6. **test-trial-access-fix.ps1** 🧪 SCRIPT DE TESTE
**Script Automatizado de Testes**

- ✅ Teste 1: Inicializar trial
- ✅ Teste 2: Verificar estado de acesso
- ✅ Teste 3: Simular análise de imagem
- 📝 Teste 4: Verificar persistência no banco
- 📝 Teste 5: Simular trial expirado
- 📱 Instruções para testes no mobile

**Quando usar:** Para validar que a API está funcionando corretamente.

---

## 🗺️ Fluxo de Uso Recomendado

### 🔰 Iniciante / Pressa

```
1. TRIAL_ACCESS_FIX_SUMMARY.md (5 min)
   └─> Entender o problema
   
2. TRIAL_ACCESS_FIX_QUICK_START.md (30-60 min)
   └─> Implementar solução básica
   
3. test-trial-access-fix.ps1
   └─> Validar funcionamento
```

---

### 🎓 Intermediário / Completo

```
1. TRIAL_ACCESS_FIX_SUMMARY.md (5 min)
   └─> Visão geral
   
2. TRIAL_ACCESS_FIX_DIAGNOSTIC.md (15 min)
   └─> Entender arquitetura e problema
   
3. TRIAL_ACCESS_FIX_CHECKLIST.md (60-90 min)
   └─> Implementar passo a passo
   
4. TRIAL_ACCESS_FIX_EXAMPLES.cs (referência)
   └─> Comparar código
   
5. test-trial-access-fix.ps1
   └─> Validar implementação
```

---

### 🚀 Avançado / Auditoria

```
1. TRIAL_ACCESS_FIX_DIAGNOSTIC.md
   └─> Análise completa da arquitetura
   
2. Código Backend (LabelWise.Infrastructure\Services\)
   └─> AppAccessService.cs
   └─> SubscriptionService.cs
   
3. TRIAL_ACCESS_FIX_EXAMPLES.cs
   └─> Estudar padrões corretos
   
4. TRIAL_ACCESS_FIX_CHECKLIST.md
   └─> Implementar com código completo
   
5. test-trial-access-fix.ps1
   └─> Testes automatizados
   
6. Testes manuais no mobile
   └─> Validação end-to-end
```

---

## 🎯 Referência Rápida

### Arquivos Backend (✅ Funcionando)

```
LabelWise.Infrastructure\Services\AppAccessService.cs
├─> InitializeSessionAsync()    (Cria trial de 15 dias)
├─> GetAccessStateAsync()        (Retorna CanUseAnalysis)
├─> IsTrialActive()              (Valida se trial está ativo)
└─> BuildAccessState()           (hasAccess = isPremium || isTrialActive)

LabelWise.Api\Controllers\NutritionController.cs
├─> AnalyzeSimpleImage()
└─> Linha 64: if (!accessState.CanUseAnalysis) → Nega acesso
```

---

### Arquivos Mobile (⚠️ Corrigir)

```
MauiApp/Services/AppAccessManager.cs (CRIAR)
├─> GetOrCreateDeviceIdAsync()   (Único deviceId persistente)
├─> InitializeTrialAsync()        (POST /api/app-user/session)
└─> CanUseAnalysisAsync()         (GET /api/app-user/access-state)

MauiApp/ViewModels/OnboardingViewModel.cs (ATUALIZAR)
└─> StartTrialAsync()             (Usar AppAccessManager)

MauiApp/ViewModels/NutritionAnalysisViewModel.cs (ATUALIZAR)
└─> AnalyzeImageAsync()           (Verificar acesso + enviar deviceId)

MauiProgram.cs (REGISTRAR)
└─> builder.Services.AddSingleton<AppAccessManager>();
```

---

## 🔑 Conceitos-Chave

### ✅ O Que Fazer

1. **DeviceId único e persistente**
   - Usar `SecureStorage` para salvar
   - Mesmo ID em onboarding e análise

2. **Sempre consultar backend**
   - Não confiar apenas em cache local
   - Backend é a fonte de verdade

3. **Enviar deviceId em todas as requests**
   - Form-data na análise
   - Query string no access-state

4. **Centralizar lógica de acesso**
   - Um único serviço: `AppAccessManager`
   - ViewModels apenas consomem

---

### ❌ O Que Evitar

1. **DeviceId diferente em cada tela**
   - Gera múltiplos AppUsers no backend
   - Trial não é reconhecido

2. **Verificação apenas local**
   - Cache pode estar desatualizado
   - Redireciona incorretamente

3. **DeviceId ausente na análise**
   - Backend não consegue verificar acesso
   - Rejeita por falta de informação

4. **Múltiplas fontes de verdade**
   - Lógica duplicada em ViewModels
   - Difícil manter consistência

---

## 📊 Métricas de Validação

### ✅ Indicadores de Sucesso

- [ ] Trial ativado no onboarding
- [ ] `IsTrialActive = true` no backend
- [ ] `CanUseAnalysis = true` no backend
- [ ] Análise funciona durante 15 dias
- [ ] Após expiração, redireciona para assinatura
- [ ] Logs mostram mesmo deviceId em todo o fluxo
- [ ] Zero erros 403 com trial ativo

---

### ❌ Indicadores de Problema

- [ ] Trial ativado mas análise nega acesso
- [ ] DeviceId diferente entre onboarding e análise
- [ ] `CanUseAnalysis = false` com trial ativo
- [ ] Erro 403 mesmo após ativar trial
- [ ] Redirecionamento para assinatura com trial válido

---

## 🆘 Troubleshooting

### Problema 1: Trial não é ativado

**Verificar:**
```powershell
# Testar API
.\test-trial-access-fix.ps1

# Verificar logs do backend
# Application Insights: "Trial criado para deviceId"
```

---

### Problema 2: Análise nega acesso com trial ativo

**Verificar:**
```csharp
// Mobile: DeviceId é o mesmo?
Console.WriteLine($"[DEBUG] DeviceId: {deviceId}");

// Mobile: CanUseAnalysis retorna true?
var canAnalyze = await _accessManager.CanUseAnalysisAsync();
Console.WriteLine($"[DEBUG] CanAnalyze: {canAnalyze}");

// Mobile: DeviceId está sendo enviado?
form.Add(new StringContent(deviceId), "deviceId");
```

---

### Problema 3: Erro 403 Forbidden

**Causas:**
1. DeviceId não foi enviado
2. Trial expirou
3. AppUser não existe no backend

**Debug:**
```sql
-- PostgreSQL
SELECT device_id, trial_ends_at, is_premium 
FROM app_users 
WHERE device_id = 'android-abc123';
```

---

## 📞 Suporte

**Dúvidas sobre:**
- Arquitetura: `TRIAL_ACCESS_FIX_DIAGNOSTIC.md`
- Implementação: `TRIAL_ACCESS_FIX_CHECKLIST.md`
- Exemplos: `TRIAL_ACCESS_FIX_EXAMPLES.cs`
- Testes: `test-trial-access-fix.ps1`

---

## 🎉 Conclusão

Esta documentação fornece tudo o que você precisa para:

1. ✅ Entender o problema (DIAGNOSTIC)
2. ✅ Implementar a solução (CHECKLIST + QUICK START)
3. ✅ Validar a correção (EXAMPLES + test script)
4. ✅ Manter funcionando (SUMMARY + INDEX)

**Tempo total:** 1-2 horas para implementação completa e testes.

**Resultado:** Trial de 15 dias funcionando perfeitamente em todo o app! 🚀

---

**Criado em:** 31/05/2025  
**Versão:** 1.0  
**Status:** ✅ Documentação completa
