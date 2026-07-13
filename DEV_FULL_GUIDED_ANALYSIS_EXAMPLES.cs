// ═══════════════════════════════════════════════════════════════════════════
// EXEMPLOS DE USO - DEV FULL GUIDED ANALYSIS ENDPOINT
// ═══════════════════════════════════════════════════════════════════════════
// Este arquivo demonstra como usar o endpoint de desenvolvimento para testar
// o fluxo completo de captura guiada em diferentes cenários.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos de uso do endpoint /api/dev/full-guided-analysis-test
    /// </summary>
    public class DevFullGuidedAnalysisExamples
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;
        private string _authToken;

        public DevFullGuidedAnalysisExamples(string apiBaseUrl = "https://localhost:7319")
        {
            _apiBaseUrl = apiBaseUrl;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(apiBaseUrl)
            };
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 1: ANÁLISE COMPLETA COM TODAS AS IMAGENS
        // ═══════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Envia todas as 4 imagens + barcode para análise completa.
        /// </summary>
        public async Task Example1_CompleteAnalysisAsync()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 1: ANÁLISE COMPLETA COM TODAS AS IMAGENS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            // 1. Login
            await LoginAsync("test@example.com", "Test@123");

            // 2. Preparar multipart form data
            using var content = new MultipartFormDataContent();

            // Adicionar imagens
            AddImageToForm(content, "frontImage", @"C:\temp\images\front.jpg");
            AddImageToForm(content, "ingredientsImage", @"C:\temp\images\ingredients.jpg");
            AddImageToForm(content, "nutritionImage", @"C:\temp\images\nutrition.jpg");
            AddImageToForm(content, "allergenImage", @"C:\temp\images\allergen.jpg");

            // Adicionar barcode
            content.Add(new StringContent("7891234567890"), "barcode");

            // Adicionar metadados
            content.Add(new StringContent("pt-BR"), "languageCode");
            content.Add(new StringContent("C# Example Client"), "deviceInfo");

            // 3. Enviar request
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

            var response = await _httpClient.PostAsync("/api/dev/full-guided-analysis-test", content);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine("Response:");
            Console.WriteLine(JsonDocument.Parse(json).RootElement.ToString());
            Console.WriteLine();

            // 4. Processar resultado
            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<FullGuidedAnalysisResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                Console.WriteLine($"✅ Success: {result.Success}");
                Console.WriteLine($"⏱️  Duration: {result.TotalDuration}");
                Console.WriteLine($"📊 Steps Processed: {result.ProcessedSteps.Count}");

                if (result.ProductIdentification != null)
                {
                    Console.WriteLine($"🏷️  Product: {result.ProductIdentification.ProductName}");
                    Console.WriteLine($"🏢 Brand: {result.ProductIdentification.Brand}");
                }

                if (result.FinalAnalysis != null)
                {
                    Console.WriteLine($"🎯 Classification: {result.FinalAnalysis.Classification}");
                    Console.WriteLine($"⭐ Score: {result.FinalAnalysis.OverallScore:F2}/5.0");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 2: TESTE APENAS COM INGREDIENTES
        // ═══════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Testa envio apenas da imagem de ingredientes.
        /// </summary>
        public async Task Example2_IngredientsOnlyAsync()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 2: TESTE APENAS COM INGREDIENTES");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            await LoginAsync("test@example.com", "Test@123");

            using var content = new MultipartFormDataContent();
            AddImageToForm(content, "ingredientsImage", @"C:\temp\images\ingredients.jpg");
            content.Add(new StringContent("pt-BR"), "languageCode");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            var response = await _httpClient.PostAsync("/api/dev/full-guided-analysis-test", content);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<FullGuidedAnalysisResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                Console.WriteLine($"✅ Success: {result.Success}");
                Console.WriteLine($"⚠️  Warnings: {result.Warnings.Count}");
                Console.WriteLine($"📝 Missing Steps: {result.MissingRequiredSteps.Count}");

                if (result.Ingredients != null)
                {
                    Console.WriteLine($"🥗 Ingredients Detected: {result.Ingredients.TotalCount}");
                    Console.WriteLine($"   Confidence: {result.Ingredients.ParseConfidence:P2}");

                    foreach (var ingredient in result.Ingredients.DetectedIngredients)
                    {
                        Console.WriteLine($"   - {ingredient}");
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Expected Warnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"   ⚠️  {warning}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 3: TESTE APENAS COM NUTRIÇÃO
        // ═══════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Testa envio apenas da tabela nutricional.
        /// </summary>
        public async Task Example3_NutritionOnlyAsync()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 3: TESTE APENAS COM NUTRIÇÃO");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            await LoginAsync("test@example.com", "Test@123");

            using var content = new MultipartFormDataContent();
            AddImageToForm(content, "nutritionImage", @"C:\temp\images\nutrition.jpg");
            content.Add(new StringContent("pt-BR"), "languageCode");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            var response = await _httpClient.PostAsync("/api/dev/full-guided-analysis-test", content);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<FullGuidedAnalysisResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result.NutritionalFacts != null)
                {
                    Console.WriteLine($"📊 Nutritional Facts:");
                    Console.WriteLine($"   Serving Size: {result.NutritionalFacts.ServingSize}");
                    Console.WriteLine($"   Calories: {result.NutritionalFacts.Calories} kcal");
                    Console.WriteLine($"   Nutrients Detected: {result.NutritionalFacts.NutrientsDetected}");
                    Console.WriteLine($"   Parse Confidence: {result.NutritionalFacts.ParseConfidence:P2}");
                    Console.WriteLine();
                    Console.WriteLine("   Nutrients:");

                    foreach (var nutrient in result.NutritionalFacts.Nutrients)
                    {
                        Console.WriteLine($"   - {nutrient.Key}: {nutrient.Value.ValuePer100g}{nutrient.Value.Unit}");
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 4: TESTE COM BARCODE APENAS
        // ═══════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Testa envio apenas do código de barras (sem imagens).
        /// </summary>
        public async Task Example4_BarcodeOnlyAsync()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 4: TESTE COM BARCODE APENAS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            await LoginAsync("test@example.com", "Test@123");

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent("7891234567890"), "barcode");
            content.Add(new StringContent("pt-BR"), "languageCode");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            var response = await _httpClient.PostAsync("/api/dev/full-guided-analysis-test", content);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<FullGuidedAnalysisResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                Console.WriteLine($"✅ Success: {result.Success}");

                if (result.ProductIdentification != null)
                {
                    Console.WriteLine($"🏷️  Product Identification:");
                    Console.WriteLine($"   Barcode: {result.ProductIdentification.Barcode}");
                    Console.WriteLine($"   Method: {result.ProductIdentification.Method}");
                    Console.WriteLine($"   Confidence: {result.ProductIdentification.Confidence:P2}");

                    if (!string.IsNullOrEmpty(result.ProductIdentification.ProductName))
                    {
                        Console.WriteLine($"   Product: {result.ProductIdentification.ProductName}");
                        Console.WriteLine($"   Brand: {result.ProductIdentification.Brand}");
                    }
                    else
                    {
                        Console.WriteLine("   ⚠️  Produto não encontrado no catálogo");
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 5: ANÁLISE DE METADADOS DETALHADOS
        // ═══════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Demonstra como acessar metadados detalhados de cada etapa processada.
        /// </summary>
        public async Task Example5_DetailedMetadataAsync()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 5: ANÁLISE DE METADADOS DETALHADOS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            await LoginAsync("test@example.com", "Test@123");

            using var content = new MultipartFormDataContent();
            AddImageToForm(content, "ingredientsImage", @"C:\temp\images\ingredients.jpg");
            AddImageToForm(content, "nutritionImage", @"C:\temp\images\nutrition.jpg");
            content.Add(new StringContent("pt-BR"), "languageCode");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            var response = await _httpClient.PostAsync("/api/dev/full-guided-analysis-test", content);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<FullGuidedAnalysisResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                Console.WriteLine($"📊 PROCESSED STEPS DETAILS:");
                Console.WriteLine();

                foreach (var step in result.ProcessedSteps)
                {
                    var statusIcon = step.Success ? "✅" : "❌";
                    Console.WriteLine($"{statusIcon} {step.StepName} ({step.CaptureType})");
                    Console.WriteLine($"   Success: {step.Success}");
                    Console.WriteLine($"   Duration: {step.Duration}");
                    Console.WriteLine($"   File Size: {step.FileSizeBytes / 1024.0:F2} KB");

                    if (step.OcrResult != null)
                    {
                        Console.WriteLine($"   OCR:");
                        Console.WriteLine($"      Confidence: {step.OcrResult.Confidence:P2}");
                        Console.WriteLine($"      Text Length: {step.OcrResult.TextLength}");
                        Console.WriteLine($"      Provider: {step.OcrResult.Provider}");
                        Console.WriteLine($"      Duration: {step.OcrResult.OcrDuration}");
                        Console.WriteLine($"      Preview: {step.OcrResult.PreviewText?.Substring(0, Math.Min(100, step.OcrResult.PreviewText.Length))}...");
                    }

                    if (step.ParsingResult != null)
                    {
                        Console.WriteLine($"   Parsing:");
                        Console.WriteLine($"      Confidence: {step.ParsingResult.Confidence:P2}");
                        Console.WriteLine($"      Items Extracted: {step.ParsingResult.ItemsExtracted}");
                        Console.WriteLine($"      Duration: {step.ParsingResult.ParsingDuration}");
                    }

                    if (step.StepWarnings.Count > 0)
                    {
                        Console.WriteLine($"   Warnings:");
                        foreach (var warning in step.StepWarnings)
                        {
                            Console.WriteLine($"      ⚠️  {warning}");
                        }
                    }

                    if (step.StepErrors.Count > 0)
                    {
                        Console.WriteLine($"   Errors:");
                        foreach (var error in step.StepErrors)
                        {
                            Console.WriteLine($"      ❌ {error}");
                        }
                    }

                    Console.WriteLine();
                }

                // Confidence Details
                if (result.ConfidenceDetails != null)
                {
                    Console.WriteLine("🎓 CONFIDENCE DETAILS:");
                    Console.WriteLine($"   Overall: {result.ConfidenceDetails.Overall:P2}");
                    foreach (var dim in result.ConfidenceDetails.Dimensions)
                    {
                        Console.WriteLine($"   {dim.Key}: {dim.Value:P2}");
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 6: TRATAMENTO DE ERROS
        // ═══════════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Demonstra como tratar diferentes tipos de erros.
        /// </summary>
        public async Task Example6_ErrorHandlingAsync()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 6: TRATAMENTO DE ERROS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            await LoginAsync("test@example.com", "Test@123");

            // Teste 1: Sem imagens
            Console.WriteLine("📋 Teste 1: Request sem imagens nem barcode");
            using (var content = new MultipartFormDataContent())
            {
                content.Add(new StringContent("pt-BR"), "languageCode");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                var response = await _httpClient.PostAsync("/api/dev/full-guided-analysis-test", content);

                Console.WriteLine($"   Status: {response.StatusCode}"); // Esperado: 400 Bad Request
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"   Error: {error}");
                }
            }
            Console.WriteLine();

            // Teste 2: Arquivo muito grande (simulação)
            Console.WriteLine("📋 Teste 2: Arquivo muito grande");
            Console.WriteLine("   (Criar arquivo > 10MB para testar)");
            Console.WriteLine();

            // Teste 3: Token inválido
            Console.WriteLine("📋 Teste 3: Token JWT inválido");
            using (var content = new MultipartFormDataContent())
            {
                AddImageToForm(content, "nutritionImage", @"C:\temp\images\nutrition.jpg");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-invalido");
                var response = await _httpClient.PostAsync("/api/dev/full-guided-analysis-test", content);

                Console.WriteLine($"   Status: {response.StatusCode}"); // Esperado: 401 Unauthorized
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // MÉTODOS AUXILIARES
        // ═══════════════════════════════════════════════════════════════════════════

        private async Task LoginAsync(string email, string password)
        {
            var loginContent = new StringContent(
                JsonSerializer.Serialize(new { email, password }),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/api/auth/login", loginContent);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var loginResult = JsonSerializer.Deserialize<LoginResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                _authToken = loginResult.Token;
                Console.WriteLine($"✅ Login successful. Token: {_authToken.Substring(0, 20)}...");
                Console.WriteLine();
            }
            else
            {
                throw new Exception($"Login failed: {json}");
            }
        }

        private void AddImageToForm(MultipartFormDataContent content, string fieldName, string filePath)
        {
            if (File.Exists(filePath))
            {
                var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                content.Add(fileContent, fieldName, Path.GetFileName(filePath));
                Console.WriteLine($"   📎 Added: {fieldName} -> {Path.GetFileName(filePath)}");
            }
            else
            {
                Console.WriteLine($"   ⚠️  File not found: {filePath}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DTOs SIMPLIFICADOS (usar os reais do projeto)
    // ═══════════════════════════════════════════════════════════════════════════

    public class LoginResponse
    {
        public string Token { get; set; }
    }

    public class FullGuidedAnalysisResponse
    {
        public Guid SessionId { get; set; }
        public DateTime ProcessedAt { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public bool Success { get; set; }
        public ProductIdentificationSummary ProductIdentification { get; set; }
        public IngredientsDetectionSummary Ingredients { get; set; }
        public AllergensDetectionSummary Allergens { get; set; }
        public NutritionalFactsSummary NutritionalFacts { get; set; }
        public FinalAnalysisSummary FinalAnalysis { get; set; }
        public ConfidenceDetailsDto ConfidenceDetails { get; set; }
        public List<ProcessedStepMetadata> ProcessedSteps { get; set; } = new();
        public List<string> MissingRequiredSteps { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    // Adicionar outras classes conforme necessário...
}
