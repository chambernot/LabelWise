// ═══════════════════════════════════════════════════════════════════════════
// EXEMPLOS DE USO DA API DE ANÁLISE COM CAPTURETYPE
// ═══════════════════════════════════════════════════════════════════════════

using System.Net.Http;
using System.Net.Http.Headers;
using LabelWise.Application.DTOs.Analysis;
using LabelWise.Domain.Enums;

namespace LabelWise.Examples;

/// <summary>
/// Exemplos de como consumir a API de análise de imagens com CaptureType.
/// </summary>
public class CaptureTypeApiExamples
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.labelwise.com";

    public CaptureTypeApiExamples(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 1: Análise de Tabela Nutricional
    // ═══════════════════════════════════════════════════════════════════════
    
    public async Task<CapturedImageAnalysisResponse?> AnalyzeNutritionTableAsync(
        string imagePath,
        string? barcode = null)
    {
        using var form = new MultipartFormDataContent();
        
        // Adicionar arquivo
        var fileBytes = await File.ReadAllBytesAsync(imagePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(fileContent, "file", Path.GetFileName(imagePath));
        
        // Definir CaptureType
        form.Add(new StringContent(((int)CaptureType.NutritionTable).ToString()), "captureType");
        
        // Barcode opcional
        if (!string.IsNullOrEmpty(barcode))
        {
            form.Add(new StringContent(barcode), "barcode");
        }
        
        var response = await _httpClient.PostAsync($"{BaseUrl}/api/pipeline/analyze", form);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<CapturedImageAnalysisResponse>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 2: Análise de Lista de Ingredientes
    // ═══════════════════════════════════════════════════════════════════════
    
    public async Task<CapturedImageAnalysisResponse?> AnalyzeIngredientsListAsync(
        Stream imageStream,
        string fileName,
        string languageCode = "pt")
    {
        using var form = new MultipartFormDataContent();
        
        // Adicionar arquivo via stream
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(streamContent, "file", fileName);
        
        // Definir CaptureType
        form.Add(new StringContent(((int)CaptureType.IngredientsList).ToString()), "captureType");
        
        // Idioma
        form.Add(new StringContent(languageCode), "languageCode");
        
        var response = await _httpClient.PostAsync($"{BaseUrl}/api/pipeline/analyze", form);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<CapturedImageAnalysisResponse>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 3: Identificação por Código de Barras (sem imagem)
    // ═══════════════════════════════════════════════════════════════════════
    
    public async Task<CapturedImageAnalysisResponse?> IdentifyByBarcodeAsync(string barcode)
    {
        using var form = new MultipartFormDataContent();
        
        // CaptureType = Barcode
        form.Add(new StringContent(((int)CaptureType.Barcode).ToString()), "captureType");
        
        // Barcode obrigatório
        form.Add(new StringContent(barcode), "barcode");
        
        // Habilitar busca em bases externas
        form.Add(new StringContent("true"), "enableExternalDatabaseLookup");
        
        var response = await _httpClient.PostAsync($"{BaseUrl}/api/pipeline/analyze", form);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<CapturedImageAnalysisResponse>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 4: Análise de Embalagem Frontal com todas as opções
    // ═══════════════════════════════════════════════════════════════════════
    
    public async Task<CapturedImageAnalysisResponse?> AnalyzeFrontPackagingAsync(
        byte[] imageBytes,
        string fileName,
        CapturedImageAnalysisRequest options)
    {
        using var form = new MultipartFormDataContent();
        
        // Arquivo
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        form.Add(fileContent, "file", fileName);
        
        // CaptureType
        form.Add(new StringContent(((int)CaptureType.FrontPackaging).ToString()), "captureType");
        
        // Opções adicionais
        if (!string.IsNullOrEmpty(options.Barcode))
        {
            form.Add(new StringContent(options.Barcode), "barcode");
        }
        
        form.Add(new StringContent(options.LanguageCode), "languageCode");
        form.Add(new StringContent(options.EnableExternalDatabaseLookup.ToString()), "enableExternalDatabaseLookup");
        form.Add(new StringContent(options.EnableMultiProviderOcr.ToString()), "enableMultiProviderOcr");
        form.Add(new StringContent(options.ExecuteNutritionalAnalysis.ToString()), "executeNutritionalAnalysis");
        
        var response = await _httpClient.PostAsync($"{BaseUrl}/api/pipeline/analyze", form);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<CapturedImageAnalysisResponse>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 5: Análise de Declaração de Alérgenos
    // ═══════════════════════════════════════════════════════════════════════
    
    public async Task<CapturedImageAnalysisResponse?> AnalyzeAllergenStatementAsync(
        string imagePath)
    {
        using var form = new MultipartFormDataContent();
        
        var fileBytes = await File.ReadAllBytesAsync(imagePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(fileContent, "file", Path.GetFileName(imagePath));
        
        form.Add(new StringContent(((int)CaptureType.AllergenStatement).ToString()), "captureType");
        
        var response = await _httpClient.PostAsync($"{BaseUrl}/api/pipeline/analyze", form);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<CapturedImageAnalysisResponse>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 6: Obter tipos de captura suportados
    // ═══════════════════════════════════════════════════════════════════════
    
    public async Task<IEnumerable<CaptureTypeInfo>?> GetSupportedCaptureTypesAsync()
    {
        var response = await _httpClient.GetAsync($"{BaseUrl}/api/pipeline/capture-types");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<IEnumerable<CaptureTypeInfo>>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 7: Análise completa com tratamento de erros
    // ═══════════════════════════════════════════════════════════════════════
    
    public async Task<AnalysisResult> AnalyzeWithErrorHandlingAsync(
        string imagePath,
        CaptureType captureType,
        string? barcode = null)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            
            // Validar se precisa de arquivo
            if (captureType != CaptureType.Barcode)
            {
                if (!File.Exists(imagePath))
                {
                    return AnalysisResult.Fail("Arquivo não encontrado.");
                }
                
                var fileBytes = await File.ReadAllBytesAsync(imagePath);
                
                // Validar tamanho (max 5MB)
                if (fileBytes.Length > 5 * 1024 * 1024)
                {
                    return AnalysisResult.Fail("Arquivo muito grande. Máximo: 5MB.");
                }
                
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                form.Add(fileContent, "file", Path.GetFileName(imagePath));
            }
            
            // Validar barcode se necessário
            if (captureType == CaptureType.Barcode)
            {
                if (string.IsNullOrEmpty(barcode))
                {
                    return AnalysisResult.Fail("Barcode é obrigatório para este tipo de captura.");
                }
                
                if (!IsValidBarcode(barcode))
                {
                    return AnalysisResult.Fail("Formato de barcode inválido.");
                }
            }
            
            form.Add(new StringContent(((int)captureType).ToString()), "captureType");
            
            if (!string.IsNullOrEmpty(barcode))
            {
                form.Add(new StringContent(barcode), "barcode");
            }
            
            var response = await _httpClient.PostAsync($"{BaseUrl}/api/pipeline/analyze", form);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return AnalysisResult.Fail($"Erro na API: {response.StatusCode} - {errorContent}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<CapturedImageAnalysisResponse>();
            
            if (result == null)
            {
                return AnalysisResult.Fail("Resposta inválida da API.");
            }
            
            if (!result.Success)
            {
                return AnalysisResult.Fail(result.ErrorMessage ?? "Erro desconhecido.");
            }
            
            return AnalysisResult.Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return AnalysisResult.Fail($"Erro de conexão: {ex.Message}");
        }
        catch (Exception ex)
        {
            return AnalysisResult.Fail($"Erro inesperado: {ex.Message}");
        }
    }
    
    private static bool IsValidBarcode(string barcode)
    {
        var clean = barcode.Trim().Replace(" ", "").Replace("-", "");
        return System.Text.RegularExpressions.Regex.IsMatch(clean, @"^\d{8}$|^\d{12}$|^\d{13}$");
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// CLASSES AUXILIARES
// ═══════════════════════════════════════════════════════════════════════════

public class CaptureTypeInfo
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresFile { get; set; }
    public bool RequiresBarcode { get; set; }
}

public class AnalysisResult
{
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }
    public CapturedImageAnalysisResponse? Data { get; private set; }
    
    public static AnalysisResult Ok(CapturedImageAnalysisResponse data) =>
        new() { Success = true, Data = data };
    
    public static AnalysisResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

// ═══════════════════════════════════════════════════════════════════════════
// EXEMPLO DE USO NO CÓDIGO DO CLIENTE
// ═══════════════════════════════════════════════════════════════════════════

public class ClientUsageExample
{
    public async Task RunExamplesAsync()
    {
        var httpClient = new HttpClient();
        var api = new CaptureTypeApiExamples(httpClient);
        
        // ─────────────────────────────────────────────────────────────────
        // Exemplo 1: Analisar tabela nutricional
        // ─────────────────────────────────────────────────────────────────
        Console.WriteLine("📊 Analisando tabela nutricional...");
        var nutritionResult = await api.AnalyzeNutritionTableAsync(
            "C:\\Images\\rotulo_nutricional.jpg",
            barcode: "7891234567890");
        
        if (nutritionResult?.Success == true)
        {
            Console.WriteLine($"✅ Calorias: {nutritionResult.LabelReadingResult?.NutritionalInfo?.Calories}");
            Console.WriteLine($"✅ Ingredientes: {nutritionResult.LabelReadingResult?.Ingredients.Count}");
            Console.WriteLine($"✅ Score: {nutritionResult.FinalAnalysis?.GeneralScore:F2}");
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Exemplo 2: Identificar produto por código de barras
        // ─────────────────────────────────────────────────────────────────
        Console.WriteLine("\n🔍 Identificando produto por barcode...");
        var barcodeResult = await api.IdentifyByBarcodeAsync("7891234567890");
        
        if (barcodeResult?.Success == true)
        {
            Console.WriteLine($"✅ Produto: {barcodeResult.IdentificationResult?.ProductName}");
            Console.WriteLine($"✅ Marca: {barcodeResult.IdentificationResult?.Brand}");
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Exemplo 3: Verificar tipos de captura disponíveis
        // ─────────────────────────────────────────────────────────────────
        Console.WriteLine("\n📋 Tipos de captura suportados:");
        var captureTypes = await api.GetSupportedCaptureTypesAsync();
        
        foreach (var ct in captureTypes ?? Enumerable.Empty<CaptureTypeInfo>())
        {
            Console.WriteLine($"  • {ct.Name} ({ct.Value}): {ct.Description}");
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Exemplo 4: Análise com tratamento de erros
        // ─────────────────────────────────────────────────────────────────
        Console.WriteLine("\n🛡️ Análise com tratamento de erros...");
        var safeResult = await api.AnalyzeWithErrorHandlingAsync(
            "C:\\Images\\ingredientes.jpg",
            CaptureType.IngredientsList);
        
        if (safeResult.Success)
        {
            Console.WriteLine($"✅ Análise concluída com sucesso!");
            Console.WriteLine($"   Confiança: {safeResult.Data?.OverallConfidence:P0}");
        }
        else
        {
            Console.WriteLine($"❌ Erro: {safeResult.ErrorMessage}");
        }
    }
}
