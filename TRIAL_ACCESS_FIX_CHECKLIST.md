# ✅ Checklist de Correção: Trial de 15 Dias

## 📱 Parte 1: Verificação do Código Mobile Atual

### 1.1 DeviceId Consistente

```bash
# Buscar implementações de deviceId
grep -r "deviceId" --include="*.cs" MauiApp/
grep -r "DeviceId" --include="*.cs" MauiApp/
```

**Verificar:**
- [ ] Existe um único método `GetDeviceId()` usado em todo o app?
- [ ] DeviceId é persistido no `SecureStorage` ou `Preferences`?
- [ ] DeviceId é o mesmo no onboarding e na análise?

**Teste Rápido:**
```csharp
// Adicione log temporário
var deviceIdOnboarding = await GetDeviceIdAsync();
Debug.WriteLine($"[ONBOARDING] DeviceId: {deviceIdOnboarding}");

var deviceIdAnalysis = await GetDeviceIdAsync();
Debug.WriteLine($"[ANALYSIS] DeviceId: {deviceIdAnalysis}");

// Devem ser IDÊNTICOS
```

---

### 1.2 Persistência do Trial

**Procurar por:**
```bash
grep -r "trial" --include="*.cs" MauiApp/
grep -r "IsTrialActive" --include="*.cs" MauiApp/
```

**Perguntas:**
- [ ] O app salva `IsTrialActive` localmente após chamar `/api/app-user/session`?
- [ ] O app lê estado local antes de verificar com o backend?
- [ ] Há lógica de cache que pode estar desatualizada?

**Anti-padrão comum:**
```csharp
// ❌ ERRADO - Usa apenas cache local
var isTrialActive = Preferences.Get("trial_active", false);
if (!isTrialActive)
{
    await NavigateToSubscriptionAsync();
    return;
}

// ✅ CORRETO - Consulta backend
var accessState = await _apiClient.GetAccessStateAsync(deviceId);
if (!accessState.CanUseAnalysis)
{
    await NavigateToSubscriptionAsync();
    return;
}
```

---

### 1.3 Chamadas à API

**Buscar chamadas HTTP:**
```bash
grep -r "app-user/session" --include="*.cs" MauiApp/
grep -r "access-state" --include="*.cs" MauiApp/
grep -r "analyze-simple-image" --include="*.cs" MauiApp/
```

**Verificar:**
- [ ] Onboarding chama `POST /api/app-user/session`?
- [ ] Análise chama `GET /api/app-user/access-state` antes de analisar?
- [ ] Análise envia `deviceId` no form-data ou header?

---

## 📋 Parte 2: Implementação do AppAccessManager

### 2.1 Criar Serviço Centralizado

**Arquivo:** `MauiApp/Services/AppAccessManager.cs`

```csharp
using System.Text.Json;

namespace MauiApp.Services;

public class AppAccessManager
{
    private readonly ISecureStorage _secureStorage;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AppAccessManager> _logger;

    private const string DEVICE_ID_KEY = "app_device_id";
    private const string ACCESS_STATE_CACHE_KEY = "access_state_cache";
    private const string CACHE_TIMESTAMP_KEY = "access_cache_timestamp";

    public AppAccessManager(
        ISecureStorage secureStorage,
        HttpClient httpClient,
        ILogger<AppAccessManager> logger)
    {
        _secureStorage = secureStorage;
        _httpClient = httpClient;
        _logger = logger;
    }

    // ========================================
    // DeviceId - Única Fonte de Verdade
    // ========================================

    public async Task<string> GetOrCreateDeviceIdAsync()
    {
        try
        {
            var deviceId = await _secureStorage.GetAsync(DEVICE_ID_KEY);

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                deviceId = GenerateDeviceId();
                await _secureStorage.SetAsync(DEVICE_ID_KEY, deviceId);
                _logger.LogInformation("DeviceId criado: {DeviceId}", deviceId);
            }
            else
            {
                _logger.LogDebug("DeviceId existente: {DeviceId}", deviceId);
            }

            return deviceId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter deviceId");
            throw;
        }
    }

    private string GenerateDeviceId()
    {
        var guid = Guid.NewGuid().ToString("N");
        var platform = DeviceInfo.Platform.ToString().ToLower();
        return $"{platform}-{guid}";
    }

    // ========================================
    // Inicialização do Trial (Onboarding)
    // ========================================

    public async Task<AppAccessState> InitializeTrialAsync()
    {
        var deviceId = await GetOrCreateDeviceIdAsync();
        var platform = DeviceInfo.Platform.ToString().ToLower();

        _logger.LogInformation("Inicializando trial - DeviceId: {DeviceId}, Platform: {Platform}", 
            deviceId, platform);

        try
        {
            var request = new
            {
                deviceId = deviceId,
                platform = platform
            };

            var response = await _httpClient.PostAsJsonAsync("/api/app-user/session", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Erro ao inicializar sessão: {StatusCode} - {Error}", 
                    response.StatusCode, error);
                throw new HttpRequestException($"Erro ao inicializar sessão: {response.StatusCode}");
            }

            var sessionResponse = await response.Content.ReadFromJsonAsync<AppSessionResponse>();

            if (sessionResponse == null)
            {
                throw new InvalidOperationException("Resposta da API é nula");
            }

            _logger.LogInformation(
                "Trial inicializado - IsActive: {IsActive}, EndsAt: {EndsAt}, Days: {Days}",
                sessionResponse.IsTrialActive,
                sessionResponse.TrialEndsAt,
                sessionResponse.DaysRemaining);

            // Cache local para uso offline
            await CacheAccessStateAsync(new AppAccessStateResponse
            {
                DeviceId = sessionResponse.DeviceId,
                IsTrialActive = sessionResponse.IsTrialActive,
                IsPremium = sessionResponse.IsPremium,
                TrialEndsAt = sessionResponse.TrialEndsAt,
                DaysRemaining = sessionResponse.DaysRemaining,
                CanUseAnalysis = sessionResponse.IsTrialActive || sessionResponse.IsPremium,
                CanUseComparison = sessionResponse.IsTrialActive || sessionResponse.IsPremium,
                CanUseHistory = sessionResponse.IsTrialActive || sessionResponse.IsPremium
            });

            return new AppAccessState
            {
                DeviceId = sessionResponse.DeviceId,
                IsTrialActive = sessionResponse.IsTrialActive,
                IsPremium = sessionResponse.IsPremium,
                TrialEndsAt = sessionResponse.TrialEndsAt,
                DaysRemaining = sessionResponse.DaysRemaining
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inicializar trial");
            throw;
        }
    }

    // ========================================
    // Verificação de Acesso (Análise)
    // ========================================

    public async Task<bool> CanUseAnalysisAsync()
    {
        var deviceId = await GetOrCreateDeviceIdAsync();

        _logger.LogInformation("Verificando acesso para análise - DeviceId: {DeviceId}", deviceId);

        try
        {
            var accessState = await GetAccessStateFromApiAsync(deviceId);

            _logger.LogInformation(
                "Estado de acesso - CanAnalyze: {CanAnalyze}, Trial: {Trial}, Premium: {Premium}",
                accessState.CanUseAnalysis,
                accessState.IsTrialActive,
                accessState.IsPremium);

            // Atualiza cache
            await CacheAccessStateAsync(accessState);

            return accessState.CanUseAnalysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar acesso - usando cache");

            // Fallback: usa cache (modo offline)
            var cachedState = await GetCachedAccessStateAsync();
            return cachedState?.CanUseAnalysis ?? false;
        }
    }

    public async Task<AppAccessStateResponse> GetAccessStateAsync()
    {
        var deviceId = await GetOrCreateDeviceIdAsync();

        try
        {
            var state = await GetAccessStateFromApiAsync(deviceId);
            await CacheAccessStateAsync(state);
            return state;
        }
        catch
        {
            return await GetCachedAccessStateAsync() ?? CreateDeniedAccessState(deviceId);
        }
    }

    // ========================================
    // Helpers Privados
    // ========================================

    private async Task<AppAccessStateResponse> GetAccessStateFromApiAsync(string deviceId)
    {
        var response = await _httpClient.GetAsync($"/api/app-user/access-state?deviceId={deviceId}");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Erro ao obter estado de acesso: {StatusCode} - {Error}", 
                response.StatusCode, error);
            throw new HttpRequestException($"Erro na API: {response.StatusCode}");
        }

        var state = await response.Content.ReadFromJsonAsync<AppAccessStateResponse>();
        
        if (state == null)
        {
            throw new InvalidOperationException("Resposta da API é nula");
        }

        return state;
    }

    private async Task CacheAccessStateAsync(AppAccessStateResponse state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state);
            await _secureStorage.SetAsync(ACCESS_STATE_CACHE_KEY, json);
            await _secureStorage.SetAsync(CACHE_TIMESTAMP_KEY, DateTimeOffset.UtcNow.ToString("o"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao salvar cache de acesso");
        }
    }

    private async Task<AppAccessStateResponse?> GetCachedAccessStateAsync()
    {
        try
        {
            var json = await _secureStorage.GetAsync(ACCESS_STATE_CACHE_KEY);
            
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var state = JsonSerializer.Deserialize<AppAccessStateResponse>(json);

            // Verifica se cache expirou (>1 hora)
            var timestampStr = await _secureStorage.GetAsync(CACHE_TIMESTAMP_KEY);
            if (DateTimeOffset.TryParse(timestampStr, out var timestamp))
            {
                if (DateTimeOffset.UtcNow - timestamp > TimeSpan.FromHours(1))
                {
                    _logger.LogWarning("Cache de acesso expirado");
                    return null;
                }
            }

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao ler cache de acesso");
            return null;
        }
    }

    private AppAccessStateResponse CreateDeniedAccessState(string deviceId)
    {
        return new AppAccessStateResponse
        {
            DeviceId = deviceId,
            IsTrialActive = false,
            IsPremium = false,
            CanUseAnalysis = false,
            CanUseComparison = false,
            CanUseHistory = false,
            Message = "Não foi possível verificar o acesso. Tente novamente."
        };
    }
}

// ========================================
// DTOs
// ========================================

public class AppSessionResponse
{
    public string DeviceId { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
    public bool IsTrialActive { get; set; }
    public DateTimeOffset TrialEndsAt { get; set; }
    public int DaysRemaining { get; set; }
    public string SubscriptionStatus { get; set; } = "none";
}

public class AppAccessStateResponse
{
    public string DeviceId { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
    public bool IsTrialActive { get; set; }
    public DateTimeOffset TrialEndsAt { get; set; }
    public int DaysRemaining { get; set; }
    public string SubscriptionStatus { get; set; } = "none";
    public bool CanUseAnalysis { get; set; }
    public bool CanUseComparison { get; set; }
    public bool CanUseHistory { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AppAccessState
{
    public string DeviceId { get; set; } = string.Empty;
    public bool IsTrialActive { get; set; }
    public bool IsPremium { get; set; }
    public DateTimeOffset TrialEndsAt { get; set; }
    public int DaysRemaining { get; set; }
}
```

---

### 2.2 Registrar Serviço (MauiProgram.cs)

```csharp
// MauiProgram.cs
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { });

        // HttpClient configurado
        builder.Services.AddHttpClient("AppApi", client =>
        {
            client.BaseAddress = new Uri("https://your-api.azurewebsites.net");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddSingleton(sp => 
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return factory.CreateClient("AppApi");
        });

        // SecureStorage
        builder.Services.AddSingleton(SecureStorage.Default);

        // AppAccessManager - ÚNICA FONTE DE VERDADE
        builder.Services.AddSingleton<AppAccessManager>();

        // ViewModels
        builder.Services.AddTransient<OnboardingViewModel>();
        builder.Services.AddTransient<NutritionAnalysisViewModel>();

        return builder.Build();
    }
}
```

---

## 📋 Parte 3: Atualizar ViewModels

### 3.1 OnboardingViewModel

```csharp
// ViewModels/OnboardingViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MauiApp.ViewModels;

public partial class OnboardingViewModel : ObservableObject
{
    private readonly AppAccessManager _accessManager;
    private readonly INavigationService _navigation;
    private readonly ILogger<OnboardingViewModel> _logger;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public OnboardingViewModel(
        AppAccessManager accessManager,
        INavigationService navigation,
        ILogger<OnboardingViewModel> logger)
    {
        _accessManager = accessManager;
        _navigation = navigation;
        _logger = logger;
    }

    [RelayCommand]
    private async Task StartTrialAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Ativando trial de 15 dias...";

        try
        {
            _logger.LogInformation("Usuário solicitou ativação de trial");

            var accessState = await _accessManager.InitializeTrialAsync();

            if (accessState.IsTrialActive)
            {
                _logger.LogInformation("Trial ativado com sucesso - {Days} dias restantes", 
                    accessState.DaysRemaining);

                StatusMessage = $"Trial ativado! Você tem {accessState.DaysRemaining} dias grátis.";
                await Task.Delay(1000); // Feedback visual

                // Navega para tela principal
                await _navigation.NavigateToAsync("//main");
            }
            else
            {
                _logger.LogWarning("Trial não foi ativado - IsPremium: {IsPremium}", 
                    accessState.IsPremium);

                StatusMessage = "Erro ao ativar trial. Tente novamente.";
                await ShowAlertAsync("Erro", "Não foi possível ativar o período de teste.");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de rede ao ativar trial");
            StatusMessage = "Erro de conexão.";
            await ShowAlertAsync("Sem Conexão", 
                "Verifique sua internet e tente novamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao ativar trial");
            StatusMessage = "Erro inesperado.";
            await ShowAlertAsync("Erro", 
                "Ocorreu um erro. Tente novamente mais tarde.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SkipToSubscriptionAsync()
    {
        await _navigation.NavigateToAsync("//subscription");
    }

    private async Task ShowAlertAsync(string title, string message)
    {
        await Application.Current?.MainPage?.DisplayAlert(title, message, "OK")!;
    }
}
```

---

### 3.2 NutritionAnalysisViewModel

```csharp
// ViewModels/NutritionAnalysisViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MauiApp.ViewModels;

public partial class NutritionAnalysisViewModel : ObservableObject
{
    private readonly AppAccessManager _accessManager;
    private readonly INutritionApiClient _nutritionClient;
    private readonly INavigationService _navigation;
    private readonly ILogger<NutritionAnalysisViewModel> _logger;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private NutritionAnalysisResult? _analysisResult;

    public NutritionAnalysisViewModel(
        AppAccessManager accessManager,
        INutritionApiClient nutritionClient,
        INavigationService navigation,
        ILogger<NutritionAnalysisViewModel> logger)
    {
        _accessManager = accessManager;
        _nutritionClient = nutritionClient;
        _navigation = navigation;
        _logger = logger;
    }

    [RelayCommand]
    private async Task PickAndAnalyzeImageAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            var photo = await MediaPicker.PickPhotoAsync();
            
            if (photo != null)
            {
                await AnalyzeImageAsync(photo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao selecionar imagem");
            await ShowAlertAsync("Erro", "Não foi possível abrir a galeria.");
        }
    }

    [RelayCommand]
    private async Task TakePhotoAndAnalyzeAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            var photo = await MediaPicker.CapturePhotoAsync();
            
            if (photo != null)
            {
                await AnalyzeImageAsync(photo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao tirar foto");
            await ShowAlertAsync("Erro", "Não foi possível acessar a câmera.");
        }
    }

    private async Task AnalyzeImageAsync(FileResult imageFile)
    {
        IsBusy = true;
        StatusMessage = "Verificando acesso...";

        try
        {
            // ========================================
            // VERIFICAÇÃO DE ACESSO - BACKEND
            // ========================================
            
            var canAnalyze = await _accessManager.CanUseAnalysisAsync();

            if (!canAnalyze)
            {
                _logger.LogWarning("Acesso negado para análise - redirecionando para assinatura");
                
                StatusMessage = "Trial expirado.";
                await ShowAlertAsync("Acesso Necessário", 
                    "Seu período de teste expirou. Assine para continuar usando o app.");
                
                await _navigation.NavigateToAsync("//subscription");
                return;
            }

            _logger.LogInformation("Acesso permitido - iniciando análise");

            // ========================================
            // OBTER DEVICE ID
            // ========================================
            
            var deviceId = await _accessManager.GetOrCreateDeviceIdAsync();
            _logger.LogInformation("Análise com deviceId: {DeviceId}", deviceId);

            // ========================================
            // LER IMAGEM
            // ========================================
            
            StatusMessage = "Carregando imagem...";
            var imageBytes = await ReadImageBytesAsync(imageFile);
            _logger.LogInformation("Imagem carregada: {Size} bytes", imageBytes.Length);

            // ========================================
            // ENVIAR ANÁLISE COM DEVICE ID
            // ========================================
            
            StatusMessage = "Analisando produto...";
            
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
                _logger.LogInformation("Análise concluída: {Product}", result.ProductName);
                
                AnalysisResult = result;
                StatusMessage = "Análise concluída!";
                
                await _navigation.NavigateToAsync("//results", new Dictionary<string, object>
                {
                    ["Result"] = result
                });
            }
            else if (result.AccessDenied)
            {
                _logger.LogWarning("Backend negou acesso durante análise");
                
                StatusMessage = "Acesso negado.";
                await ShowAlertAsync("Acesso Necessário", 
                    result.ErrorMessage ?? "Assine para continuar.");
                
                await _navigation.NavigateToAsync("//subscription");
            }
            else
            {
                _logger.LogWarning("Análise falhou: {Error}", result.ErrorMessage);
                
                StatusMessage = "Erro na análise.";
                await ShowAlertAsync("Erro", 
                    result.ErrorMessage ?? "Não foi possível analisar a imagem.");
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("403 Forbidden ao analisar");
            
            StatusMessage = "Acesso negado.";
            await ShowAlertAsync("Acesso Necessário", 
                "Seu trial expirou ou você não tem uma assinatura ativa.");
            
            await _navigation.NavigateToAsync("//subscription");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de rede ao analisar");
            
            StatusMessage = "Erro de conexão.";
            await ShowAlertAsync("Sem Conexão", 
                "Verifique sua internet e tente novamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao analisar");
            
            StatusMessage = "Erro inesperado.";
            await ShowAlertAsync("Erro", 
                "Ocorreu um erro ao processar a análise.");
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

    private async Task ShowAlertAsync(string title, string message)
    {
        await Application.Current?.MainPage?.DisplayAlert(title, message, "OK")!;
    }
}
```

---

## 📋 Parte 4: Validação

### 4.1 Teste Manual

```
1. Desinstalar app completamente
2. Instalar nova versão
3. Abrir app → Tela de onboarding
4. Clicar "Iniciar trial de 15 dias"
5. [LOG] Verificar: "Trial inicializado - IsActive: true"
6. Navegar para tela principal ✅
7. Tirar foto de produto
8. [LOG] Verificar: "Acesso permitido - iniciando análise"
9. [LOG] Verificar: "Análise com deviceId: android-abc123" (mesmo ID)
10. Análise funciona ✅
11. Ver resultado ✅
```

---

### 4.2 Teste de Trial Expirado

```sql
-- No backend PostgreSQL
UPDATE app_users 
SET trial_ends_at = NOW() - INTERVAL '1 day'
WHERE device_id = 'android-abc123';
```

```
1. Fechar e reabrir app
2. Tentar analisar produto
3. [LOG] Verificar: "Acesso negado - redirecionando"
4. App redireciona para assinatura ✅
```

---

### 4.3 Logs Esperados

**Onboarding (Trial Ativado):**
```
[INFO] Usuário solicitou ativação de trial
[INFO] Inicializando trial - DeviceId: android-abc123, Platform: android
[INFO] Trial inicializado - IsActive: true, EndsAt: 2025-06-15, Days: 15
[INFO] Trial ativado com sucesso - 15 dias restantes
```

**Análise (Acesso Permitido):**
```
[INFO] Verificando acesso para análise - DeviceId: android-abc123
[INFO] Estado de acesso - CanAnalyze: true, Trial: true, Premium: false
[INFO] Acesso permitido - iniciando análise
[INFO] Análise com deviceId: android-abc123
[INFO] Imagem carregada: 245678 bytes
[INFO] Análise concluída: Biscoito Oreo
```

**Análise (Acesso Negado):**
```
[INFO] Verificando acesso para análise - DeviceId: android-abc123
[INFO] Estado de acesso - CanAnalyze: false, Trial: false, Premium: false
[WARN] Acesso negado para análise - redirecionando para assinatura
```

---

## ✅ Checklist Final

- [ ] `AppAccessManager` implementado e registrado
- [ ] `OnboardingViewModel` usa `InitializeTrialAsync()`
- [ ] `NutritionAnalysisViewModel` usa `CanUseAnalysisAsync()` antes de analisar
- [ ] DeviceId único obtido por `GetOrCreateDeviceIdAsync()`
- [ ] DeviceId enviado em TODOS os requests de análise
- [ ] Logs implementados em pontos críticos
- [ ] Teste manual: Trial ativo → Análise funciona
- [ ] Teste manual: Trial expirado → Redireciona para assinatura
- [ ] Código antigo removido (verificações locais obsoletas)

---

## 🎯 Resumo

**Problema:** Trial não reconhecido na análise.

**Causa:** Inconsistência entre onboarding e análise (deviceId diferente, cache local desatualizado, ou falta de verificação com backend).

**Solução:** 
1. Criar `AppAccessManager` como única fonte de verdade
2. Sempre consultar backend para decisões de acesso
3. Garantir mesmo `deviceId` em todo o fluxo
4. Adicionar logs para rastreamento

**Resultado:** Trial de 15 dias funciona corretamente em todo o app.
