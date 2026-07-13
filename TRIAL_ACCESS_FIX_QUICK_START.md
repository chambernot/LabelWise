# 🚀 Quick Start: Correção Trial de 15 Dias

## ⚡ Problema em 30 Segundos

Trial ativado no onboarding → Análise redireciona para assinatura ❌

**Causa:** DeviceId inconsistente ou verificação de acesso incorreta.

---

## ✅ Solução em 5 Passos

### 1️⃣ Criar AppAccessManager (Única Fonte de Verdade)

**Arquivo:** `MauiApp/Services/AppAccessManager.cs`

```csharp
using System.Text.Json;

public class AppAccessManager
{
    private readonly ISecureStorage _secureStorage;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AppAccessManager> _logger;
    private const string DEVICE_ID_KEY = "app_device_id";

    public AppAccessManager(ISecureStorage storage, HttpClient client, ILogger<AppAccessManager> logger)
    {
        _secureStorage = storage;
        _httpClient = client;
        _logger = logger;
    }

    // DeviceId único e persistente
    public async Task<string> GetOrCreateDeviceIdAsync()
    {
        var deviceId = await _secureStorage.GetAsync(DEVICE_ID_KEY);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = $"{DeviceInfo.Platform.ToString().ToLower()}-{Guid.NewGuid():N}";
            await _secureStorage.SetAsync(DEVICE_ID_KEY, deviceId);
            _logger.LogInformation("DeviceId criado: {DeviceId}", deviceId);
        }
        return deviceId;
    }

    // Inicializar trial (Onboarding)
    public async Task<bool> InitializeTrialAsync()
    {
        var deviceId = await GetOrCreateDeviceIdAsync();
        var platform = DeviceInfo.Platform.ToString().ToLower();

        var response = await _httpClient.PostAsJsonAsync("/api/app-user/session", 
            new { deviceId, platform });
        
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();

        _logger.LogInformation("Trial: {IsActive}, Days: {Days}", 
            session.IsTrialActive, session.DaysRemaining);

        return session.IsTrialActive;
    }

    // Verificar se pode analisar (Análise)
    public async Task<bool> CanUseAnalysisAsync()
    {
        var deviceId = await GetOrCreateDeviceIdAsync();
        var response = await _httpClient.GetAsync($"/api/app-user/access-state?deviceId={deviceId}");
        
        response.EnsureSuccessStatusCode();
        var state = await response.Content.ReadFromJsonAsync<AccessStateResponse>();

        _logger.LogInformation("CanAnalyze: {Can}, Trial: {Trial}, Premium: {Premium}",
            state.CanUseAnalysis, state.IsTrialActive, state.IsPremium);

        return state.CanUseAnalysis;
    }
}

public record SessionResponse(bool IsTrialActive, int DaysRemaining);
public record AccessStateResponse(bool CanUseAnalysis, bool IsTrialActive, bool IsPremium);
```

---

### 2️⃣ Registrar no MauiProgram.cs

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // HttpClient
        builder.Services.AddHttpClient("AppApi", c => 
        {
            c.BaseAddress = new Uri("https://your-api.azurewebsites.net");
        });
        builder.Services.AddSingleton(sp => 
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("AppApi"));

        // SecureStorage
        builder.Services.AddSingleton(SecureStorage.Default);

        // AppAccessManager ✅
        builder.Services.AddSingleton<AppAccessManager>();

        return builder.Build();
    }
}
```

---

### 3️⃣ Atualizar OnboardingViewModel

```csharp
public class OnboardingViewModel : ObservableObject
{
    private readonly AppAccessManager _access;

    public OnboardingViewModel(AppAccessManager access)
    {
        _access = access;
    }

    [RelayCommand]
    async Task StartTrial()
    {
        var success = await _access.InitializeTrialAsync();
        
        if (success)
        {
            await Shell.Current.GoToAsync("//main");
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Erro", 
                "Não foi possível ativar o trial", "OK");
        }
    }
}
```

---

### 4️⃣ Atualizar NutritionAnalysisViewModel

```csharp
public class NutritionAnalysisViewModel : ObservableObject
{
    private readonly AppAccessManager _access;
    private readonly HttpClient _client;

    public NutritionAnalysisViewModel(AppAccessManager access, HttpClient client)
    {
        _access = access;
        _client = client;
    }

    [RelayCommand]
    async Task Analyze(FileResult image)
    {
        // 1. Verificar acesso
        if (!await _access.CanUseAnalysisAsync())
        {
            await Application.Current.MainPage.DisplayAlert("Trial Expirado", 
                "Assine para continuar", "OK");
            await Shell.Current.GoToAsync("//subscription");
            return;
        }

        // 2. Obter deviceId
        var deviceId = await _access.GetOrCreateDeviceIdAsync();

        // 3. Preparar request
        using var form = new MultipartFormDataContent();
        
        var imageBytes = await ReadImageAsync(image);
        var imageContent = new ByteArrayContent(imageBytes);
        form.Add(imageContent, "file", image.FileName);
        
        form.Add(new StringContent(deviceId), "deviceId"); // ✅ ESSENCIAL
        form.Add(new StringContent("pt-BR"), "languageCode");

        // 4. Enviar
        var response = await _client.PostAsync("/api/nutrition/analyze-simple-image", form);
        
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            await Shell.Current.GoToAsync("//subscription");
            return;
        }

        var result = await response.Content.ReadFromJsonAsync<AnalysisResult>();
        // Processar resultado...
    }

    async Task<byte[]> ReadImageAsync(FileResult file)
    {
        using var stream = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}
```

---

### 5️⃣ Testar

```bash
# 1. Desinstalar app
adb uninstall com.yourapp

# 2. Instalar nova versão
dotnet build -t:Run -f net10.0-android

# 3. Abrir app
# 4. Clicar "Iniciar trial de 15 dias"
# 5. Tentar analisar produto
# 6. ✅ Deve funcionar!
```

---

## 🔍 Verificação Rápida

### ✅ Checklist

- [ ] `AppAccessManager` criado
- [ ] Registrado no `MauiProgram.cs`
- [ ] `OnboardingViewModel` usa `InitializeTrialAsync()`
- [ ] `AnalysisViewModel` usa `CanUseAnalysisAsync()` antes de analisar
- [ ] DeviceId obtido por `GetOrCreateDeviceIdAsync()`
- [ ] DeviceId enviado no form-data da análise

---

## 📱 Teste Manual

```
1. Abrir app → Onboarding
2. Clicar "Iniciar trial"
3. Ver logs: "Trial: true, Days: 15" ✅
4. Navegar para análise
5. Tirar foto de produto
6. Ver logs: "CanAnalyze: true, Trial: true" ✅
7. Análise funciona ✅
```

---

## 🐛 Logs de Diagnóstico

**Adicione ao código:**

```csharp
// AppAccessManager.cs
public async Task<string> GetOrCreateDeviceIdAsync()
{
    var deviceId = await _secureStorage.GetAsync(DEVICE_ID_KEY);
    if (string.IsNullOrWhiteSpace(deviceId))
    {
        deviceId = GenerateDeviceId();
        await _secureStorage.SetAsync(DEVICE_ID_KEY, deviceId);
    }
    
    Console.WriteLine($"[DEBUG] DeviceId: {deviceId}"); // ✅
    return deviceId;
}

public async Task<bool> InitializeTrialAsync()
{
    // ...
    var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
    Console.WriteLine($"[DEBUG] Trial Ativo: {session.IsTrialActive}, Dias: {session.DaysRemaining}"); // ✅
    return session.IsTrialActive;
}

public async Task<bool> CanUseAnalysisAsync()
{
    // ...
    var state = await response.Content.ReadFromJsonAsync<AccessStateResponse>();
    Console.WriteLine($"[DEBUG] Pode Analisar: {state.CanUseAnalysis}, Trial: {state.IsTrialActive}"); // ✅
    return state.CanUseAnalysis;
}
```

**Saída esperada:**

```
[DEBUG] DeviceId: android-abc123def456
[DEBUG] Trial Ativo: true, Dias: 15
[DEBUG] DeviceId: android-abc123def456
[DEBUG] Pode Analisar: true, Trial: true
```

---

## ❌ Problemas Comuns

### Problema 1: DeviceId Diferente

```
[DEBUG] DeviceId: android-abc123  (onboarding)
[DEBUG] DeviceId: android-xyz789  (análise)  ← ERRADO!
```

**Solução:** Verificar se `GetOrCreateDeviceIdAsync()` é usado em AMBOS.

---

### Problema 2: CanAnalyze Sempre False

```
[DEBUG] Pode Analisar: false, Trial: false
```

**Causas:**
- DeviceId não foi enviado na inicialização do trial
- Backend não criou AppUser corretamente
- Trial expirado (verificar data no banco)

**Debug:**
```sql
-- PostgreSQL
SELECT device_id, trial_ends_at, is_premium 
FROM app_users 
WHERE device_id = 'android-abc123';
```

---

### Problema 3: Erro 403 na Análise

```
Status: 403 Forbidden
```

**Causa:** Backend negou acesso porque:
- DeviceId não foi enviado no form-data
- Trial realmente expirou
- AppUser não existe

**Solução:**
```csharp
// Verificar se deviceId está sendo enviado
form.Add(new StringContent(deviceId), "deviceId"); // ✅
```

---

## 🎯 Resumo

**3 Regras de Ouro:**

1. **DeviceId único:** Sempre use `await _accessManager.GetOrCreateDeviceIdAsync()`
2. **Consulte o backend:** Sempre use `await _accessManager.CanUseAnalysisAsync()`
3. **Envie o DeviceId:** Sempre adicione `deviceId` ao form-data

**Resultado:** Trial de 15 dias funciona perfeitamente! ✅

---

## 📚 Documentação Completa

- **Diagnóstico:** `TRIAL_ACCESS_FIX_DIAGNOSTIC.md`
- **Checklist:** `TRIAL_ACCESS_FIX_CHECKLIST.md`
- **Exemplos:** `TRIAL_ACCESS_FIX_EXAMPLES.cs`
- **Quick Start:** Este arquivo

---

## 🆘 Precisa de Ajuda?

**Verifique os logs:**
- Onboarding: "Trial Ativo: true"?
- Análise: "DeviceId" é o mesmo?
- Análise: "Pode Analisar: true"?

**Se não funcionar:**
1. Desinstale o app completamente
2. Limpe cache: `adb shell pm clear com.yourapp`
3. Reinstale e teste novamente
4. Verifique logs do backend (Application Insights)

---

**Tempo estimado de implementação:** 30-60 minutos

**Complexidade:** Baixa (substituir chamadas existentes por `AppAccessManager`)

**Impacto:** Alto (corrige bug crítico de trial)
