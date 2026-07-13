// ═══════════════════════════════════════════════════════════════════════════
// LABEL READING SERVICE - EXEMPLOS DE USO
// ═══════════════════════════════════════════════════════════════════════════

using LabelWise.Application.DTOs.LabelReading;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos práticos de uso do LabelReadingService.
    /// </summary>
    public class LabelReadingExamples
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 1: Leitura Completa do Rótulo (Múltiplas Capturas)
        // ═══════════════════════════════════════════════════════════════════════════

        public class Example1_CompleteLabel : ControllerBase
        {
            private readonly ILabelReadingService _labelReadingService;

            public Example1_CompleteLabel(ILabelReadingService labelReadingService)
            {
                _labelReadingService = labelReadingService;
            }

            /// <summary>
            /// Exemplo: Usuário tira 3 fotos (tabela nutricional, ingredientes, alérgenos).
            /// O serviço processa todas e consolida os dados.
            /// </summary>
            [HttpPost("api/label-reading/complete")]
            public async Task<IActionResult> ReadCompleteLabel(
                [FromForm] IFormFile nutritionTableImage,
                [FromForm] IFormFile ingredientsImage,
                [FromForm] IFormFile allergensImage)
            {
                // Criar request com múltiplas capturas
                var request = new LabelReadingRequest
                {
                    UserId = 123,
                    LanguageCode = "pt",
                    EnableMultiProviderOcr = true,
                    OcrConfidenceThreshold = 0.85,
                    Captures = new List<LabelCapture>()
                };

                // Adicionar captura 1: Tabela Nutricional
                if (nutritionTableImage != null)
                {
                    using var ms1 = new MemoryStream();
                    await nutritionTableImage.CopyToAsync(ms1);

                    request.Captures.Add(new LabelCapture
                    {
                        CaptureType = CaptureType.NutritionTable,
                        ImageData = ms1.ToArray(),
                        FileName = nutritionTableImage.FileName,
                        ContentType = nutritionTableImage.ContentType,
                        Priority = 1 // Alta prioridade
                    });
                }

                // Adicionar captura 2: Lista de Ingredientes
                if (ingredientsImage != null)
                {
                    using var ms2 = new MemoryStream();
                    await ingredientsImage.CopyToAsync(ms2);

                    request.Captures.Add(new LabelCapture
                    {
                        CaptureType = CaptureType.IngredientsList,
                        ImageData = ms2.ToArray(),
                        FileName = ingredientsImage.FileName,
                        ContentType = ingredientsImage.ContentType,
                        Priority = 2
                    });
                }

                // Adicionar captura 3: Declaração de Alérgenos
                if (allergensImage != null)
                {
                    using var ms3 = new MemoryStream();
                    await allergensImage.CopyToAsync(ms3);

                    request.Captures.Add(new LabelCapture
                    {
                        CaptureType = CaptureType.AllergenStatement,
                        ImageData = ms3.ToArray(),
                        FileName = allergensImage.FileName,
                        ContentType = allergensImage.ContentType,
                        Priority = 3
                    });
                }

                // Executar leitura
                var result = await _labelReadingService.ReadLabelAsync(request);

                if (!result.Success)
                {
                    return BadRequest(new
                    {
                        error = result.ErrorMessage,
                        warnings = result.Warnings
                    });
                }

                // Retornar dados consolidados
                return Ok(new
                {
                    success = true,
                    confidence = result.OverallConfidence,

                    // Informações nutricionais extraídas
                    nutritionalInfo = new
                    {
                        servingSize = result.NutritionalInfo?.ServingSize,
                        calories = result.NutritionalInfo?.Calories,
                        carbohydrates = result.NutritionalInfo?.Carbohydrates,
                        proteins = result.NutritionalInfo?.Proteins,
                        totalFat = result.NutritionalInfo?.TotalFat,
                        saturatedFat = result.NutritionalInfo?.SaturatedFat,
                        fiber = result.NutritionalInfo?.Fiber,
                        sodium = result.NutritionalInfo?.Sodium,
                        sugars = result.NutritionalInfo?.Sugars
                    },

                    // Lista de ingredientes
                    ingredients = result.Ingredients,

                    // Alérgenos identificados
                    allergens = result.Allergens,

                    // Claims nutricionais (ex: "Sem glúten", "Rico em fibras")
                    nutritionalClaims = result.NutritionalClaims,

                    // Detalhes de cada captura processada
                    captureDetails = result.CaptureResults.Select(c => new
                    {
                        type = c.CaptureType.ToString(),
                        success = c.Success,
                        confidence = c.Confidence,
                        ocrProvider = c.OcrProvider,
                        processingTime = c.ProcessingTimeSeconds,
                        error = c.ErrorMessage
                    }),

                    // Metadata e warnings
                    warnings = result.Warnings,
                    processingTime = result.ProcessingTimeSeconds,
                    metadata = result.Metadata
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 2: Leitura Específica - Apenas Tabela Nutricional
        // ═══════════════════════════════════════════════════════════════════════════

        public class Example2_NutritionTableOnly : ControllerBase
        {
            private readonly ILabelReadingService _labelReadingService;

            public Example2_NutritionTableOnly(ILabelReadingService labelReadingService)
            {
                _labelReadingService = labelReadingService;
            }

            /// <summary>
            /// Exemplo: Extrair apenas informações nutricionais de uma imagem.
            /// Útil quando o usuário quer analisar apenas a tabela nutricional.
            /// </summary>
            [HttpPost("api/label-reading/nutrition-table")]
            public async Task<IActionResult> ReadNutritionTable([FromForm] IFormFile image)
            {
                if (image == null || image.Length == 0)
                {
                    return BadRequest(new { error = "Nenhuma imagem fornecida" });
                }

                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);

                // Método especializado para leitura de tabela nutricional
                var nutritionInfo = await _labelReadingService.ReadNutritionTableAsync(
                    memoryStream.ToArray(),
                    languageCode: "pt");

                if (nutritionInfo == null)
                {
                    return BadRequest(new
                    {
                        error = "Não foi possível extrair informações nutricionais da imagem"
                    });
                }

                // Calcular score nutricional simplificado
                var nutritionalScore = CalculateSimpleNutritionalScore(nutritionInfo);

                return Ok(new
                {
                    success = true,
                    data = nutritionInfo,
                    analysis = new
                    {
                        score = nutritionalScore,
                        hasHighSodium = nutritionInfo.Sodium > 600, // > 600mg = alto sódio
                        hasHighSugars = nutritionInfo.Sugars > 15, // > 15g = alto açúcar
                        hasHighFat = nutritionInfo.TotalFat > 10, // > 10g = alta gordura
                        isHighFiber = nutritionInfo.Fiber >= 3 // >= 3g = rico em fibras
                    }
                });
            }

            private double CalculateSimpleNutritionalScore(NutritionalInformationDto info)
            {
                var score = 100.0;

                // Penalizar alto sódio
                if (info.Sodium > 600) score -= 20;
                else if (info.Sodium > 400) score -= 10;

                // Penalizar alto açúcar
                if (info.Sugars > 15) score -= 20;
                else if (info.Sugars > 10) score -= 10;

                // Penalizar alta gordura saturada
                if (info.SaturatedFat > 5) score -= 15;
                else if (info.SaturatedFat > 3) score -= 7;

                // Bonificar fibras
                if (info.Fiber >= 3) score += 10;

                return Math.Max(0, Math.Min(100, score));
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 3: Leitura Específica - Apenas Ingredientes
        // ═══════════════════════════════════════════════════════════════════════════

        public class Example3_IngredientsOnly : ControllerBase
        {
            private readonly ILabelReadingService _labelReadingService;

            public Example3_IngredientsOnly(ILabelReadingService labelReadingService)
            {
                _labelReadingService = labelReadingService;
            }

            /// <summary>
            /// Exemplo: Extrair apenas lista de ingredientes.
            /// Útil para análise de composição ou verificação de restrições.
            /// </summary>
            [HttpPost("api/label-reading/ingredients")]
            public async Task<IActionResult> ReadIngredients([FromForm] IFormFile image)
            {
                if (image == null || image.Length == 0)
                {
                    return BadRequest(new { error = "Nenhuma imagem fornecida" });
                }

                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);

                var ingredients = await _labelReadingService.ReadIngredientsAsync(
                    memoryStream.ToArray(),
                    languageCode: "pt");

                if (!ingredients.Any())
                {
                    return BadRequest(new
                    {
                        error = "Nenhum ingrediente identificado na imagem"
                    });
                }

                // Análise de ingredientes
                var analysis = AnalyzeIngredients(ingredients);

                return Ok(new
                {
                    success = true,
                    totalIngredients = ingredients.Count,
                    ingredients = ingredients,
                    analysis = new
                    {
                        hasArtificialSweeteners = analysis.HasArtificialSweeteners,
                        hasPreservatives = analysis.HasPreservatives,
                        hasColorants = analysis.HasColorants,
                        isUltraProcessed = analysis.IsUltraProcessed,
                        warnings = analysis.Warnings
                    }
                });
            }

            private IngredientAnalysis AnalyzeIngredients(List<string> ingredients)
            {
                var analysis = new IngredientAnalysis();

                var ingredientsText = string.Join(" ", ingredients).ToLowerInvariant();

                // Detectar adoçantes artificiais
                var artificialSweeteners = new[] { "aspartame", "sucralose", "acesulfame", "sacarina" };
                analysis.HasArtificialSweeteners = artificialSweeteners.Any(s => ingredientsText.Contains(s));

                // Detectar conservantes
                var preservatives = new[] { "benzoato", "sorbato", "nitrito", "nitrato" };
                analysis.HasPreservatives = preservatives.Any(p => ingredientsText.Contains(p));

                // Detectar corantes
                var colorants = new[] { "corante", "tartrazina", "amaranto", "caramelo" };
                analysis.HasColorants = colorants.Any(c => ingredientsText.Contains(c));

                // Produto ultraprocessado se tem muitos ingredientes ou aditivos
                analysis.IsUltraProcessed = ingredients.Count > 10 ||
                    (analysis.HasArtificialSweeteners && analysis.HasPreservatives);

                // Gerar warnings
                if (analysis.HasArtificialSweeteners)
                    analysis.Warnings.Add("Contém adoçantes artificiais");
                if (analysis.HasPreservatives)
                    analysis.Warnings.Add("Contém conservantes");
                if (analysis.HasColorants)
                    analysis.Warnings.Add("Contém corantes");
                if (analysis.IsUltraProcessed)
                    analysis.Warnings.Add("Produto ultraprocessado");

                return analysis;
            }

            public class IngredientAnalysis
            {
                public bool HasArtificialSweeteners { get; set; }
                public bool HasPreservatives { get; set; }
                public bool HasColorants { get; set; }
                public bool IsUltraProcessed { get; set; }
                public List<string> Warnings { get; set; } = new();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 4: Leitura Específica - Apenas Alérgenos
        // ═══════════════════════════════════════════════════════════════════════════

        public class Example4_AllergensOnly : ControllerBase
        {
            private readonly ILabelReadingService _labelReadingService;

            public Example4_AllergensOnly(ILabelReadingService labelReadingService)
            {
                _labelReadingService = labelReadingService;
            }

            /// <summary>
            /// Exemplo: Extrair apenas declarações de alérgenos.
            /// Útil para verificação de alergias e restrições alimentares.
            /// </summary>
            [HttpPost("api/label-reading/allergens")]
            public async Task<IActionResult> ReadAllergens(
                [FromForm] IFormFile image,
                [FromQuery] string[] userAllergens)
            {
                if (image == null || image.Length == 0)
                {
                    return BadRequest(new { error = "Nenhuma imagem fornecida" });
                }

                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);

                var allergens = await _labelReadingService.ReadAllergensAsync(
                    memoryStream.ToArray(),
                    languageCode: "pt");

                // Verificar se produto é seguro para o usuário
                var userAllergensList = userAllergens?.Select(a => a.ToLowerInvariant()).ToList()
                    ?? new List<string>();

                var matchingAllergens = allergens
                    .Where(a => userAllergensList.Any(ua => a.ToLowerInvariant().Contains(ua)))
                    .ToList();

                var isSafe = !matchingAllergens.Any();

                return Ok(new
                {
                    success = true,
                    allergens = allergens,
                    userCheck = new
                    {
                        isSafe = isSafe,
                        matchingAllergens = matchingAllergens,
                        recommendation = isSafe
                            ? "✅ Produto seguro para consumo"
                            : "⚠️ ATENÇÃO: Produto contém alérgenos aos quais você é sensível"
                    }
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 5: Processamento em Lote (Múltiplas Imagens)
        // ═══════════════════════════════════════════════════════════════════════════

        public class Example5_BatchProcessing : ControllerBase
        {
            private readonly ILabelReadingService _labelReadingService;

            public Example5_BatchProcessing(ILabelReadingService labelReadingService)
            {
                _labelReadingService = labelReadingService;
            }

            /// <summary>
            /// Exemplo: Processar múltiplas imagens de diferentes produtos.
            /// Útil para análise comparativa ou catalogação em massa.
            /// </summary>
            [HttpPost("api/label-reading/batch")]
            public async Task<IActionResult> BatchReadLabels([FromForm] IFormFileCollection images)
            {
                if (images == null || !images.Any())
                {
                    return BadRequest(new { error = "Nenhuma imagem fornecida" });
                }

                var results = new List<object>();

                foreach (var image in images)
                {
                    try
                    {
                        using var memoryStream = new MemoryStream();
                        await image.CopyToAsync(memoryStream);

                        // Tentar inferir tipo de captura do nome do arquivo
                        var captureType = InferCaptureType(image.FileName);

                        var request = new LabelReadingRequest
                        {
                            UserId = 0,
                            LanguageCode = "pt",
                            Captures = new List<LabelCapture>
                            {
                                new LabelCapture
                                {
                                    CaptureType = captureType,
                                    ImageData = memoryStream.ToArray(),
                                    FileName = image.FileName
                                }
                            }
                        };

                        var result = await _labelReadingService.ReadLabelAsync(request);

                        results.Add(new
                        {
                            fileName = image.FileName,
                            success = result.Success,
                            confidence = result.OverallConfidence,
                            captureType = captureType.ToString(),
                            error = result.ErrorMessage
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            fileName = image.FileName,
                            success = false,
                            error = ex.Message
                        });
                    }
                }

                return Ok(new
                {
                    totalProcessed = images.Count,
                    successCount = results.Count(r => (bool)((dynamic)r).success),
                    results = results
                });
            }

            private CaptureType InferCaptureType(string fileName)
            {
                var lowerName = fileName.ToLowerInvariant();

                if (lowerName.Contains("nutrition") || lowerName.Contains("tabela"))
                    return CaptureType.NutritionTable;

                if (lowerName.Contains("ingredients") || lowerName.Contains("ingredientes"))
                    return CaptureType.IngredientsList;

                if (lowerName.Contains("allergen") || lowerName.Contains("alergicos"))
                    return CaptureType.AllergenStatement;

                if (lowerName.Contains("front") || lowerName.Contains("frente"))
                    return CaptureType.FrontPackaging;

                if (lowerName.Contains("barcode") || lowerName.Contains("codigo"))
                    return CaptureType.Barcode;

                return CaptureType.FrontPackaging; // Default
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 6: Uso Direto em Serviço de Negócio
        // ═══════════════════════════════════════════════════════════════════════════

        public class ProductAnalysisBusinessService
        {
            private readonly ILabelReadingService _labelReadingService;

            public ProductAnalysisBusinessService(ILabelReadingService labelReadingService)
            {
                _labelReadingService = labelReadingService;
            }

            /// <summary>
            /// Exemplo: Integração com lógica de negócio.
            /// Lê rótulo e já retorna análise completa.
            /// </summary>
            public async Task<ProductAnalysisResult> AnalyzeProductLabel(
                byte[] nutritionTableImage,
                byte[] ingredientsImage,
                int userId,
                List<string> userAllergens)
            {
                // 1. Ler rótulo
                var request = new LabelReadingRequest
                {
                    UserId = userId,
                    LanguageCode = "pt",
                    Captures = new List<LabelCapture>
                    {
                        new LabelCapture
                        {
                            CaptureType = CaptureType.NutritionTable,
                            ImageData = nutritionTableImage
                        },
                        new LabelCapture
                        {
                            CaptureType = CaptureType.IngredientsList,
                            ImageData = ingredientsImage
                        }
                    }
                };

                var readingResult = await _labelReadingService.ReadLabelAsync(request);

                if (!readingResult.Success)
                {
                    return new ProductAnalysisResult
                    {
                        Success = false,
                        ErrorMessage = readingResult.ErrorMessage
                    };
                }

                // 2. Analisar dados extraídos
                var analysis = new ProductAnalysisResult
                {
                    Success = true,
                    ReadingConfidence = readingResult.OverallConfidence,
                    NutritionalInfo = readingResult.NutritionalInfo,
                    Ingredients = readingResult.Ingredients,
                    Allergens = readingResult.Allergens
                };

                // 3. Verificar alérgenos do usuário
                analysis.AllergenWarnings = readingResult.Allergens
                    .Where(a => userAllergens.Any(ua => a.ToLowerInvariant().Contains(ua.ToLowerInvariant())))
                    .ToList();

                analysis.IsSafeForUser = !analysis.AllergenWarnings.Any();

                // 4. Calcular score nutricional
                if (readingResult.NutritionalInfo != null)
                {
                    analysis.NutritionalScore = CalculateNutritionalScore(readingResult.NutritionalInfo);
                }

                // 5. Detectar ultraprocessado
                analysis.IsUltraProcessed = readingResult.Ingredients.Count > 10;

                return analysis;
            }

            private double CalculateNutritionalScore(NutritionalInformationDto info)
            {
                // Implementação simplificada
                var score = 50.0;

                if (info.Fiber >= 3) score += 15;
                if (info.Sodium <= 400) score += 10;
                if (info.Sugars <= 10) score += 15;
                if (info.SaturatedFat <= 3) score += 10;

                return Math.Min(100, score);
            }

            public class ProductAnalysisResult
            {
                public bool Success { get; set; }
                public string? ErrorMessage { get; set; }
                public double ReadingConfidence { get; set; }
                public NutritionalInformationDto? NutritionalInfo { get; set; }
                public List<string> Ingredients { get; set; } = new();
                public List<string> Allergens { get; set; } = new();
                public List<string> AllergenWarnings { get; set; } = new();
                public bool IsSafeForUser { get; set; }
                public double NutritionalScore { get; set; }
                public bool IsUltraProcessed { get; set; }
            }
        }
    }
}
