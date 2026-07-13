using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Application.Confidence;
using LabelWise.Application.DTOs;
using LabelWise.Application.DTOs.Analysis;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Parsing;
using LabelWise.Application.QualityGate;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Storage;

namespace LabelWise.Infrastructure.Services
{
    /// <summary>
    /// Implementação do orquestrador do pipeline de análise de produto.
    /// Fluxo: Upload → OCR → Parser → Motor de Regras → Quality Gate Multidimensional → Resumo Final
    /// </summary>
    public class ProductAnalysisPipelineOrchestrator : IProductAnalysisPipelineOrchestrator
    {
        private readonly IImageUploadService _uploadService;
        private readonly IOcrProvider _ocrProvider;
        private readonly IIngredientAllergenParser _parser;
        private readonly IProductAnalysisEngine _analysisEngine;
        private readonly IFileStorage _fileStorage;
        private readonly IProductRepository _productRepository;
        private readonly IUserProfileRepository _userProfileRepository;
        private readonly IAnalysisWriteRepository _analysisWriteRepository;
        private readonly MultidimensionalQualityGate _qualityGate;

        public ProductAnalysisPipelineOrchestrator(
            IImageUploadService uploadService,
            IOcrProvider ocrProvider,
            IIngredientAllergenParser parser,
            IProductAnalysisEngine analysisEngine,
            IFileStorage fileStorage,
            IProductRepository productRepository,
            IUserProfileRepository userProfileRepository,
            IAnalysisWriteRepository analysisWriteRepository)
        {
            _uploadService = uploadService;
            _ocrProvider = ocrProvider;
            _parser = parser;
            _analysisEngine = analysisEngine;
            _fileStorage = fileStorage;
            _productRepository = productRepository;
            _userProfileRepository = userProfileRepository;
            _analysisWriteRepository = analysisWriteRepository;
            _qualityGate = new MultidimensionalQualityGate();
        }

        public async Task<ProductAnalysisPipelineResultDto> ExecutePipelineAsync(
            Stream imageStream,
            string fileName,
            Guid? userId = null)
        {
            var pipelineId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;
            var overallStopwatch = Stopwatch.StartNew();

            var result = new ProductAnalysisPipelineResultDto
            {
                Metadata = new PipelineMetadataDto
                {
                    PipelineId = pipelineId,
                    StartTime = startTime
                }
            };

            try
            {
                // ETAPA 1: Upload e Validação da Imagem
                var uploadResult = await ExecuteUploadStepAsync(imageStream, fileName, result.Metadata);
                if (!uploadResult.Success)
                {
                    result.AnalysisResult = CreateErrorResult("Falha no upload da imagem", uploadResult.ErrorMessage);
                    return result;
                }

                // ETAPA 2: OCR - Extração de Texto da Imagem
                var ocrResult = await ExecuteOcrStepAsync(uploadResult, result.Metadata);
                if (!ocrResult.Success)
                {
                    await CleanupAsync(uploadResult.ImagePath);
                    result.AnalysisResult = CreateErrorResult("Falha na extração de texto (OCR)", ocrResult.ErrorMessage);
                    return result;
                }

                // ETAPA 3: Parsing - Análise e estruturação do texto
                var parseResult = await ExecuteParsingStepAsync(ocrResult, result.Metadata);

                // ETAPA 4: Análise - Motor de Regras e Scoring
                var analysisResult = await ExecuteAnalysisStepAsync(
                    parseResult,
                    ocrResult, // Passar OCR result para quality gate
                    userId,
                    result.Metadata);

                // Incluir texto OCR bruto no resultado para depuração e validação
                analysisResult.ExtractedText = ocrResult.RawText;

                result.AnalysisResult = analysisResult;

                // Cleanup
                await CleanupAsync(uploadResult.ImagePath);
            }
            catch (Exception ex)
            {
                result.AnalysisResult = CreateErrorResult("Erro inesperado no pipeline", ex.Message);
            }
            finally
            {
                overallStopwatch.Stop();
                result.Metadata.EndTime = DateTime.UtcNow;
                result.Metadata.TotalDurationMs = overallStopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }

        #region Pipeline Steps

        private async Task<ImageUploadResultDto> ExecuteUploadStepAsync(
            Stream imageStream,
            string fileName,
            PipelineMetadataDto metadata)
        {
            var stepWatch = Stopwatch.StartNew();
            var stepMetadata = new StepMetadata { StepName = "Upload" };

            try
            {
                var result = await _uploadService.UploadImageAsync(imageStream, fileName);
                stepMetadata.Success = result.Success;
                stepMetadata.ErrorMessage = result.ErrorMessage;
                stepMetadata.AdditionalData = new { result.FileSize, result.ContentType };

                return result;
            }
            finally
            {
                stepWatch.Stop();
                stepMetadata.DurationMs = stepWatch.Elapsed.TotalMilliseconds;
                metadata.UploadStep = stepMetadata;
            }
        }

        private async Task<OcrResultDto> ExecuteOcrStepAsync(
            ImageUploadResultDto uploadResult,
            PipelineMetadataDto metadata)
        {
            var stepWatch = Stopwatch.StartNew();
            var stepMetadata = new StepMetadata { StepName = "OCR" };

            // Capturar informações do provider ANTES da execução para garantir visibilidade
            var providerName = _ocrProvider.ProviderName;
            var providerType = _ocrProvider.GetType().Name;

            // Log detalhado do provider concreto sendo usado
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"🔍 [OCR EXECUTION] Provider Information:");
            Console.WriteLine($"   • Provider Name: {providerName}");
            Console.WriteLine($"   • Provider Type: {providerType}");
            Console.WriteLine($"   • Assembly: {_ocrProvider.GetType().Assembly.GetName().Name}");
            Console.WriteLine($"   • Processing: {uploadResult.FileName}");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");

            // Adicionar ao metadata do pipeline (nível superior para fácil acesso)
            metadata.OcrProviderName = providerName;
            metadata.OcrProviderVersion = providerType;

            try
            {
                var ocrRequest = new OcrRequestDto
                {
                    ImagePath = uploadResult.ImagePath,
                    FileName = uploadResult.FileName,
                    ContentType = uploadResult.ContentType
                };

                var result = await _ocrProvider.ExtractTextAsync(ocrRequest);
                stepMetadata.Success = result.Success;
                stepMetadata.ErrorMessage = result.ErrorMessage;
                stepMetadata.AdditionalData = new
                {
                    result.Confidence,
                    TextLength = result.RawText.Length,
                    BlocksCount = result.TextBlocks.Count,
                    ProviderName = providerName,
                    ProviderType = providerType
                };

                // Log do resultado
                if (result.Success)
                {
                    Console.WriteLine($"✅ [OCR SUCCESS] Extracted {result.RawText.Length} characters with {result.Confidence:P1} confidence");
                }
                else
                {
                    Console.WriteLine($"❌ [OCR FAILED] Error: {result.ErrorMessage}");
                }

                return result;
            }
            finally
            {
                stepWatch.Stop();
                stepMetadata.DurationMs = stepWatch.Elapsed.TotalMilliseconds;
                metadata.OcrStep = stepMetadata;
            }
        }

        private async Task<IngredientAllergenParseResult> ExecuteParsingStepAsync(
            OcrResultDto ocrResult,
            PipelineMetadataDto metadata)
        {
            var stepWatch = Stopwatch.StartNew();
            var stepMetadata = new StepMetadata { StepName = "Parsing" };

            try
            {
                var result = _parser.Parse(ocrResult.RawText);
                stepMetadata.Success = true;
                stepMetadata.AdditionalData = new
                {
                    IngredientsCount = result.Ingredients.Count,
                    AllergensCount = result.Allergens.Count,
                    result.ProductName
                };

                return result;
            }
            catch (Exception ex)
            {
                stepMetadata.Success = false;
                stepMetadata.ErrorMessage = ex.Message;
                // Retorna resultado vazio em caso de erro
                return new IngredientAllergenParseResult();
            }
            finally
            {
                stepWatch.Stop();
                stepMetadata.DurationMs = stepWatch.Elapsed.TotalMilliseconds;
                metadata.ParsingStep = stepMetadata;
            }
        }

        private async Task<ProductAnalysisResultDto> ExecuteAnalysisStepAsync(
            IngredientAllergenParseResult parseResult,
            OcrResultDto ocrResult,
            Guid? userId,
            PipelineMetadataDto metadata)
        {
            var stepWatch = Stopwatch.StartNew();
            var stepMetadata = new StepMetadata { StepName = "Analysis" };

            try
            {
                // ═══════════════════════════════════════════════════════════════════════════════
                // TRATAMENTO ESPECIAL PARA ANÁLISES PARCIAIS (NutritionTable, etc.)
                // ═══════════════════════════════════════════════════════════════════════════════
                if (parseResult.IsPartialAnalysis)
                {
                    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
                    Console.WriteLine($"📊 [PARTIAL ANALYSIS] Processing partial analysis for {parseResult.SourceCaptureType}");
                    Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");

                    return await ExecutePartialAnalysisAsync(parseResult, ocrResult, userId, metadata, stepMetadata);
                }

                // ═══════════════════════════════════════════════════════════════════════════════
                // FLUXO NORMAL PARA ANÁLISES COMPLETAS
                // ═══════════════════════════════════════════════════════════════════════════════

                // Criar entidade de produto com os dados parseados
                var product = new Product(
                    parseResult.ProductName ?? "Produto Desconhecido",
                    parseResult.Brand,
                    null);

                // Criar informações nutricionais (se disponíveis)
                NutritionalInfo? nutritionalInfo = null;
                if (parseResult.Nutrition != null)
                {
                    nutritionalInfo = new NutritionalInfo(product.Id);

                    nutritionalInfo.UpdateMacros(
                        calories: (decimal?)parseResult.Nutrition.Calories,
                        totalFat: (decimal?)parseResult.Nutrition.TotalFat,
                        satFat: (decimal?)parseResult.Nutrition.SaturatedFat,
                        transFat: (decimal?)parseResult.Nutrition.TransFat,
                        cholesterol: (decimal?)parseResult.Nutrition.Cholesterol,
                        sodium: (decimal?)parseResult.Nutrition.Sodium,
                        carbs: (decimal?)parseResult.Nutrition.TotalCarbohydrate,
                        fiber: (decimal?)parseResult.Nutrition.DietaryFiber,
                        sugars: (decimal?)parseResult.Nutrition.Sugars,
                        protein: (decimal?)parseResult.Nutrition.Protein);

                    if (!string.IsNullOrEmpty(parseResult.Nutrition.ServingSize))
                    {
                        nutritionalInfo.UpdateServing(parseResult.Nutrition.ServingSize, null);
                    }

                    product.SetNutritionalInfo(nutritionalInfo);
                }

                // Criar ingredientes
                var ingredients = parseResult.Ingredients
                    .Select(ing => new ProductIngredient(product.Id, ing, null))
                    .ToList();

                if (ingredients.Any())
                {
                    foreach (var ingredient in ingredients)
                    {
                        product.AddIngredient(ingredient);
                    }
                }

                // Criar alérgenos
                var allergens = parseResult.Allergens
                    .Select(allergen => new ProductAllergen(product.Id, allergen, true))
                    .ToList();

                if (allergens.Any())
                {
                    foreach (var allergen in allergens)
                    {
                        product.AddAllergen(allergen);
                    }
                }

                // Carregar perfil do usuário se fornecido
                UserProfile? userProfile = null;
                if (userId.HasValue)
                {
                    userProfile = await _userProfileRepository.GetByUserIdAsync(userId.Value);
                }

                // Executar motor de análise (retorna DTO com scores, alerts, recommendations)
                var analysisResult = _analysisEngine.Analyze(
                    product,
                    nutritionalInfo,
                    ingredients,
                    allergens,
                    userProfile);

                // ═══════════════════════════════════════════════════════════════════
                // ETAPA 5: QUALITY GATE MULTIDIMENSIONAL
                // Calcula confiança em 3 dimensões: Identificação, Leitura, Análise
                // ═══════════════════════════════════════════════════════════════════
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
                Console.WriteLine("🎯 [QUALITY GATE MULTIDIMENSIONAL] Aplicando Quality Gate...");

                var qualityGateResult = _qualityGate.ApplyQualityGate(
                    analysisResult,
                    ocrResult.RawText,
                    ocrResult.Confidence,
                    parseResult);

                // Log detalhado das dimensões de confiança
                Console.WriteLine($"   📊 CONFIANÇA MULTIDIMENSIONAL:");
                Console.WriteLine($"      • Identificação do Produto: {qualityGateResult.Confidence.ProductIdentification.Score}");
                Console.WriteLine($"      • Leitura do Rótulo: {qualityGateResult.Confidence.LabelReading.Score}");
                Console.WriteLine($"      • Análise Final: {qualityGateResult.Confidence.FinalAnalysis.Score}");
                Console.WriteLine($"      • GERAL: {qualityGateResult.Confidence.OverallConfidence}");
                Console.WriteLine($"   📝 AJUSTES:");
                Console.WriteLine($"      • Classification: {qualityGateResult.ClassificationAdjustment.OriginalClassification} → {qualityGateResult.AdjustedClassification}");
                Console.WriteLine($"      • General Score: {qualityGateResult.ScoreAdjustment.OriginalGeneralScore:F2} → {qualityGateResult.AdjustedGeneralScore:F2}");
                Console.WriteLine($"      • Penalty Applied: {qualityGateResult.ScoreAdjustment.PenaltyApplied:P0}");
                Console.WriteLine($"   ✅ Quality Gate Passed: {qualityGateResult.Passed}");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");

                // Aplicar ajustes do Quality Gate Multidimensional
                analysisResult.ConfidenceLevel = qualityGateResult.LegacyConfidenceLevel;
                analysisResult.ConfidenceDetails = qualityGateResult.ConfidenceDto;
                analysisResult.Classification = qualityGateResult.AdjustedClassification;
                analysisResult.GeneralScore = qualityGateResult.AdjustedGeneralScore;
                analysisResult.PersonalizedScore = qualityGateResult.AdjustedPersonalizedScore;
                analysisResult.Summary = qualityGateResult.AdjustedSummary;
                analysisResult.ShortSummary = qualityGateResult.AdjustedShortSummary;

                // Adicionar alertas de confiança se necessário
                if (!qualityGateResult.Passed)
                {
                    analysisResult.Alerts.Insert(0, $"⚠️ {qualityGateResult.QualityMessage}");
                }

                // Adicionar alertas específicos de confiança
                foreach (var alert in qualityGateResult.Confidence.FinalAnalysis.ConfidenceAlerts)
                {
                    if (!analysisResult.Alerts.Contains(alert))
                    {
                        analysisResult.Alerts.Add($"ℹ️ {alert}");
                    }
                }

                // ===== PERSISTIR ANÁLISE COMPLETA NO BANCO =====

                // 1. Criar entidade ProductAnalysis
                var classification = DetermineClassification(analysisResult.GeneralScore, analysisResult.PersonalizedScore);
                var confidence = DetermineConfidenceLevel(analysisResult.ConfidenceLevel);

                var productAnalysis = new ProductAnalysis(
                    productId: product.Id,
                    userId: userId,
                    classification: classification,
                    confidence: confidence,
                    summary: analysisResult.Summary ?? string.Empty
                );

                productAnalysis.AttachProduct(product);

                // 2. Persistir Alerts
                foreach (var alertMsg in analysisResult.Alerts)
                {
                    var alert = new AnalysisAlert(
                        productAnalysisId: productAnalysis.Id,
                        message: alertMsg,
                        severity: classification,
                        confidence: confidence
                    );
                    productAnalysis.AddAlert(alert);
                }

                // 3. Persistir Recommendations
                foreach (var recMsg in analysisResult.Recommendations)
                {
                    var recommendation = new AnalysisRecommendation(
                        productAnalysisId: productAnalysis.Id,
                        recommendation: recMsg,
                        reason: null,
                        explanationLevel: Domain.Enums.ExplanationLevel.Brief
                    );
                    productAnalysis.AddRecommendation(recommendation);
                }

                await _productRepository.AddAsync(product);
                await _analysisWriteRepository.AddAsync(productAnalysis);

                // ===== POPULAR O DTO COM DADOS COMPLETOS =====
                analysisResult.AnalysisId = productAnalysis.Id;
                analysisResult.ProductId = product.Id;
                analysisResult.ProductName = product.Name;
                analysisResult.Brand = product.Brand;
                analysisResult.Classification = classification.ToString();
                analysisResult.ConfidenceLevel = confidence.ToString();
                analysisResult.ExtractedIngredients = parseResult.Ingredients;
                analysisResult.ExtractedAllergens = parseResult.Allergens;
                // ExtractedText já foi preenchido com o texto OCR bruto no pipeline principal
                analysisResult.CreatedAt = productAnalysis.CreatedAt.DateTime;

                stepMetadata.Success = true;
                stepMetadata.AdditionalData = new
                {
                    analysisResult.GeneralScore,
                    analysisResult.PersonalizedScore,
                    AlertsCount = analysisResult.Alerts.Count,
                    RecommendationsCount = analysisResult.Recommendations.Count,
                    ProductId = product.Id,
                    AnalysisId = productAnalysis.Id,
                    QualityGatePassed = qualityGateResult.Passed
                };

                return analysisResult;
            }
            catch (Exception ex)
            {
                stepMetadata.Success = false;
                stepMetadata.ErrorMessage = ex.Message;
                return CreateErrorResult("Erro na análise", ex.Message);
            }
            finally
            {
                stepWatch.Stop();
                stepMetadata.DurationMs = stepWatch.Elapsed.TotalMilliseconds;
                metadata.AnalysisStep = stepMetadata;
            }
        }

        /// <summary>
        /// Executa análise parcial para capturas de tabela nutricional, ingredientes, etc.
        /// Não penaliza por falta de identificação do produto.
        /// </summary>
        private async Task<ProductAnalysisResultDto> ExecutePartialAnalysisAsync(
            IngredientAllergenParseResult parseResult,
            OcrResultDto ocrResult,
            Guid? userId,
            PipelineMetadataDto metadata,
            StepMetadata stepMetadata)
        {
            try
            {
                var captureType = parseResult.SourceCaptureType ?? CaptureType.NutritionTable;
                var hasNutritionData = parseResult.Nutrition?.HasData ?? false;
                var nutritionalFieldsCount = parseResult.Nutrition?.FilledFieldsCount ?? 0;
                var hasIngredients = parseResult.HasIngredients;
                var hasAllergens = parseResult.HasAllergens;

                // ═══════════════════════════════════════════════════════════════════
                // CALCULAR SCORE BASEADO NO CONTEÚDO DISPONÍVEL
                // ═══════════════════════════════════════════════════════════════════
                double partialScore = 0.5; // Base para análise parcial

                // Bônus por dados nutricionais (até +0.35 por 14 campos)
                if (hasNutritionData)
                {
                    partialScore += Math.Min(0.35, nutritionalFieldsCount * 0.025);
                    Console.WriteLine($"   📊 Nutrition fields: {nutritionalFieldsCount} (+{Math.Min(0.35, nutritionalFieldsCount * 0.025):F2})");
                }

                // Bônus por ingredientes
                if (hasIngredients)
                {
                    partialScore += Math.Min(0.15, parseResult.Ingredients.Count * 0.01);
                }

                // Bônus por detecção de alérgenos (importante para segurança)
                if (hasAllergens)
                {
                    partialScore += 0.05;
                }

                // Ajustar pela confiança do OCR (peso 50%)
                double confidenceMultiplier = 0.5 + ocrResult.Confidence * 0.5;
                partialScore *= confidenceMultiplier;

                // Cap em 85% para análise parcial
                partialScore = Math.Min(0.85, partialScore);

                // ═══════════════════════════════════════════════════════════════════
                // DETERMINAR NÍVEL DE CONFIANÇA (CONSISTENTE COM QUALITY GATE)
                // ═══════════════════════════════════════════════════════════════════
                var confidenceLevel = DeterminePartialConfidenceLevelConsistent(partialScore, hasNutritionData, nutritionalFieldsCount);

                // ═══════════════════════════════════════════════════════════════════
                // GERAR SUMÁRIO APROPRIADO PARA ANÁLISE PARCIAL
                // ═══════════════════════════════════════════════════════════════════
                var summary = GeneratePartialAnalysisSummary(parseResult, captureType);
                var shortSummary = GeneratePartialAnalysisShortSummary(parseResult, captureType, partialScore);

                // ═══════════════════════════════════════════════════════════════════
                // MAPEAR NUTRITIONAL FACTS (SEMPRE PREENCHER QUANDO HÁ DADOS)
                // ═══════════════════════════════════════════════════════════════════
                var nutritionalFacts = MapToNutritionalFactsDto(parseResult.Nutrition);

                // Garantir que NutritionalFacts reflete os dados corretamente
                if (nutritionalFacts != null)
                {
                    nutritionalFacts.ExtractedFieldsCount = nutritionalFieldsCount;
                    nutritionalFacts.IsComplete = nutritionalFieldsCount >= 5 && 
                        parseResult.Nutrition?.Calories.HasValue == true &&
                        parseResult.Nutrition?.TotalCarbohydrate.HasValue == true &&
                        parseResult.Nutrition?.Protein.HasValue == true;
                }

                // ═══════════════════════════════════════════════════════════════════
                // CONSTRUIR RESULTADO COM CAMPOS CONSISTENTES
                // ═══════════════════════════════════════════════════════════════════
                var result = new ProductAnalysisResultDto
                {
                    // Produto não identificado (esperado para análise parcial)
                    ProductName = "Análise Parcial",
                    Brand = null,

                    // Scores
                    GeneralScore = partialScore,
                    PersonalizedScore = partialScore,

                    // Classification para análise parcial
                    Classification = "Partial",
                    ConfidenceLevel = confidenceLevel, // Nível consistente

                    // Sumários
                    Summary = summary,
                    ShortSummary = shortSummary,

                    // ══════ FLAGS DE ANÁLISE PARCIAL (CHAVE PARA CONSISTÊNCIA) ══════
                    IsPartialAnalysis = true,
                    CaptureType = captureType, // IMPORTANTE: Propagar CaptureType
                    MissingSteps = parseResult.MissingSteps,

                    // ══════ NUTRITIONAL FACTS (SEMPRE PREENCHER QUANDO HÁ DADOS) ══════
                    NutritionalFacts = nutritionalFacts,

                    // Dados extraídos
                    ExtractedIngredients = parseResult.Ingredients,
                    ExtractedAllergens = parseResult.Allergens,
                    ExtractedText = ocrResult.RawText,

                    // Alertas e recomendações
                    Alerts = GeneratePartialAnalysisAlerts(parseResult, captureType),
                    Recommendations = GeneratePartialAnalysisRecommendations(parseResult, captureType)
                };

                // Log do resultado
                Console.WriteLine($"   ✅ Partial Analysis Result:");
                Console.WriteLine($"      • Score: {partialScore:F2}");
                Console.WriteLine($"      • Confidence Level: {result.ConfidenceLevel}");
                Console.WriteLine($"      • CaptureType: {captureType}");
                Console.WriteLine($"      • Has Nutrition: {hasNutritionData}");
                Console.WriteLine($"      • Nutritional Fields: {nutritionalFieldsCount}");
                Console.WriteLine($"      • NutritionalFacts Populated: {nutritionalFacts != null}");
                Console.WriteLine($"      • Missing Steps: {string.Join(", ", result.MissingSteps)}");

                stepMetadata.Success = true;
                stepMetadata.AdditionalData = new
                {
                    IsPartialAnalysis = true,
                    CaptureType = captureType.ToString(),
                    Score = partialScore,
                    ConfidenceLevel = confidenceLevel,
                    HasNutritionData = hasNutritionData,
                    NutritionalFieldsCount = nutritionalFieldsCount,
                    HasIngredients = hasIngredients,
                    HasAllergens = hasAllergens,
                    MissingSteps = parseResult.MissingSteps,
                    NutritionalFactsPopulated = nutritionalFacts != null
                };

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [PARTIAL ANALYSIS] Error: {ex.Message}");
                return CreateErrorResult("Erro na análise parcial", ex.Message);
            }
        }

        /// <summary>
        /// Determina o nível de confiança de forma consistente com o Quality Gate.
        /// Usa os mesmos thresholds do ConfidenceScore para garantir alinhamento.
        /// </summary>
        private string DeterminePartialConfidenceLevelConsistent(
            double score, 
            bool hasNutritionData, 
            int nutritionalFieldsCount)
        {
            // Usar os mesmos thresholds de ConfidenceThresholds para consistência
            // High >= 0.90, Medium >= 0.65, Low >= 0.40, VeryLow < 0.40

            if (score >= 0.90)
                return "Alto";

            if (score >= 0.65)
                return "Médio";

            // Para análises parciais com dados nutricionais válidos,
            // mesmo com score baixo, considerar pelo menos "Médio" se houver dados
            if (hasNutritionData && nutritionalFieldsCount >= 5 && score >= 0.50)
                return "Médio";

            if (score >= 0.40)
                return "Baixo";

            return "Muito Baixo";
        }

        private string GeneratePartialAnalysisSummary(IngredientAllergenParseResult parseResult, CaptureType captureType)
        {
            var hasNutrition = parseResult.Nutrition?.HasData ?? false;
            var nutritionalFieldsCount = parseResult.Nutrition?.FilledFieldsCount ?? 0;
            var hasIngredients = parseResult.HasIngredients;
            var hasAllergens = parseResult.HasAllergens;

            return captureType switch
            {
                CaptureType.NutritionTable when hasNutrition && nutritionalFieldsCount >= 5 =>
                    $"📊 **Tabela nutricional identificada com sucesso!** " +
                    $"Extraídos {nutritionalFieldsCount} valores nutricionais. " +
                    "Envie a frente da embalagem e a lista de ingredientes para completar a análise.",

                CaptureType.NutritionTable when hasNutrition =>
                    $"📊 **Tabela nutricional parcialmente lida.** " +
                    $"Foram extraídos {nutritionalFieldsCount} valores nutricionais. " +
                    "Tente uma foto mais nítida ou envie outras partes do rótulo para análise completa.",

                CaptureType.NutritionTable =>
                    "📊 **Imagem de tabela nutricional recebida.** " +
                    "Não foi possível extrair valores nutricionais. " +
                    "Envie uma foto mais nítida da tabela nutricional.",

                CaptureType.IngredientsList when hasIngredients =>
                    $"📋 **Lista de ingredientes identificada!** " +
                    $"Encontrados {parseResult.Ingredients.Count} ingredientes" +
                    (hasAllergens ? $" e {parseResult.Allergens.Count} alérgenos" : "") + ". " +
                    "Envie a tabela nutricional para uma análise completa.",

                CaptureType.AllergenStatement when hasAllergens =>
                    $"⚠️ **Declaração de alérgenos identificada!** " +
                    $"Detectados {parseResult.Allergens.Count} alérgenos" +
                    (parseResult.ConfirmedAllergens.Count > 0 ? $" ({parseResult.ConfirmedAllergens.Count} confirmados)" : "") + ". " +
                    "Envie a tabela nutricional e ingredientes para análise completa.",

                CaptureType.FrontPackaging =>
                    $"📦 **Embalagem frontal identificada.** " +
                    (!string.IsNullOrEmpty(parseResult.ProductName) ? $"Produto: {parseResult.ProductName}. " : "") +
                    (!string.IsNullOrEmpty(parseResult.Brand) ? $"Marca: {parseResult.Brand}. " : "") +
                    "Envie a tabela nutricional e ingredientes para análise completa.",

                _ => "📷 **Captura parcial processada.** Envie imagens adicionais para completar a análise."
            };
        }

        private string GeneratePartialAnalysisShortSummary(
            IngredientAllergenParseResult parseResult, 
            CaptureType captureType, 
            double score)
        {
            var scoreDisplay = (int)Math.Round(score * 100);
            var nutritionalFieldsCount = parseResult.Nutrition?.FilledFieldsCount ?? 0;
            var hasNutrition = parseResult.Nutrition?.HasData ?? false;

            return captureType switch
            {
                CaptureType.NutritionTable when hasNutrition && nutritionalFieldsCount >= 5 =>
                    $"✅ Tabela nutricional lida ({nutritionalFieldsCount} campos). Envie ingredientes para análise completa.",

                CaptureType.NutritionTable when hasNutrition =>
                    $"📊 Tabela parcial ({nutritionalFieldsCount} campos). Envie foto mais nítida ou ingredientes.",

                CaptureType.NutritionTable =>
                    $"📊 Tabela detectada. Envie foto mais nítida da tabela nutricional.",

                CaptureType.IngredientsList =>
                    $"✅ Ingredientes identificados ({parseResult.Ingredients.Count}). Envie tabela nutricional.",

                CaptureType.AllergenStatement =>
                    $"⚠️ Alérgenos detectados ({parseResult.Allergens.Count}). Envie mais fotos para análise completa.",

                CaptureType.FrontPackaging =>
                    $"📦 Embalagem identificada. Envie tabela nutricional para análise completa.",

                _ => $"Análise parcial ({scoreDisplay}/100). Complete com mais imagens."
            };
        }

        private NutritionalFactsDto? MapToNutritionalFactsDto(NutritionData? nutrition)
        {
            // Retorna null apenas se nutrition for null
            // Se há dados (mesmo parciais), retorna o DTO preenchido
            if (nutrition == null)
                return null;

            // Criar DTO mesmo se HasData for false - pode ter alguns campos
            var dto = new NutritionalFactsDto
            {
                ServingSize = nutrition.ServingSize,
                ServingsPerContainer = nutrition.ServingsPerContainer,
                Calories = nutrition.Calories,
                TotalCarbohydrate = nutrition.TotalCarbohydrate,
                Sugars = nutrition.Sugars,
                AddedSugars = nutrition.AddedSugars,
                Lactose = nutrition.Lactose,
                Protein = nutrition.Protein,
                TotalFat = nutrition.TotalFat,
                SaturatedFat = nutrition.SaturatedFat,
                TransFat = nutrition.TransFat,
                Cholesterol = nutrition.Cholesterol,
                Sodium = nutrition.Sodium,
                DietaryFiber = nutrition.DietaryFiber,
                Calcium = nutrition.Calcium,
                Iron = nutrition.Iron,
                DailyValuePercentages = nutrition.DailyValuePercentages ?? new Dictionary<string, double>(),
                ExtractedFieldsCount = nutrition.FilledFieldsCount,
                IsComplete = nutrition.Calories.HasValue && 
                            nutrition.TotalCarbohydrate.HasValue && 
                            nutrition.Protein.HasValue && 
                            nutrition.TotalFat.HasValue &&
                            nutrition.Sodium.HasValue
            };

            return dto;
        }

        private List<string> GeneratePartialAnalysisAlerts(
            IngredientAllergenParseResult parseResult, 
            CaptureType captureType)
        {
            var alerts = new List<string>();

            // Alertas de alérgenos (sempre importantes)
            if (parseResult.ConfirmedAllergens.Count > 0)
            {
                alerts.Add($"⚠️ ALÉRGENOS DETECTADOS: {string.Join(", ", parseResult.ConfirmedAllergens)}");
            }

            if (parseResult.MayContainAllergens.Count > 0)
            {
                alerts.Add($"⚠️ PODE CONTER: {string.Join(", ", parseResult.MayContainAllergens)}");
            }

            // Alertas nutricionais (se temos dados)
            if (parseResult.Nutrition != null)
            {
                if (parseResult.Nutrition.Sodium.HasValue && parseResult.Nutrition.Sodium > 600)
                {
                    alerts.Add($"⚠️ Alto teor de sódio: {parseResult.Nutrition.Sodium}mg por porção");
                }

                if (parseResult.Nutrition.AddedSugars.HasValue && parseResult.Nutrition.AddedSugars > 10)
                {
                    alerts.Add($"⚠️ Alto teor de açúcares adicionados: {parseResult.Nutrition.AddedSugars}g por porção");
                }

                if (parseResult.Nutrition.TransFat.HasValue && parseResult.Nutrition.TransFat > 0)
                {
                    alerts.Add($"⚠️ Contém gordura trans: {parseResult.Nutrition.TransFat}g");
                }
            }

            // Alerta sobre análise parcial
            alerts.Add($"ℹ️ Análise parcial ({captureType}). Complete com mais capturas para resultado final.");

            return alerts;
        }

        private List<string> GeneratePartialAnalysisRecommendations(
            IngredientAllergenParseResult parseResult, 
            CaptureType captureType)
        {
            var recommendations = new List<string>();

            switch (captureType)
            {
                case CaptureType.NutritionTable:
                    recommendations.Add("📋 Envie foto da lista de ingredientes para verificar aditivos e conservantes");
                    recommendations.Add("📦 Envie foto da frente da embalagem para identificar o produto");
                    break;

                case CaptureType.IngredientsList:
                    recommendations.Add("📊 Envie foto da tabela nutricional para análise de macronutrientes");
                    recommendations.Add("📦 Envie foto da frente da embalagem para identificar o produto");
                    break;

                case CaptureType.AllergenStatement:
                    recommendations.Add("📊 Envie foto da tabela nutricional para análise completa");
                    recommendations.Add("📋 Envie foto da lista de ingredientes para verificar composição");
                    break;

                case CaptureType.FrontPackaging:
                    recommendations.Add("📊 Envie foto da tabela nutricional para análise de macronutrientes");
                    recommendations.Add("📋 Envie foto da lista de ingredientes para verificar aditivos");
                    break;
            }

            return recommendations;
        }

        private Domain.Enums.AnalysisClassification DetermineClassification(double generalScore, double personalizedScore)
        {
            var avgScore = (generalScore + personalizedScore) / 2.0;

            // Classificação baseada no score médio com thresholds ajustados
            if (avgScore >= 0.80) return Domain.Enums.AnalysisClassification.Excellent;
            if (avgScore >= 0.65) return Domain.Enums.AnalysisClassification.Safe;
            if (avgScore >= 0.50) return Domain.Enums.AnalysisClassification.Moderate;
            if (avgScore >= 0.35) return Domain.Enums.AnalysisClassification.Caution;
            if (avgScore >= 0.20) return Domain.Enums.AnalysisClassification.Unsafe;
            return Domain.Enums.AnalysisClassification.Avoid;
        }

        private Domain.Enums.ConfidenceLevel DetermineConfidenceLevel(string confidenceStr)
        {
            return confidenceStr?.ToLowerInvariant() switch
            {
                "alto" or "high" => Domain.Enums.ConfidenceLevel.High,
                "médio" or "medium" => Domain.Enums.ConfidenceLevel.Medium,
                "baixo" or "low" => Domain.Enums.ConfidenceLevel.Low,
                _ => Domain.Enums.ConfidenceLevel.Medium
            };
        }

        #endregion

        #region Helper Methods

        private ProductAnalysisResultDto CreateErrorResult(string summary, string? details)
        {
            return new ProductAnalysisResultDto
            {
                ProductName = "Análise não concluída",
                Summary = summary,
                Alerts = details != null ? new() { details } : new(),
                GeneralScore = 0,
                PersonalizedScore = 0,
                ConfidenceLevel = "Baixo"
            };
        }

        private async Task CleanupAsync(string? imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    await _fileStorage.DeleteAsync(imagePath);
                }
                catch
                {
                    // Log em produção, mas não falhar o pipeline por isso
                }
            }
        }

        #endregion

        #region New Methods with CaptureType Support

        /// <inheritdoc />
        public async Task<ProductAnalysisPipelineResultDto> ExecutePipelineWithCaptureTypeAsync(
            Stream imageStream,
            string fileName,
            CaptureType captureType,
            CapturedImageAnalysisRequest request,
            Guid? userId = null)
        {
            var pipelineId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;
            var overallStopwatch = Stopwatch.StartNew();

            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"🎯 [PIPELINE] Starting analysis with CaptureType: {captureType}");
            Console.WriteLine($"   • Pipeline ID: {pipelineId}");
            Console.WriteLine($"   • File: {fileName}");
            Console.WriteLine($"   • Barcode: {request.Barcode ?? "N/A"}");
            Console.WriteLine($"   • Language: {request.LanguageCode}");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");

            var result = new ProductAnalysisPipelineResultDto
            {
                Metadata = new PipelineMetadataDto
                {
                    PipelineId = pipelineId,
                    StartTime = startTime
                }
            };

            try
            {
                // ETAPA 1: Upload e Validação da Imagem
                var uploadResult = await ExecuteUploadStepAsync(imageStream, fileName, result.Metadata);
                if (!uploadResult.Success)
                {
                    result.AnalysisResult = CreateErrorResult("Falha no upload da imagem", uploadResult.ErrorMessage);
                    return result;
                }

                // ETAPA 2: OCR - Extração de Texto da Imagem
                // Otimizar OCR baseado no CaptureType
                var ocrResult = await ExecuteOcrStepWithCaptureTypeAsync(uploadResult, captureType, request, result.Metadata);
                if (!ocrResult.Success)
                {
                    await CleanupAsync(uploadResult.ImagePath);
                    result.AnalysisResult = CreateErrorResult("Falha na extração de texto (OCR)", ocrResult.ErrorMessage);
                    return result;
                }

                // ETAPA 3: Parsing - Análise e estruturação do texto baseado no CaptureType
                var parseResult = await ExecuteParsingStepWithCaptureTypeAsync(ocrResult, captureType, result.Metadata);

                // Se temos barcode no request, adicionar ao parseResult
                if (!string.IsNullOrEmpty(request.Barcode) && string.IsNullOrEmpty(parseResult.Barcode))
                {
                    parseResult.Barcode = request.Barcode;
                }

                // ETAPA 4: Análise - Motor de Regras e Scoring
                var analysisResult = await ExecuteAnalysisStepAsync(
                    parseResult,
                    ocrResult,
                    userId,
                    result.Metadata);

                // Incluir texto OCR bruto no resultado
                analysisResult.ExtractedText = ocrResult.RawText;

                result.AnalysisResult = analysisResult;

                // Cleanup
                await CleanupAsync(uploadResult.ImagePath);

                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
                Console.WriteLine($"✅ [PIPELINE] Analysis completed successfully");
                Console.WriteLine($"   • CaptureType: {captureType}");
                Console.WriteLine($"   • General Score: {analysisResult.GeneralScore:F2}");
                Console.WriteLine($"   • Ingredients: {analysisResult.ExtractedIngredients.Count}");
                Console.WriteLine($"   • Allergens: {analysisResult.ExtractedAllergens.Count}");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [PIPELINE] Error: {ex.Message}");
                result.AnalysisResult = CreateErrorResult("Erro inesperado no pipeline", ex.Message);
            }
            finally
            {
                overallStopwatch.Stop();
                result.Metadata.EndTime = DateTime.UtcNow;
                result.Metadata.TotalDurationMs = overallStopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<ProductAnalysisPipelineResultDto> ProcessBarcodeAsync(
            string barcode,
            Guid? userId = null)
        {
            var pipelineId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;
            var overallStopwatch = Stopwatch.StartNew();

            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"🔍 [BARCODE PIPELINE] Processing barcode: {barcode}");
            Console.WriteLine($"   • Pipeline ID: {pipelineId}");
            Console.WriteLine($"   • User ID: {userId?.ToString() ?? "Anonymous"}");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");

            var result = new ProductAnalysisPipelineResultDto
            {
                Metadata = new PipelineMetadataDto
                {
                    PipelineId = pipelineId,
                    StartTime = startTime
                }
            };

            try
            {
                // Skip upload e OCR para processamento apenas de barcode
                result.Metadata.UploadStep = new StepMetadata
                {
                    StepName = "Upload",
                    Success = true,
                    DurationMs = 0,
                    AdditionalData = new { Skipped = true, Reason = "Barcode-only processing" }
                };

                result.Metadata.OcrStep = new StepMetadata
                {
                    StepName = "OCR",
                    Success = true,
                    DurationMs = 0,
                    AdditionalData = new { Skipped = true, Reason = "Barcode provided directly" }
                };

                // Criar um parseResult básico com o barcode
                var parseResult = new IngredientAllergenParseResult
                {
                    Barcode = barcode,
                    ProductName = $"Produto {barcode}"
                };

                result.Metadata.ParsingStep = new StepMetadata
                {
                    StepName = "Parsing",
                    Success = true,
                    DurationMs = 0,
                    AdditionalData = new { Barcode = barcode }
                };

                // Criar um OcrResult mock para a análise
                var mockOcrResult = new OcrResultDto
                {
                    Success = true,
                    RawText = $"Código de barras: {barcode}",
                    Confidence = 1.0
                };

                // Executar análise
                var analysisResult = await ExecuteAnalysisStepAsync(
                    parseResult,
                    mockOcrResult,
                    userId,
                    result.Metadata);

                analysisResult.ExtractedText = $"Produto identificado pelo código de barras: {barcode}";
                result.AnalysisResult = analysisResult;

                Console.WriteLine($"✅ [BARCODE PIPELINE] Completed for barcode: {barcode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [BARCODE PIPELINE] Error: {ex.Message}");
                result.AnalysisResult = CreateErrorResult("Erro ao processar código de barras", ex.Message);
            }
            finally
            {
                overallStopwatch.Stop();
                result.Metadata.EndTime = DateTime.UtcNow;
                result.Metadata.TotalDurationMs = overallStopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }

        private async Task<OcrResultDto> ExecuteOcrStepWithCaptureTypeAsync(
            ImageUploadResultDto uploadResult,
            CaptureType captureType,
            CapturedImageAnalysisRequest request,
            PipelineMetadataDto metadata)
        {
            var stepWatch = Stopwatch.StartNew();
            var stepMetadata = new StepMetadata { StepName = "OCR" };

            var providerName = _ocrProvider.ProviderName;
            var providerType = _ocrProvider.GetType().Name;

            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"🔍 [OCR] CaptureType-optimized extraction");
            Console.WriteLine($"   • CaptureType: {captureType}");
            Console.WriteLine($"   • Provider: {providerName}");
            Console.WriteLine($"   • Multi-Provider: {request.EnableMultiProviderOcr}");
            Console.WriteLine($"   • Confidence Threshold: {request.OcrConfidenceThreshold:P0}");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");

            metadata.OcrProviderName = providerName;
            metadata.OcrProviderVersion = providerType;

            try
            {
                var ocrRequest = new OcrRequestDto
                {
                    ImagePath = uploadResult.ImagePath,
                    FileName = uploadResult.FileName,
                    ContentType = uploadResult.ContentType
                };

                var result = await _ocrProvider.ExtractTextAsync(ocrRequest);

                stepMetadata.Success = result.Success;
                stepMetadata.ErrorMessage = result.ErrorMessage;
                stepMetadata.AdditionalData = new
                {
                    result.Confidence,
                    TextLength = result.RawText.Length,
                    BlocksCount = result.TextBlocks.Count,
                    CaptureType = captureType.ToString(),
                    ProviderName = providerName
                };

                if (result.Success)
                {
                    Console.WriteLine($"✅ [OCR] Extracted {result.RawText.Length} characters ({result.Confidence:P1} confidence)");
                }

                return result;
            }
            finally
            {
                stepWatch.Stop();
                stepMetadata.DurationMs = stepWatch.Elapsed.TotalMilliseconds;
                metadata.OcrStep = stepMetadata;
            }
        }

        private async Task<IngredientAllergenParseResult> ExecuteParsingStepWithCaptureTypeAsync(
            OcrResultDto ocrResult,
            CaptureType captureType,
            PipelineMetadataDto metadata)
        {
            var stepWatch = Stopwatch.StartNew();
            var stepMetadata = new StepMetadata { StepName = "Parsing" };

            Console.WriteLine($"📝 [PARSING] Processing for CaptureType: {captureType}");

            try
            {
                IngredientAllergenParseResult result;

                // ═══════════════════════════════════════════════════════════════════════════════
                // TRATAMENTO ESPECIAL POR TIPO DE CAPTURA
                // ═══════════════════════════════════════════════════════════════════════════════

                switch (captureType)
                {
                    case CaptureType.NutritionTable:
                        result = ParseNutritionTableCapture(ocrResult.RawText);
                        break;

                    case CaptureType.IngredientsList:
                        result = ParseIngredientsListCapture(ocrResult.RawText);
                        break;

                    case CaptureType.AllergenStatement:
                        result = ParseAllergenStatementCapture(ocrResult.RawText);
                        break;

                    case CaptureType.FrontPackaging:
                        result = ParseFrontPackagingCapture(ocrResult.RawText);
                        break;

                    default:
                        // Para outros tipos (Barcode, etc.), usar parser padrão
                        result = _parser.Parse(ocrResult.RawText);
                        break;
                }

                // Adicionar metadados sobre o tipo de captura
                if (result.Metadata == null)
                {
                    result.Metadata = new Dictionary<string, string>();
                }
                result.Metadata["CaptureType"] = captureType.ToString();
                result.SourceCaptureType = captureType;

                stepMetadata.Success = true;
                stepMetadata.AdditionalData = new
                {
                    CaptureType = captureType.ToString(),
                    IngredientsCount = result.Ingredients.Count,
                    AllergensCount = result.Allergens.Count,
                    result.ProductName,
                    HasNutritionInfo = result.Nutrition != null,
                    result.IsPartialAnalysis,
                    MissingSteps = result.MissingSteps
                };

                Console.WriteLine($"✅ [PARSING] CaptureType={captureType}");
                Console.WriteLine($"   • Ingredients: {result.Ingredients.Count}");
                Console.WriteLine($"   • Allergens: {result.Allergens.Count}");
                Console.WriteLine($"   • Has Nutrition: {result.Nutrition != null}");
                Console.WriteLine($"   • Is Partial: {result.IsPartialAnalysis}");
                if (result.MissingSteps.Count > 0)
                {
                    Console.WriteLine($"   • Missing Steps: {string.Join(", ", result.MissingSteps)}");
                }

                return result;
            }
            catch (Exception ex)
            {
                stepMetadata.Success = false;
                stepMetadata.ErrorMessage = ex.Message;
                Console.WriteLine($"❌ [PARSING] Error: {ex.Message}");
                return new IngredientAllergenParseResult();
            }
            finally
            {
                stepWatch.Stop();
                stepMetadata.DurationMs = stepWatch.Elapsed.TotalMilliseconds;
                metadata.ParsingStep = stepMetadata;
            }
        }

        /// <summary>
        /// Parser especializado para capturas de tabela nutricional.
        /// Não tenta identificar produto/marca, foca apenas em nutrientes.
        /// </summary>
        private IngredientAllergenParseResult ParseNutritionTableCapture(string ocrText)
        {
            Console.WriteLine("📊 [NUTRITION TABLE PARSER] Parsing nutrition table capture...");

            var nutritionParser = new Application.Parsing.Strategies.NutritionTableParser();
            var nutritionResult = nutritionParser.Parse(ocrText);

            var result = new IngredientAllergenParseResult
            {
                // NÃO tentar identificar produto/marca para tabela nutricional
                ProductName = null,
                Brand = null,
                IsProductNameValidated = false,
                IsBrandValidated = false,

                // Marcar como análise parcial
                IsPartialAnalysis = true,
                SourceCaptureType = CaptureType.NutritionTable,
                MissingSteps = new List<string> { "IngredientsList", "FrontPackaging" },
                PartialAnalysisMessage = "Tabela nutricional identificada com sucesso. " +
                    "Envie ingredientes ou frente da embalagem para completar a análise.",

                // Mapear nutrientes
                Nutrition = MapNutritionTableToNutritionData(nutritionResult),

                // Ajustar confiança baseada no parsing
                ParsingConfidence = nutritionResult.Confidence,
                ValidationWarnings = nutritionResult.ValidationWarnings
            };

            Console.WriteLine($"   ✅ Extracted {nutritionResult.ExtractedFieldsCount} nutritional fields");
            Console.WriteLine($"   ✅ Confidence: {nutritionResult.Confidence}");

            return result;
        }

        /// <summary>
        /// Parser especializado para capturas de lista de ingredientes.
        /// </summary>
        private IngredientAllergenParseResult ParseIngredientsListCapture(string ocrText)
        {
            Console.WriteLine("📋 [INGREDIENTS LIST PARSER] Parsing ingredients list capture...");

            // Usar parser padrão que já extrai ingredientes
            var result = _parser.Parse(ocrText);

            // Ajustar para análise parcial
            result.IsPartialAnalysis = true;
            result.SourceCaptureType = CaptureType.IngredientsList;
            result.MissingSteps = new List<string> { "NutritionTable", "FrontPackaging" };
            result.PartialAnalysisMessage = "Lista de ingredientes identificada com sucesso. " +
                "Envie tabela nutricional para análise completa.";

            // Se não encontrou produto/marca na lista de ingredientes, isso é esperado
            if (string.IsNullOrEmpty(result.ProductName))
            {
                result.ProductName = null;
                result.IsProductNameValidated = false;
            }

            Console.WriteLine($"   ✅ Extracted {result.Ingredients.Count} ingredients");
            Console.WriteLine($"   ✅ Extracted {result.Allergens.Count} allergens");

            return result;
        }

        /// <summary>
        /// Parser especializado para capturas de declaração de alérgenos.
        /// </summary>
        private IngredientAllergenParseResult ParseAllergenStatementCapture(string ocrText)
        {
            Console.WriteLine("⚠️ [ALLERGEN STATEMENT PARSER] Parsing allergen statement capture...");

            // Usar parser padrão
            var result = _parser.Parse(ocrText);

            // Ajustar para análise parcial focada em alérgenos
            result.IsPartialAnalysis = true;
            result.SourceCaptureType = CaptureType.AllergenStatement;
            result.MissingSteps = new List<string> { "NutritionTable", "IngredientsList", "FrontPackaging" };
            result.PartialAnalysisMessage = "Declaração de alérgenos identificada. " +
                "Envie tabela nutricional e ingredientes para análise completa.";

            if (string.IsNullOrEmpty(result.ProductName))
            {
                result.ProductName = null;
                result.IsProductNameValidated = false;
            }

            Console.WriteLine($"   ✅ Extracted {result.Allergens.Count} allergens");
            Console.WriteLine($"   ✅ Confirmed: {result.ConfirmedAllergens.Count}");
            Console.WriteLine($"   ✅ May Contain: {result.MayContainAllergens.Count}");

            return result;
        }

        /// <summary>
        /// Parser especializado para capturas de embalagem frontal.
        /// Foca em identificar produto/marca e claims.
        /// </summary>
        private IngredientAllergenParseResult ParseFrontPackagingCapture(string ocrText)
        {
            Console.WriteLine("📦 [FRONT PACKAGING PARSER] Parsing front packaging capture...");

            // Usar parser padrão
            var result = _parser.Parse(ocrText);

            // Ajustar para análise parcial
            result.IsPartialAnalysis = true;
            result.SourceCaptureType = CaptureType.FrontPackaging;
            result.MissingSteps = new List<string> { "NutritionTable", "IngredientsList" };
            result.PartialAnalysisMessage = "Embalagem frontal identificada. " +
                "Envie tabela nutricional e ingredientes para análise completa.";

            Console.WriteLine($"   ✅ Product Name: {result.ProductName ?? "N/A"}");
            Console.WriteLine($"   ✅ Brand: {result.Brand ?? "N/A"}");

            return result;
        }

        /// <summary>
        /// Mapeia o resultado do NutritionTableParser para NutritionData.
        /// </summary>
        private static NutritionData MapNutritionTableToNutritionData(
            Application.Parsing.Strategies.NutritionTableParseResult nutritionResult)
        {
            return new NutritionData
            {
                ServingSize = nutritionResult.ServingSize,
                ServingsPerContainer = nutritionResult.ServingsPerContainer,
                Calories = nutritionResult.Calories,
                TotalCarbohydrate = nutritionResult.TotalCarbohydrate,
                Sugars = nutritionResult.Sugars,
                AddedSugars = nutritionResult.AddedSugars,
                Lactose = nutritionResult.Lactose,
                Protein = nutritionResult.Protein,
                TotalFat = nutritionResult.TotalFat,
                SaturatedFat = nutritionResult.SaturatedFat,
                TransFat = nutritionResult.TransFat,
                Cholesterol = nutritionResult.Cholesterol,
                Sodium = nutritionResult.Sodium,
                DietaryFiber = nutritionResult.DietaryFiber,
                Calcium = nutritionResult.Calcium,
                Iron = nutritionResult.Iron,
                VitaminA = nutritionResult.VitaminA,
                VitaminC = nutritionResult.VitaminC,
                VitaminD = nutritionResult.VitaminD,
                DailyValuePercentages = nutritionResult.DailyValuePercentages
            };
        }

        #endregion
    }
}
