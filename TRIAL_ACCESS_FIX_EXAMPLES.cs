# 🔄 Trial Access Fix - Exemplos Antes e Depois

## 📱 Exemplo 1: Obtenção do DeviceId

### ❌ ANTES (Inconsistente)

```csharp
// OnboardingViewModel.cs
public async Task StartTrialAsync()
{
    // Gera um novo ID toda vez
    var deviceId = Guid.NewGuid().ToString();
    await _apiClient.InitializeSessionAsync(deviceId, "android");
    await Navigation.PushAsync(new MainPage());
}

// NutritionAnalysisViewModel.cs
public async Task AnalyzeAsync()
{
    // Usa outro ID!
    var deviceId = DeviceInfo.Name + "_" + DeviceInfo.Idiom;
    await _apiClient.AnalyzeImageAsync(deviceId, imageBytes);
}

// Resultado: Dois AppUsers diferentes no backend
```

### ✅ DEPOIS (Consistente)

```csharp
// AppAccessManager.cs
private const string DEVICE_ID_KEY = "app_device_id";

public async Task<string> GetOrCreateDeviceIdAsync()
{
    var deviceId = await _secureStorage.GetAsync(DEVICE_ID_KEY);
    
    if (string.IsNullOrWhiteSpace(deviceId))
    {
        deviceId = GenerateDeviceId();
        await _secureStorage.SetAsync(DEVICE_ID_KEY, deviceId);
    }
    
    return deviceId; // Sempre o mesmo!
}

private string GenerateDeviceId()
{
    var guid = Guid.NewGuid().ToString("N");
    var platform = DeviceInfo.Platform.ToString().ToLower();
    return $"{platform}-{guid}";
}

// OnboardingViewModel.cs
public async Task StartTrialAsync()
{
    await _accessManager.InitializeTrialAsync();
    // Internamente usa GetOrCreateDeviceIdAsync()
}

// NutritionAnalysisViewModel.cs
public async Task AnalyzeAsync()
{
    var deviceId = await _accessManager.GetOrCreateDeviceIdAsync();
    // Mesmo ID usado no onboarding ✅
}
```

---

## 📱 Exemplo 2: Verificação de Acesso

### ❌ ANTES (Apenas Cache Local)

```csharp
// NutritionAnalysisViewModel.cs
public async Task AnalyzeImageAsync(byte[] imageData)
{
    // Verifica apenas cache local
    var trialActive = Preferences.Get("trial_active", false);
    var trialEndsAt = Preferences.Get("trial_ends_at", DateTime.MinValue);
    
    if (!trialActive || trialEndsAt < DateTime.Now)
    {
        // Redireciona mesmo se trial estiver ativo no backend!
        await Navigation.PushAsync(new SubscriptionPage());
        return;
    }
    
    // Continua análise
    var result = await _apiClient.AnalyzeAsync(imageData);
}

// Problema: Cache pode estar desatualizado
// - Trial pode ter sido ativado mas cache não atualizado
// - Usuário pode ter assinado premium no backend
```

### ✅ DEPOIS (Sempre Consulta Backend)

```csharp
// NutritionAnalysisViewModel.cs
public async Task AnalyzeImageAsync(byte[] imageData)
{
    // Consulta backend (fonte de verdade)
    var canAnalyze = await _accessManager.CanUseAnalysisAsync();
    
    if (!canAnalyze)
    {
        // Backend negou - motivo legítimo
        await Navigation.PushAsync(new SubscriptionPage());
        return;
    }
    
    // Backend permitiu - prossegue
    var deviceId = await _accessManager.GetOrCreateDeviceIdAsync();
    var result = await _apiClient.AnalyzeAsync(deviceId, imageData);
}

// AppAccessManager.cs
public async Task<bool> CanUseAnalysisAsync()
{
    var deviceId = await GetOrCreateDeviceIdAsync();
    
    try
    {
        // Consulta API
        var response = await _httpClient.GetAsync(
            $"/api/app-user/access-state?deviceId={deviceId}");
        
        var state = await response.Content.ReadFromJsonAsync<AppAccessStateResponse>();
        
        // Atualiza cache como fallback offline
        await CacheAccessStateAsync(state);
        
        return state.CanUseAnalysis;
    }
    catch
    {
        // Erro de rede: usa cache
        var cached = await GetCachedAccessStateAsync();
        return cached?.CanUseAnalysis ?? false;
    }
}
```

---

## 📱 Exemplo 3: Inicialização do Trial

### ❌ ANTES (Não Persiste Resposta)

```csharp
// OnboardingViewModel.cs
public async Task StartTrialAsync()
{
    var deviceId = GetDeviceId();
    
    var request = new { deviceId, platform = "android" };
    var response = await _httpClient.PostAsJsonAsync("/api/app-user/session", request);
    var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
    
    // Recebe IsTrialActive = true do backend
    // MAS NÃO SALVA EM LUGAR NENHUM ❌
    
    await Navigation.PushAsync(new MainPage());
}

// MainPage.xaml.cs
protected override async void OnAppearing()
{
    base.OnAppearing();
    
    // Tenta ler estado local
    var hasAccess = Preferences.Get("has_access", false); // false! ❌
    
    if (!hasAccess)
    {
        // Redireciona incorretamente
        await Navigation.PushAsync(new SubscriptionPage());
    }
}
```

### ✅ DEPOIS (Persiste e Usa Corretamente)

```csharp
// OnboardingViewModel.cs
public async Task StartTrialAsync()
{
    // AppAccessManager gerencia tudo
    var accessState = await _accessManager.InitializeTrialAsync();
    
    if (accessState.IsTrialActive)
    {
        // Trial ativado com sucesso
        await Navigation.PushAsync(new MainPage());
    }
}

// AppAccessManager.cs
public async Task<AppAccessState> InitializeTrialAsync()
{
    var deviceId = await GetOrCreateDeviceIdAsync();
    var platform = DeviceInfo.Platform.ToString().ToLower();
    
    var request = new { deviceId, platform };
    var response = await _httpClient.PostAsJsonAsync("/api/app-user/session", request);
    var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
    
    // Persiste cache local
    await CacheAccessStateAsync(new AppAccessStateResponse
    {
        IsTrialActive = session.IsTrialActive,
        IsPremium = session.IsPremium,
        TrialEndsAt = session.TrialEndsAt,
        CanUseAnalysis = session.IsTrialActive || session.IsPremium
    });
    
    return new AppAccessState
    {
        IsTrialActive = session.IsTrialActive,
        IsPremium = session.IsPremium
    };
}

// MainPage.xaml.cs
protected override async void OnAppearing()
{
    base.OnAppearing();
    
    // Consulta backend
    var accessState = await _accessManager.GetAccessStateAsync();
    
    if (!accessState.CanUseAnalysis)
    {
        await Navigation.PushAsync(new SubscriptionPage());
    }
}
```

---

## 📱 Exemplo 4: Envio do DeviceId na Análise

### ❌ ANTES (DeviceId Ausente ou no Header Errado)

```csharp
// NutritionApiClient.cs
public async Task<AnalysisResult> AnalyzeImageAsync(byte[] imageData)
{
    using var form = new MultipartFormDataContent();
    
    // Adiciona apenas a imagem
    var imageContent = new ByteArrayContent(imageData);
    form.Add(imageContent, "file", "image.jpg");
    
    // DeviceId NÃO é enviado ❌
    
    var response = await _httpClient.PostAsync("/api/nutrition/analyze-simple-image", form);
    return await response.Content.ReadFromJsonAsync<AnalysisResult>();
}

// Backend não consegue verificar acesso!
// Pode rejeitar por falta de informação
```

### ✅ DEPOIS (DeviceId no Form-Data)

```csharp
// NutritionApiClient.cs
public async Task<AnalysisResult> AnalyzeImageAsync(string deviceId, byte[] imageData)
{
    using var form = new MultipartFormDataContent();
    
    // Imagem
    var imageContent = new ByteArrayContent(imageData);
    imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
    form.Add(imageContent, "file", "image.jpg");
    
    // DeviceId - ESSENCIAL ✅
    form.Add(new StringContent(deviceId), "deviceId");
    
    // Language
    form.Add(new StringContent("pt-BR"), "languageCode");
    
    var response = await _httpClient.PostAsync("/api/nutrition/analyze-simple-image", form);
    
    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
    {
        // Backend negou acesso
        return new AnalysisResult
        {
            Success = false,
            AccessDenied = true
        };
    }
    
    return await response.Content.ReadFromJsonAsync<AnalysisResult>();
}

// ViewModel usa assim:
var deviceId = await _accessManager.GetOrCreateDeviceIdAsync();
var result = await _nutritionClient.AnalyzeImageAsync(deviceId, imageBytes);
```

---

## 📱 Exemplo 5: Tratamento de Erro 403

### ❌ ANTES (Erro Genérico)

```csharp
// NutritionAnalysisViewModel.cs
public async Task AnalyzeAsync()
{
    try
    {
        var result = await _apiClient.AnalyzeImageAsync(imageBytes);
        
        if (result.Success)
        {
            ShowResult(result);
        }
        else
        {
            // Não distingue motivo
            await DisplayAlert("Erro", "Falha na análise", "OK");
        }
    }
    catch (HttpRequestException ex)
    {
        // Não trata 403 especificamente
        await DisplayAlert("Erro", ex.Message, "OK");
    }
}
```

### ✅ DEPOIS (Tratamento Específico de Acesso Negado)

```csharp
// NutritionAnalysisViewModel.cs
public async Task AnalyzeAsync()
{
    try
    {
        // Verifica acesso ANTES
        if (!await _accessManager.CanUseAnalysisAsync())
        {
            await DisplayAlert("Acesso Necessário", 
                "Seu trial expirou. Assine para continuar.", "OK");
            await Navigation.PushAsync(new SubscriptionPage());
            return;
        }
        
        var deviceId = await _accessManager.GetOrCreateDeviceIdAsync();
        var result = await _apiClient.AnalyzeImageAsync(deviceId, imageBytes);
        
        if (result.Success)
        {
            ShowResult(result);
        }
        else if (result.AccessDenied)
        {
            // Backend negou durante a análise
            await DisplayAlert("Acesso Negado", result.ErrorMessage, "OK");
            await Navigation.PushAsync(new SubscriptionPage());
        }
        else
        {
            await DisplayAlert("Erro", result.ErrorMessage, "OK");
        }
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
    {
        // 403 - Redireciona para assinatura
        await DisplayAlert("Acesso Necessário", 
            "Seu trial expirou ou você não tem assinatura ativa.", "OK");
        await Navigation.PushAsync(new SubscriptionPage());
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao analisar");
        await DisplayAlert("Erro", "Erro inesperado ao analisar imagem.", "OK");
    }
}
```

---

## 📱 Exemplo 6: Logs de Diagnóstico

### ❌ ANTES (Sem Logs)

```csharp
// OnboardingViewModel.cs
public async Task StartTrialAsync()
{
    var deviceId = GetDeviceId();
    var response = await _apiClient.InitializeSessionAsync(deviceId);
    await Navigation.PushAsync(new MainPage());
}

// NutritionAnalysisViewModel.cs
public async Task AnalyzeAsync()
{
    var canAnalyze = CheckLocalAccess();
    if (!canAnalyze)
    {
        await Navigation.PushAsync(new SubscriptionPage());
        return;
    }
    
    var result = await _apiClient.AnalyzeAsync(imageBytes);
}

// Impossível diagnosticar problema!
```

### ✅ DEPOIS (Logs Completos)

```csharp
// AppAccessManager.cs
public async Task<AppAccessState> InitializeTrialAsync()
{
    var deviceId = await GetOrCreateDeviceIdAsync();
    var platform = DeviceInfo.Platform.ToString().ToLower();
    
    _logger.LogInformation(
        "[TRIAL_INIT] DeviceId: {DeviceId}, Platform: {Platform}", 
        deviceId, platform);
    
    var response = await _httpClient.PostAsJsonAsync("/api/app-user/session", 
        new { deviceId, platform });
    
    var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
    
    _logger.LogInformation(
        "[TRIAL_INIT] Resposta - IsActive: {IsActive}, EndsAt: {EndsAt}, Days: {Days}",
        session.IsTrialActive,
        session.TrialEndsAt,
        session.DaysRemaining);
    
    return MapToAccessState(session);
}

public async Task<bool> CanUseAnalysisAsync()
{
    var deviceId = await GetOrCreateDeviceIdAsync();
    
    _logger.LogInformation(
        "[ACCESS_CHECK] Verificando acesso para DeviceId: {DeviceId}", 
        deviceId);
    
    var state = await GetAccessStateFromApiAsync(deviceId);
    
    _logger.LogInformation(
        "[ACCESS_CHECK] Resultado - CanAnalyze: {CanAnalyze}, Trial: {IsTrialActive}, Premium: {IsPremium}",
        state.CanUseAnalysis,
        state.IsTrialActive,
        state.IsPremium);
    
    return state.CanUseAnalysis;
}

// NutritionAnalysisViewModel.cs
public async Task AnalyzeAsync()
{
    _logger.LogInformation("[ANALYSIS] Iniciando verificação de acesso");
    
    var canAnalyze = await _accessManager.CanUseAnalysisAsync();
    
    if (!canAnalyze)
    {
        _logger.LogWarning("[ANALYSIS] Acesso negado - redirecionando para assinatura");
        await Navigation.PushAsync(new SubscriptionPage());
        return;
    }
    
    _logger.LogInformation("[ANALYSIS] Acesso permitido - iniciando análise");
    
    var deviceId = await _accessManager.GetOrCreateDeviceIdAsync();
    _logger.LogInformation("[ANALYSIS] DeviceId usado: {DeviceId}", deviceId);
    
    var result = await _apiClient.AnalyzeAsync(deviceId, imageBytes);
    
    _logger.LogInformation("[ANALYSIS] Resultado - Success: {Success}", result.Success);
}

// Saída de log esperada:
// [TRIAL_INIT] DeviceId: android-abc123, Platform: android
// [TRIAL_INIT] Resposta - IsActive: true, EndsAt: 2025-06-15, Days: 15
// [ACCESS_CHECK] Verificando acesso para DeviceId: android-abc123
// [ACCESS_CHECK] Resultado - CanAnalyze: true, Trial: true, Premium: false
// [ANALYSIS] Iniciando verificação de acesso
// [ANALYSIS] Acesso permitido - iniciando análise
// [ANALYSIS] DeviceId usado: android-abc123
// [ANALYSIS] Resultado - Success: true
```

---

## 📱 Exemplo 7: Fluxo Completo Antes e Depois

### ❌ ANTES

```csharp
// 1. Onboarding
var deviceId1 = Guid.NewGuid().ToString(); // "abc-123"
await _api.InitSession(deviceId1);
Preferences.Set("trial", true); // Cache local
await Navigation.Push(new MainPage());

// 2. MainPage carrega
var hasTrial = Preferences.Get("trial", false); // true (OK)
// Libera acesso baseado em cache

// 3. Análise
var deviceId2 = DeviceInfo.Name; // "Pixel 7" (diferente!)
await _api.Analyze(deviceId2, image);

// Backend:
// - Recebe deviceId "Pixel 7"
// - Não encontra AppUser com esse ID
// - Cria novo AppUser sem trial configurado corretamente
// - Nega acesso ❌
```

### ✅ DEPOIS

```csharp
// 1. Onboarding
var state = await _accessManager.InitializeTrialAsync();
// Internamente:
// - GetOrCreateDeviceId() → "android-abc123"
// - POST /session com "android-abc123"
// - Backend cria AppUser com trial 15 dias
// - Salva cache local
await Navigation.Push(new MainPage());

// 2. MainPage carrega
var accessState = await _accessManager.GetAccessStateAsync();
// - GetOrCreateDeviceId() → "android-abc123" (mesmo!)
// - GET /access-state?deviceId=android-abc123
// - Backend retorna CanUseAnalysis = true
// - Atualiza cache local

// 3. Análise
var canAnalyze = await _accessManager.CanUseAnalysisAsync();
// - GetOrCreateDeviceId() → "android-abc123" (mesmo!)
// - GET /access-state?deviceId=android-abc123
// - Backend valida trial ativo
// - Retorna CanUseAnalysis = true ✅

if (canAnalyze)
{
    var deviceId = await _accessManager.GetOrCreateDeviceIdAsync(); // "android-abc123"
    await _api.Analyze(deviceId, image);
    // Backend:
    // - Recebe deviceId "android-abc123"
    // - Encontra AppUser existente
    // - Valida trial ativo
    // - Permite análise ✅
}
```

---

## 🎯 Resumo das Mudanças

| Aspecto | ❌ Antes | ✅ Depois |
|---------|----------|-----------|
| **DeviceId** | Diferente em cada tela | Único, persistido, consistente |
| **Verificação de Acesso** | Cache local obsoleto | Sempre consulta backend |
| **Persistência** | Não salva resposta da API | Cache atualizado após cada chamada |
| **DeviceId na Análise** | Ausente ou inconsistente | Sempre enviado, mesmo ID |
| **Erro 403** | Tratamento genérico | Redireciona para assinatura |
| **Logs** | Ausentes | Completos para diagnóstico |
| **Fonte de Verdade** | Múltiplas fontes | AppAccessManager centralizado |

---

## ✅ Resultado Final

**Antes:** Trial ativado no onboarding, mas análise redireciona para assinatura (bug).

**Depois:** Trial ativado no onboarding, análise funciona perfeitamente durante 15 dias.
