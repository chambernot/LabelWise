using System.Diagnostics;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.AI;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Application.Presentation;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Serviço refatorado de análise nutricional com separação clara entre
    /// dados extraídos visualmente e dados inferidos/estimados.
    /// </summary>
    public class RefactoredNutritionAnalysisService : INutritionAnalysisService
    {
        private readonly IVisualInterpreter _visualInterpreter;
        private readonly ILogger<RefactoredNutritionAnalysisService> _logger;

        private static readonly List<ProductCategoryNutritionProfile> _nutritionProfiles = InitializeNutritionProfiles();

        public RefactoredNutritionAnalysisService(
            IVisualInterpreter visualInterpreter,
            ILogger<RefactoredNutritionAnalysisService> logger)
        {
            _visualInterpreter = visualInterpreter ?? throw new ArgumentNullException(nameof(visualInterpreter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<NutritionAnalysisResponseDto> AnalyzeProductImageAsync(
            byte[] imageData,
            string fileName,
            string languageCode = "pt",
            List<string>? requestedProfiles = null,
            Guid? userId = null,
            string? deviceId = null)
        {
            var refactoredResponse = await AnalyzeProductImageRefactoredAsync(imageData, fileName, languageCode, requestedProfiles);

            var normalizedVisibleClaims = NormalizeVisibleClaims(refactoredResponse.VisibleClaims);

            return new NutritionAnalysisResponseDto
            {
                Success = refactoredResponse.Success,
                ProductName = refactoredResponse.ProductName,
                Brand = refactoredResponse.Brand,
                Category = refactoredResponse.Category,
                PackageWeight = refactoredResponse.PackageWeight,
                AnalysisMode = refactoredResponse.AnalysisMode,
                VisibleClaims = normalizedVisibleClaims,
                EstimatedNutritionProfile = refactoredResponse.EstimatedNutritionProfile,
                Classification = MapClassification(refactoredResponse),
                Summary = refactoredResponse.Summary,
                ConfidenceDetails = MapConfidenceDetails(refactoredResponse),
                Warnings = refactoredResponse.Warnings ?? new List<string>(),
                ErrorMessage = refactoredResponse.ErrorMessage,
                ProcessingTimeSeconds = refactoredResponse.ProcessingTimeSeconds,
                ResumoRapido = refactoredResponse.ResumoRapido ?? new List<string>(),
                ExplicacaoScore = refactoredResponse.ExplicacaoScore,
                PontoPrincipal = refactoredResponse.PontoPrincipal,
                Tom = refactoredResponse.Tom
            };
        }

        private static ProductClassificationDto? MapClassification(RefactoredNutritionAnalysisResponse response)
        {
            if (!response.Success)
            {
                return null;
            }

            var classification = response.Classification ?? new ProfileClassificationDto();

            return new ProductClassificationDto
            {
                Diabetic = classification.Diabetic == null ? null : new HealthProfileResult { Status = classification.Diabetic.Status, Reason = classification.Diabetic.Reason },
                BloodPressure = classification.BloodPressure == null ? null : new HealthProfileResult { Status = classification.BloodPressure.Status, Reason = classification.BloodPressure.Reason },
                WeightLoss = classification.WeightLoss == null ? null : new HealthProfileResult { Status = classification.WeightLoss.Status, Reason = classification.WeightLoss.Reason },
                MuscleGain = classification.MuscleGain == null ? null : new HealthProfileResult { Status = classification.MuscleGain.Status, Reason = classification.MuscleGain.Reason }
            };
        }

        private static ConfidenceDetailsDto? MapConfidenceDetails(RefactoredNutritionAnalysisResponse response)
        {
            if (!response.Success)
            {
                return null;
            }

            var details = response.ConfidenceDetails ?? new NutritionConfidenceDetailsDto
            {
                ProductIdentification = 0,
                VisibleClaimsExtraction = 0,
                EstimatedNutritionProfile = 0,
                Classification = 0
            };

            return new ConfidenceDetailsDto
            {
                ProductIdentification = details.ProductIdentification,
                VisibleClaimsExtraction = details.VisibleClaimsExtraction,
                EstimatedNutritionProfile = details.EstimatedNutritionProfile,
                Classification = details.Classification
            };
        }

        private static List<string> NormalizeVisibleClaims(List<string>? visibleClaims)
        {
            if (visibleClaims == null || visibleClaims.Count == 0)
            {
                return new List<string>();
            }

            var cleaned = visibleClaims
                .Select(c => (c ?? string.Empty).Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            // Heurística: unir fragmentos quando um claim termina com palavra "conector" (ex.: "Vitaminas D")
            // e o próximo começa com uma lista (ex.: "B1, B2...").
            static bool EndsWithConnector(string value)
            {
                var v = value.TrimEnd(',', ';', ':').Trim();
                return v.EndsWith("vitaminas", StringComparison.OrdinalIgnoreCase)
                    || v.EndsWith("vitamina", StringComparison.OrdinalIgnoreCase)
                    || v.EndsWith("minerais", StringComparison.OrdinalIgnoreCase)
                    || v.EndsWith("mineral", StringComparison.OrdinalIgnoreCase)
                    || v.EndsWith("de", StringComparison.OrdinalIgnoreCase)
                    || v.EndsWith("do", StringComparison.OrdinalIgnoreCase)
                    || v.EndsWith("da", StringComparison.OrdinalIgnoreCase)
                    || v.EndsWith("dos", StringComparison.OrdinalIgnoreCase)
                    || v.EndsWith("das", StringComparison.OrdinalIgnoreCase)
                    || v.EndsWith("e", StringComparison.OrdinalIgnoreCase);
            }

            static bool LooksLikeContinuation(string value)
            {
                var v = value.Trim();
                return Regex.IsMatch(v, @"^(?:[A-Za-z]\d+|\d+|[A-Za-z]{1,3})(?:\b|[,:])")
                    || v.StartsWith(",", StringComparison.Ordinal)
                    || v.StartsWith("e ", StringComparison.OrdinalIgnoreCase);
            }

            var normalized = new List<string>();
            var i = 0;
            while (i < cleaned.Count)
            {
                var current = cleaned[i];
                if (i + 1 < cleaned.Count && EndsWithConnector(current) && LooksLikeContinuation(cleaned[i + 1]))
                {
                    current = current.TrimEnd(',', ';', ':').Trim() + ", " + cleaned[i + 1].Trim();
                    i += 2;
                }
                else
                {
                    i++;
                }

                normalized.Add(current);
            }

            // Remove duplicados preservando ordem
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduped = new List<string>();
            foreach (var c in normalized)
            {
                if (seen.Add(c))
                {
                    deduped.Add(c);
                }
            }

            return deduped;
        }

        /// <summary>
        /// Método principal de análise refatorado.
        /// </summary>
        public async Task<RefactoredNutritionAnalysisResponse> AnalyzeProductImageRefactoredAsync(
            byte[] imageData,
            string fileName,
            string languageCode = "pt",
            List<string>? requestedProfiles = null)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("🍎 Iniciando análise nutricional refatorada");
            _logger.LogInformation("   FileName: {FileName}", fileName);
            _logger.LogInformation("   ImageSize: {Size} bytes", imageData.Length);
            _logger.LogInformation("   Language: {Language}", languageCode);
            _logger.LogInformation("═══════════════════════════════════════════════════════════");

            try
            {
                // ═══════════════════════════════════════════════════════════
                // ETAPA 1: Interpretação Visual
                // ═══════════════════════════════════════════════════════════
                var visionResult = await PerformVisualInterpretationAsync(imageData, fileName);

                if (visionResult == null)
                {
                    return CreateErrorResponse(
                        "Não foi possível interpretar a imagem",
                        stopwatch.Elapsed.TotalSeconds);
                }

                _logger.LogInformation("✅ Interpretação visual concluída");
                _logger.LogInformation("   Produto: {Name}", visionResult.ProbableProductName ?? "N/A");
                _logger.LogInformation("   Marca: {Brand}", visionResult.ProbableBrand ?? "N/A");
                _logger.LogInformation("   Categoria: {Category}", visionResult.ProbableCategory ?? "N/A");
                _logger.LogInformation("   Peso: {Weight}", visionResult.ProbablePackageWeight ?? "N/A");
                _logger.LogInformation("   Claims: {ClaimCount}", visionResult.VisibleClaims.Count);

                // ═══════════════════════════════════════════════════════════
                // ETAPA 2: Determinar modo de análise
                // ═══════════════════════════════════════════════════════════
                var analysisMode = DetermineAnalysisMode(visionResult);
                _logger.LogInformation("📋 Modo de análise: {Mode}", analysisMode);

                // ═══════════════════════════════════════════════════════════
                // ETAPA 3: Encontrar perfil nutricional
                // ═══════════════════════════════════════════════════════════
                var nutritionProfile = FindMatchingNutritionProfile(visionResult.ProbableCategory);
                _logger.LogInformation("✅ Perfil nutricional: {Profile}", nutritionProfile.CategoryName);

                // ═══════════════════════════════════════════════════════════
                // ETAPA 4: Construir estimativa nutricional
                // ═══════════════════════════════════════════════════════════
                var estimatedNutritionProfile = BuildEstimatedNutritionProfile(
                    nutritionProfile,
                    visionResult.ProbablePackageWeight);

                // ═══════════════════════════════════════════════════════════
                // ETAPA 5: Classificar para perfis de saúde
                // ═══════════════════════════════════════════════════════════
                var classification = BuildClassification(nutritionProfile, requestedProfiles);

                // ═══════════════════════════════════════════════════════════
                // ETAPA 6: Calcular confiança detalhada
                // ═══════════════════════════════════════════════════════════
                var confidenceDetails = BuildConfidenceDetails(visionResult, analysisMode);

                // ═══════════════════════════════════════════════════════════
                // ETAPA 7: Gerar avisos
                // ═══════════════════════════════════════════════════════════
                var warnings = BuildWarnings(analysisMode);

                // ═══════════════════════════════════════════════════════════
                // ETAPA 8: Gerar resumo
                // ═══════════════════════════════════════════════════════════
                var summary = BuildSummary(visionResult, nutritionProfile, analysisMode);

                stopwatch.Stop();

                var response = new RefactoredNutritionAnalysisResponse
                {
                    Success = true,
                    ProductName = visionResult.ProbableProductName,
                    Brand = visionResult.ProbableBrand,
                    Category = visionResult.ProbableCategory ?? nutritionProfile.CategoryName,
                    PackageWeight = visionResult.ProbablePackageWeight,
                    AnalysisMode = analysisMode,
                    VisibleClaims = visionResult.VisibleClaims,
                    EstimatedNutritionProfile = estimatedNutritionProfile,
                    Classification = classification,
                    Summary = summary,
                    ConfidenceDetails = confidenceDetails,
                    Warnings = warnings,
                    ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds
                };

                NutritionTextPresentationBuilder.Apply(response);

                _logger.LogInformation("═══════════════════════════════════════════════════════════");
                _logger.LogInformation("✅ Análise nutricional refatorada concluída");
                _logger.LogInformation("   ProcessingTime: {Time:F2}s", response.ProcessingTimeSeconds);
                _logger.LogInformation("═══════════════════════════════════════════════════════════");

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "❌ Erro durante análise nutricional refatorada");

                return CreateErrorResponse(
                    $"Erro durante análise: {ex.Message}",
                    stopwatch.Elapsed.TotalSeconds);
            }
        }

        #region Private Methods - Visual Interpretation

        private async Task<VisualInterpretationResult?> PerformVisualInterpretationAsync(
            byte[] imageData,
            string fileName)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"nutrition_{Guid.NewGuid()}.jpg");

                try
                {
                    await File.WriteAllBytesAsync(tempPath, imageData);

                    var visionRequest = new VisualInterpretationRequest
                    {
                        ImagePath = tempPath
                    };

                    var visionResult = await _visualInterpreter.InterpretImageAsync(visionRequest);

                    if (string.IsNullOrWhiteSpace(visionResult.ProbableProductName))
                    {
                        _logger.LogWarning("⚠️ Nome do produto não identificado");
                        return null;
                    }

                    return visionResult;
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); }
                        catch { /* ignore cleanup errors */ }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao realizar interpretação visual");
                return null;
            }
        }

        #endregion

        #region Private Methods - Analysis Mode

        private AnalysisMode DetermineAnalysisMode(VisualInterpretationResult visionResult)
        {
            // Se detectou tabela nutricional, usar modo completo
            if (visionResult.ProbableCaptureType == CaptureType.NutritionTable)
            {
                return AnalysisMode.FullNutritionLabel;
            }

            // Caso contrário, usar modo frontal apenas
            return AnalysisMode.FrontOfPackageOnly;
        }

        #endregion

        #region Private Methods - Nutrition Profile

        private ProductCategoryNutritionProfile FindMatchingNutritionProfile(string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return GetGenericProfile();
            }

            var normalizedCategory = category.ToLowerInvariant().Trim();

            var exactMatch = _nutritionProfiles.FirstOrDefault(p =>
                p.CategoryName.Equals(normalizedCategory, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                return exactMatch;
            }

            var partialMatch = _nutritionProfiles.FirstOrDefault(p =>
                normalizedCategory.Contains(p.CategoryName.ToLowerInvariant()) ||
                p.CategoryName.ToLowerInvariant().Contains(normalizedCategory) ||
                p.Keywords.Any(k => normalizedCategory.Contains(k.ToLowerInvariant())));

            return partialMatch ?? GetGenericProfile();
        }

        #endregion

        #region Private Methods - Estimated Nutrition

        private EstimatedNutritionProfileDto BuildEstimatedNutritionProfile(
            ProductCategoryNutritionProfile profile,
            string? packageWeight)
        {
                var estimatedDto = new EstimatedNutritionProfileDto
            {
                    CaloriesPer100g = profile.CaloriesPer100g,
                    EstimatedSugarPer100g = (int)Math.Round(profile.SugarPer100g),
                    EstimatedProteinPer100g = (int)Math.Round(profile.ProteinPer100g),
                    EstimatedSodiumPer100g = (int)Math.Round(profile.SodiumPer100g),
                    EstimatedFiberPer100g = (int)Math.Round(profile.FiberPer100g),
                    EstimatedFatPer100g = (int)Math.Round(profile.FatPer100g),
                Basis = "Estimativa por categoria visual, sem leitura da tabela nutricional oficial"
            };

            if (!string.IsNullOrWhiteSpace(packageWeight))
            {
                var weightInGrams = ExtractWeightInGrams(packageWeight);
                if (weightInGrams.HasValue)
                {
                    estimatedDto.EstimatedPackageCalories = (int)(profile.CaloriesPer100g * weightInGrams.Value / 100.0);
                }
            }

            return estimatedDto;
        }

        private double? ExtractWeightInGrams(string packageWeight)
        {
            var match = Regex.Match(packageWeight, @"(\d+(?:[.,]\d+)?)\s*(g|kg|ml|l)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            if (!double.TryParse(match.Groups[1].Value.Replace(',', '.'), out var value))
                return null;

            var unit = match.Groups[2].Value.ToLowerInvariant();

            return unit switch
            {
                "kg" => value * 1000,
                "l" => value * 1000,
                "ml" => value,
                "g" => value,
                _ => null
            };
        }

        #endregion

        #region Private Methods - Classification

        private ProfileClassificationDto BuildClassification(
            ProductCategoryNutritionProfile profile,
            List<string>? requestedProfiles)
        {
            return new ProfileClassificationDto
            {
                Diabetic = ClassifyForDiabetic(profile),
                BloodPressure = ClassifyForBloodPressure(profile),
                WeightLoss = ClassifyForWeightLoss(profile),
                MuscleGain = ClassifyForMuscleGain(profile)
            };
        }

        private ProfileStatusDto ClassifyForDiabetic(ProductCategoryNutritionProfile profile)
        {
            if (profile.SugarLevel == "Alto" || profile.CalorieDensity == "Alta")
            {
                return new ProfileStatusDto
                {
                    Status = "nao_recomendado",
                    Reason = "Alto teor estimado de açúcar e carboidratos refinados"
                };
            }

            if (profile.SugarLevel == "Moderado" && profile.FiberPer100g < 3)
            {
                return new ProfileStatusDto
                {
                    Status = "consumo_moderado",
                    Reason = "Açúcar moderado com baixa fibra, consumir com cautela"
                };
            }

            return new ProfileStatusDto
            {
                Status = "mais_adequado",
                Reason = "Perfil de açúcar e carboidratos mais favorável"
            };
        }

        private ProfileStatusDto ClassifyForBloodPressure(ProductCategoryNutritionProfile profile)
        {
            if (profile.SodiumLevel == "Alto" || profile.IsUltraProcessed)
            {
                return new ProfileStatusDto
                {
                    Status = "nao_recomendado",
                    Reason = "Produto ultraprocessado com possível presença elevada de sódio e gordura saturada"
                };
            }

            if (profile.SodiumLevel == "Moderado")
            {
                return new ProfileStatusDto
                {
                    Status = "consumo_moderado",
                    Reason = "Sódio moderado, adequar porção ao dia"
                };
            }

            return new ProfileStatusDto
            {
                Status = "mais_adequado",
                Reason = "Baixo teor de sódio estimado"
            };
        }

        private ProfileStatusDto ClassifyForWeightLoss(ProductCategoryNutritionProfile profile)
        {
            if (profile.CalorieDensity == "Alta" && profile.FiberPer100g < 3)
            {
                return new ProfileStatusDto
                {
                    Status = "nao_recomendado",
                    Reason = "Alta densidade calórica e baixa fibra"
                };
            }

            if (profile.CalorieDensity == "Moderada")
            {
                return new ProfileStatusDto
                {
                    Status = "consumo_moderado",
                    Reason = "Densidade calórica moderada, controlar porção"
                };
            }

            return new ProfileStatusDto
            {
                Status = "mais_adequado",
                Reason = "Baixa densidade calórica e bom teor de fibras"
            };
        }

        private ProfileStatusDto ClassifyForMuscleGain(ProductCategoryNutritionProfile profile)
        {
            if (profile.ProteinLevel == "Alto" && profile.ProteinPer100g >= 15)
            {
                return new ProfileStatusDto
                {
                    Status = "bom",
                    Reason = "Alto teor proteico"
                };
            }

            if (profile.ProteinLevel == "Moderado" && profile.ProteinPer100g >= 8)
            {
                return new ProfileStatusDto
                {
                    Status = "moderado",
                    Reason = "Teor proteico moderado"
                };
            }

            return new ProfileStatusDto
            {
                Status = "fraco",
                Reason = "Baixo teor proteico"
            };
        }

        private ProfileStatusDto BuildProfileStatus(string status, string reason)
        {
            return new ProfileStatusDto
            {
                Status = status,
                Reason = reason
            };
        }

        #endregion

        #region Private Methods - Confidence

        private NutritionConfidenceDetailsDto BuildConfidenceDetails(
            VisualInterpretationResult visionResult,
            AnalysisMode analysisMode)
        {
            var baseConfidence = visionResult.InterpretationConfidence switch
            {
                ConfidenceLevel.High => 0.90,
                ConfidenceLevel.Medium => 0.70,
                ConfidenceLevel.Low => 0.45,
                _ => 0.30
            };

            var claimsConfidence = visionResult.VisibleClaims.Count > 0 ? 0.85 : 0.50;

            var nutritionConfidence = analysisMode == AnalysisMode.FullNutritionLabel ? 0.90 : 0.55;

            var classificationConfidence = analysisMode == AnalysisMode.FullNutritionLabel ? 0.85 : 0.70;

            return new NutritionConfidenceDetailsDto
            {
                ProductIdentification = baseConfidence,
                VisibleClaimsExtraction = claimsConfidence,
                EstimatedNutritionProfile = nutritionConfidence,
                Classification = classificationConfidence
            };
        }

        #endregion

        #region Private Methods - Warnings

        private List<string> BuildWarnings(AnalysisMode analysisMode)
        {
            var warnings = new List<string>();

            if (analysisMode == AnalysisMode.FrontOfPackageOnly)
            {
                warnings.Add("Análise estimada com base na imagem frontal do produto");
                warnings.Add("Valores nutricionais não foram extraídos da tabela nutricional oficial");
                warnings.Add("Para análise precisa, envie a parte traseira com tabela nutricional e ingredientes");
            }

            return warnings;
        }

        #endregion

        #region Private Methods - Summary

        private string BuildSummary(
            VisualInterpretationResult visionResult,
            ProductCategoryNutritionProfile profile,
            AnalysisMode analysisMode)
        {
            var parts = new List<string>();

            var category = visionResult.ProbableCategory ?? profile.CategoryName;
            var categoryDescription = profile.IsUltraProcessed
                ? $"{category} ultraprocessado"
                : category;

            parts.Add(categoryDescription.First().ToString().ToUpper() + categoryDescription.Substring(1));

            if (visionResult.VisibleClaims.Any(c => c.Contains("vitamina", StringComparison.OrdinalIgnoreCase) ||
                                                    c.Contains("mineral", StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add("com fortificação de vitaminas e minerais");
            }

            if (profile.ProteinPer100g >= 15)
            {
                parts.Add("com teor relevante de proteína");
            }
            else if (profile.ProteinPer100g < 5)
            {
                parts.Add("sem indicação de alto teor proteico");
            }

            if (profile.SugarPer100g > 10)
            {
                parts.Add("com provável presença relevante de açúcar");
            }

            if (analysisMode == AnalysisMode.FrontOfPackageOnly)
            {
                parts.Add("baseado em análise visual da embalagem");
            }

            return string.Join(", ", parts).TrimEnd(',', ' ') + ".";
        }

        #endregion

        #region Error Handling

        private RefactoredNutritionAnalysisResponse CreateErrorResponse(string errorMessage, double processingTime)
        {
            return new RefactoredNutritionAnalysisResponse
            {
                Success = false,
                ErrorMessage = errorMessage,
                ProcessingTimeSeconds = processingTime
            };
        }

        #endregion

        #region Nutrition Profiles Database

        private static List<ProductCategoryNutritionProfile> InitializeNutritionProfiles()
        {
            return new List<ProductCategoryNutritionProfile>
            {
                new ProductCategoryNutritionProfile
                {
                    CategoryName = "achocolatado em pó",
                    Keywords = new List<string> { "achocolatado", "chocolate em pó", "nescau", "toddy", "chocolatto" },
                    CaloriesPer100g = 380,
                    ProteinPer100g = 4.0,
                    SugarPer100g = 75.0,
                    SodiumPer100g = 150.0,
                    FiberPer100g = 3.0,
                    FatPer100g = 2.5,
                    IsUltraProcessed = true,
                    SugarLevel = "Alto",
                    SodiumLevel = "Moderado",
                    CalorieDensity = "Alta",
                    ProteinLevel = "Baixo"
                },
                new ProductCategoryNutritionProfile
                {
                    CategoryName = "biscoito recheado",
                    Keywords = new List<string> { "biscoito", "bolacha", "recheado", "oreo", "negresco", "passatempo" },
                    CaloriesPer100g = 480,
                    ProteinPer100g = 5.0,
                    SugarPer100g = 35.0,
                    SodiumPer100g = 350.0,
                    FiberPer100g = 2.5,
                    FatPer100g = 20.0,
                    IsUltraProcessed = true,
                    SugarLevel = "Alto",
                    SodiumLevel = "Alto",
                    CalorieDensity = "Alta",
                    ProteinLevel = "Baixo"
                }
            };
        }

        private static ProductCategoryNutritionProfile GetGenericProfile()
        {
            return new ProductCategoryNutritionProfile
            {
                CategoryName = "alimento processado genérico",
                Keywords = new List<string>(),
                CaloriesPer100g = 250,
                ProteinPer100g = 5.0,
                SugarPer100g = 10.0,
                SodiumPer100g = 300.0,
                FiberPer100g = 2.0,
                FatPer100g = 10.0,
                IsUltraProcessed = true,
                SugarLevel = "Moderado",
                SodiumLevel = "Moderado",
                CalorieDensity = "Moderada",
                ProteinLevel = "Baixo"
            };
        }

        #endregion
    }
}
