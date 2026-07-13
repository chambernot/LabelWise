# Diagnóstico e Correção: Trial de 15 Dias não Reconhecido

## 🎯 Problema Identificado

**Sintoma:** Após ativar o trial de 15 dias no onboarding, o app redireciona para a tela de contratação ao tentar analisar produtos.

**Causa Raiz:** Inconsistência entre a persistência/leitura do estado de acesso entre o onboarding e a tela de análise no app mobile MAUI.

---

## 🔍 Arquitetura Backend (Funcionando Corretamente)

### 1. Fluxo de Inicialização do Trial

```
POST /api/app-user/session
{
  "deviceId": "ABC123",
  "platform": "android"
}

Resposta:
{
  "deviceId": "ABC123",
  "isPremium": false,
  "isTrialActive": true,
  "trialEndsAt": "2025-06-15T12:00:00Z",
  "daysRemaining": 15,
  "subscriptionStatus": "none"
}
```

### 2. Verificação de Acesso

```
GET /api/app-user/access-state?deviceId=ABC123

Resposta:
{
  "deviceId": "ABC123",
  "isPremium": false,
  "isTrialActive": true,
  "trialEndsAt": "2025-06-15T12:00:00Z",
  "daysRemaining": 15,
  "subscriptionStatus": "none",
  "canUseAnalysis": true,
  "canUseComparison": true,
  "canUseHistory": true,
  "message": "Seu período de trial está ativo. Restam 15 dias."
}
```

### 3. Endpoint de Análise com Verificação

```csharp
// NutritionController.cs linha 62-69
if (!string.IsNullOrWhiteSpace(deviceId))
{
    var accessState = await _appAccessService.GetAccessStateAsync(deviceId);
    if (!accessState.CanUseAnalysis)
    {
        _logger.LogWarning("Acesso negado para análise. DeviceId={DeviceId}", deviceId);
        return StatusCode(StatusCodes.Status403Forbidden, CreateAccessDeniedResponse(accessState));
    }
}
```

**Conclusão Backend:** A API funciona corretamente:
- ✅ Trial é criado com 15 dias automaticamente
- ✅ `CanUseAnalysis` retorna `true` quando trial ativo
- ✅ Só nega acesso quando `IsTrialActive = false` E `IsPremium = false`

---

## 🐛 Problema no App Mobile MAUI

### Cenários Possíveis

#### ❌ Cenário 1: DeviceId Inconsistente
```csharp
// Onboarding usa um deviceId
await sessionService.InitializeAsync("device-ABC-123");

// Análise usa outro deviceId
await analysisService.AnalyzeAsync("DEVICE_ABC_123"); // Diferente!
```

**Sintoma:** Backend cria dois AppUser distintos.

---

#### ❌ Cenário 2: Estado Não Persistido Localmente
```csharp
// OnboardingViewModel.cs
var sessionResponse = await _sessionService.InitializeAsync(deviceId);
// sessionResponse.IsTrialActive = true ✅
// MAS NÃO SALVA NO SecureStorage/Preferences ❌

// NutritionAnalysisViewModel.cs
var canAnalyze = _secureStorage.Get("IsTrialActive"); // null ❌
if (!canAnalyze)
{
    NavigateTo("SubscriptionPage"); // ERRO!
}
```

---

#### ❌ Cenário 3: Verificação Local em Vez de Consultar API
```csharp
// ERRADO: Verifica apenas estado local
var isTrialActive = Preferences.Get("trial_active", false);
if (!isTrialActive)
{
    // Redireciona mesmo com trial ativo no backend
}

// CORRETO: Sempre consulta o backend
var accessState = await _appAccessService.GetAccessStateAsync(deviceId);
if (!accessState.CanUseAnalysis)
{
    // Só redireciona se backend negar
}
```

---

#### ❌ Cenário 4: DeviceId Não Enviado na Análise
```csharp
// ERRADO: Não envia deviceId
var request = new AnalyzeRequest
{
    ImageData = imageBytes,
    // DeviceId ausente! ❌
};

// Backend não consegue verificar acesso
// Pode rejeitar por falta de informação
```

---

## ✅ Solução Completa

### 1. **Serviço Centralizado de Acesso**

```csharp
// Services/AppAccessManager.cs
public class AppAccessManager
{
    private readonly ISecureStorage _secureStorage;
    private readonly IAppAccessApiClient _apiClient;
    private const string DEVICE_ID_KEY = "app_device_id";
    private const string TRIAL_ACTIVE_KEY = "trial_active";
    private const string TRIAL_ENDS_AT_KEY = "trial_ends_at";

    public async Task<string> GetOrCreateDeviceIdAsync()
    {
        var deviceId = await _secureStorage.GetAsync(DEVICE_ID_KEY);
        
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = GenerateDeviceId();
            await _secureStorage.SetAsync(DEVICE_ID_KEY, deviceId);
        }
        
        return deviceId;
    }

    public async Task<AppAccessState> InitializeSessionAsync()
    {
        var deviceId = await GetOrCreateDeviceIdAsync();
        var platform = DeviceInfo.Platform.ToString().ToLower();
        
        var response = await _apiClient.InitializeSessionAsync(deviceId, platform);
        
        // Persiste estado local como cache
        await _secureStorage.SetAsync(TRIAL_ACTIVE_KEY, response.IsTrialActive.ToString());
        await _secureStorage.SetAsync(TRIAL_ENDS_AT_KEY, response.TrialEndsAt.ToString("o"));
        
        return new AppAccessState
        {
            DeviceId = response.DeviceId,
            IsTrialActive = response.IsTrialActive,
            IsPremium = response.IsPremium,
            CanUseAnalysis = response.IsTrialActive || response.IsPremium
        };
    }

    public async Task<bool> CanUseAnalysisAsync()
    {
        var deviceId = await GetOrCreateDeviceIdAsync();
        
        // SEMPRE consulta o backend (fonte de verdade)
        var accessState = await _apiClient.GetAccessStateAsync(deviceId);
        
        // Atualiza cache local
        await _secureStorage.SetAsync(TRIAL_ACTIVE_KEY, accessState.IsTrialActive.ToString());
        
        return accessState.CanUseAnalysis;
    }

    private string GenerateDeviceId()
    {
        var guid = Guid.NewGuid().ToString("N");
        var platform = DeviceInfo.Platform.ToString().ToLower();
        return $"{platform}-{guid}";
    }
}
```

---

### 2. **API Client Consistente**

```csharp
// Services/AppAccessApiClient.cs
public class AppAccessApiClient : IAppAccessApiClient
{
    private readonly HttpClient _httpClient;

    public async Task<AppSessionResponse> InitializeSessionAsync(string deviceId, string platform)
    {
        var request = new
        {
            deviceId = deviceId,
            platform = platform
        };

        var response = await _httpClient.PostAsJsonAsync("/api/app-user/session", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AppSessionResponse>();
    }

    public async Task<AppAccessStateResponse> GetAccessStateAsync(string deviceId)
    {
        var response = await _httpClient.GetAsync($"/api/app-user/access-state?deviceId={deviceId}");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AppAccessStateResponse>();
    }
}
```

---

### 3. **Onboarding ViewModel Corrigido**

```csharp
// ViewModels/OnboardingViewModel.cs
public class OnboardingViewModel : BaseViewModel
{
    private readonly AppAccessManager _accessManager;
    private readonly INavigationService _navigation;

    [RelayCommand]
    private async Task StartTrialAsync()
    {
        IsBusy = true;

        try
        {
            // Inicializa sessão e cria trial
            var accessState = await _accessManager.InitializeSessionAsync();

            if (accessState.IsTrialActive)
            {
                await _navigation.NavigateToAsync("//main");
            }
            else
            {
                // Não deveria acontecer, mas trata erro
                await ShowAlertAsync("Erro", "Não foi possível ativar o trial.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar trial");
            await ShowAlertAsync("Erro", "Erro ao conectar ao servidor.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

---

### 4. **Nutrition Analysis ViewModel Corrigido**

```csharp
// ViewModels/NutritionAnalysisViewModel.cs
public class NutritionAnalysisViewModel : BaseViewModel
{
    private readonly AppAccessManager _accessManager;
    private readonly INutritionApiClient _nutritionClient;
    private readonly INavigationService _navigation;

    [RelayCommand]
    private async Task AnalyzeImageAsync(FileResult imageFile)
    {
        IsBusy = true;

        try
        {
            // VERIFICAÇÃO DE ACESSO - Consulta backend
            var canAnalyze = await _accessManager.CanUseAnalysisAsync();
            
            if (!canAnalyze)
            {
                // Backend negou acesso - trial expirado ou sem assinatura
                await _navigation.NavigateToAsync("//subscription");
                return;
            }

            // Obtém deviceId consistente
            var deviceId = await _accessManager.GetOrCreateDeviceIdAsync();

            // Prepara imagem
            var imageBytes = await ReadImageBytesAsync(imageFile);

            // ENVIA ANÁLISE COM DEVICE ID
            var request = new AnalyzeImageRequest
            {
                ImageData = imageBytes,
                FileName = imageFile.FileName,
                DeviceId = deviceId, // ✅ ESSENCIAL
                LanguageCode = "pt-BR"
            };

            var result = await _nutritionClient.AnalyzeImageAsync(request);

            if (result.Success)
            {
                AnalysisResult = result;
                await _navigation.NavigateToAsync("//results", result);
            }
            else if (result.AccessDenied)
            {
                // Backend negou por questão de acesso
                await _navigation.NavigateToAsync("//subscription");
            }
            else
            {
                await ShowAlertAsync("Erro", result.ErrorMessage);
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // 403 - Acesso negado
            await _navigation.NavigateToAsync("//subscription");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao analisar imagem");
            await ShowAlertAsync("Erro", "Erro ao processar análise.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<byte[]> ReadImageBytesAsync(FileResult file)
    {
        using var stream = await file.OpenReadAsync();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}
```

---

### 5. **Nutrition API Client**

```csharp
// Services/NutritionApiClient.cs
public class NutritionApiClient : INutritionApiClient
{
    private readonly HttpClient _httpClient;

    public async Task<NutritionAnalysisResponse> AnalyzeImageAsync(AnalyzeImageRequest request)
    {
        using var form = new MultipartFormDataContent();
        
        // Imagem
        var imageContent = new ByteArrayContent(request.ImageData);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(imageContent, "file", request.FileName);

        // DeviceId - ESSENCIAL para verificação de acesso
        if (!string.IsNullOrWhiteSpace(request.DeviceId))
        {
            form.Add(new StringContent(request.DeviceId), "deviceId");
        }

        // Language
        if (!string.IsNullOrWhiteSpace(request.LanguageCode))
        {
            form.Add(new StringContent(request.LanguageCode), "languageCode");
        }

        var response = await _httpClient.PostAsync("/api/nutrition/analyze-simple-image", form);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // Trial expirado ou sem assinatura
            return new NutritionAnalysisResponse
            {
                Success = false,
                AccessDenied = true,
                ErrorMessage = "Acesso negado. Trial expirado ou assinatura necessária."
            };
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NutritionAnalysisResponse>();
    }
}
```

---

## 🔧 Logs de Diagnóstico

### Backend (já implementado)

```csharp
// AppAccessService.cs linha 34
_logger.LogInformation("Trial criado para deviceId {DeviceId}. TrialEndsAt={TrialEndsAt}", 
    normalizedDeviceId, appUser.TrialEndsAt);

// NutritionController.cs linha 67
_logger.LogWarning("Acesso negado para análise. DeviceId={DeviceId}", deviceId);
```

### Mobile (adicionar)

```csharp
// AppAccessManager.cs
public async Task<AppAccessState> InitializeSessionAsync()
{
    var deviceId = await GetOrCreateDeviceIdAsync();
    _logger.LogInformation("Inicializando sessão com deviceId: {DeviceId}", deviceId);
    
    var response = await _apiClient.InitializeSessionAsync(deviceId, platform);
    
    _logger.LogInformation(
        "Sessão inicializada - Trial: {IsTrialActive}, Premium: {IsPremium}, EndsAt: {TrialEndsAt}",
        response.IsTrialActive,
        response.IsPremium,
        response.TrialEndsAt);
    
    return accessState;
}

public async Task<bool> CanUseAnalysisAsync()
{
    var deviceId = await GetOrCreateDeviceIdAsync();
    _logger.LogInformation("Verificando acesso para deviceId: {DeviceId}", deviceId);
    
    var accessState = await _apiClient.GetAccessStateAsync(deviceId);
    
    _logger.LogInformation(
        "Estado de acesso - CanUse: {CanUse}, Trial: {IsTrialActive}, Premium: {IsPremium}",
        accessState.CanUseAnalysis,
        accessState.IsTrialActive,
        accessState.IsPremium);
    
    return accessState.CanUseAnalysis;
}
```

---

## 📋 Checklist de Verificação

### ✅ Backend (já implementado)
- [x] Trial criado automaticamente com 15 dias
- [x] Endpoint `/api/app-user/session` retorna `IsTrialActive`
- [x] Endpoint `/api/app-user/access-state` retorna `CanUseAnalysis`
- [x] Análise verifica `CanUseAnalysis` antes de processar
- [x] Logs de auditoria implementados

### 🔍 Mobile (verificar e corrigir)
- [ ] DeviceId único e persistente no `SecureStorage`
- [ ] Onboarding chama `/api/app-user/session` e salva resposta
- [ ] Análise consulta `/api/app-user/access-state` ANTES de analisar
- [ ] DeviceId enviado em TODOS os requests de análise
- [ ] Não há verificação local que ignore o backend
- [ ] Redirecionamento para assinatura APENAS se backend negar acesso
- [ ] Logs implementados para rastrear fluxo

---

## 🧪 Testes

### 1. Teste de Trial Ativo

```http
### Criar sessão
POST {{host}}/api/app-user/session
Content-Type: application/json

{
  "deviceId": "test-trial-001",
  "platform": "android"
}

### Verificar estado
GET {{host}}/api/app-user/access-state?deviceId=test-trial-001

### Analisar imagem (deve funcionar)
POST {{host}}/api/nutrition/analyze-simple-image
Content-Type: multipart/form-data

--boundary
Content-Disposition: form-data; name="deviceId"

test-trial-001
--boundary
Content-Disposition: form-data; name="file"; filename="test.jpg"
Content-Type: image/jpeg

<binary data>
--boundary--
```

**Resultado Esperado:**
- Session: `isTrialActive = true`, `daysRemaining = 15`
- AccessState: `canUseAnalysis = true`
- Análise: Status 200 OK

---

### 2. Teste de Trial Expirado

```http
### Simular trial expirado (alterar TrialEndsAt no banco)
UPDATE app_users 
SET trial_ends_at = NOW() - INTERVAL '1 day'
WHERE device_id = 'test-trial-expired';

### Verificar estado
GET {{host}}/api/app-user/access-state?deviceId=test-trial-expired

### Tentar analisar (deve negar)
POST {{host}}/api/nutrition/analyze-simple-image
Content-Type: multipart/form-data

--boundary
Content-Disposition: form-data; name="deviceId"

test-trial-expired
--boundary--
```

**Resultado Esperado:**
- AccessState: `isTrialActive = false`, `canUseAnalysis = false`
- Análise: Status 403 Forbidden

---

## 🎯 Próximos Passos

1. **Auditar código mobile:**
   - Buscar por `Preferences.Get("trial")`
   - Buscar por `SecureStorage.Get("trial")`
   - Verificar `OnboardingViewModel` e `AnalysisViewModel`

2. **Implementar AppAccessManager:**
   - Fonte única de verdade para deviceId
   - Sempre consulta backend para decisões de acesso

3. **Adicionar logs:**
   - Rastrear deviceId em cada chamada
   - Registrar resultado de `CanUseAnalysis`

4. **Testar fluxo completo:**
   - Onboarding → Trial ativo → Análise funciona
   - Trial expirado → Redirecionamento para assinatura
   - DeviceId consistente entre telas

---

## 📚 Referências

- `LabelWise.Api\Controllers\NutritionController.cs` (linhas 62-69)
- `LabelWise.Infrastructure\Services\AppAccessService.cs` (linhas 85-98)
- `LabelWise.Domain\Entities\AppUser.cs` (linha 26: `TrialEndsAt = nowUtc.AddDays(trialDays)`)
