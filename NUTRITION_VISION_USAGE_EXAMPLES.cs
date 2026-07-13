using System;
using System.IO;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos práticos de uso da análise nutricional visual com Azure OpenAI.
    /// </summary>
    public class NutritionVisionExamples
    {
        private readonly INutritionAnalysisService _nutritionService;

        public NutritionVisionExamples(INutritionAnalysisService nutritionService)
        {
            _nutritionService = nutritionService;
        }

        /// <summary>
        /// Exemplo 1: Análise básica de produto a partir de arquivo local
        /// </summary>
        public async Task<NutritionAnalysisResponseDto> BasicProductAnalysisExample()
        {
            // Simula leitura de arquivo de imagem
            string imagePath = @"C:\temp\produto-exemplo.jpg";
            byte[] imageData = await File.ReadAllBytesAsync(imagePath);

            // Chama o serviço de análise nutricional
            var result = await _nutritionService.AnalyzeProductImageAsync(
                imageData: imageData,
                fileName: "produto-exemplo.jpg",
                languageCode: "pt"
            );

            // Exemplo de resposta esperada:
            /*
            {
              "success": true,
              "productName": "Biscoito Recheado Chocolate",
              "brand": "Marca X",
              "category": "biscoito recheado",
              "packageWeight": "140g",
              "analysisMode": "FrontOfPackageOnly",
              "visibleClaims": [
                "Fonte de vitaminas e minerais",
                "Ferro e zinco"
              ],
              "estimatedNutritionProfile": {
                "caloriesPer100g": null,
                "estimatedPackageCalories": null,
                "estimatedSugarPer100g": null,
                "estimatedProteinPer100g": null,
                "estimatedSodiumPer100g": null,
                "estimatedFiberPer100g": null,
                "estimatedFatPer100g": null,
                "basis": "Estimativa por categoria visual, sem leitura da tabela nutricional oficial"
              },
              "classification": {
                "diabetic": {
                  "status": "nao_recomendado",
                  "reason": "Biscoito recheado tipicamente alto em açúcar"
                },
                "bloodPressure": {
                  "status": "consumo_moderado",
                  "reason": "Provável presença de sódio em quantidade moderada"
                },
                "weightLoss": {
                  "status": "nao_recomendado",
                  "reason": "Alto em calorias e açúcar, baixa densidade nutricional"
                },
                "muscleGain": {
                  "status": "fraco",
                  "reason": "Baixo teor proteico esperado para a categoria"
                }
              },
              "summary": "Biscoito recheado com provável presença relevante de açúcar e baixa densidade proteica (estimativa por categoria).",
              "confidenceDetails": {
                "productIdentification": 0.9,
                "visibleClaimsExtraction": 0.8,
                "estimatedNutritionProfile": 0.3,
                "classification": 0.7
              },
              "warnings": [
                "Análise baseada apenas na parte frontal da embalagem",
                "Valores nutricionais não foram extraídos da tabela oficial",
                "Para análise precisa, envie a imagem da tabela nutricional e ingredientes"
              ],
              "errorMessage": null,
              "processingTimeSeconds": 2.3
            }
            */

            return result;
        }

        /// <summary>
        /// Exemplo 2: Análise de produto com tabela nutricional visível
        /// </summary>
        public async Task<NutritionAnalysisResponseDto> NutritionTableAnalysisExample()
        {
            string imagePath = @"C:\temp\produto-com-tabela.jpg";
            byte[] imageData = await File.ReadAllBytesAsync(imagePath);

            var result = await _nutritionService.AnalyzeProductImageAsync(
                imageData: imageData,
                fileName: "produto-com-tabela.jpg"
            );

            // Exemplo de resposta com tabela nutricional:
            /*
            {
              "success": true,
              "productName": "Iogurte Proteico",
              "brand": "FitBrand",
              "category": "iogurte proteico",
              "packageWeight": "170g",
              "analysisMode": "FullNutritionLabel",
              "visibleClaims": [
                "Alto em proteínas",
                "Zero açúcar",
                "Sem lactose"
              ],
              "estimatedNutritionProfile": {
                "caloriesPer100g": 95,
                "estimatedPackageCalories": 162,
                "estimatedSugarPer100g": 0,
                "estimatedProteinPer100g": 15,
                "estimatedSodiumPer100g": 120,
                "estimatedFiberPer100g": 0,
                "estimatedFatPer100g": 3,
                "basis": "Valores extraídos da tabela nutricional oficial visível na embalagem"
              },
              "classification": {
                "diabetic": {
                  "status": "adequado",
                  "reason": "Zero açúcar e baixo em carboidratos"
                },
                "bloodPressure": {
                  "status": "adequado",
                  "reason": "Baixo teor de sódio"
                },
                "weightLoss": {
                  "status": "adequado",
                  "reason": "Baixo em calorias e alto em proteínas"
                },
                "muscleGain": {
                  "status": "adequado",
                  "reason": "Alto teor proteico (15g/100g)"
                }
              },
              "summary": "Iogurte proteico adequado para dietas restritivas, alto em proteínas e sem açúcar adicionado.",
              "warnings": [],
              "errorMessage": null
            }
            */

            return result;
        }

        /// <summary>
        /// Exemplo 3: Análise a partir de upload via API (IFormFile)
        /// </summary>
        public async Task<NutritionAnalysisResponseDto> ApiUploadAnalysisExample(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0)
            {
                throw new ArgumentException("Arquivo de imagem é obrigatório");
            }

            // Converte IFormFile para byte array
            using var memoryStream = new MemoryStream();
            await uploadedFile.CopyToAsync(memoryStream);
            byte[] imageData = memoryStream.ToArray();

            // Valida tipo de arquivo
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(uploadedFile.ContentType?.ToLowerInvariant()))
            {
                throw new ArgumentException($"Tipo de arquivo não suportado: {uploadedFile.ContentType}");
            }

            // Chama análise
            var result = await _nutritionService.AnalyzeProductImageAsync(
                imageData: imageData,
                fileName: uploadedFile.FileName,
                languageCode: "pt"
            );

            return result;
        }

        /// <summary>
        /// Exemplo 4: Tratamento de diferentes cenários de erro
        /// </summary>
        public async Task<NutritionAnalysisResponseDto> ErrorHandlingExample()
        {
            try
            {
                // Simula imagem corrompida ou inválida
                byte[] invalidImageData = new byte[] { 0x00, 0x01, 0x02 };

                var result = await _nutritionService.AnalyzeProductImageAsync(
                    imageData: invalidImageData,
                    fileName: "invalid-image.jpg"
                );

                // Exemplo de resposta de erro:
                /*
                {
                  "success": false,
                  "productName": null,
                  "brand": null,
                  "category": null,
                  "packageWeight": null,
                  "analysisMode": "FrontOfPackageOnly",
                  "visibleClaims": [],
                  "estimatedNutritionProfile": {
                    "caloriesPer100g": null,
                    "estimatedPackageCalories": null,
                    "estimatedSugarPer100g": null,
                    "estimatedProteinPer100g": null,
                    "estimatedSodiumPer100g": null,
                    "estimatedFiberPer100g": null,
                    "estimatedFatPer100g": null,
                    "basis": "Não foi possível analisar a imagem com confiança suficiente"
                  },
                  "classification": {
                    "diabetic": {
                      "status": "indeterminado",
                      "reason": "Não foi possível identificar o produto com segurança"
                    },
                    "bloodPressure": {
                      "status": "indeterminado", 
                      "reason": "Não foi possível identificar o produto com segurança"
                    },
                    "weightLoss": {
                      "status": "indeterminado",
                      "reason": "Não foi possível identificar o produto com segurança"
                    },
                    "muscleGain": {
                      "status": "indeterminado",
                      "reason": "Não foi possível identificar o produto com segurança"
                    }
                  },
                  "summary": "Não foi possível identificar o produto com confiança suficiente a partir da imagem enviada.",
                  "confidenceDetails": {
                    "productIdentification": 0.0,
                    "visibleClaimsExtraction": 0.0,
                    "estimatedNutritionProfile": 0.0,
                    "classification": 0.0
                  },
                  "warnings": [
                    "Imagem insuficiente para análise confiável"
                  ],
                  "errorMessage": "Não foi possível interpretar a imagem do produto com segurança",
                  "processingTimeSeconds": 1.2
                }
                */

                return result;
            }
            catch (Exception ex)
            {
                // Log do erro e retorno de resposta estruturada
                Console.WriteLine($"Erro durante análise: {ex.Message}");
                throw; // Re-throw para tratamento upstream
            }
        }

        /// <summary>
        /// Exemplo 5: Validação de confiança mínima
        /// </summary>
        public async Task<(bool IsReliable, NutritionAnalysisResponseDto Result)> ReliabilityValidationExample(byte[] imageData)
        {
            var result = await _nutritionService.AnalyzeProductImageAsync(
                imageData: imageData,
                fileName: "produto-validacao.jpg"
            );

            // Define critérios de confiabilidade
            const double minProductIdentificationConfidence = 0.7;
            const double minOverallConfidence = 0.6;

            bool isReliable = result.Success &&
                             result.ConfidenceDetails != null &&
                             result.ConfidenceDetails.ProductIdentification >= minProductIdentificationConfidence &&
                             GetOverallConfidence(result.ConfidenceDetails) >= minOverallConfidence;

            return (isReliable, result);
        }

        /// <summary>
        /// Exemplo 6: Integração com sistema de cache
        /// </summary>
        public async Task<NutritionAnalysisResponseDto> CachedAnalysisExample(byte[] imageData, string cacheKey)
        {
            // Pseudo-código para demonstrar integração com cache
            /*
            var cachedResult = await _cacheService.GetAsync<NutritionAnalysisResponseDto>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }
            */

            var result = await _nutritionService.AnalyzeProductImageAsync(
                imageData: imageData,
                fileName: "cached-product.jpg"
            );

            // Cache apenas resultados com confiança alta
            if (result.Success && result.ConfidenceDetails?.ProductIdentification >= 0.8)
            {
                // await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(24));
            }

            return result;
        }

        /// <summary>
        /// Calcula confiança geral com base em todos os aspectos
        /// </summary>
        private double GetOverallConfidence(ConfidenceDetailsDto confidence)
        {
            return (confidence.ProductIdentification + 
                   confidence.VisibleClaimsExtraction + 
                   confidence.EstimatedNutritionProfile + 
                   confidence.Classification) / 4.0;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════════
// EXEMPLO DE USO EM CONTROLLER
// ═══════════════════════════════════════════════════════════════════════════════════

/*
[ApiController]
[Route("api/[controller]")]
public class NutritionAnalysisController : ControllerBase
{
    private readonly INutritionAnalysisService _nutritionService;
    private readonly ILogger<NutritionAnalysisController> _logger;

    public NutritionAnalysisController(
        INutritionAnalysisService nutritionService,
        ILogger<NutritionAnalysisController> logger)
    {
        _nutritionService = nutritionService;
        _logger = logger;
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<NutritionAnalysisResponseDto>> AnalyzeProduct(
        [FromForm] IFormFile image,
        [FromForm] string? languageCode = "pt")
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest(new { error = "Imagem é obrigatória" });
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await image.CopyToAsync(memoryStream);
            var imageData = memoryStream.ToArray();

            var result = await _nutritionService.AnalyzeProductImageAsync(
                imageData,
                image.FileName,
                languageCode ?? "pt"
            );

            // Log para monitoramento
            _logger.LogInformation(
                "Nutrition analysis completed: Success={Success}, Product={ProductName}, ProcessingTime={ProcessingTime}s",
                result.Success,
                result.ProductName ?? "unknown",
                result.ProcessingTimeSeconds
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during nutrition analysis");
            return StatusCode(500, new { error = "Erro interno durante análise" });
        }
    }
}
*/