using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using LabelWise.Application.DTOs.AI;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Application.Presentation;
using LabelWise.Application.Validation;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using LabelWise.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.NutritionPipeline;

/// <summary>
/// Pipeline de análise nutricional com validação híbrida OCR.
/// 
/// ┌─────────────────────────────────────────────────────────┐
/// │           IMAGEM DA TABELA NUTRICIONAL                  │
/// └─────────────────────────────────────────────────────────┘
///                        │
///            ┌───────────┴───────────┐
///            ▼                       ▼
/// ┌─────────────────────┐   ┌─────────────────────┐
/// │ Azure OpenAI Vision │   │ Computer Vision OCR │
/// │  (Contexto + IA)    │   │   (OCR Preciso)     │
/// └─────────────────────┘   └─────────────────────┘
///            │                       │
///            ▼                       ▼
///   ┌──────────────┐         ┌──────────────┐
///   │ Calorias: 436│         │ Calorias: 519│ ✅
///   │ Proteína: 6.1│         │ Proteína: 5.2│ ✅
///   └──────────────┘         └──────────────┘
///                │
///                ▼
///        ┌────────────────┐
///        │   VALIDADOR    │
///        │  Se divergir   │
///        │  > 15%, usar   │
///        │  Computer      │
///        │  Vision        │
///        └────────────────┘
/// 
/// FLUXO DO PIPELINE:
/// • STAGE 1: Interpretação Visual (Azure OpenAI Vision) - Extrai dados com contexto
/// • STAGE 2: Extração de dados brutos da resposta da IA
/// • STAGE 2b: Rebuild da tabela nutricional via parser
/// • STAGE 2c: Validação Híbrida OCR (NOVO) - Valida com Computer Vision OCR
/// • STAGE 3-12: Normalização, validação, fallback, scoring, persistência
/// 
/// A validação híbrida garante precisão nos valores críticos (calorias, macros)
/// usando OCR de alta precisão quando detecta divergências > 15%.
/// </summary>
public sealed class NutritionAnalysisPipeline : INutritionAnalysisService
{
    private readonly IVisualInterpreter _visualInterpreter;
    private readonly INutritionSanitizer _nutritionSanitizer;
    private readonly ICategoryNormalizationService _categoryNormalization;
    private readonly IDatabaseNutritionFallbackService _databaseFallback;
    private readonly ICategoryDecisionEngine _categoryDecision;
    private readonly IScoreCalculator _scoreCalculator;
    private readonly IScoreInterpretationService _scoreInterpretation;
    private readonly IAnalysisConsistencyValidator _consistencyValidator;
    private readonly INutritionParseSanityValidator _sanityValidator;
    private readonly INutritionResponseMapper _responseMapper;
    private readonly IProductRepository _productRepository;
    private readonly IAnalysisWriteRepository _analysisWriteRepository;
    private readonly IHybridOcrValidator _hybridOcrValidator;
    private readonly IOcrProvider _azureVisionOcr;
    private readonly StructuredTableOcrParser _structuredParser;
    private readonly ILogger<NutritionAnalysisPipeline> _logger;
    public NutritionServingModel ServingModel { get; set; } = NutritionServingModel.Unknown;
    public string? NormalizationApplied { get; set; }
    public NutritionAnalysisPipeline(
        IVisualInterpreter visualInterpreter,
        INutritionSanitizer nutritionSanitizer,
        ICategoryNormalizationService categoryNormalization,
        IDatabaseNutritionFallbackService databaseFallback,
        ICategoryDecisionEngine categoryDecision,
        IScoreCalculator scoreCalculator,
        IScoreInterpretationService scoreInterpretation,
        IAnalysisConsistencyValidator consistencyValidator,
        INutritionParseSanityValidator sanityValidator,
        INutritionResponseMapper responseMapper,
        IProductRepository productRepository,
        IAnalysisWriteRepository analysisWriteRepository,
        IHybridOcrValidator hybridOcrValidator,
        IOcrProvider azureVisionOcr,
        StructuredTableOcrParser structuredParser,
        ILogger<NutritionAnalysisPipeline> logger)
    {
        _visualInterpreter = visualInterpreter;
        _nutritionSanitizer = nutritionSanitizer;
        _categoryNormalization = categoryNormalization;
        _databaseFallback = databaseFallback;
        _categoryDecision = categoryDecision;
        _scoreCalculator = scoreCalculator;
        _scoreInterpretation = scoreInterpretation;
        _consistencyValidator = consistencyValidator;
        _sanityValidator = sanityValidator ?? throw new ArgumentNullException(nameof(sanityValidator));
        _responseMapper = responseMapper;
        _productRepository = productRepository;
        _analysisWriteRepository = analysisWriteRepository;
        _hybridOcrValidator = hybridOcrValidator ?? throw new ArgumentNullException(nameof(hybridOcrValidator));
        _azureVisionOcr = azureVisionOcr ?? throw new ArgumentNullException(nameof(azureVisionOcr));
        _structuredParser = structuredParser ?? throw new ArgumentNullException(nameof(structuredParser));
        _logger = logger;
    }

    public async Task<NutritionAnalysisResponseDto> AnalyzeProductImageAsync(
        byte[] imageData,
        string fileName,
        string languageCode = "pt",
        List<string>? requestedProfiles = null,
        Guid? userId = null,
        string? deviceId = null)
    {
        var stopwatch = Stopwatch.StartNew();

        var context = new NutritionAnalysisContext
        {
            FileName = fileName,
            LanguageCode = languageCode,
            RequestedProfiles = requestedProfiles,
            UserId = userId,
            DeviceId = deviceId
        };

        try
        {
            // ══════════════════════════════════════════════════════════════
            // 🔧 MODO SIMPLIFICADO: APENAS OCR VISION
            // OpenAI Vision DESABILITADO temporariamente para testes
            // ══════════════════════════════════════════════════════════════

            _logger.LogInformation("[Pipeline] ⚡ MODO OCR PURO - OpenAI Vision desabilitado");

            var tempPath = Path.Combine(Path.GetTempPath(), $"ocr_only_{Guid.NewGuid()}.jpg");
            try
            {
                await File.WriteAllBytesAsync(tempPath, imageData);

                var ocrRequest = new LabelWise.Application.DTOs.OcrRequestDto
                {
                    ImagePath = tempPath,
                    FileName = fileName
                };

                var ocrResult = await _azureVisionOcr.ExtractTextAsync(ocrRequest);
                if (!ocrResult.Success || string.IsNullOrWhiteSpace(ocrResult.RawText))
                {
                    _logger.LogWarning("[Pipeline] ❌ OCR não conseguiu extrair texto da imagem");
                    context.Success = false;
                    context.ErrorMessage = "Não foi possível extrair texto da imagem. Por favor, tire uma foto clara da tabela nutricional com boa iluminação.";
                    context.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                    context.HasReliableNutritionData = false;
                    context.FallbackType = "cannot_read";
                    return _responseMapper.Map(context);
                }

                _logger.LogInformation("[Pipeline] OCR extraiu {Lines} linhas com confiança {Confidence:F2}%",
                    ocrResult.TextBlocks?.Count ?? 0, ocrResult.Confidence * 100);

                // ── Validação de tabela template (sem valores preenchidos) ────────────────
                // Detecta tabelas que existem estruturalmente (palavras-chave presentes)
                // mas não possuem valores numéricos reais — templates, artes sem impressão
                // ou fotos que capturam apenas o layout da tabela.
                {
                    var ocrNumbers = NutritionTableValidator.ExtractNumbers(ocrResult.RawText);
                    bool tableKeywordsPresent = NutritionTableDetectedInRawText(ocrResult.RawText);
                    bool isValidTable = NutritionTableValidator.IsValidTable(ocrResult.RawText, ocrNumbers);

                    if (tableKeywordsPresent && !isValidTable)
                    {
                        _logger.LogWarning(
                            "[Pipeline] ⚠️ Template de tabela detectado — palavras-chave presentes mas sem valores numéricos reais (numericCount={Count})",
                            ocrNumbers.Count);

                        context.Success = true;
                        context.HasReliableNutritionData = false;
                        context.FallbackType = "template_table";
                        context.PublicAnalysisMode = AnalysisMode.FrontOfPackageOnly;
                        context.AnalysisMode = NutritionDecisionMode.CategoryFallback;
                        context.Evidence.HasVisibleNutritionTable = true;
                        context.Evidence.HasReliableNumericExtraction = false;
                        context.NutritionFlags.Add("NutritionTable:template_only");
                        context.NutritionFlags.Add("DataQuality:empty_table");
                        // Sem FinalNutritionProfile → sem score, sem classificação, sem fallback
                        context.Warnings.Add("Tabela nutricional detectada, mas não contém valores preenchidos.");
                        context.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                        return _responseMapper.Map(context);
                    }
                }

                // ✅ NOVO: Usar parser estruturado com TextBlocks + coordenadas espaciais
                StructuredNutritionResult parsed;

                if (ocrResult.TextBlocks != null && ocrResult.TextBlocks.Any())
                {
                    _logger.LogInformation("[Pipeline] 🎯 Usando PARSER ESTRUTURADO (TextBlocks com coordenadas)");
                    parsed = _structuredParser.ParseStructured(ocrResult.TextBlocks, ocrResult.RawText);
                }
                else
                {
                    _logger.LogWarning("[Pipeline] ⚠️ TextBlocks não disponíveis, usando parser simples (fallback)");
                    var lines = ocrResult.RawText.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                    var simpleParser = new NutritionTableParser();
                    var simpleParsed = simpleParser.Parse(lines);

                    parsed = new StructuredNutritionResult
                    {
                        Success = simpleParsed.HasAnyValue,
                        Calories = simpleParsed.Calories,
                        Protein = simpleParsed.Protein,
                        Fat = simpleParsed.Fat,
                        SaturatedFat = simpleParsed.SaturatedFat,
                        Carbs = simpleParsed.Carbs,
                        Sugar = simpleParsed.Sugar,
                        AddedSugar = simpleParsed.AddedSugar,
                        Fiber = simpleParsed.Fiber,
                        Sodium = simpleParsed.Sodium,
                        Unit = simpleParsed.Unit
                    };
                }

                if (!parsed.Success)
                {
                    _logger.LogWarning("[Pipeline] ❌ Parser estruturado falhou — verificando se OCR detectou texto de tabela nutricional...");

                    // Detectar se o texto do OCR, mesmo que garbled, sugere presença de tabela nutricional.
                    // OCR de imagens escuras/baixo contraste pode retornar texto ilegível mas reconhecer
                    // palavras-chave como "INFORMAÇÃO NUTRICIONAL", "PORÇÃO", "gordura", etc.
                    // Nesses casos, o GPT-4.1 deve ser ativado como extrator primário.
                    bool nutritionTableInRawText = NutritionTableDetectedInRawText(ocrResult.RawText);

                    if (!nutritionTableInRawText)
                    {
                        _logger.LogWarning("[Pipeline] ❌ Nenhum sinal de tabela nutricional no texto OCR");
                        context.Success = false;
                        context.ErrorMessage = "Não foi possível extrair os valores nutricionais da tabela. Verifique se a imagem está nítida e com boa iluminação.";
                        context.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                        context.HasReliableNutritionData = false;
                        context.FallbackType = "cannot_parse";
                        return _responseMapper.Map(context);
                    }

                    // Tabela detectada no texto OCR mas valores muito garbled para o parser.
                    // 🔒 GPT-4.1 é chamado APENAS para metadados (nome / marca / categoria).
                    // Os valores numéricos são preenchidos pelo fallback por categoria no Stage9.
                    _logger.LogInformation("[Pipeline] 📊 Tabela detectada no texto OCR (valores garbled) → GPT-4.1 (metadados) + fallback de categoria");

                    var extractedLinesGpt = ocrResult.RawText.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                    context.FinalNutritionProfile = new EstimatedNutritionProfileDto
                    {
                        Basis = "OCR não conseguiu extrair valores numéricos — perfil será estimado por categoria"
                    };
                    context.VisionResult = new VisualInterpretationResult
                    {
                        ProbableCaptureType = CaptureType.NutritionTable,
                        RawExtractedText = extractedLinesGpt,
                        ConfidenceDetails = new ConfidenceDetailsDto
                        {
                            EstimatedNutritionProfile = ocrResult.Confidence * 0.5
                        },
                        Classification = new ProductClassificationDto()
                    };
                    context.CategoryRaw      = "Produto alimentício";
                    context.PublicAnalysisMode = AnalysisMode.FrontOfPackageOnly;
                    context.AnalysisMode     = NutritionDecisionMode.CategoryFallback;
                    context.Evidence.HasVisibleNutritionTable = true;
                    context.Evidence.HasReliableNumericExtraction = false;
                    context.HasReliableNutritionData = false;
                    context.NutritionDataSource = DataSource.Fallback;
                    context.FallbackType = "category_based";
                    context.NutritionFlags.Add("OCR_GARBLED:fallback_only");
                    context.NutritionFlags.Add("NutritionTable:detected");
                    context.NutritionFlags.Add("DataQuality:partial");

                    // Metadados via GPT-4.1 (sem qualquer dado numérico)
                    await TryEnrichWithGptAsync(context, imageData, fileName, new StructuredNutritionResult());

                    // Pular o bloco de setup normal e ir direto para o Stage 2d
                    goto pipelineContinue;
                }

                // Validar dados mínimos extraídos
                bool hasMinimumData = 
                    parsed.Calories.HasValue &&
                    (parsed.Carbs.HasValue || parsed.Protein.HasValue || parsed.Fat.HasValue);

                if (!hasMinimumData)
                {
                    _logger.LogWarning("[Pipeline] ❌ Dados insuficientes (Calories={Cal}, Carbs={Carbs}, Protein={Prot}, Fat={Fat}) — verificando tabela no OCR...",
                        parsed.Calories, parsed.Carbs, parsed.Protein, parsed.Fat);

                    // Mesmo que o parser tenha retornado Success=true, pode ter extraído
                    // apenas um nutriente secundário (ex.: fibra de um OCR garbled).
                    // Se o texto bruto do OCR sugere tabela nutricional → ativar GPT.
                    if (NutritionTableDetectedInRawText(ocrResult.RawText))
                    {
                        _logger.LogInformation("[Pipeline] 📊 Dados insuficientes mas tabela detectada → GPT-4.1 como extrator primário");
                        goto activateGptAsPrimary;
                    }

                    context.Success = false;
                    context.ErrorMessage = parsed.ErrorMessage ??
                        "Conseguimos detectar a tabela nutricional, mas não foi possível extrair dados suficientes. Tire uma foto mais nítida, garantindo boa iluminação e foco na tabela.";
                    context.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
                    context.HasReliableNutritionData = false;
                    context.FallbackType = "insufficient_data";
                    return _responseMapper.Map(context);

                    activateGptAsPrimary:
                    var extractedLinesGpt2 = ocrResult.RawText.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                    context.FinalNutritionProfile = new EstimatedNutritionProfileDto
                    {
                        Basis = "OCR insuficiente — perfil será estimado por categoria"
                    };
                    context.VisionResult = new VisualInterpretationResult
                    {
                        ProbableCaptureType = CaptureType.NutritionTable,
                        RawExtractedText = extractedLinesGpt2,
                        ConfidenceDetails = new ConfidenceDetailsDto
                        {
                            EstimatedNutritionProfile = ocrResult.Confidence * 0.5
                        },
                        Classification = new ProductClassificationDto()
                    };
                    context.CategoryRaw      = "Produto alimentício";
                    context.PublicAnalysisMode = AnalysisMode.FrontOfPackageOnly;
                    context.AnalysisMode     = NutritionDecisionMode.CategoryFallback;
                    context.Evidence.HasVisibleNutritionTable = true;
                    context.Evidence.HasReliableNumericExtraction = false;
                    context.HasReliableNutritionData = false;
                    context.NutritionDataSource = DataSource.Fallback;
                    context.FallbackType = "category_based";
                    context.NutritionFlags.Add("OCR_INSUFFICIENT:fallback_only");
                    context.NutritionFlags.Add("NutritionTable:detected");
                    context.NutritionFlags.Add("DataQuality:partial");

                    // Metadados via GPT-4.1 (sem qualquer dado numérico)
                    await TryEnrichWithGptAsync(context, imageData, fileName, new StructuredNutritionResult());

                    goto pipelineContinue;
                }

                // ✅ SUCESSO - Criar profile com valores reais do OCR
                context.FinalNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = parsed.Unit == "g" ? parsed.Calories : null,
                    CaloriesPer100ml = parsed.Unit == "ml" ? parsed.Calories : null,
                    EstimatedCarbsPer100g = parsed.Carbs,
                    EstimatedSugarPer100g = parsed.Sugar,
                    EstimatedAddedSugarPer100g = parsed.AddedSugar,
                    EstimatedProteinPer100g = parsed.Protein,
                    EstimatedFatPer100g = parsed.Fat,
                    EstimatedSaturatedFatPer100g = parsed.SaturatedFat,
                    EstimatedFiberPer100g = parsed.Fiber,
                    // Sódio: sanitizado imediatamente — valores < 5 mg com macros presentes
                    // indicam leitura de coluna errada pelo parser estruturado (ex.: coluna de porção).
                    // Sódio null aqui → preenchido pelo GPT-4.1 (enriquecimento) ou fallback por categoria.
                    EstimatedSodiumPer100g = SanitizeOcrSodium(parsed.Sodium, parsed.Carbs, parsed.Protein),
                    NutritionUnit = parsed.Unit,
                    Basis = parsed.Unit == "ml" ? "100 ml (extraído via OCR estruturado)" : "100 g (extraído via OCR estruturado)",
                    DataSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Provider"] = "Azure Computer Vision OCR + Structured Parser",
                        ["ExtractionMethod"] = "STRUCTURED_OCR_WITH_COORDINATES",
                        ["Confidence"] = ocrResult.Confidence.ToString("F2"),
                        ["ParserType"] = ocrResult.TextBlocks != null && ocrResult.TextBlocks.Any() 
                            ? "StructuredTableOcrParser" 
                            : "NutritionTableParser (fallback)"
                    }
                };

                // ✅ Definir flags de qualidade de dados (IMPORTANTE para o ResponseBuilder)
                context.HasReliableNutritionData = true;
                context.NutritionDataSource = DataSource.Real;
                context.FallbackType = "real";

                // Adicionar flags que o ResponseBuilder espera
                context.NutritionFlags.Add("NutritionTable:detected");
                context.NutritionFlags.Add("DataQuality:full");
                context.NutritionFlags.Add($"CriticalValues:{CountExtractedValuesStructured(parsed)}/5");

                // 🔥 CRÍTICO: Marcar evidência de tabela nutricional (NÃO PODE SER SOBRESCRITO)
                context.Evidence.HasVisibleNutritionTable = true;
                context.Evidence.HasReliableNumericExtraction = true;

                // 🔥 NOVO: Flag para garantir que Stage2d NÃO sobrescreva este valor
                context.NutritionFlags.Add("OCR_MODE:nutrition_table_confirmed");

                _logger.LogInformation("[Pipeline] ✅ Valores extraídos do OCR:");
                _logger.LogInformation("[Pipeline]    • Calorias: {Cal} kcal", parsed.Calories);
                _logger.LogInformation("[Pipeline]    • Proteína: {Prot} g", parsed.Protein);
                _logger.LogInformation("[Pipeline]    • Gordura: {Fat} g", parsed.Fat);
                _logger.LogInformation("[Pipeline]    • Carboidratos: {Carbs} g", parsed.Carbs);
                _logger.LogInformation("[Pipeline]    • Sódio: {Sodium} mg", parsed.Sodium);
                _logger.LogInformation("[Pipeline] ✅ Quality flags: HasTable=true, DataQuality=full, Reliable=true");

                // Criar contexto mínimo para o pipeline (SEM DADOS FAKE)
                var extractedLines = ocrResult.RawText.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                context.VisionResult = new VisualInterpretationResult
                {
                    ProbableCaptureType = CaptureType.NutritionTable,
                    RawExtractedText = extractedLines,
                    ConfidenceDetails = new ConfidenceDetailsDto
                    {
                        ProductIdentification = 0,
                        VisibleClaimsExtraction = 0,
                        EstimatedNutritionProfile = ocrResult.Confidence,
                        Classification = 0
                    },
                    Classification = new ProductClassificationDto()
                };

                context.CategoryRaw = "Produto alimentício"; // Categoria genérica
                context.PublicAnalysisMode = AnalysisMode.FullNutritionLabel;
                context.AnalysisMode = NutritionDecisionMode.FullNutritionLabel;
                context.HasReliableNutritionData = true;

                // ── STAGE 1.5: Enriquecimento via GPT-4.1 (fallback inteligente) ──────
                // Chamado quando OCR não fornece dados completos ou identificação do produto
                await TryEnrichWithGptAsync(context, imageData, fileName, parsed);

                pipelineContinue: ; // GPT garbled-recovery ou enriquecimento normal concluído
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch { /* best effort */ }
                }
            }

            // ══════ STAGE 2d: Validação de Dados Mínimos (SEGURANÇA) ══════
            Stage2d_ValidateMinimumNutritionData(context);

            // ══════ STAGE 3: Normalize (sem inventar valores) ══════
            Stage3_Normalize(context);

            // ══════ STAGE 4: Detect DataSource ══════
            Stage4_DetectDataSource(context);

            // ══════ STAGE 5: Validation (inconsistências e ranges) ══════
            Stage5_Validate(context);
            // ══════ STAGE 6: Detect Serving Model (NOVO) ══════
            Stage6_DetectServingModel(context);

            // ══════ STAGE 7: Normalização absoluta (NOVO) ══════
            Stage7_NormalizeToCanonical100g(context);

            // ══════ STAGE 8: Lock de dados ══════
            Stage8_LockNutritionData(context);

            // ══════ STAGE 9: Fallback controlado ══════
            await Stage9_ApplyFallbackIfNeeded(context);

            // ══════ STAGE 8: Category Engine ══════
            await Stage8_CategoryEngine(context);

            // ══════ STAGE 8b: Detect ProductForm + Normalize Category ══════
            DetectProductForm(context);
            NormalizeCategoryByForm(context);

            // ══════ STAGE 8c: Ajuste de interpretação por forma ══════
            AdjustNutritionByForm(context);

            // ══════ STAGE 8d: Validação cruzada por forma ══════
            ValidateCrossByProductForm(context);

            // ══════ STAGE 9: Enrichment ══════
            Stage9_Enrichment(context);

            // ══════ STAGE 9b: Guard — bloqueia score/perfis quando não há dados nutricionais válidos ══════
            Stage9b_GuardEmptyNutrition(context);

            // ══════ STAGE 10: Score protegido por confiança ══════
            Stage10_CalculateDeterministicScore(context);

            // ══════ STAGE 11: Interpretação semântica ══════
            Stage11_InterpretSemantics(context);

            // ══════ STAGE 12: Finalização ══════
            await Stage12_FinalValidationAndPersist(context, fileName, userId, deviceId);

            context.Success = HasUsableAnalysis(context);
            context.ErrorMessage = context.Success ? null : "Não foi possível interpretar dados úteis da imagem";
            context.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
            
            var response = _responseMapper.Map(context);

            NutritionTextPresentationBuilder.Apply(response);

            // Regras pós-mapeamento: ajuste de reason e validador final de conflito
            ApplyPostScoreResponseRules(response);

            // Enforcement conservador final
            /* COMENTADO - Não temos visionResult no modo OCR puro
            if (response.Success)
            {
                ApplyConservativeModeEnforcement(response, visionResult);
            }
            */

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante pipeline de análise nutricional");
            context.Success = false;
            context.ErrorMessage = $"Erro durante análise: {ex.Message}";
            context.ProcessingTimeSeconds = stopwatch.Elapsed.TotalSeconds;
            return _responseMapper.Map(context);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 1.5: Enriquecimento de METADADOS via GPT-4.1
    //
    // 🔒 REGRA DE INTEGRIDADE NUMÉRICA (POLÍTICA DE PRODUÇÃO):
    //   GPT-4.1 NUNCA produz, sugere ou sobrescreve valores numéricos
    //   nutricionais. OCR é a ÚNICA fonte de verdade para números
    //   (calorias, proteína, gordura, sódio, etc.). Quando o OCR não
    //   tem um valor, o pipeline recorre exclusivamente ao fallback
    //   por categoria (Stage9_ApplyFallbackIfNeeded).
    //
    // GPT é usado APENAS para:
    //   • productName
    //   • brand
    //   • category
    //   • visibleClaims (e ingredientes, quando disponíveis)
    //
    // Qualquer perfil nutricional retornado pelo GPT é IGNORADO.
    // ═══════════════════════════════════════════════════════════════════

    private async Task TryEnrichWithGptAsync(
        NutritionAnalysisContext context,
        byte[] imageData,
        string fileName,
        StructuredNutritionResult ocrParsed)
    {
        // GPT só é chamado para preencher METADADOS ausentes (nome / marca / categoria).
        // Valores numéricos retornados pelo GPT são descartados — OCR + fallback são a
        // única fonte para nutrição.
        bool missingProductId = string.IsNullOrWhiteSpace(context.ProductName)
                              || string.IsNullOrWhiteSpace(context.Brand)
                              || string.IsNullOrWhiteSpace(context.CategoryRaw)
                              || string.Equals(context.CategoryRaw, "Produto alimentício", StringComparison.OrdinalIgnoreCase);

        if (!missingProductId)
        {
            _logger.LogInformation("[Pipeline.GPT] ✅ Metadados completos — chamada GPT-4.1 não necessária");
            return;
        }

        _logger.LogInformation("[Pipeline.GPT] 🤖 Solicitando GPT-4.1 apenas para METADADOS (produto / marca / categoria)");

        var tempPath = Path.Combine(Path.GetTempPath(), $"gpt_enrich_{Guid.NewGuid()}.jpg");
        try
        {
            await File.WriteAllBytesAsync(tempPath, imageData);

            var gptResult = await _visualInterpreter.InterpretImageAsync(
                new VisualInterpretationRequest { ImagePath = tempPath });

            if (gptResult == null || !string.IsNullOrWhiteSpace(gptResult.ErrorMessage))
            {
                _logger.LogWarning("[Pipeline.GPT] ⚠️ GPT-4.1 retornou erro: {Error}", gptResult?.ErrorMessage);
                return;
            }

            // ── Identificação do produto (GPT supera OCR aqui) ───────────
            var gptProductName = gptResult.ProbableProductName ?? gptResult.ProductName;
            if (string.IsNullOrWhiteSpace(context.ProductName) && IsUsableGptIdentifier(gptProductName))
            {
                context.ProductName = gptProductName;
                _logger.LogInformation("[Pipeline.GPT] 📦 ProductName: {Name}", context.ProductName);
            }

            var gptBrand = gptResult.ProbableBrand ?? gptResult.Brand;
            if (string.IsNullOrWhiteSpace(context.Brand) && IsUsableGptIdentifier(gptBrand))
            {
                context.Brand = gptBrand;
                _logger.LogInformation("[Pipeline.GPT] 🏷️ Brand: {Brand}", context.Brand);
            }

            var gptCategory = gptResult.ProbableCategory ?? gptResult.Category;
            if (!string.IsNullOrWhiteSpace(gptCategory) &&
                (string.IsNullOrWhiteSpace(context.CategoryRaw) || context.CategoryRaw == "Produto alimentício"))
            {
                context.CategoryRaw = gptCategory;
                _logger.LogInformation("[Pipeline.GPT] 🗂️ Category: {Cat}", gptCategory);
            }

            if (!string.IsNullOrWhiteSpace(gptResult.ProbablePackageWeight) &&
                string.IsNullOrWhiteSpace(context.PackageWeight))
                context.PackageWeight = gptResult.ProbablePackageWeight;

            // ── Visible claims (metadados textuais) ──────────────────────
            if (gptResult.VisibleClaims != null && gptResult.VisibleClaims.Count > 0)
            {
                foreach (var claim in gptResult.VisibleClaims)
                {
                    if (!string.IsNullOrWhiteSpace(claim) &&
                        !context.VisibleClaims.Contains(claim, StringComparer.OrdinalIgnoreCase))
                    {
                        context.VisibleClaims.Add(claim);
                    }
                }
            }

            // 🔒 INTEGRIDADE NUMÉRICA: dados nutricionais retornados pelo GPT são
            // descartados deliberadamente. OCR é a única fonte numérica; campos
            // ausentes serão preenchidos pelo fallback por categoria no Stage9.
            if (gptResult.EstimatedNutritionProfile != null)
            {
                _logger.LogInformation(
                    "[Pipeline.GPT] 🔒 Perfil nutricional do GPT IGNORADO por política de integridade — OCR + fallback são as únicas fontes numéricas");
                context.NutritionFlags.Add("GPT4.1:nutrition_ignored");
            }

            context.NutritionFlags.Add("GPT4.1:metadata_only");
            _logger.LogInformation("[Pipeline.GPT] ✅ Enriquecimento de metadados GPT-4.1 concluído");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Pipeline.GPT] ⚠️ Falha no GPT-4.1 — pipeline continua apenas com OCR");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
        }
    }

    /// <summary>
    /// Retorna true somente se o valor retornado pelo GPT representa uma identificação
    /// real — filtra strings de placeholder como "Marca não identificada", "Desconhecido", etc.
    /// </summary>
    private static bool IsUsableGptIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var placeholders = new[]
        {
            "não identificad", "nao identificad",
            "desconhecid",
            "não disponível", "nao disponivel",
            "não encontrad", "nao encontrad",
            "indisponível", "indisponivel",
            "sem informação", "sem informacao",
            "não visível", "nao visivel",
            "ilegível", "ilegivel",
            "n/a", "unknown", "not found", "not identified"
        };

        var lower = value.ToLowerInvariant().Trim();
        return !placeholders.Any(p => lower.Contains(p));
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 1: Interpretação Visual
    // ═══════════════════════════════════════════════════════════════════

    private async Task<VisualInterpretationResult?> Stage1_VisualInterpretation(byte[] imageData)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"nutrition_{Guid.NewGuid()}.jpg");
        try
        {
            await File.WriteAllBytesAsync(tempPath, imageData);
            _logger.LogInformation("[Pipeline.Stage1] Starting visual interpretation");
            var result = await _visualInterpreter.InterpretImageAsync(new VisualInterpretationRequest { ImagePath = tempPath });
            _logger.LogInformation("[Pipeline.Stage1] Completed with confidence: {Confidence}", result.InterpretationConfidence);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Pipeline.Stage1] Error during visual interpretation");
            return null;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch { /* best effort */ }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 2: Extract (sem inferência)
    // ═══════════════════════════════════════════════════════════════════
    private void Stage2b_RebuildNutritionTable(NutritionAnalysisContext context, VisualInterpretationResult visionResult)
    {
        // Se já houve correção pelo OCR híbrido (Stage2c), NÃO sobrescrever
        if (context.HybridOcrCorrectionApplied)
        {
            _logger.LogInformation("[Pipeline.Stage2b] ⏭️ Pulando rebuild - valores já corrigidos pelo OCR híbrido");
            return;
        }

        if (visionResult.RawExtractedText == null || !visionResult.RawExtractedText.Any())
            return;

        var lines = visionResult.RawExtractedText;

        var detected = NutritionColumnParser.DetectColumns(lines);
        _logger.LogInformation(
            "Detected columns: ml={ml}, g={g}, portion={portion}",
            detected.Per100mlIndex?.ToString() ?? "null",
            detected.Per100gIndex?.ToString() ?? "null",
            detected.PortionIndex?.ToString() ?? "null");

        if (!detected.HasAnyColumn)
            return;

        var parser = new NutritionTableParser();
        var parsed = parser.Parse(lines);
        if (!parsed.HasAnyValue)
            return;

        // Apenas aplicar se ainda não temos valores melhores
        context.FinalNutritionProfile = new EstimatedNutritionProfileDto
        {
            CaloriesPer100g = parsed.Unit == "g" ? parsed.Calories : null,
            CaloriesPer100ml = parsed.Unit == "ml" ? parsed.Calories : null,
            EstimatedCarbsPer100g = parsed.Carbs,
            EstimatedSugarPer100g = parsed.Sugar,
            EstimatedAddedSugarPer100g = parsed.AddedSugar,
            EstimatedProteinPer100g = parsed.Protein,
            EstimatedFatPer100g = parsed.Fat,
            EstimatedSaturatedFatPer100g = parsed.SaturatedFat,
            EstimatedFiberPer100g = parsed.Fiber,
            EstimatedSodiumPer100g = parsed.Sodium,
            NutritionUnit = parsed.Unit,
            Basis = parsed.Unit == "ml" ? "100 ml (produto preparado)" : "100 g"
        };

        context.NutritionColumnUsed = parsed.Unit == "ml" ? ColumnType.Per100Ml.ToString() : ColumnType.Per100g.ToString();

        _logger.LogInformation("Parsed nutrient: Protein={value}", parsed.Protein?.ToString() ?? "null");

        _logger.LogInformation("Unit detected: {unit}", context.FinalNutritionProfile.NutritionUnit ?? "null");
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 2c: Validação Híbrida OCR (Computer Vision para validar OpenAI)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Valida os valores extraídos pelo Azure OpenAI Vision usando Computer Vision OCR.
    /// Se detectar divergências > 15%, usa os valores do Computer Vision (mais precisos).
    /// 
    /// Fluxo:
    /// 1. Azure OpenAI Vision (contexto + IA) → valores já extraídos
    /// 2. Azure Computer Vision OCR (OCR preciso) → validação
    /// 3. Comparar e corrigir se divergência > 15%
    /// </summary>
    private async Task Stage2c_HybridOcrValidation(NutritionAnalysisContext context, byte[] imageData)
    {
        if (context.FinalNutritionProfile == null)
        {
            _logger.LogInformation("[Pipeline.Stage2c] Skipping hybrid OCR validation - no nutrition profile to validate");
            return;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"ocr_validation_{Guid.NewGuid()}.jpg");
        try
        {
            _logger.LogInformation("[Pipeline.Stage2c] ┌──────────────────────────────────────────┐");
            _logger.LogInformation("[Pipeline.Stage2c] │  VALIDAÇÃO HÍBRIDA OCR (Azure Vision)   │");
            _logger.LogInformation("[Pipeline.Stage2c] └──────────────────────────────────────────┘");

            // Log valores ANTES da validação
            _logger.LogInformation("[Pipeline.Stage2c] 📊 Valores OpenAI (ANTES da validação):");
            _logger.LogInformation("[Pipeline.Stage2c]    • Calorias: {Cal} kcal", 
                context.FinalNutritionProfile.CaloriesPer100g ?? context.FinalNutritionProfile.CaloriesPer100ml);
            _logger.LogInformation("[Pipeline.Stage2c]    • Proteína: {Prot} g", 
                context.FinalNutritionProfile.EstimatedProteinPer100g);
            _logger.LogInformation("[Pipeline.Stage2c]    • Gordura: {Fat} g", 
                context.FinalNutritionProfile.EstimatedFatPer100g);
            _logger.LogInformation("[Pipeline.Stage2c]    • Carboidratos: {Carbs} g", 
                context.FinalNutritionProfile.EstimatedCarbsPer100g);
            _logger.LogInformation("[Pipeline.Stage2c]    • Sódio: {Sodium} mg", 
                context.FinalNutritionProfile.EstimatedSodiumPer100g);

            await File.WriteAllBytesAsync(tempPath, imageData);

            var warnings = context.Warnings ?? new List<string>();

            // Executar validação híbrida usando Computer Vision OCR
            var correctionApplied = await _hybridOcrValidator.ValidateAndCorrectAsync(
                context.FinalNutritionProfile,
                tempPath,
                warnings);

            context.Warnings = warnings;
            context.HybridOcrValidationApplied = true;
            context.HybridOcrCorrectionApplied = correctionApplied;
            context.HybridOcrValidationMethod = "Azure Computer Vision OCR";

            if (correctionApplied)
            {
                _logger.LogInformation("[Pipeline.Stage2c] ✅ Correções aplicadas via Computer Vision OCR");
                _logger.LogInformation("[Pipeline.Stage2c] 📊 Valores APÓS validação OCR:");
                _logger.LogInformation("[Pipeline.Stage2c]    • Calorias: {Cal} kcal", 
                    context.FinalNutritionProfile.CaloriesPer100g ?? context.FinalNutritionProfile.CaloriesPer100ml);
                _logger.LogInformation("[Pipeline.Stage2c]    • Proteína: {Prot} g", 
                    context.FinalNutritionProfile.EstimatedProteinPer100g);
                _logger.LogInformation("[Pipeline.Stage2c]    • Gordura: {Fat} g", 
                    context.FinalNutritionProfile.EstimatedFatPer100g);
                _logger.LogInformation("[Pipeline.Stage2c]    • Carboidratos: {Carbs} g", 
                    context.FinalNutritionProfile.EstimatedCarbsPer100g);
                _logger.LogInformation("[Pipeline.Stage2c]    • Sódio: {Sodium} mg", 
                    context.FinalNutritionProfile.EstimatedSodiumPer100g);
            }
            else
            {
                _logger.LogInformation("[Pipeline.Stage2c] ✅ Validação OK - valores consistentes entre OpenAI Vision e Computer Vision");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Pipeline.Stage2c] ⚠️ Erro durante validação híbrida OCR, mantendo valores originais");
            context.HybridOcrValidationApplied = false;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch { /* best effort */ }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 2d: Validação de Dados Mínimos (Segurança do Negócio)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Valida se há tabela nutricional e dados mínimos suficientes para análise.
    /// Esta é uma camada crítica de segurança do negócio que garante que só 
    /// retornamos análises quando há dados reais da tabela nutricional.
    /// 
    /// Critérios de validação:
    /// 1. Tabela nutricional detectada na imagem
    /// 2. Pelo menos 3 dos 5 valores críticos presentes:
    ///    - Calorias
    ///    - Proteínas
    ///    - Gorduras
    ///    - Carboidratos
    ///    - Sódio
    /// 
    /// Flags definidas:
    /// - HasNutritionTable: true/false
    /// - HasMinimumNutritionData: true/false
    /// - NutritionDataQuality: "full", "partial", "category_only", "insufficient"
    /// </summary>
    private void Stage2d_ValidateMinimumNutritionData(NutritionAnalysisContext context)
    {
        _logger.LogInformation("[Pipeline.Stage2d] ┌──────────────────────────────────────────┐");
        _logger.LogInformation("[Pipeline.Stage2d] │  VALIDAÇÃO DE DADOS MÍNIMOS (SEGURANÇA) │");
        _logger.LogInformation("[Pipeline.Stage2d] └──────────────────────────────────────────┘");

        var profile = context.FinalNutritionProfile;
        var visionResult = context.VisionResult;

        // 🔥 CRÍTICO: Se está em modo OCR puro e já extraiu valores, NÃO SOBRESCREVER hasNutritionTable
        bool isOcrModeConfirmed = context.NutritionFlags.Any(f => f == "OCR_MODE:nutrition_table_confirmed");

        if (isOcrModeConfirmed)
        {
            _logger.LogInformation("[Pipeline.Stage2d] ✅ MODO OCR CONFIRMADO - hasNutritionTable=true (não será sobrescrito)");

            // 🔥 CRÍTICO: SEMPRE marcar tabela como detectada em modo OCR
            context.Evidence.HasVisibleNutritionTable = true;
            context.Evidence.HasReliableNumericExtraction = true;

            // Já temos hasNutritionTable=true setado antes, apenas validar dados
            var ocrCriticalValuesCount = CountCriticalValues(profile);

            _logger.LogInformation("[Pipeline.Stage2d] 📊 Análise de dados:");
            _logger.LogInformation("[Pipeline.Stage2d]    • Tabela detectada: ✅ SIM (OCR mode)");
            _logger.LogInformation("[Pipeline.Stage2d]    • Valores críticos: {Count}/5", ocrCriticalValuesCount);

            // Atualizar flags de qualidade
            var ocrDataQuality = ocrCriticalValuesCount >= 4 ? "full" : 
                                ocrCriticalValuesCount >= 3 ? "partial" : "insufficient";

            // 🔥 NOVO: Se tiver dados insuficientes, NÃO retornar erro
            // OCR extraiu ALGO, então tem tabela, só precisa de mais dados
            if (ocrCriticalValuesCount >= 2)
            {
                context.HasReliableNutritionData = true;
                _logger.LogInformation("[Pipeline.Stage2d] ✅ Dados suficientes para análise ({Count}/5)", ocrCriticalValuesCount);
            }
            else
            {
                context.HasReliableNutritionData = false;
                _logger.LogWarning("[Pipeline.Stage2d] ⚠️ Dados insuficientes, mas tabela detectada ({Count}/5)", ocrCriticalValuesCount);
            }

            context.NutritionFlags.Add($"DataQuality:{ocrDataQuality}");
            context.NutritionFlags.Add($"NutritionTable:detected");
            context.NutritionFlags.Add($"CriticalValues:{ocrCriticalValuesCount}/5");

            _logger.LogInformation("[Pipeline.Stage2d] 🎯 Resultado Final:");
            _logger.LogInformation("[Pipeline.Stage2d]    • hasNutritionTable: ✅ TRUE (OCR mode - FORÇADO)");
            _logger.LogInformation("[Pipeline.Stage2d]    • hasMinimumData: {HasMinimum}", context.HasReliableNutritionData);
            _logger.LogInformation("[Pipeline.Stage2d]    • dataQuality: {Quality}", ocrDataQuality);
            _logger.LogInformation("[Pipeline.Stage2d] └──────────────────────────────────────────┘");
            return;
        }

        // ═══════════════════════════════════════════════════════════════════
        // MODO NORMAL (com OpenAI Vision)
        // ═══════════════════════════════════════════════════════════════════

        // 1. Verificar se há indicação de tabela nutricional
        // PRIORIDADE 1: Texto extraído (mais confiável)
        // PRIORIDADE 2: Valores nutricionais extraídos
        // PRIORIDADE 3: ProbableCaptureType do OpenAI
        bool hasNutritionTable = false;

        if (visionResult != null && visionResult.RawExtractedText != null && visionResult.RawExtractedText.Any())
        {
            var rawText = string.Join(" ", visionResult.RawExtractedText).ToLowerInvariant();

            // Detectar indicadores FORTES de tabela nutricional
            hasNutritionTable = rawText.Contains("informação nutricional") ||
                               rawText.Contains("informacao nutricional") ||
                               rawText.Contains("tabela nutricional") ||
                               rawText.Contains("valor energético") ||
                               rawText.Contains("valor energetico") ||
                               rawText.Contains("carboidrato") ||
                               rawText.Contains("gordura") && rawText.Contains("proteína") && (rawText.Contains("100g") || rawText.Contains("100 g") || rawText.Contains("100ml") || rawText.Contains("100 ml"));

            _logger.LogInformation("[Pipeline.Stage2d] Detecção por texto extraído: {HasTable} (contém palavras-chave)", hasNutritionTable ? "✅ TABELA DETECTADA" : "❌ Não detectada");
        }

        // 🔧 NOVO: Se não detectou por texto, mas extraiu valores nutricionais, considerar como tabela detectada
        if (!hasNutritionTable && profile != null)
        {
            var hasNutrientValues = (profile.CaloriesPer100g ?? profile.CaloriesPer100ml).HasValue ||
                                   profile.EstimatedProteinPer100g.HasValue ||
                                   profile.EstimatedCarbsPer100g.HasValue ||
                                   profile.EstimatedFatPer100g.HasValue ||
                                   profile.EstimatedSodiumPer100g.HasValue;

            if (hasNutrientValues)
            {
                hasNutritionTable = true;
                _logger.LogInformation("[Pipeline.Stage2d] Detecção por valores extraídos: ✅ TABELA INFERIDA (valores presentes)");
            }
        }

        // Fallback: verificar ProbableCaptureType (menos confiável)
        if (!hasNutritionTable && visionResult != null)
        {
            hasNutritionTable = visionResult.ProbableCaptureType == CaptureType.NutritionTable;
            if (hasNutritionTable)
            {
                _logger.LogInformation("[Pipeline.Stage2d] Detecção por CaptureType: ✅ NutritionTable");
            }
        }

        context.Evidence.HasVisibleNutritionTable = hasNutritionTable;

        // 2. Contar quantos valores críticos estão presentes
        int criticalValuesCount = CountCriticalValues(profile);

        _logger.LogInformation("[Pipeline.Stage2d] 📊 Análise de dados:");
        _logger.LogInformation("[Pipeline.Stage2d]    • Tabela detectada: {HasTable}", hasNutritionTable ? "✅ SIM" : "❌ NÃO");
        _logger.LogInformation("[Pipeline.Stage2d]    • Valores críticos: {Count}/5", criticalValuesCount);

        // 3. Determinar qualidade dos dados
        bool hasMinimumData = criticalValuesCount >= 3 && hasNutritionTable;
        string dataQuality;

        if (hasNutritionTable && criticalValuesCount >= 4)
        {
            dataQuality = "full";
            _logger.LogInformation("[Pipeline.Stage2d] ✅ QUALIDADE: COMPLETA - Tabela nutricional com dados suficientes");
        }
        else if (hasNutritionTable && criticalValuesCount >= 3)
        {
            dataQuality = "partial";
            _logger.LogInformation("[Pipeline.Stage2d] ⚠️ QUALIDADE: PARCIAL - Tabela detectada mas alguns dados faltando");
        }
        else if (!hasNutritionTable && criticalValuesCount >= 2)
        {
            dataQuality = "category_only";
            _logger.LogInformation("[Pipeline.Stage2d] ⚠️ QUALIDADE: APENAS CATEGORIA - Sem tabela, usando estimativas");
        }
        else
        {
            dataQuality = "insufficient";
            _logger.LogWarning("[Pipeline.Stage2d] ❌ QUALIDADE: INSUFICIENTE - Dados inadequados para análise confiável");

            // Adicionar warning ao contexto
            if (context.Warnings == null) context.Warnings = new List<string>();
            context.Warnings.Add("⚠️ ATENÇÃO: Tabela nutricional não detectada ou dados insuficientes. Análise pode não ser confiável.");
        }

        // 4. Atualizar contexto com flags de segurança
        context.Evidence.HasReliableNumericExtraction = hasMinimumData;
        context.HasReliableNutritionData = hasMinimumData;
        context.NutritionFlags.Add($"DataQuality:{dataQuality}");
        context.NutritionFlags.Add($"NutritionTable:{(hasNutritionTable ? "detected" : "not_detected")}");
        context.NutritionFlags.Add($"CriticalValues:{criticalValuesCount}/5");

        _logger.LogInformation("[Pipeline.Stage2d] 🎯 Resultado Final:");
        _logger.LogInformation("[Pipeline.Stage2d]    • hasNutritionTable: {HasTable}", hasNutritionTable);
        _logger.LogInformation("[Pipeline.Stage2d]    • hasMinimumData: {HasMinimum}", hasMinimumData);
        _logger.LogInformation("[Pipeline.Stage2d]    • dataQuality: {Quality}", dataQuality);
        _logger.LogInformation("[Pipeline.Stage2d] └──────────────────────────────────────────┘");
    }

    /// <summary>
    /// Conta valores críticos extraídos (helper para Stage2d)
    /// </summary>
    private static int CountCriticalValues(EstimatedNutritionProfileDto? profile)
    {
        if (profile == null) return 0;

        var criticalValues = new[]
        {
            (profile.CaloriesPer100g ?? profile.CaloriesPer100ml).HasValue,
            profile.EstimatedProteinPer100g.HasValue && profile.EstimatedProteinPer100g >= 0,
            profile.EstimatedFatPer100g.HasValue && profile.EstimatedFatPer100g >= 0,
            profile.EstimatedCarbsPer100g.HasValue && profile.EstimatedCarbsPer100g >= 0,
            profile.EstimatedSodiumPer100g.HasValue && profile.EstimatedSodiumPer100g >= 0
        };

        return criticalValues.Count(v => v);
    }




    private static void Stage2_ExtractRaw(NutritionAnalysisContext context, VisualInterpretationResult visionResult)
    {
        context.ProductName = visionResult.ProductName ?? visionResult.ProbableProductName;
        context.Brand = visionResult.Brand ?? visionResult.ProbableBrand;
        context.CategoryRaw = visionResult.Category ?? visionResult.ProbableCategory;
        context.PackageWeight = visionResult.PackageWeight ?? visionResult.ProbablePackageWeight;
        context.VisibleClaims = visionResult.VisibleClaims ?? [];
        context.ExtractedNutrition = visionResult.EstimatedNutritionProfile;
        context.HealthProfiles = visionResult.Classification ?? new ProductClassificationDto();
        context.ConfidenceDetails = visionResult.ConfidenceDetails ?? new ConfidenceDetailsDto();

        context.PublicAnalysisMode = visionResult.ProbableCaptureType == CaptureType.NutritionTable
            ? AnalysisMode.FullNutritionLabel
            : AnalysisMode.FrontOfPackageOnly;
        context.AnalysisMode = visionResult.ProbableCaptureType == CaptureType.NutritionTable
            ? NutritionDecisionMode.FullNutritionLabel
            : NutritionDecisionMode.FrontOfPackageOnly;
        context.FinalNutritionProfile = context.ExtractedNutrition == null
            ? new EstimatedNutritionProfileDto { Basis = "Dados não extraídos da tabela nutricional." }
            : new EstimatedNutritionProfileDto
            {
                CaloriesPer100g = context.ExtractedNutrition.CaloriesPer100g,
                CaloriesPer100ml = context.ExtractedNutrition.CaloriesPer100ml,
                EstimatedPackageCalories = context.ExtractedNutrition.EstimatedPackageCalories,
                EstimatedCarbsPer100g = context.ExtractedNutrition.EstimatedCarbsPer100g,
                EstimatedSugarPer100g = context.ExtractedNutrition.EstimatedSugarPer100g,
                EstimatedAddedSugarPer100g = context.ExtractedNutrition.EstimatedAddedSugarPer100g,
                EstimatedSaturatedFatPer100g = context.ExtractedNutrition.EstimatedSaturatedFatPer100g,
                EstimatedProteinPer100g = context.ExtractedNutrition.EstimatedProteinPer100g,
                EstimatedSodiumPer100g = context.ExtractedNutrition.EstimatedSodiumPer100g,
                EstimatedFiberPer100g = context.ExtractedNutrition.EstimatedFiberPer100g,
                EstimatedFatPer100g = context.ExtractedNutrition.EstimatedFatPer100g,
                Basis = context.ExtractedNutrition.Basis,
                ParserConfidence = context.ExtractedNutrition.ParserConfidence,
                NutritionUnit = context.ExtractedNutrition.NutritionUnit,
                IsCorrectedByOcr = context.ExtractedNutrition.IsCorrectedByOcr,
                DataSource = context.ExtractedNutrition.DataSource != null
                    ? new Dictionary<string, string>(context.ExtractedNutrition.DataSource, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 3: Normalize (sem inventar valores)
    // ═══════════════════════════════════════════════════════════════════

    private void Stage3_Normalize(NutritionAnalysisContext context)
    {
        if (context.FinalNutritionProfile == null)
            context.FinalNutritionProfile = new EstimatedNutritionProfileDto();

        var profile = context.FinalNutritionProfile;

        profile.CaloriesPer100g = NormalizeRange(profile.CaloriesPer100g, 0, 900, context, "Calorias fora da faixa válida (0–900).");
        profile.CaloriesPer100ml = NormalizeRange(profile.CaloriesPer100ml, 0, 900, context, "Calorias fora da faixa válida (0–900) para 100 ml.");
        profile.EstimatedSugarPer100g = NormalizeRange(profile.EstimatedSugarPer100g, 0, 100, context, "Açúcar fora da faixa válida (0–100).");
        profile.EstimatedFatPer100g = NormalizeRange(profile.EstimatedFatPer100g, 0, 100, context, "Gordura fora da faixa válida (0–100).");
        profile.EstimatedSodiumPer100g = NormalizeRange(profile.EstimatedSodiumPer100g, 0, 5000, context, "Sódio fora da faixa válida (0–5000).", false);
        profile.EstimatedProteinPer100g = NormalizeRange(profile.EstimatedProteinPer100g, 0, 100, context, "Proteína fora da faixa válida (0–100).");
        profile.EstimatedCarbsPer100g = NormalizeRange(profile.EstimatedCarbsPer100g, 0, 100, context, "Carboidratos fora da faixa válida (0–100).");
        profile.EstimatedFiberPer100g = NormalizeRange(profile.EstimatedFiberPer100g, 0, 100, context, "Fibras fora da faixa válida (0–100).");
    }


    // ═══════════════════════════════════════════════════════════════════
    // STAGE 4: Detect DataSource
    // ═══════════════════════════════════════════════════════════════════

    private static void Stage4_DetectDataSource(NutritionAnalysisContext context)
    {
        var validNutrients = CountValidCoreNutrients(context.FinalNutritionProfile);

        context.NutritionDataSource = validNutrients switch
        {
            >= 4 => DataSource.Real,
            >= 1 => DataSource.Partial,
            _ => DataSource.Inferred
        };

        context.HasReliableNutritionData = context.NutritionDataSource is DataSource.Real or DataSource.Partial;
        context.FallbackType = context.NutritionDataSource switch
        {
            DataSource.Real => "real",
            DataSource.Partial => "partial",
            DataSource.Fallback => "category_based",
            _ => "inferred"
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 5: Validation (caloria + ranges)
    // ═══════════════════════════════════════════════════════════════════

    private void Stage5_Validate(NutritionAnalysisContext context)
    {
        var profile = context.FinalNutritionProfile;
        if (profile == null) return;

        var hasMacroSet = profile.EstimatedProteinPer100g.HasValue
            && profile.EstimatedCarbsPer100g.HasValue
            && profile.EstimatedFatPer100g.HasValue
            && profile.CaloriesPer100g.HasValue;

        if (hasMacroSet)
        {
            var expectedCalories = profile.EstimatedProteinPer100g!.Value * 4
                + profile.EstimatedCarbsPer100g!.Value * 4
                + profile.EstimatedFatPer100g!.Value * 9;

            if (expectedCalories > 0)
            {
                var delta = Math.Abs(profile.CaloriesPer100g!.Value - expectedCalories) / expectedCalories;
                if (delta > 0.30)
                {
                    // 🔧 AUTO-CORREÇÃO: Inferir carboidratos corretos a partir das calorias
                    var inferredCarbs = (profile.CaloriesPer100g!.Value - 
                                        (profile.EstimatedProteinPer100g!.Value * 4) - 
                                        (profile.EstimatedFatPer100g!.Value * 9)) / 4;

                    if (inferredCarbs > 0 && inferredCarbs <= 100)
                    {
                        _logger.LogWarning(
                            "[Pipeline.Stage5] ✅ Carboidratos corrigidos automaticamente: {Old}g → {New}g (inferido por calorias)",
                            profile.EstimatedCarbsPer100g!.Value, Math.Round(inferredCarbs, 1));

                        AddDistinct(context.Warnings, 
                            $"Carboidratos corrigidos de {profile.EstimatedCarbsPer100g!.Value:F1}g para {inferredCarbs:F1}g (inferido por validação calórica).");

                        profile.EstimatedCarbsPer100g = Math.Round(inferredCarbs, 1);

                        // Recalcular delta após correção
                        expectedCalories = profile.EstimatedProteinPer100g.Value * 4
                            + profile.EstimatedCarbsPer100g.Value * 4
                            + profile.EstimatedFatPer100g.Value * 9;
                        delta = Math.Abs(profile.CaloriesPer100g.Value - expectedCalories) / expectedCalories;
                    }

                    // Se ainda inconsistente após correção, marcar como warning
                    if (delta > 0.30)
                    {
                        context.IsInconsistent = true;
                        AddDistinct(context.ConsistencyIssues, "Inconsistência calórica detectada (>30%).");
                        _logger.LogWarning("[Pipeline.Stage5] Inconsistência calórica detectada. Delta={Delta:P1}", delta);
                    }
                }
            }
        }

        ValidateCriticalRange(profile.EstimatedSugarPer100g, 0, 100, context, "açúcar");
        ValidateCriticalRange(profile.EstimatedFatPer100g, 0, 100, context, "gordura");
        ValidateCriticalRange(profile.EstimatedSodiumPer100g, 0, 5000, context, "sódio", false);
    }

    
    // ═══════════════════════════════════════════════════════════════════
    // STAGE 7: Fallback controlado
    // ═══════════════════════════════════════════════════════════════════

    private void Stage6_DetectServingModel(NutritionAnalysisContext context)
    {
        var unit = (context.FinalNutritionProfile?.NutritionUnit ?? string.Empty).ToLowerInvariant();
        var basis = (context.FinalNutritionProfile?.Basis ?? "").ToLower();

        if (unit == "ml" || basis.Contains("100 ml"))
        {
            context.ServingModel = NutritionServingModel.Per100ml;
        }
        else if (unit == "g" || basis.Contains("100 g"))
        {
            context.ServingModel = NutritionServingModel.Per100g;
        }
        else if (basis.Contains("porção") || basis.Contains("porcao"))
        {
            context.ServingModel = NutritionServingModel.PerPortion;
        }
        else
        {
            context.ServingModel = NutritionServingModel.Unknown;
        }

        _logger.LogInformation("[ServingModel] {Model}", context.ServingModel);
    }

    private async Task Stage9_ApplyFallbackIfNeeded(NutritionAnalysisContext context)
    {
        if (context.IsNutritionLocked)
            return;

        if (context.NutritionDataSource == DataSource.Real)
            return;

        // 🔥 REGRA CRÍTICA: Se o OCR extraiu QUALQUER dado real, NUNCA substituir —
        // apenas preencher campos ausentes. OCR é sempre a fonte primária.
        bool hasOcrData = context.NutritionDataSource == DataSource.Partial
            || context.NutritionFlags.Any(f => f.StartsWith("OCR_MODE:", StringComparison.OrdinalIgnoreCase));

        var resolvedCategory = ResolveCategory(context.CategoryRaw);
        var fallback = await TryGetCategoryFallbackProfile(context, resolvedCategory);

        if (fallback == null)
            return;

        if (hasOcrData && context.FinalNutritionProfile != null)
        {
            // Tag existing OCR fields before merge so provenance is preserved
            TagOcrFieldSources(context.FinalNutritionProfile);

            // Fill only absent fields — OCR values are never overridden
            var mergedFields = MergeMissingValues(context.FinalNutritionProfile, fallback);

            if (mergedFields.Count > 0)
            {
                context.FallbackType = "hybrid_ocr_category";

                if (!string.IsNullOrWhiteSpace(context.FinalNutritionProfile.Basis)
                    && !context.FinalNutritionProfile.Basis.Contains("fallback", StringComparison.OrdinalIgnoreCase))
                {
                    context.FinalNutritionProfile.Basis += " + fallback por categoria (campos ausentes)";
                }

                context.FinalNutritionProfile.IsCorrectedByOcr = true;
                AddDistinct(context.NutritionFlags, "DataSource:hybrid");
                AddDistinct(context.Warnings,
                    "Dados parcialmente complementados por referência de categoria (campos não extraídos pelo OCR).");

                _logger.LogInformation("[Fallback] Campos ausentes preenchidos pelo fallback — dados OCR preservados");
            }
            else
            {
                // All fields already populated by OCR — fallback was not needed
                context.FallbackType = "real";
                _logger.LogInformation("[Fallback] Todos os campos OCR presentes — fallback não aplicado");
            }
        }
        else
        {
            // Sem dados OCR: substituição completa por fallback de categoria
            context.FinalNutritionProfile = fallback;
            context.NutritionDataSource = DataSource.Fallback;
            context.FallbackType = "category_based";
            context.FinalNutritionProfile.IsCorrectedByOcr = false;
            AddDistinct(context.NutritionFlags, "DataSource:fallback");
            AddDistinct(context.Warnings, "Fallback nutricional aplicado por categoria (sem dados OCR disponíveis).");

            _logger.LogInformation("[Fallback] Substituição completa por categoria (sem dados OCR)");
        }
    }


    // ═══════════════════════════════════════════════════════════════════
    // STAGE 8: Category Engine
    // ═══════════════════════════════════════════════════════════════════

    private async Task Stage8_CategoryEngine(NutritionAnalysisContext context)
    {
        var resolvedCategory = ResolveCategory(context.CategoryRaw);

        // 🔧 NOVO: Inferir categoria baseado em perfil nutricional quando categoria genérica
        string? inferredCategory = null;
        if (IsGenericCategory(resolvedCategory) && context.FinalNutritionProfile != null)
        {
            inferredCategory = InferCategoryFromNutritionProfile(context.FinalNutritionProfile);
            if (!string.IsNullOrWhiteSpace(inferredCategory))
            {
                _logger.LogInformation(
                    "[Pipeline.Stage8] 🎯 Categoria inferida por perfil nutricional: '{Old}' → '{New}'",
                    resolvedCategory, inferredCategory);
                _logger.LogInformation(
                    "[Pipeline.Stage8] 📊 Perfil usado: Cal={Cal}, Carbs={Carbs}, Fat={Fat}, SatFat={SatFat}",
                    context.FinalNutritionProfile.CaloriesPer100g,
                    context.FinalNutritionProfile.EstimatedCarbsPer100g,
                    context.FinalNutritionProfile.EstimatedFatPer100g,
                    context.FinalNutritionProfile.EstimatedSaturatedFatPer100g);

                resolvedCategory = inferredCategory;
                context.CategoryRaw = inferredCategory;

                // 🔥 CRÍTICO: Marcar categoria como inferida para proteger contra sobrescrita
                context.NutritionFlags.Add($"CATEGORY_INFERRED:{inferredCategory}");
            }
            else
            {
                _logger.LogWarning("[Pipeline.Stage8] ⚠️ Não conseguiu inferir categoria do perfil nutricional");
            }
        }

        context.CategoryNormalized = resolvedCategory;

        // 🔧 CRÍTICO: Categoria inferida tem PRIORIDADE ABSOLUTA sobre normalização externa
        bool hasInferredCategory = !string.IsNullOrWhiteSpace(inferredCategory);

        try
        {
            var normalization = await _categoryNormalization.NormalizeAsync(
                resolvedCategory, context.ProductName, context.VisibleClaims, context.Brand);

            context.CategoryNormalization = normalization;

            // Se temos categoria inferida, só usar normalização se for MUITO mais específica
            if (hasInferredCategory)
            {
                _logger.LogInformation(
                    "[Pipeline.Stage8] Categoria normalização retornou: '{Normalized}' (inferida: '{Inferred}')",
                    normalization.NormalizedCategoryName, inferredCategory);

                // 🔥 REGRA RIGOROSA: Só aceitar normalização se for COMPROVADAMENTE melhor
                bool shouldUseNormalization = !string.IsNullOrWhiteSpace(normalization.NormalizedCategoryName) &&
                    !IsGenericCategory(normalization.NormalizedCategoryName) &&
                    normalization.NormalizedCategoryName.Length > (inferredCategory!.Length + 5) && // +5 chars mínimo
                    !normalization.NormalizedCategoryName.Contains("feij", StringComparison.OrdinalIgnoreCase); // ❌ NUNCA aceitar "feijão"

                if (shouldUseNormalization)
                {
                    // Normalização é SIGNIFICATIVAMENTE mais específica, usar ela
                    context.CategoryNormalized = normalization.NormalizedCategoryName;
                    _logger.LogInformation("[Pipeline.Stage8] ✅ Usando categoria normalizada (comprovadamente mais específica)");
                }
                else
                {
                    // 🔥 MANTER CATEGORIA INFERIDA (mais confiável)
                    context.CategoryNormalized = inferredCategory;
                    _logger.LogInformation("[Pipeline.Stage8] ✅ MANTENDO CATEGORIA INFERIDA (prioridade absoluta)");
                    _logger.LogInformation("[Pipeline.Stage8] ⚠️ Normalização rejeitada: '{Normalized}' (genérica ou feijão)", 
                        normalization.NormalizedCategoryName);
                }
            }
            else
            {
                // Sem inferência, usar normalização normalmente
                context.CategoryNormalized = normalization.NormalizedCategoryName;
            }

            context.CategoryNormalizedCode = normalization.NormalizedCategoryCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Pipeline.Stage8] Category normalization failed.");
            // Manter categoria inferida em caso de erro
            if (hasInferredCategory)
            {
                context.CategoryNormalized = inferredCategory;
                _logger.LogInformation("[Pipeline.Stage8] ✅ Mantendo categoria inferida após erro de normalização");
            }
        }

        context.CategoryDecision = _categoryDecision.Decide(context);
        context.ProcessingLevel = context.CategoryDecision.ProcessingLevel;
        context.IsUltraProcessed = context.CategoryDecision.IsUltraProcessed;

        // 🔥 CRÍTICO: SEMPRE inferir ProcessingLevel baseado em perfil nutricional (GENÉRICO)
        // Funciona para QUALQUER tipo de produto, não apenas categorias conhecidas
        if (context.FinalNutritionProfile != null)
        {
            var categoryForInference = hasInferredCategory 
                ? inferredCategory! 
                : (context.CategoryNormalized ?? context.CategoryRaw);

            var inferredProcessingLevel = InferProcessingLevelFromNutrition(
                context.FinalNutritionProfile, 
                categoryForInference);

            if (!string.IsNullOrWhiteSpace(inferredProcessingLevel))
            {
                // 🔥 REGRA: Inferência nutricional tem PRIORIDADE sobre CategoryDecision
                // Só sobrescrever se inferência for "pior" (mais processado)
                var needsOverride = ShouldOverrideProcessingLevel(
                    context.ProcessingLevel, 
                    inferredProcessingLevel);

                if (needsOverride)
                {
                    _logger.LogInformation(
                        "[Pipeline.Stage8] 🏭 ProcessingLevel SOBRESCRITO por perfil nutricional: '{Old}' → '{New}'",
                        context.ProcessingLevel, inferredProcessingLevel);
                    context.ProcessingLevel = inferredProcessingLevel;
                    context.IsUltraProcessed = inferredProcessingLevel == "ultraprocessado";
                }
                else
                {
                    _logger.LogInformation(
                        "[Pipeline.Stage8] ✅ ProcessingLevel mantido: '{Current}' (inferido: '{Inferred}')",
                        context.ProcessingLevel, inferredProcessingLevel);
                }
            }
        }

        // 🔥 SAFETY NET: Categorias OBVIAMENTE ultraprocessadas (última camada de segurança)
        var categoryNorm = Norm(context.CategoryNormalized);
        if (categoryNorm.Contains("biscoito") || categoryNorm.Contains("wafer") ||
            categoryNorm.Contains("chocolate") || categoryNorm.Contains("salgadinho") ||
            categoryNorm.Contains("refrigerante") || categoryNorm.Contains("embutido"))
        {
            if (context.ProcessingLevel != "ultraprocessado")
            {
                _logger.LogWarning(
                    "[Pipeline.Stage8] 🔥 SAFETY NET: Forçando ultraprocessado para categoria óbvia '{Category}' (era '{Old}')",
                    context.CategoryNormalized, context.ProcessingLevel);
                context.ProcessingLevel = "ultraprocessado";
                context.IsUltraProcessed = true;
            }
        }

        _logger.LogInformation(
            "[Pipeline.Stage8] 📋 Categoria FINAL: '{Category}' | ProcessingLevel: '{Level}' | Ultraprocessado: {Ultra}",
            context.CategoryNormalized, context.ProcessingLevel, context.IsUltraProcessed);
    }

    /// <summary>
    /// Verifica se categoria é genérica (ex: "Produto alimentício", "Feijão" sem contexto)
    /// </summary>
    private static bool IsGenericCategory(string category)
    {
        var norm = Norm(category);
        return norm == "produto alimenticio" || 
               norm == "alimento geral" || 
               norm == "feijao" ||  // Feijão sozinho é genérico demais
               norm == "produto" ||
               string.IsNullOrWhiteSpace(norm);
    }

    /// <summary>
    /// Infere categoria baseado em perfil nutricional característico
    /// </summary>
    private static string? InferCategoryFromNutritionProfile(EstimatedNutritionProfileDto profile)
    {
        var calories = profile.CaloriesPer100g ?? profile.CaloriesPer100ml ?? 0;
        var carbs = profile.EstimatedCarbsPer100g ?? 0;
        var protein = profile.EstimatedProteinPer100g ?? 0;
        var fat = profile.EstimatedFatPer100g ?? 0;
        var sugar = profile.EstimatedSugarPer100g ?? 0;
        var satFat = profile.EstimatedSaturatedFatPer100g ?? 0;

        // BISCOITO/WAFER: Calorias altas (450-550), gordura alta (20-35g), carbs altos (50-70g)
        if (calories >= 450 && calories <= 600 && 
            fat >= 15 && fat <= 40 && 
            carbs >= 45 && carbs <= 80 &&
            satFat >= 10)
        {
            return "Biscoito / Wafer";
        }

        // CHOCOLATE: Calorias muito altas (>500), gordura muito alta (>30g), açúcar alto
        if (calories > 500 && fat > 30 && sugar > 40)
        {
            return "Chocolate";
        }

        // CEREAL/GRANOLA: Calorias médio-altas (350-450), carbs altos (60-80g), fibra alta
        if (calories >= 350 && calories <= 480 && 
            carbs >= 55 && carbs <= 85 && 
            (profile.EstimatedFiberPer100g ?? 0) >= 5)
        {
            return "Cereal / Granola";
        }

        // SALGADINHO: Calorias altas (480-550), gordura alta (25-35g), sódio muito alto
        if (calories >= 480 && calories <= 560 && 
            fat >= 20 && fat <= 40 && 
            (profile.EstimatedSodiumPer100g ?? 0) >= 500)
        {
            return "Salgadinho";
        }

        // Não conseguiu inferir com certeza
        return null;
    }

    /// <summary>
    /// Infere ProcessingLevel baseado em perfil nutricional e categoria
    /// GENÉRICO - Funciona para QUALQUER tipo de produto
    /// </summary>
    private static string? InferProcessingLevelFromNutrition(
        EstimatedNutritionProfileDto profile, 
        string category)
    {
        var norm = Norm(category);

        // ULTRAPROCESSADOS ÓBVIOS por categoria (prioridade alta)
        if (norm.Contains("biscoito") || norm.Contains("wafer") || 
            norm.Contains("salgadinho") || norm.Contains("refrigerante") ||
            norm.Contains("chocolate") || norm.Contains("embutido") ||
            norm.Contains("suco industrializado") || norm.Contains("nectar"))
        {
            return "ultraprocessado";
        }

        // 🔥 NOVO: ULTRAPROCESSADO por perfil nutricional (GENÉRICO)
        var sugar = profile.EstimatedSugarPer100g ?? 0;
        var fat = profile.EstimatedFatPer100g ?? 0;
        var satFat = profile.EstimatedSaturatedFatPer100g ?? 0;
        var sodium = profile.EstimatedSodiumPer100g ?? 0;
        var calories = profile.CaloriesPer100g ?? profile.CaloriesPer100ml ?? 0;
        var fiber = profile.EstimatedFiberPer100g ?? 0;

        // ═══════════════════════════════════════════════════════════════════
        // REGRAS GENÉRICAS DE ULTRAPROCESSAMENTO (baseado em NOVA)
        // ═══════════════════════════════════════════════════════════════════

        // REGRA 1: Alta densidade calórica + açúcar alto + baixa fibra
        // Ex: Biscoitos, bolos, barras de cereal industrializadas
        if (calories > 400 && sugar > 15 && fiber < 3)
        {
            return "ultraprocessado";
        }

        // REGRA 2: Alta densidade calórica + gordura saturada alta + baixa fibra
        // Ex: Salgadinhos, batata chips, produtos fritos
        if (calories > 400 && satFat > 10 && fiber < 3)
        {
            return "ultraprocessado";
        }

        // REGRA 3: Sódio muito alto (indicador de aditivos)
        // Ex: Temperos prontos, caldos industrializados, embutidos
        if (sodium > 800)
        {
            return "ultraprocessado";
        }

        // REGRA 4: Bebidas com açúcar alto (sucos, refrigerantes, achocolatados)
        // Considera base 100ml
        var isLiquid = profile.NutritionUnit == "ml" || calories < 150;
        if (isLiquid && sugar > 10)
        {
            return "ultraprocessado";
        }

        // REGRA 5: Combinação perigosa - açúcar + sódio + baixa fibra
        // Ex: Produtos prontos para consumo
        if (sugar > 10 && sodium > 400 && fiber < 2)
        {
            return "ultraprocessado";
        }

        // ═══════════════════════════════════════════════════════════════════
        // PROCESSADO (intermediário)
        // ═══════════════════════════════════════════════════════════════════

        // REGRA 6: Densidade calórica moderada + açúcar ou sódio moderado
        if (calories > 250 && (sugar > 8 || sodium > 400))
        {
            return "processado";
        }

        // REGRA 7: Produtos com moderada adição de ingredientes
        if (sugar > 5 && sodium > 200)
        {
            return "processado";
        }

        // ═══════════════════════════════════════════════════════════════════
        // IN NATURA / MINIMAMENTE PROCESSADO
        // ═══════════════════════════════════════════════════════════════════

        // REGRA 8: Perfil muito limpo (arroz, feijão, grãos)
        if (calories < 200 && sugar < 3 && sodium < 100 && fiber > 3)
        {
            return "minimamente_processado";
        }

        // REGRA 9: Alimentos naturais com baixo processamento
        if (sugar < 2 && sodium < 50 && fiber > 2)
        {
            return "minimamente_processado";
        }

        return null; // Deixar decisão padrão do CategoryDecision
    }

    /// <summary>
    /// Decide se deve sobrescrever ProcessingLevel atual com o inferido
    /// Prioriza classificação mais restritiva (mais processado)
    /// </summary>
    private static bool ShouldOverrideProcessingLevel(string? current, string inferred)
    {
        // Ordem de severidade (pior → melhor)
        var severity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["ultraprocessado"] = 3,
            ["processado"] = 2,
            ["minimamente_processado"] = 1,
            ["in_natura"] = 0
        };

        if (string.IsNullOrWhiteSpace(current))
            return true; // Sem classificação atual, aceitar inferida

        var currentSeverity = severity.GetValueOrDefault(current, 1);
        var inferredSeverity = severity.GetValueOrDefault(inferred, 1);

        // Só sobrescrever se inferência for MAIS RESTRITIVA (pior)
        return inferredSeverity > currentSeverity;
    }

    private void FixLiquidBasisUsingRawText(NutritionAnalysisContext context)
    {
        var raw = context.VisionResult?.RawExtractedText;
        if (raw == null || raw.Count == 0) return;

        var profile = context.FinalNutritionProfile;
        if (profile == null) return;

        var columns = NutritionColumnParser.DetectColumns(raw);
        if (columns.Per100mlIndex.HasValue)
        {
            context.ProductForm = ProductForm.LiquidPrepared;

            if (!profile.CaloriesPer100ml.HasValue && profile.CaloriesPer100g.HasValue)
            {
                profile.CaloriesPer100ml = profile.CaloriesPer100g;
                profile.CaloriesPer100g = null;
            }

            profile.NutritionUnit = "ml";

            if (string.IsNullOrWhiteSpace(profile.Basis) || !profile.Basis.Contains("100 ml", StringComparison.OrdinalIgnoreCase))
            {
                profile.Basis = "100 ml (produto preparado)";
            }

            AddDistinct(context.Warnings,
                "Tabela nutricional interpretada como 100 ml (produto preparado), não por 100g.");

            if ((context.CategoryNormalized ?? "").Contains("achocolatado"))
            {
                context.CategoryNormalized = "bebida achocolatada";
            }
        }
    }
    private void DetectProductForm(NutritionAnalysisContext context)
    {
        var profile = context.FinalNutritionProfile;
        var category = Norm(context.CategoryNormalized ?? context.CategoryRaw);
        var basis = Norm(profile?.Basis);
        var calories = profile?.CaloriesPer100g ?? 0;

        if ((!string.IsNullOrWhiteSpace(basis) && (basis.Contains("100 ml") || basis.Contains("ml")))
            || (calories <= 120 && category.Contains("achocolatado")))
        {
            context.ProductForm = ProductForm.LiquidPrepared;
        }
        else if (category.Contains("pó") || category.Contains("po") || category.Contains("powder"))
        {
            context.ProductForm = ProductForm.Powder;
        }
        else if (category.Contains("barra") || category.Contains("biscoito") || category.Contains("iogurte") || category.Contains("alimento"))
        {
            context.ProductForm = ProductForm.Solid;
        }
        else
        {
            context.ProductForm = ProductForm.Unknown;
        }

        _logger.LogInformation("[Pipeline.ProductForm] ProductForm detectado: {Form}", context.ProductForm);
    }

    private static double? Multiply(double? value, double factor)
    {
        if (!value.HasValue) return null;
        return Math.Round(value.Value * factor, 2);
    }

    private double ExtractPortionWeight(NutritionAnalysisContext context)
    {
        var basis = context.FinalNutritionProfile?.Basis ?? "";

        var match = System.Text.RegularExpressions.Regex.Match(basis, @"(\d+)\s?g");

        if (match.Success && double.TryParse(match.Groups[1].Value, out var grams))
            return grams;

        return 20; // fallback padrão seguro
    }

    private void Stage8_LockNutritionData(NutritionAnalysisContext context)
    {
        bool isOcrConfirmed = context.NutritionFlags
            .Any(f => f.StartsWith("OCR_MODE:", StringComparison.OrdinalIgnoreCase));

        // Travar quando:
        // • Dados reais completos e sem inconsistência (comportamento original)
        // • OCR extraiu dados confirmados — mesmo parciais, não devem ser substituídos
        context.IsNutritionLocked =
            (context.NutritionDataSource == DataSource.Real && !context.IsInconsistent)
            || (isOcrConfirmed && context.NutritionDataSource == DataSource.Partial && !context.IsInconsistent);

        if (context.IsNutritionLocked)
            _logger.LogInformation("[Pipeline] Dados travados (source={Source}, ocrConfirmed={Ocr})",
                context.NutritionDataSource, isOcrConfirmed);
    }
    private void Stage7_NormalizeToCanonical100g(NutritionAnalysisContext context)
    {
        var profile = context.FinalNutritionProfile;
        if (profile == null) return;

        // 🔥 REGRA 1: NÃO NORMALIZAR LÍQUIDO PREPARADO
        if (context.ProductForm == ProductForm.LiquidPrepared)
        {
            context.NormalizationApplied = "kept_liquid_100ml";
            return;
        }

        // 🔥 REGRA 2: já está correto
        if (context.ServingModel == NutritionServingModel.Per100g)
        {
            context.NormalizationApplied = "already_100g";
            return;
        }

        // 🔥 REGRA 3: porção → 100g
        if (context.ServingModel == NutritionServingModel.PerPortion)
        {
            var portion = ExtractPortionWeight(context);

            if (portion > 0)
            {
                var factor = 100.0 / portion;

                profile.CaloriesPer100g = Multiply(profile.CaloriesPer100g, factor);
                profile.EstimatedSugarPer100g = Multiply(profile.EstimatedSugarPer100g, factor);
                profile.EstimatedProteinPer100g = Multiply(profile.EstimatedProteinPer100g, factor);
                profile.EstimatedFatPer100g = Multiply(profile.EstimatedFatPer100g, factor);
                profile.EstimatedSodiumPer100g = Multiply(profile.EstimatedSodiumPer100g, factor);
                profile.EstimatedCarbsPer100g = Multiply(profile.EstimatedCarbsPer100g, factor);
                profile.EstimatedFiberPer100g = Multiply(profile.EstimatedFiberPer100g, factor);

                context.NormalizationApplied = "portion_to_100g";

                _logger.LogInformation("[Normalize] Portion → 100g factor={Factor}", factor);
            }

            return;
        }

        // 🔥 REGRA 4: ml → g (default seguro)
        if (context.ServingModel == NutritionServingModel.Per100ml)
        {
            context.NormalizationApplied = "ml_assumed_equal_g";
            return;
        }
    }
    private void NormalizeCategoryByForm(NutritionAnalysisContext context)
    {
        var original = context.CategoryNormalized ?? context.CategoryRaw;
        var normalized = Norm(original);

        if (context.ProductForm == ProductForm.LiquidPrepared && normalized.Contains("achocolatado"))
        {
            context.CategoryNormalized = "bebida achocolatada";
        }
        else if (context.ProductForm == ProductForm.Powder && normalized.Contains("achocolatado"))
        {
            context.CategoryNormalized = "achocolatado em pó";
        }

        if (!string.Equals(original, context.CategoryNormalized, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "[Pipeline.ProductForm] Categoria normalizada por forma: '{OldCategory}' -> '{NewCategory}'",
                original ?? "n/a",
                context.CategoryNormalized ?? "n/a");
        }
    }

    private void AdjustNutritionByForm(NutritionAnalysisContext context)
    {
        var profile = context.FinalNutritionProfile;
        if (profile == null) return;

        switch (context.ProductForm)
        {
            case ProductForm.LiquidPrepared:
                AddDistinct(context.NutritionFlags, "liquid_prepared");
                if (context.IsUltraProcessed && context.ProcessingLevel.Equals("ultraprocessado", StringComparison.OrdinalIgnoreCase))
                {
                    context.IsUltraProcessed = false;
                    context.ProcessingLevel = "processado";
                }
                AddDistinct(context.Warnings, "Leitura interpretada como produto líquido preparado (base por ml). A densidade calórica tende a ser menor.");
                break;

            case ProductForm.Powder:
                AddDistinct(context.NutritionFlags, "powder_concentrated");
                AddDistinct(context.Warnings, "Leitura interpretada como produto em pó. Valores refletem produto concentrado.");
                break;

            case ProductForm.Solid:
                AddDistinct(context.NutritionFlags, "solid_standard");
                break;
        }
    }

    private void AdjustScoreByForm(NutritionAnalysisContext context)
    {
        var profile = context.FinalNutritionProfile;
        if (profile == null) return;

        var sugar = profile.EstimatedSugarPer100g ?? 0;
        if (sugar <= 0) return;

        var sugarPenaltyProxy = sugar switch
        {
            >= 15 => 25,
            >= 10 => 15,
            >= 5 => 8,
            _ => 0
        };

        if (sugarPenaltyProxy == 0) return;

        if (context.ProductForm == ProductForm.LiquidPrepared)
        {
            var boost = (int)Math.Round(sugarPenaltyProxy * 0.20);
            context.ScoreAdjusted += boost;
            _logger.LogInformation("[Pipeline.ProductForm] Ajuste de score aplicado (líquido): +{Delta}", boost);
        }
        else if (context.ProductForm == ProductForm.Powder)
        {
            var penalty = (int)Math.Round(sugarPenaltyProxy * 0.15);
            context.ScoreAdjusted -= penalty;
            _logger.LogInformation("[Pipeline.ProductForm] Ajuste de score aplicado (pó): -{Delta}", penalty);
        }
    }

    private void ValidateCrossByProductForm(NutritionAnalysisContext context)
    {
        var calories = context.FinalNutritionProfile?.CaloriesPer100g;
        if (!calories.HasValue) return;

        if (context.ProductForm == ProductForm.LiquidPrepared && calories.Value > 200)
        {
            context.IsInconsistent = true;
            AddDistinct(context.ConsistencyIssues, "Produto líquido preparado com calorias acima do esperado.");
            _logger.LogWarning("[Pipeline.ProductForm] Inconsistência detectada: LiquidPrepared com {Calories} kcal/100.", calories.Value);
        }

        if (context.ProductForm == ProductForm.Powder && calories.Value < 200)
        {
            context.IsInconsistent = true;
            AddDistinct(context.ConsistencyIssues, "Produto em pó com calorias abaixo do esperado.");
            _logger.LogWarning("[Pipeline.ProductForm] Inconsistência detectada: Powder com {Calories} kcal/100.", calories.Value);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 9: Enrichment
    // ═══════════════════════════════════════════════════════════════════

    // Prefixes of pipeline-operational flags that must survive the Stage9 enrichment clear.
    private static readonly string[] _pipelineFlagPrefixes =
    [
        "NutritionTable:", "DataQuality:", "CriticalValues:", "OCR_MODE:",
        "GPT4.1:", "CATEGORY_INFERRED:", "liquid_prepared", "powder_concentrated",
        "solid_standard", "DataSource:"
    ];

    private static void Stage9_Enrichment(NutritionAnalysisContext context)
    {
        var profile = context.FinalNutritionProfile;
        if (profile == null) return;

        // Preserve critical pipeline flags — clear only enrichment-level flags
        var preserved = context.NutritionFlags
            .Where(f => _pipelineFlagPrefixes.Any(p => f.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        context.NutritionFlags.Clear();
        context.PrincipalOffenders.Clear();

        foreach (var flag in preserved)
            context.NutritionFlags.Add(flag);

        if ((profile.EstimatedSugarPer100g ?? 0) >= 10)
        {
            AddDistinct(context.NutritionFlags, "high_sugar");
            AddDistinct(context.PrincipalOffenders, "açúcar");
        }

        if ((profile.EstimatedFatPer100g ?? 0) >= 17.5)
        {
            AddDistinct(context.NutritionFlags, "high_fat");
            AddDistinct(context.PrincipalOffenders, "gordura");
        }

        if ((profile.EstimatedSodiumPer100g ?? 0) >= 600)
        {
            AddDistinct(context.NutritionFlags, "high_sodium");
            AddDistinct(context.PrincipalOffenders, "sódio");
        }

        context.PrincipalOffender = context.PrincipalOffenders.Count switch
        {
            0 => string.Empty,
            1 => context.PrincipalOffenders[0],
            _ => string.Join(", ", context.PrincipalOffenders)
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 9b: Guard — bloqueia score/perfis sem dados nutricionais válidos
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Retorna true quando o perfil possui pelo menos um macro extraído
    /// (calorias, carboidratos, gordura ou proteína).
    /// Qualquer um desses campos confirma que a tabela foi realmente lida.
    /// </summary>
    private static bool HasValidNutritionForScoring(EstimatedNutritionProfileDto? profile)
    {
        if (profile == null) return false;

        return profile.CaloriesPer100g.HasValue
            || profile.CaloriesPer100ml.HasValue
            || profile.EstimatedCarbsPer100g.HasValue
            || profile.EstimatedFatPer100g.HasValue
            || profile.EstimatedProteinPer100g.HasValue;
    }

    /// <summary>
    /// Verifica se há dados nutricionais mínimos antes de calcular score e perfis.
    /// Quando não há dados válidos:
    ///   • BlockScoreAndProfiles = true  → mapper retorna score=null, perfis=null
    ///   • HasReliableNutritionData = false
    ///   • HealthProfiles limpo
    ///   • Summary fixo com mensagem de ausência de dados
    ///   • Warning adicionado para o usuário
    /// Nunca gera score a partir de categoria, fallback ou estimativas.
    /// </summary>
    private void Stage9b_GuardEmptyNutrition(NutritionAnalysisContext context)
    {
        if (HasValidNutritionForScoring(context.FinalNutritionProfile))
            return;

        _logger.LogWarning(
            "[Pipeline.Stage9b] ⛔ Nenhum dado nutricional válido detectado — score e perfis bloqueados. " +
            "FallbackType={FallbackType}, DataSource={DataSource}",
            context.FallbackType, context.NutritionDataSource);

        context.BlockScoreAndProfiles = true;
        context.HasReliableNutritionData = false;
        context.FallbackType = "no_nutrition_data";

        // Bloquear qualquer perfil de saúde gerado por categoria ou estimativa
        context.HealthProfiles = new ProductClassificationDto();
        context.PrincipalOffender = string.Empty;
        context.PrincipalOffenders.Clear();

        // Sumário determinístico — sem adivinhação
        context.Summary = "Não foi possível analisar os dados nutricionais a partir da imagem.";

        AddDistinct(context.Warnings,
            "Não foi possível extrair dados nutricionais válidos. " +
            "Tire uma foto clara da tabela nutricional com boa iluminação e foco.");

        // Limpar flags de score que possam ter sido definidas por estágios anteriores
        context.NutritionFlags.RemoveAll(f =>
            f.StartsWith("high_", StringComparison.OrdinalIgnoreCase) ||
            f.StartsWith("DataQuality:full", StringComparison.OrdinalIgnoreCase));

        AddDistinct(context.NutritionFlags, "DataQuality:no_nutrition");
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 10: Score
    // ═══════════════════════════════════════════════════════════════════

    private void Stage10_CalculateDeterministicScore(NutritionAnalysisContext context)
    {
        if (context.BlockScoreAndProfiles)
        {
            _logger.LogInformation("[Pipeline.Stage10] ⏭️ Cálculo de score ignorado — BlockScoreAndProfiles=true");
            return;
        }

        Stage7_CalculateScore(context);

        if (context.IsInconsistent)
        {
            context.ScoreAdjusted = Math.Max(0, context.ScoreAdjusted - 12);
            context.ConfidenceDetails.EstimatedNutritionProfile = Math.Min(context.ConfidenceDetails.EstimatedNutritionProfile, 0.45);
            AddDistinct(context.Warnings, "Score com penalidade por inconsistência dos dados extraídos.");
        }

        if (context.NutritionDataSource == DataSource.Fallback)
        {
            context.ScoreAdjusted = Math.Max(0, (int)Math.Round(context.ScoreAdjusted * 0.85));
            context.ConfidenceDetails.EstimatedNutritionProfile =
                Math.Min(context.ConfidenceDetails.EstimatedNutritionProfile, 0.35);
            AddDistinct(context.Warnings, "Score reduzido por uso de fallback de categoria (sem tabela nutricional lida).");
        }
        else if (context.FallbackType == "hybrid_ocr_category")
        {
            // Dados parcialmente complementados por categoria — confiança moderada
            context.ConfidenceDetails.EstimatedNutritionProfile =
                Math.Min(context.ConfidenceDetails.EstimatedNutritionProfile, 0.65);
            AddDistinct(context.Warnings,
                "Confiança moderada: alguns campos nutricionais foram complementados por referência de categoria.");
        }

        AdjustScoreByForm(context);

        context.ScoreAdjusted = Math.Clamp(context.ScoreAdjusted, 0, 100);
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 11: Interpretação semântica
    // ═══════════════════════════════════════════════════════════════════
    
    private void Stage11_InterpretSemantics(NutritionAnalysisContext context)
    {
        if (context.BlockScoreAndProfiles)
        {
            _logger.LogInformation("[Pipeline.Stage11] ⏭️ Interpretação semântica ignorada — BlockScoreAndProfiles=true");
            return;
        }

        Stage8_InterpretScore(context);
        ApplyPostScoreContextRules(context);
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 12: Validação final + persistência
    // ═══════════════════════════════════════════════════════════════════

    private async Task Stage12_FinalValidationAndPersist(NutritionAnalysisContext context, string fileName, Guid? userId, string? deviceId)
    {
        EnsureFinalNumericCoherence(context);
        await Stage9_ValidateAndFinalize(context, fileName, userId, deviceId);
        EnsureFinalNumericCoherence(context);
    }

    private static int CountValidCoreNutrients(EstimatedNutritionProfileDto? profile)
    {
        if (profile == null) return 0;

        var values = new double?[]
        {
            profile.CaloriesPer100g ?? profile.CaloriesPer100ml,
            profile.EstimatedSugarPer100g,
            profile.EstimatedProteinPer100g,
            profile.EstimatedFatPer100g,
            profile.EstimatedSodiumPer100g
        };

        return values.Count(v => v.HasValue && v.Value >= 0);
    }

    private static double? NormalizeRange(double? value, double min, double max, NutritionAnalysisContext context, string warning, bool grams = true)
    {
        if (!value.HasValue) return null;
        if (value.Value >= min && value.Value <= max) return value.Value;

        AddDistinct(context.Warnings, warning);
        context.IsInconsistent = true;
        return null;
    }

    private static void ValidateCriticalRange(double? value, double min, double max, NutritionAnalysisContext context, string nutrient, bool grams = true)
    {
        if (!value.HasValue) return;
        if (value.Value >= min && value.Value <= max) return;

        context.IsInconsistent = true;
        AddDistinct(context.ConsistencyIssues, $"Valor de {nutrient} fora do intervalo permitido.");
    }

    private async Task<EstimatedNutritionProfileDto?> TryGetCategoryFallbackProfile(NutritionAnalysisContext context, string resolvedCategory)
    {
        if (!string.IsNullOrWhiteSpace(context.CategoryNormalizedCode))
        {
            try
            {
                var fallbackResult = await _databaseFallback.ApplyFallbackAsync(null, context.CategoryNormalizedCode, nameof(AnalysisMode.FrontOfPackageOnly));
                if (!fallbackResult.ProfileRejected && fallbackResult.Profile != null)
                    return fallbackResult.Profile;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Pipeline.Stage7] Falha no fallback por banco, usando fallback local.");
            }
        }

        return GetLocalCategoryFallback(resolvedCategory);
    }

    private static EstimatedNutritionProfileDto? GetLocalCategoryFallback(string category)
    {
        var normalized = Norm(category);

        if (normalized.Contains("refrigerante"))
            return new EstimatedNutritionProfileDto { CaloriesPer100g = 42, EstimatedSugarPer100g = 10.6, EstimatedProteinPer100g = 0, EstimatedFatPer100g = 0, EstimatedSodiumPer100g = 12, Basis = "Fallback por categoria" };

        if (normalized.Contains("biscoito") || normalized.Contains("bolacha"))
            return new EstimatedNutritionProfileDto { CaloriesPer100g = 470, EstimatedSugarPer100g = 22, EstimatedProteinPer100g = 6, EstimatedFatPer100g = 19, EstimatedSodiumPer100g = 420, Basis = "Fallback por categoria" };

        if (normalized.Contains("salgadinho"))
            return new EstimatedNutritionProfileDto { CaloriesPer100g = 520, EstimatedSugarPer100g = 2, EstimatedProteinPer100g = 6, EstimatedFatPer100g = 29, EstimatedSodiumPer100g = 680, Basis = "Fallback por categoria" };

        if (normalized.Contains("iogurte"))
            return new EstimatedNutritionProfileDto { CaloriesPer100g = 80, EstimatedSugarPer100g = 11, EstimatedProteinPer100g = 3.5, EstimatedFatPer100g = 2.7, EstimatedSodiumPer100g = 55, Basis = "Fallback por categoria" };

        if (normalized.Contains("arroz"))
            return new EstimatedNutritionProfileDto { CaloriesPer100g = 360, EstimatedSugarPer100g = 0.5, EstimatedProteinPer100g = 7, EstimatedFatPer100g = 1, EstimatedSodiumPer100g = 10, Basis = "Fallback por categoria" };

        return new EstimatedNutritionProfileDto
        {
            CaloriesPer100g = 220,
            EstimatedSugarPer100g = 6,
            EstimatedProteinPer100g = 7,
            EstimatedFatPer100g = 8,
            EstimatedSodiumPer100g = 220,
            Basis = "Fallback por categoria"
        };
    }

    private static string ResolveCategory(string? category)
    {
        var normalized = Norm(category);
        if (string.IsNullOrWhiteSpace(normalized)) return "alimento geral";

        if (normalized.Contains("refrigerante") || normalized.Contains("soda")) return "refrigerante";
        if (normalized.Contains("biscoito") || normalized.Contains("bolacha")) return "biscoito";
        if (normalized.Contains("salgadinho")) return "salgadinho";
        if (normalized.Contains("iogurte")) return "iogurte";
        if (normalized.Contains("arroz")) return "arroz";
        if (normalized.Contains("queijo")) return "queijo";
        if (normalized.Contains("suco") || normalized.Contains("néctar") || normalized.Contains("nectar")) return "suco";

        return normalized;
    }

    private static void EnsureFinalNumericCoherence(NutritionAnalysisContext context)
    {
        var n = context.FinalNutritionProfile;
        if (n != null)
        {
            n.CaloriesPer100g = EnsureNonNegative(n.CaloriesPer100g);
            n.CaloriesPer100ml = EnsureNonNegative(n.CaloriesPer100ml);
            n.EstimatedSugarPer100g = EnsureBounded(n.EstimatedSugarPer100g, 0, 100);
            n.EstimatedFatPer100g = EnsureBounded(n.EstimatedFatPer100g, 0, 100);
            n.EstimatedSodiumPer100g = EnsureBounded(n.EstimatedSodiumPer100g, 0, 5000);
        }

        context.ScoreAdjusted = Math.Clamp(context.ScoreAdjusted, 0, 100);
        context.ScoreRaw = Math.Clamp(context.ScoreRaw, 0, 100);
    }

    private static double? EnsureNonNegative(double? value) => value.HasValue && value.Value < 0 ? null : value;
    private static double? EnsureBounded(double? value, double min, double max) =>
        value.HasValue && (value.Value < min || value.Value > max) ? null : value;

    private static void ApplyProductNameFallback(NutritionAnalysisContext context)
    {
        if (string.IsNullOrWhiteSpace(context.CategoryRaw)) return;

        if (string.IsNullOrWhiteSpace(context.ProductName) || ShouldUseCategoryAsProductName(context.ProductName, context.CategoryRaw))
        {
            context.ProductName = BuildCategoryAwareProductName(context.CategoryRaw, context.VisibleClaims);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 3: Decisão de Categoria
    // ═══════════════════════════════════════════════════════════════════

    private async Task Stage3_CategoryDecision(NutritionAnalysisContext context)
    {
        // REQ#7: Prioridade por palavras-chave no nome ou no texto OCR antes da normalização
        ApplyKeywordCategoryOverride(context);

        try
        {
            var normalization = await _categoryNormalization.NormalizeAsync(
                context.CategoryRaw, context.ProductName, context.VisibleClaims, context.Brand);

            context.CategoryNormalization = normalization;
            context.CategoryNormalized = normalization.NormalizedCategoryName;
            context.CategoryNormalizedCode = normalization.NormalizedCategoryCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Pipeline.Stage3] Category normalization failed");
            context.CategoryNormalized = context.CategoryRaw;
        }

        context.CategoryDecision = _categoryDecision.Decide(context);
        context.ProcessingLevel = context.CategoryDecision.ProcessingLevel;
        context.IsUltraProcessed = context.CategoryDecision.IsUltraProcessed;
    }

    /// <summary>
    /// Sobrescreve a categoria bruta com base em palavras-chave de alta confiança no nome
    /// do produto ou no texto OCR.
    /// Evita classificações genéricas incorretas (ex: coco → queijo).
    /// </summary>
    private static void ApplyKeywordCategoryOverride(NutritionAnalysisContext context)
    {
        // Consolidar fontes de texto para busca
        var sources = new[]
        {
            context.ProductName,
            context.CategoryRaw,
            context.Brand
        };
        var combined = string.Join(" ", sources.Where(s => !string.IsNullOrWhiteSpace(s)))
                             .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(combined)) return;

        // Regra coco: produto contém coco → derivado de coco
        if (combined.Contains("coco") || combined.Contains("coconut"))
        {
            context.CategoryRaw = "Derivados de coco";
            return;
        }

        // Outras regras de alta confiança (expansível)
        if (combined.Contains("whey") || combined.Contains("proteína em pó") || combined.Contains("protein powder"))
        {
            context.CategoryRaw = "Proteína em pó";
            return;
        }

        if (combined.Contains("azeite") || combined.Contains("olive oil"))
        {
            context.CategoryRaw = "Azeite de oliva";
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 4: Determinação de Confiabilidade
    // ═══════════════════════════════════════════════════════════════════

    private void Stage4_DetermineReliability(NutritionAnalysisContext context, VisualInterpretationResult visionResult)
    {
        var nutritionConfidence = context.ConfidenceDetails.EstimatedNutritionProfile;
        var hasProbableTable    = visionResult.ProbableCaptureType == CaptureType.NutritionTable;
        var hasProfileFromAI    = visionResult.EstimatedNutritionProfile != null;
        var hasQuantitativeData = HasQuantitativeData(context.FinalNutritionProfile);

        // ── Caminho 1: Tabela confirmada com alta confiança ──────────────────
        if (context.PublicAnalysisMode == AnalysisMode.FullNutritionLabel
            && nutritionConfidence >= 0.6
            && hasProfileFromAI
            && hasProbableTable)
        {
            context.HasReliableNutritionData = true;
            context.FallbackType = "real";

            // Partial check
            var profile = context.FinalNutritionProfile;
            if (profile != null)
            {
                var hasAnyNull = !profile.CaloriesPer100g.HasValue || !profile.EstimatedSugarPer100g.HasValue
                    || !profile.EstimatedProteinPer100g.HasValue || !profile.EstimatedSodiumPer100g.HasValue
                    || !profile.EstimatedFatPer100g.HasValue;
                if (hasAnyNull) context.FallbackType = "partial";
            }

            _logger.LogInformation("[Stage4] HasReliable=true (FullNutritionLabel + confidence {C:F2})", nutritionConfidence);
            InferQualitativeRisks(context, visionResult);
            return;
        }

        // ── Caminho 2: FullNutritionLabel mas confiança baixa — PORÉM há dados reais ─
        // CRÍTICO: não descartar dados numéricos extraídos pela IA mesmo com confidence < 0.6.
        // O modelo pode ter baixa confiança declarada mas ter extraído valores corretos.
        if (context.PublicAnalysisMode == AnalysisMode.FullNutritionLabel && hasQuantitativeData)
        {
            context.HasReliableNutritionData = true;
            context.FallbackType = "partial";
            _logger.LogWarning(
                "[Stage4] HasReliable=true (dados presentes apesar de confidence baixa {C:F2}). " +
                "AnalysisMode promovido. FallbackType=partial.", nutritionConfidence);
            InferQualitativeRisks(context, visionResult);
            return;
        }

        // ── Caminho 3: FrontOfPackageOnly ou sem dados — modo qualitativo ────
        context.HasReliableNutritionData = false;
        context.FallbackType = context.PublicAnalysisMode == AnalysisMode.FrontOfPackageOnly
            ? "category_based"
            : "unknown";

        _logger.LogInformation(
            "[Stage4] HasReliable=false. Mode={Mode}, Confidence={C:F2}, HasData={D}",
            context.PublicAnalysisMode, nutritionConfidence, hasQuantitativeData);

        InferQualitativeRisks(context, visionResult);
    }

    private static void InferQualitativeRisks(NutritionAnalysisContext context, VisualInterpretationResult visionResult)
    {
        var category = Norm(context.CategoryNormalized ?? context.CategoryRaw);
        var claims = context.VisibleClaims.Concat(visionResult.VisibleClaims ?? []).Select(c => c.ToLowerInvariant()).ToList();

        if (category.Contains("refrigerante") || category.Contains("achocolatado") || category.Contains("biscoito recheado")
            || category.Contains("chocolate") || category.Contains("sobremesa"))
            AddDistinct(context.InferredRisks, "alto_acucar");

        if (category.Contains("salgadinho") || category.Contains("tempero") || category.Contains("embutido")
            || category.Contains("queijo ralado") || category.Contains("macarrão instantâneo") || category.Contains("miojo"))
            AddDistinct(context.InferredRisks, "alto_sodio");

        if (category.Contains("biscoito") || category.Contains("bolacha") || category.Contains("chocolate")
            || category.Contains("salgadinho") || category.Contains("fritura")
            || claims.Any(c => c.Contains("gordura vegetal") || c.Contains("gordura hidrogenada") || c.Contains("gordura trans")))
            AddDistinct(context.InferredRisks, "alta_gordura");

        if (category.Contains("refrigerante") || category.Contains("salgadinho") || category.Contains("biscoito recheado")
            || category.Contains("embutido") || category.Contains("achocolatado em pó") || context.IsUltraProcessed)
            AddDistinct(context.InferredRisks, "ultraprocessado");

        if (claims.Any(c => c.Contains("glutamato") || c.Contains("corante") || c.Contains("aromatizante") || c.Contains("realçador de sabor")))
            AddDistinct(context.InferredRisks, "aditivos_quimicos");

        // Inferir riscos de ingredientes visíveis (sal, açúcar, gordura)
        if (claims.Any(c => c.Contains("sal") || c.Contains("glutamato monossódico") || c.Contains("msg") || c.Contains("realçador de sabor")))
            AddDistinct(context.InferredRisks, "alto_sodio");

        if (claims.Any(c => c.Contains("açúcar") || c.Contains("acucar") || c.Contains("xarope") || c.Contains("glucose") || c.Contains("frutose") || c.Contains("sacarose")))
            AddDistinct(context.InferredRisks, "alto_acucar");

        // Inferir da classificação da IA
        if (context.HealthProfiles.Diabetic?.Status?.Contains("nao", StringComparison.OrdinalIgnoreCase) == true)
            AddDistinct(context.InferredRisks, "alto_acucar");
        if (context.HealthProfiles.BloodPressure?.Status?.Contains("nao", StringComparison.OrdinalIgnoreCase) == true)
            AddDistinct(context.InferredRisks, "alto_sodio");
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 5: Fallback Nutricional
    // ═══════════════════════════════════════════════════════════════════

    private async Task Stage5_NutritionFallback(NutritionAnalysisContext context, VisualInterpretationResult visionResult)
    {
        if (!context.HasReliableNutritionData)
        {
            _logger.LogInformation("[Pipeline.Stage5] Clearing numeric data, HasReliableData=false");
            if (context.FinalNutritionProfile != null)
            {
                context.FinalNutritionProfile.CaloriesPer100g = null;
                context.FinalNutritionProfile.EstimatedPackageCalories = null;
                context.FinalNutritionProfile.EstimatedSugarPer100g = null;
                context.FinalNutritionProfile.EstimatedProteinPer100g = null;
                context.FinalNutritionProfile.EstimatedSodiumPer100g = null;
                context.FinalNutritionProfile.EstimatedFiberPer100g = null;
                context.FinalNutritionProfile.EstimatedFatPer100g = null;
                context.FinalNutritionProfile.Basis = "Análise baseada apenas na categoria, sem dados nutricionais exatos da tabela nutricional";
            }
            return;
        }

        if (context.PublicAnalysisMode != AnalysisMode.FrontOfPackageOnly) return;
        if (string.IsNullOrWhiteSpace(context.CategoryNormalizedCode)) return;

        try
        {
            var fallbackResult = await _databaseFallback.ApplyFallbackAsync(
                null, context.CategoryNormalizedCode, nameof(AnalysisMode.FrontOfPackageOnly));

            if (!fallbackResult.ProfileRejected && fallbackResult.Profile != null && context.FinalNutritionProfile != null)
            {
                TagOcrFieldSources(context.FinalNutritionProfile);
                MergeMissingValues(context.FinalNutritionProfile, fallbackResult.Profile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Pipeline.Stage5] Database fallback failed");
        }
    }

    /// <summary>
    /// Tags all existing numeric fields in the profile as coming from OCR in the DataSource
    /// dictionary AND in the typed FieldValues dictionary.
    /// Must be called BEFORE MergeMissingValues so per-field provenance is preserved.
    /// </summary>
    private static void TagOcrFieldSources(EstimatedNutritionProfileDto profile)
    {
        if (profile.DataSource == null)
            profile.DataSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (profile.FieldValues == null)
            profile.FieldValues = new Dictionary<string, FieldValue>(StringComparer.OrdinalIgnoreCase);

        // Parse overall OCR confidence from metadata if available
        double ocrConf = 0.90;
        if (profile.DataSource.TryGetValue("Confidence", out var confStr)
            && double.TryParse(confStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            ocrConf = System.Math.Clamp(parsed, 0.0, 1.0);
        }

        void Tag(string dsKey, string fvKey, double? value)
        {
            if (!value.HasValue) return;
            if (!profile.DataSource.ContainsKey(dsKey))
                profile.DataSource[dsKey] = "OCR";
            if (!profile.FieldValues.ContainsKey(fvKey))
                profile.FieldValues[fvKey] = FieldValue.FromOcr(value.Value, ocrConf);
        }

        var cal = profile.CaloriesPer100g ?? profile.CaloriesPer100ml;
        Tag("CaloriesPer100g",             NutritionFieldMergeEngine.Calories,     cal);
        Tag("EstimatedCarbsPer100g",       NutritionFieldMergeEngine.Carbs,        profile.EstimatedCarbsPer100g);
        Tag("EstimatedProteinPer100g",     NutritionFieldMergeEngine.Protein,      profile.EstimatedProteinPer100g);
        Tag("EstimatedFatPer100g",         NutritionFieldMergeEngine.Fat,          profile.EstimatedFatPer100g);
        Tag("EstimatedSaturatedFatPer100g",NutritionFieldMergeEngine.SaturatedFat, profile.EstimatedSaturatedFatPer100g);
        Tag("EstimatedSugarPer100g",       NutritionFieldMergeEngine.Sugar,        profile.EstimatedSugarPer100g);
        Tag("EstimatedAddedSugarPer100g",  NutritionFieldMergeEngine.AddedSugar,   profile.EstimatedAddedSugarPer100g);
        Tag("EstimatedFiberPer100g",       NutritionFieldMergeEngine.Fiber,        profile.EstimatedFiberPer100g);
        Tag("EstimatedSodiumPer100g",      NutritionFieldMergeEngine.Sodium,       profile.EstimatedSodiumPer100g);
        Tag("EstimatedPackageCalories",    "PackageCalories",                      profile.EstimatedPackageCalories);
    }

    /// <summary>
    /// Merges only absent (null) fields from <paramref name="fallback"/> into <paramref name="target"/>.
    /// OCR values are never overridden (priority rule: finalValue = ocrValue ?? fallbackValue).
    ///
    /// Delegates field-level merge and cross-field constraint validation to
    /// <see cref="NutritionFieldMergeEngine"/>. Both the flat numeric fields and
    /// <c>FieldValues</c> are kept in sync.
    /// Returns the canonical key names of fields that were actually filled from the fallback.
    /// </summary>
    private IReadOnlyList<string> MergeMissingValues(
        EstimatedNutritionProfileDto target,
        EstimatedNutritionProfileDto fallback)
    {
        if (target.FieldValues == null)
            target.FieldValues = new Dictionary<string, FieldValue>(StringComparer.OrdinalIgnoreCase);
        if (target.DataSource == null)
            target.DataSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Build FieldValue sets for both sides
        var primaryFields  = NutritionFieldMergeEngine.ExtractFromProfile(target,   "OCR",      0.90);
        var fallbackFields = NutritionFieldMergeEngine.ExtractFromProfile(fallback, "Fallback", 0.40);

        var mergedFields = new List<string>();

        // Field-level merge: only fill absent primary fields
        foreach (var key in NutritionFieldMergeEngine.AllKeys)
        {
            bool primaryHas  = primaryFields.TryGetValue(key, out var pv)  && pv.IsValid;
            bool fallbackHas = fallbackFields.TryGetValue(key, out var fv) && fv.IsValid;

            if (primaryHas || !fallbackHas) continue; // OCR locked, or nothing to fill

            mergedFields.Add(key);
            primaryFields[key] = fv!;
            _logger.LogDebug("[MergeMissing] {Field}: OCR=null → fallback={Value}", key, fv!.Value);
        }

        if (mergedFields.Count == 0)
        {
            _logger.LogInformation("[MergeMissing] Todos os campos OCR presentes — fallback não necessário");
            return mergedFields;
        }

        // Cross-field constraint validation on the merged set
        var validated = NutritionFieldMergeEngine.ValidateAndAdjust(
            primaryFields.ToDictionary(k => k.Key, k => k.Value, StringComparer.OrdinalIgnoreCase));

        // Remove fields the validator discarded (invalid fallback mappings)
        foreach (var discarded in mergedFields.ToList()
            .Where(k => !validated.ContainsKey(k)))
        {
            _logger.LogWarning(
                "[MergeMissing] ⚠️ {Field}: fallback descartado por violação de constraint", discarded);
            mergedFields.Remove(discarded);
        }

        // Write back validated fields to the flat profile
        NutritionFieldMergeEngine.ApplyToProfile(target, validated, target.NutritionUnit ?? "g");

        // Sync DataSource dict for backward compatibility
        foreach (var key in mergedFields)
            target.DataSource[key] = "fallback";

        _logger.LogInformation("[MergeMissing] Fallback fields applied: [{Fields}]",
            string.Join(", ", mergedFields));
        return mergedFields;
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 6: Sanitização + Category Overrides + Regras Conservadoras
    // ═══════════════════════════════════════════════════════════════════

    private void Stage6_SanitizeAndOverride(NutritionAnalysisContext context, VisualInterpretationResult visionResult)
    {
        // 6a. Sanitização nutricional
        var tempDto = new NutritionAnalysisResponseDto
        {
            EstimatedNutritionProfile = context.FinalNutritionProfile,
            Category = context.CategoryNormalized ?? context.CategoryRaw
        };
        var result = _nutritionSanitizer.Sanitize(tempDto);
        if (!result.IsSuccess)
            _logger.LogWarning("[Pipeline.Stage6] Sanitization issues: {Errors}", string.Join("; ", result.Errors));
        context.FinalNutritionProfile = tempDto.EstimatedNutritionProfile;

        // 6b. Category overrides de classificação
        ApplyCategoryOverrides(context);

        // 6c. Overrides por dados nutricionais reais
        if (context.HasReliableNutritionData)
            ApplyNutritionDataDrivenClassificationOverrides(context);

        // 6d. Regras conservadoras quando não há dados confiáveis
        if (!context.HasReliableNutritionData)
            ApplyConservativeClassifications(context);

        // 6e. Sanitizar reasons de classificações
        if (!context.HasReliableNutritionData)
            SanitizeClassificationReasons(context);

        // 6f. SanitizeEstimatedPackageCalories
        SanitizeEstimatedPackageCalories(context);

        // 6g. PrincipalOffender pre-score
        ApplyPrincipalOffenderIfMissing(context);
    }

    private static void ApplyCategoryOverrides(NutritionAnalysisContext context)
    {
        var classification = context.HealthProfiles;
        classification.Diabetic ??= new HealthProfileResult();
        classification.BloodPressure ??= new HealthProfileResult();
        classification.WeightLoss ??= new HealthProfileResult();
        classification.MuscleGain ??= new HealthProfileResult();

        var category = Norm(context.CategoryNormalized ?? context.CategoryRaw);

        if (category.Contains("achocolatado"))
        {
            OverrideIfWeakerOrMissing(classification.Diabetic, "nao_recomendado", "Achocolatados costumam ter alta carga de açúcar.");
            OverrideIfWeakerOrMissing(classification.WeightLoss, "nao_indicado", "Categoria densa em açúcar e calorias.");
            OverrideIfWeakerOrMissing(classification.MuscleGain, "fraco", "Produto com baixo teor proteico.");
        }

        if (IsBeverageCategory(category))
        {
            var sugar = context.FinalNutritionProfile?.EstimatedSugarPer100g ?? 0;
            if (sugar >= 5)
            {
                DowngradeIfWarranted(classification.WeightLoss!, "consumo_moderado", "Bebida com presença relevante de açúcar por porção.");
                DowngradeIfWarranted(classification.Diabetic!, "consumo_moderado", "Bebida com presença relevante de açúcar.");
            }
        }
    }

    private static void ApplyNutritionDataDrivenClassificationOverrides(NutritionAnalysisContext context)
    {
        var nutrition = context.FinalNutritionProfile;
        if (nutrition == null) return;

        var sugar = nutrition.EstimatedSugarPer100g ?? 0;
        var calories = nutrition.CaloriesPer100g ?? 0;
        var fat = nutrition.EstimatedFatPer100g ?? 0;
        var sodium = nutrition.EstimatedSodiumPer100g ?? 0;
        var category = Norm(context.CategoryNormalized ?? context.CategoryRaw);
        var isBev = IsBeverageCategory(category);
        var c = context.HealthProfiles;

        var sugarMod = isBev ? 5.0 : 8.0;
        var sugarHigh = isBev ? 10.0 : 15.0;

        if (calories > (isBev ? 200 : 350) || sugar > sugarHigh || fat > (isBev ? 8 : 17.5))
            DowngradeIfWarranted(c.WeightLoss!, "nao_recomendado", "Alta densidade energética ou excesso de açúcar/gordura.");
        else if (calories > (isBev ? 100 : 220) || sugar > sugarMod || fat > (isBev ? 3 : 10))
            DowngradeIfWarranted(c.WeightLoss!, "consumo_moderado", "Combinação de açúcar, gordura ou calorias exige atenção.");

        if (sugar > sugarHigh) DowngradeIfWarranted(c.Diabetic!, "nao_recomendado", "Alto teor de açúcar.");
        else if (sugar > sugarMod) DowngradeIfWarranted(c.Diabetic!, "consumo_moderado", "Teor moderado de açúcar.");

        if (sodium > 600) DowngradeIfWarranted(c.BloodPressure!, "nao_recomendado", "Teor elevado de sódio.");
        else if (sodium > 400) DowngradeIfWarranted(c.BloodPressure!, "consumo_moderado", "Teor moderado de sódio.");
    }

    private static void ApplyConservativeClassifications(NutritionAnalysisContext context)
    {
        MakeConservativeIfPositive(context.HealthProfiles.Diabetic, "teor de açúcares");
        MakeConservativeIfPositive(context.HealthProfiles.BloodPressure, "teor de sódio");
        MakeConservativeIfPositive(context.HealthProfiles.WeightLoss, "densidade calórica e perfil nutricional");
        MakeConservativeIfPositive(context.HealthProfiles.MuscleGain, "teor de proteína");
    }

    private static void MakeConservativeIfPositive(HealthProfileResult? profile, string nutrient)
    {
        if (profile == null) return;
        var positives = new[] { "adequado", "bom", "recomendado", "favoravel" };
        if (positives.Any(s => profile.Status?.Contains(s, StringComparison.OrdinalIgnoreCase) == true))
        {
            profile.Status = "indeterminado";
            profile.Reason = $"Não foi possível confirmar o {nutrient} sem tabela nutricional visível.";
        }
    }

    private static void SanitizeClassificationReasons(NutritionAnalysisContext context)
    {
        var profiles = new[] { context.HealthProfiles.Diabetic, context.HealthProfiles.BloodPressure, context.HealthProfiles.WeightLoss, context.HealthProfiles.MuscleGain };
        foreach (var p in profiles)
        {
            if (p?.Reason == null) continue;
            p.Reason = p.Reason.Replace("baixo teor de açúcar", "teor de açúcar não confirmado", StringComparison.OrdinalIgnoreCase);
            p.Reason = p.Reason.Replace("baixo teor de sódio", "teor de sódio não confirmado", StringComparison.OrdinalIgnoreCase);
            p.Reason = p.Reason.Replace("baixo teor de gordura", "teor de gordura não confirmado", StringComparison.OrdinalIgnoreCase);
            p.Reason = p.Reason.Replace("perfil equilibrado", "perfil não totalmente confirmado", StringComparison.OrdinalIgnoreCase);
            p.Reason = p.Reason.Replace("adequado para", "adequação não confirmada para", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void SanitizeEstimatedPackageCalories(NutritionAnalysisContext context)
    {
        var nutrition = context.FinalNutritionProfile;
        if (nutrition?.EstimatedPackageCalories == null) return;

        var category = Norm(context.CategoryNormalized ?? context.CategoryRaw);
        if (IsStapleWithAmbiguousPreparation(category))
        {
            nutrition.EstimatedPackageCalories = null;
            nutrition.Basis = AppendBasis(nutrition.Basis,
                "Calorias totais da embalagem omitidas porque a base nutricional pode variar entre produto cru, cozido ou porção.");
        }
    }

    private static void ApplyPrincipalOffenderIfMissing(NutritionAnalysisContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.PrincipalOffender)) return;

        // Qualitative fallback from category
        var qualitative = DetectQualitativePrincipalOffender(context.CategoryNormalized ?? context.CategoryRaw);
        if (!string.IsNullOrWhiteSpace(qualitative))
        {
            context.PrincipalOffender = qualitative;
            return;
        }

        if (!context.HasReliableNutritionData) return;
        var nutrition = context.FinalNutritionProfile;
        if (nutrition == null) return;

        var sugar = nutrition.EstimatedSugarPer100g ?? 0;
        var sodium = nutrition.EstimatedSodiumPer100g ?? 0;
        var fat = nutrition.EstimatedFatPer100g ?? 0;
        var category = Norm(context.CategoryNormalized ?? context.CategoryRaw);
        var isBev = IsBeverageCategory(category);
        var sugarThreshold = isBev ? 6.0 : 10.0;

        var sugarScore = sugar >= sugarThreshold ? (sugar - sugarThreshold) * (isBev ? 3 : 2) : 0;
        var sodiumScore = sodium > 400 ? (sodium - 400) / 100.0 : 0;
        var fatScore = fat > 10 ? (fat - 10) / 2.0 : 0;

        if (sugarScore > 0 && sugarScore >= sodiumScore && sugarScore >= fatScore) context.PrincipalOffender = "sugar";
        else if (sodiumScore > 0 && sodiumScore >= fatScore) context.PrincipalOffender = "sodium";
        else if (fatScore > 0) context.PrincipalOffender = "fat";
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 6b: Sanity Validator pós-parser
    // ═══════════════════════════════════════════════════════════════════

    private void Stage6b_SanityValidation(NutritionAnalysisContext context, VisualInterpretationResult visionResult)
    {
        var sanityResult = _sanityValidator.Validate(context, visionResult);

        // Aplicar perfil nutricional validado/corrigido
        context.FinalNutritionProfile = sanityResult.ValidatedNutrition;

        // Aplicar correções de modo de análise (Regras 1 e 2)
        if (sanityResult.CorrectedAnalysisMode.HasValue)
        {
            context.AnalysisMode = sanityResult.CorrectedAnalysisMode.Value;
            _logger.LogInformation("[Stage6b] AnalysisMode corrigido: {Mode}", context.AnalysisMode);
        }

        if (sanityResult.CorrectedPublicAnalysisMode.HasValue)
        {
            context.PublicAnalysisMode = sanityResult.CorrectedPublicAnalysisMode.Value;

            // Se promovido para FullNutritionLabel, garantir dados confiáveis marcados
            if (context.PublicAnalysisMode == Domain.Enums.AnalysisMode.FullNutritionLabel
                && !context.HasReliableNutritionData)
            {
                context.HasReliableNutritionData = true;
                context.FallbackType = "partial";
                _logger.LogInformation("[Stage6b] HasReliableNutritionData promovido por sanity validator.");
            }
        }

        // Aplicar ParserConfidence no perfil
        if (context.FinalNutritionProfile != null)
            context.FinalNutritionProfile.ParserConfidence = sanityResult.ParserConfidence;

        // Registrar warnings (Regra 12) — separados dos técnicos já existentes
        foreach (var warning in sanityResult.Warnings)
            AddDistinct(context.Warnings, warning);

        // Registrar inconsistências como avisos visíveis filtrados no controller
        foreach (var inconsistency in sanityResult.Inconsistencies)
            AddDistinct(context.Warnings, inconsistency);

        // Registrar inconsistências no campo de consistência do contexto
        foreach (var inconsistency in sanityResult.Inconsistencies)
            AddDistinct(context.ConsistencyIssues, inconsistency);

        // Regra 8: Se não pode calcular score confiável, reduzir confiança no ConfidenceDetails
        if (!sanityResult.CanScoreReliably && context.HasReliableNutritionData)
        {
            if (context.ConfidenceDetails != null)
            {
                context.ConfidenceDetails.EstimatedNutritionProfile =
                    Math.Min(context.ConfidenceDetails.EstimatedNutritionProfile, 0.45);
            }

            AddDistinct(context.Warnings,
                "Score calculado com dados nutricionais incompletos. Resultado pode ter margem de erro.");
        }

        // Regra 11: ShouldReprocess → logar e adicionar inconsistência
        if (sanityResult.ShouldReprocess)
        {
            _logger.LogWarning("[Stage6b] ShouldReprocess=true. Tabela detectada mas extração incompleta.");
            AddDistinct(context.ConsistencyIssues,
                "Tabela nutricional detectada, mas extração incompleta. Uma segunda passagem poderia melhorar os dados.");
        }

        _logger.LogInformation(
            "[Stage6b] Confidence={Conf}, CanScore={CanScore}, ShouldReprocess={Reprocess}, " +
            "Mode={Mode}, Warnings={W}, Inconsistencies={I}",
            sanityResult.ParserConfidence, sanityResult.CanScoreReliably, sanityResult.ShouldReprocess,
            context.PublicAnalysisMode, sanityResult.Warnings.Count, sanityResult.Inconsistencies.Count);
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 7: Cálculo de Score
    // ═══════════════════════════════════════════════════════════════════

    private void Stage7_CalculateScore(NutritionAnalysisContext context)
    {
        var calc = _scoreCalculator.Calculate(context);
        context.ScoreCalculation = calc;
        context.ScoreRaw = calc.ScoreRaw;
        context.ScoreAdjusted = calc.ScoreAdjusted;

        // Sync PrincipalOffender: score > pre-existing
        if (string.IsNullOrWhiteSpace(context.PrincipalOffender) && !string.IsNullOrWhiteSpace(calc.ProbableOffender))
            context.PrincipalOffender = calc.ProbableOffender;
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 8: Interpretação Semântica
    // ═══════════════════════════════════════════════════════════════════

    private void Stage8_InterpretScore(NutritionAnalysisContext context)
    {
        var interpretation = _scoreInterpretation.Interpret(context);
        context.ScoreInterpretation = interpretation;
        context.ScoreLabel = interpretation.Label;
        context.SafeLabel = interpretation.SafeLabel;
        context.RecommendationLevel = interpretation.RecommendationLevel;
        context.RequiresModeration = interpretation.RecommendationLevel is "consumo_moderado" or "consumo_moderado_positivo" or "atencao" or "nao_recomendado"
            || context.ProcessingLevel is "processado" or "ultraprocessado";
    }

    // ═══════════════════════════════════════════════════════════════════
    // STAGE 9: Consistência + Warnings + Summary + Persistência
    // ═══════════════════════════════════════════════════════════════════

    private async Task Stage9_ValidateAndFinalize(NutritionAnalysisContext context, string fileName, Guid? userId, string? deviceId)
    {
        _consistencyValidator.ValidateAndCorrect(context);

        // Warnings
        BuildWarnings(context);
        ApplyAutomaticWarnings(context);
        ApplyQualitativeAlerts(context);
        RefineWarningsForMobile(context);

        // Score >= 85: garantir alertas apenas positivos antes da persistência
        if (context.ScoreAdjusted >= 85)
        {
            context.Alerts = context.Alerts
                .Where(a => !a.Contains("evite", StringComparison.OrdinalIgnoreCase)
                         && !a.Contains("modera", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Summary
        BuildFinalSummary(context);

        // Coherence
        EnforceResponseCoherence(context);

        // Persistence
        if (HasUsableAnalysis(context))
        {
            try
            {
                context.AnalysisId = await PersistAnalysisAsync(context, fileName, userId, deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao persistir análise. Resposta retornada sem AnalysisId.");
                context.AnalysisId = null;
            }
        }
    }

    private static void BuildWarnings(NutritionAnalysisContext context)
    {
        if (context.PublicAnalysisMode == AnalysisMode.FrontOfPackageOnly)
            AddDistinct(context.Warnings, "Análise estimada com base na frente da embalagem e na categoria.");

        if (!context.HasReliableNutritionData)
        {
            AddDistinct(context.Warnings, "Análise baseada apenas na categoria do produto. Para avaliação precisa dos valores nutricionais, fotografe a tabela nutricional da embalagem.");
            foreach (var risk in context.InferredRisks)
            {
                var riskWarning = risk switch
                {
                    "alto_acucar" => "Categoria tipicamente com alto teor de açúcar. Moderação recomendada.",
                    "alto_sodio" => "Categoria tipicamente com alto teor de sódio. Atenção ao consumo recorrente.",
                    "alta_gordura" => "Categoria tipicamente com alta gordura. Considere o tamanho da porção.",
                    "ultraprocessado" => "Produto ultraprocessado. Priorize alimentos in natura quando possível.",
                    "aditivos_quimicos" => "Possível presença de aditivos químicos detectada nos ingredientes.",
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(riskWarning)) AddDistinct(context.Warnings, riskWarning);
            }
        }
    }

    private static void ApplyAutomaticWarnings(NutritionAnalysisContext context)
    {
        if (!context.HasReliableNutritionData) return;
        var nutrition = context.FinalNutritionProfile;
        if (nutrition == null) return;

        var category = Norm(context.CategoryNormalized ?? context.CategoryRaw);
        var isBev = IsBeverageCategory(category);
        var sugarThreshold = isBev ? 7.0 : 22.0;

        if (nutrition.EstimatedSugarPer100g >= sugarThreshold)
            AddDistinct(context.Warnings, isBev
                ? "Contém açúcar relevante por porção. Para uma bebida, esse teor concentra impacto glicêmico mesmo em 200ml."
                : "Teor estimado de açúcar elevado para consumo frequente.");

        if (nutrition.CaloriesPer100g >= 450)
            AddDistinct(context.Warnings, "Alta densidade calórica estimada por 100g.");

        if (nutrition.EstimatedSodiumPer100g >= 600)
            AddDistinct(context.Warnings, "Alto teor estimado de sódio por 100g.");

        if (IsRiceCategory(category))
        {
            if (nutrition.EstimatedFiberPer100g is null or < 3)
                AddDistinct(context.Warnings, "Baixo teor estimado de fibras para a categoria.");
            AddDistinct(context.Warnings, "Para controle glicêmico, vale moderar a porção e combinar com feijão, legumes ou proteínas.");
        }
    }

    private static void ApplyQualitativeAlerts(NutritionAnalysisContext context)
    {
        var offender = DetectQualitativePrincipalOffender(context.CategoryNormalized ?? context.CategoryRaw);
        if (!string.IsNullOrWhiteSpace(offender))
        {
            if (string.IsNullOrWhiteSpace(context.PrincipalOffender))
                context.PrincipalOffender = offender;
            AddDistinct(context.Alerts, $"Principal ponto de atenção na categoria: {offender}.");
        }

        if (!context.HasReliableNutritionData && context.ScoreAdjusted > 0)
            AddDistinct(context.Alerts, "Pontuação calculada com baixa confiança, baseada na categoria e painel frontal.");
    }

    private static void RefineWarningsForMobile(NutritionAnalysisContext context)
    {
        if (context.Warnings.Count == 0) return;

        var maxWarnings = context.PublicAnalysisMode == AnalysisMode.FrontOfPackageOnly ? 4 : 5;
        context.Warnings = context.Warnings
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxWarnings)
            .ToList();
    }

    private static void BuildFinalSummary(NutritionAnalysisContext context)
    {
        // Quando não há dados válidos, o Stage9b já definiu o sumário correto — preservá-lo.
        if (context.BlockScoreAndProfiles)
        {
            context.Summary ??= "Não foi possível analisar os dados nutricionais a partir da imagem.";
            return;
        }

        if (!context.HasReliableNutritionData)
        {
            var productName = context.ProductName ?? "Produto";
            var categoryDesc = !string.IsNullOrWhiteSpace(context.CategoryNormalized ?? context.CategoryRaw)
                ? $"da categoria {context.CategoryNormalized ?? context.CategoryRaw}" : "alimentício";
            var risksDesc = context.InferredRisks.Count > 0
                ? $" com possíveis pontos de atenção: {FormatRisks(context.InferredRisks)}" : "";

            context.Summary = $"Análise baseada apenas na categoria, sem dados nutricionais exatos. " +
                $"{productName} é um produto {categoryDesc}{risksDesc}. Para análise precisa, fotografe a tabela nutricional da embalagem.";
            return;
        }

        // Especialização: arroz
        var cat = Norm(context.CategoryNormalized ?? context.CategoryRaw);
        if (IsRiceCategory(cat))
        {
            var riceProfile = context.ScoreAdjusted >= 70
                ? "tem um perfil mais equilibrado dentro da categoria"
                : "tem um perfil nutricional intermediário e funciona como uma base neutra da refeição";
            context.Summary = $"{context.ProductName ?? "Arroz"} {riceProfile}. Para controle glicêmico ou emagrecimento, " +
                "vale moderar a porção e combinar com feijão, legumes ou proteínas. " +
                (context.PublicAnalysisMode == AnalysisMode.FullNutritionLabel
                    ? "Dados obtidos da tabela nutricional da embalagem."
                    : "Resumo estimado a partir da categoria e da frente da embalagem.");
            return;
        }

        var product  = context.ProductName ?? "Produto";
        var nutrition = context.FinalNutritionProfile;

        // ─── Condições críticas (Req#8): linguagem específica, sem termos genéricos ───
        var issues   = new List<string>();
        var benefits = new List<string>();

        if (nutrition != null)
        {
            var fat    = nutrition.EstimatedFatPer100g ?? 0;
            var satFat = nutrition.EstimatedSaturatedFatPer100g ?? 0;
            var sugar  = nutrition.EstimatedSugarPer100g ?? 0;
            var sodium = nutrition.EstimatedSodiumPer100g ?? 0;
            var prot   = nutrition.EstimatedProteinPer100g ?? 0;
            var fiber  = nutrition.EstimatedFiberPer100g ?? 0;

            // Gorduras — prioridade máxima
            if (fat >= 40)
                issues.Add($"alto teor de gordura total ({fat:0.#} g/100 g)");
            else if (fat >= 20)
                issues.Add("gordura total elevada");

            // Gordura saturada — mencionar explicitamente se suspeita ou alta
            if (satFat > 0)
            {
                bool satSuspicious = NutritionDataValidatorService.IsSaturatedFatSuspicious(nutrition);
                if (satSuspicious)
                    issues.Add($"gordura saturada possivelmente subestimada ({satFat:0.#} g/100 g)");
                else if (satFat >= 10)
                    issues.Add($"gordura saturada elevada ({satFat:0.#} g/100 g)");
            }

            if (sugar  >= 20) issues.Add($"a\u00e7\u00facar elevado ({sugar:0.#} g/100 g)");
            else if (sugar >= 10) issues.Add("teor moderado de açúcar");

            if (sodium >= 600) issues.Add($"s\u00f3dio alto ({sodium:0} mg/100 g)");
            else if (sodium >= 300) issues.Add("sódio moderado");

            if (prot  >= 12) benefits.Add($"bom teor de proteína ({prot:0.#} g)");
            if (fiber >=  6) benefits.Add($"bom teor de fibras ({fiber:0.#} g)");
            if (sugar <=  5 && sugar >= 0 && nutrition.EstimatedSugarPer100g.HasValue)
                benefits.Add("baixo teor de açúcar");
        }

        var analysisCtx = context.PublicAnalysisMode == AnalysisMode.FullNutritionLabel
            ? "Dados obtidos da tabela nutricional da embalagem."
            : "Resumo estimado a partir da categoria e da frente da embalagem.";

        if (issues.Count > 0 && benefits.Count > 0)
        {
            context.Summary = $"{product} apresenta {string.Join(" e ", issues.Take(2))}, " +
                $"com ponto positivo de {string.Join(" e ", benefits.Take(2))}. {analysisCtx}";
        }
        else if (issues.Count > 0)
        {
            context.Summary = $"{product} apresenta {string.Join(" e ", issues.Take(2))}. " +
                $"Consuma com moderação. {analysisCtx}";
        }
        else if (benefits.Count > 0)
        {
            context.Summary = $"{product} tem perfil nutricional favorável, " +
                $"com {string.Join(" e ", benefits.Take(2))}. {analysisCtx}";
        }
        else
        {
            var level = context.ScoreAdjusted switch
            {
                >= 70 => "perfil nutricional satisfatório",
                >= 50 => "perfil nutricional intermediário",
                _     => "pontos de atenção no perfil nutricional"
            };
            context.Summary = $"{product} tem {level}. {analysisCtx}";
        }
    }

    private static void EnforceResponseCoherence(NutritionAnalysisContext context)
    {
        var profile = context.FinalNutritionProfile;
        var hasQuantitative = profile != null && (
            profile.CaloriesPer100g.HasValue || profile.EstimatedSugarPer100g.HasValue
            || profile.EstimatedProteinPer100g.HasValue || profile.EstimatedSodiumPer100g.HasValue);

        // FullNutritionLabel sem dados → rebaixar
        if (context.PublicAnalysisMode == AnalysisMode.FullNutritionLabel && !hasQuantitative)
        {
            context.PublicAnalysisMode = AnalysisMode.FrontOfPackageOnly;
            AddDistinct(context.Warnings, "Modo de análise ajustado: tabela nutricional indicada, mas sem valores quantitativos extraídos.");
        }

        // Basis coerente
        if (context.PublicAnalysisMode == AnalysisMode.FrontOfPackageOnly && profile != null && !hasQuantitative)
        {
            var basis = (profile.Basis ?? "").ToLowerInvariant();
            if (!basis.Contains("não foi possível") && !basis.Contains("não disponíveis"))
                profile.Basis = "Análise baseada na frente da embalagem. Não houve extração quantitativa da tabela nutricional.";
        }

        // Summary não pode afirmar tabela se modo é FrontOfPackageOnly
        if (context.PublicAnalysisMode == AnalysisMode.FrontOfPackageOnly && context.Summary != null)
            context.Summary = context.Summary.Replace(
                "Dados obtidos da tabela nutricional da embalagem.",
                "Resumo estimado a partir da categoria e da frente da embalagem.",
                StringComparison.OrdinalIgnoreCase);

        // REQ#4 — isCorrectedByOcr: true se QUALQUER macro veio do OCR (mesmo parcial)
        if (profile != null)
        {
            bool anyOcrMacro = context.NutritionDataSource is DataSource.Real or DataSource.Partial
                || context.FallbackType == "hybrid_ocr_category"
                || context.NutritionFlags.Any(f => f.StartsWith("OCR_MODE:", StringComparison.OrdinalIgnoreCase));

            profile.IsCorrectedByOcr = anyOcrMacro;
        }

        // REQ#5 — Basis deve refletir a fonte real dos dados
        if (profile != null)
        {
            var isFallbackOnly = context.NutritionDataSource == DataSource.Fallback
                && context.FallbackType != "hybrid_ocr_category";

            var isHybrid = context.FallbackType == "hybrid_ocr_category"
                || (context.NutritionDataSource == DataSource.Partial
                    && context.NutritionFlags.Any(f => f == "DataSource:hybrid"));

            var isOcrFull = context.NutritionDataSource == DataSource.Real;

            // Sobrescrever basis apenas quando ainda não reflete a fonte correta
            var currentBasis = profile.Basis ?? "";
            if (isFallbackOnly && !currentBasis.Contains("fallback", StringComparison.OrdinalIgnoreCase)
                && !currentBasis.Contains("Estimativa", StringComparison.OrdinalIgnoreCase))
            {
                profile.Basis = $"Fallback por categoria (sem tabela nutricional lida): {context.CategoryNormalized ?? context.CategoryRaw ?? "categoria"}.";
            }
            else if (isHybrid && !currentBasis.Contains("Hybrid", StringComparison.OrdinalIgnoreCase)
                && !currentBasis.Contains("fallback", StringComparison.OrdinalIgnoreCase))
            {
                profile.Basis = currentBasis.TrimEnd('.') + " (Hybrid: OCR + complemento por categoria).";
            }
            else if (isOcrFull && !currentBasis.Contains("OCR", StringComparison.OrdinalIgnoreCase)
                && !currentBasis.Contains("100 g", StringComparison.OrdinalIgnoreCase)
                && !currentBasis.Contains("100 ml", StringComparison.OrdinalIgnoreCase))
            {
                profile.Basis = "Dados extraídos via OCR estruturado.";
            }

            // Política de integridade numérica: padronizar mensagem final do basis
            // de acordo com a fonte real dos valores numéricos.
            if (isOcrFull)
                profile.Basis = "Dados extraídos via OCR estruturado";
            else if (isHybrid)
                profile.Basis = "Dados extraídos via OCR + fallback parcial por categoria";
            else if (isFallbackOnly)
                profile.Basis = $"Dados estimados por fallback de categoria: {context.CategoryNormalized ?? context.CategoryRaw ?? "categoria"}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regras pós-score (contexto)
    // ═══════════════════════════════════════════════════════════════════

    private static void ApplyPostScoreContextRules(NutritionAnalysisContext context)
    {
        // Regra 1: Correção de categoria para cereais/grãos
        ApplyCerealCategoryCorrection(context);

        // Regra 2: Alimento natural / regra de caloria inteligente
        ApplyNaturalFoodRule(context);

        // Regra 3: Explicação positiva quando não há principal offender
        ApplyPositiveExplanationRule(context);

        // Regra 4: Anti-contradição para scores excelentes
        if (context.ScoreAdjusted >= 85)
            ApplyExcellentAntiContradiction(context);
    }

    private static void ApplyCerealCategoryCorrection(NutritionAnalysisContext context)
    {
        var category = Norm(context.CategoryNormalized ?? context.CategoryRaw);
        var productName = Norm(context.ProductName);
        var combined = $"{category} {productName}";

        var isCereal = combined.Contains("milho") || combined.Contains("pipoca")
            || combined.Contains("arroz") || combined.Contains("trigo")
            || combined.Contains("aveia") || combined.Contains("cevada")
            || combined.Contains("centeio") || combined.Contains("granola");

        if (!isCereal) return;

        // Nunca classificar cereal como feijão
        if (category.Contains("feij") || (!category.Contains("cereal") && !category.Contains("grão") && !category.Contains("grao")))
            context.CategoryNormalized = "Cereal / Grão";
    }

    private static void ApplyNaturalFoodRule(NutritionAnalysisContext context)
    {
        var nutrition = context.FinalNutritionProfile;
        if (nutrition == null) return;

        var sugar = nutrition.EstimatedSugarPer100g;
        var addedSugar = nutrition.EstimatedAddedSugarPer100g ?? 0;
        var sodium = nutrition.EstimatedSodiumPer100g;
        var fiber = nutrition.EstimatedFiberPer100g ?? 0;
        var isUltra = context.IsUltraProcessed
            || string.Equals(context.ProcessingLevel, "ultraprocessado", StringComparison.OrdinalIgnoreCase);
        var isMinimallyProcessed = string.Equals(
            context.ProcessingLevel, "minimamente_processado", StringComparison.OrdinalIgnoreCase);

        // Alimento natural: minimamente processado com perfil nutricional saudável
        // OU perfil nutricional saudável sem ser ultraprocessado
        if (sugar < 5 && addedSugar == 0 && sodium < 100 && fiber >= 5
            && (isMinimallyProcessed || !isUltra))
        {
            context.PrincipalOffender = string.Empty;
        }
    }

    private static void ApplyPositiveExplanationRule(NutritionAnalysisContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.PrincipalOffender)) return;

        const string positiveReason = "Boa qualidade nutricional geral, com destaque para fibras e baixo teor de açúcar";
        const string positivePonto = "Alimento natural com bom perfil nutricional";

        context.ExplicacaoScore = positiveReason;
        context.PontoPrincipal = positivePonto;
    }

    private static void ApplyExcellentAntiContradiction(NutritionAnalysisContext context)
    {
        var negativeTerms = new[] { "evite", "moderação", "moderacao", "cuidado" };

        context.Alerts = context.Alerts
            .Where(a => !negativeTerms.Any(t => a.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        context.Warnings = context.Warnings
            .Where(w => !negativeTerms.Any(t => w.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        AddDistinct(context.Alerts, "Boa opção para o dia a dia");
        AddDistinct(context.Alerts, "Alimento natural e nutritivo");

        context.RequiresModeration = false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regras pós-mapeamento (response)
    // ═══════════════════════════════════════════════════════════════════

    private static void ApplyPostScoreResponseRules(NutritionAnalysisResponseDto response)
    {
        if (response.Score == null) return;

        var negativeTerms = new[] { "evite", "moderação", "moderacao", "cuidado" };

        // Re-detectar alimento natural a partir do perfil nutricional da response
        // (necessário pois NutritionTextPresentationBuilder pode ter sobrescrito PrincipalOffender)
        var nutrition = response.EstimatedNutritionProfile;
        var isNaturalFood = nutrition != null
            && (nutrition.EstimatedSugarPer100g ?? double.MaxValue) < 5
            && (nutrition.EstimatedAddedSugarPer100g ?? 0) == 0
            && (nutrition.EstimatedSodiumPer100g ?? double.MaxValue) < 100
            && (nutrition.EstimatedFiberPer100g ?? 0) >= 5
            && !string.Equals(response.Score.ProcessingLevel, "ultraprocessado", StringComparison.OrdinalIgnoreCase);

        // Regra 1/3: Alimento natural — sobrescrever todos os campos de apresentação
        if (isNaturalFood)
        {
            const string positiveReason = "Boa qualidade nutricional geral, com destaque para fibras e baixo teor de açúcar";
            const string positivePonto = "Alimento natural com bom perfil nutricional";

            response.PrincipalOffender = string.Empty;
            response.Score.PrincipalOffender = string.Empty;
            response.Score.Reason = positiveReason;
            response.Score.ScoreInterpretation = $"Score {response.Score.Value}/100. {positiveReason}";
            response.ExplicacaoScore = positiveReason;
            response.PontoPrincipal = positivePonto;

            response.ResumoRapido = (response.ResumoRapido ?? [])
                .Where(r => !negativeTerms.Any(t => r.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        // Regra 2/4: Score >= 85 — remover negativos de todos os campos de texto
        if (response.Score.Value >= 85)
        {
            response.Alerts = (response.Alerts ?? [])
                .Where(a => !negativeTerms.Any(t => a.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Warnings também alimentam o campo Alerts no controller (via MapToMobileResponse)
            response.Warnings = (response.Warnings ?? [])
                .Where(w => !negativeTerms.Any(t => w.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            response.ResumoRapido = (response.ResumoRapido ?? [])
                .Where(r => !negativeTerms.Any(t => r.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            response.Score.RequiresModeration = false;

            if (!string.IsNullOrWhiteSpace(response.Score.Reason)
                && negativeTerms.Any(t => response.Score.Reason.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                response.Score.Reason = "Boa qualidade nutricional geral, com destaque para fibras e baixo teor de açúcar";
                response.Score.ScoreInterpretation = $"Score {response.Score.Value}/100. {response.Score.Reason}";
            }

            if (!string.IsNullOrWhiteSpace(response.ExplicacaoScore)
                && negativeTerms.Any(t => response.ExplicacaoScore.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                response.ExplicacaoScore = "Boa qualidade nutricional geral, com destaque para fibras e baixo teor de açúcar";
            }

            // Summary não pode ser negativo com score excelente
            if (!string.IsNullOrWhiteSpace(response.Summary)
                && negativeTerms.Any(t => response.Summary.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                response.Summary = null;
            }
        }
    }

    private void ApplyConservativeModeEnforcement(NutritionAnalysisResponseDto response, VisualInterpretationResult visionResult)
    {
        if (response.AnalysisMode != AnalysisMode.FrontOfPackageOnly) return;

        var profile = response.EstimatedNutritionProfile;
        bool allNull = profile == null || (!profile.CaloriesPer100g.HasValue && !profile.EstimatedSugarPer100g.HasValue
            && !profile.EstimatedProteinPer100g.HasValue && !profile.EstimatedSodiumPer100g.HasValue
            && !profile.EstimatedFatPer100g.HasValue && !profile.EstimatedFiberPer100g.HasValue);

        if (!allNull) return;
        if ((response.ConfidenceDetails?.EstimatedNutritionProfile ?? 0) > 0.5) return;

        _logger.LogWarning("[ConservativeMode] ENFORCEMENT. Category={Category}", response.Category ?? "unknown");

        // Force indeterminate classifications
        var positiveStatuses = new[] { "adequado", "bom", "recomendado", "favoravel" };
        foreach (var p in new[] { response.Classification?.Diabetic, response.Classification?.BloodPressure, response.Classification?.WeightLoss, response.Classification?.MuscleGain })
        {
            if (p != null && positiveStatuses.Any(s => p.Status?.Contains(s, StringComparison.OrdinalIgnoreCase) == true))
            {
                p.Status = "indeterminado";
                p.Reason = "Sem tabela nutricional visível, não foi possível confirmar dados nutricionais.";
            }
        }

        // Add disclaimer
        if (!string.IsNullOrWhiteSpace(response.Summary) && !response.Summary.Contains("⚠️"))
            response.Summary = "⚠️ Análise limitada: Sem tabela nutricional visível. " + response.Summary;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Persistência
    // ═══════════════════════════════════════════════════════════════════

    private async Task<Guid> PersistAnalysisAsync(NutritionAnalysisContext context, string fileName, Guid? userId, string? deviceId)
    {
        var product = new Product(context.ProductName ?? "Produto alimentício", context.Brand, barcode: null);

        var snapshot = new
        {
            Category = context.CategoryNormalized ?? context.CategoryRaw,
            ProductForm = context.ProductForm,
            DataSource = context.NutritionDataSource,
            IsInconsistent = context.IsInconsistent,
            AnalysisMode = context.PublicAnalysisMode,
            Score = (int?)context.ScoreAdjusted,
            ScoreLabel = context.ScoreLabel,
            PrincipalOffender = context.PrincipalOffender,
            Classification = context.HealthProfiles,
            ConfidenceDetails = context.ConfidenceDetails,
            Summary = context.Summary,
            VisibleClaims = context.VisibleClaims
        };
        var extractedData = JsonSerializer.Serialize(snapshot);

        var productLabel = new ProductLabel(product.Id, fileName ?? string.Empty, extractedData, DateTimeOffset.UtcNow);
        product.SetLabel(productLabel);

        var nutritionalInfo = BuildNutritionalInfo(product.Id, context.FinalNutritionProfile, context.PackageWeight);
        if (nutritionalInfo != null) product.SetNutritionalInfo(nutritionalInfo);

        var classification = MapAnalysisClassification(context.ScoreAdjusted, context.HealthProfiles);
        var confidence = MapConfidenceLevel(context.ConfidenceDetails, context.PublicAnalysisMode);

        var analysis = new ProductAnalysis(product.Id, userId, classification, confidence, context.Summary ?? string.Empty, deviceId);
        analysis.AttachProduct(product);

        foreach (var message in context.Alerts.Concat(context.Warnings).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.OrdinalIgnoreCase))
            analysis.AddAlert(new AnalysisAlert(analysis.Id, message, classification, confidence));

        await _productRepository.AddAsync(product);
        await _analysisWriteRepository.AddAsync(analysis);

        _logger.LogInformation("Analysis persisted. AnalysisId={Id}, Product={Name}", analysis.Id, context.ProductName ?? "N/A");
        return analysis.Id;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers (portados do legado)
    // ═══════════════════════════════════════════════════════════════════

    private static string Norm(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    /// <summary>
    /// Detecta se o texto bruto do OCR contém sinais de tabela nutricional,
    /// mesmo quando a qualidade é tão baixa que o parser não consegue extrair valores.
    ///
    /// Cenário: imagem escura/baixo contraste → OCR retorna texto muito garbled
    /// (ex.: "4800cl" em vez de "48kcal", "Queidade" em vez de "Quantidade").
    /// Nesse caso, o GPT-4.1 deve ser ativado como extrator primário.
    /// </summary>
    private static bool NutritionTableDetectedInRawText(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return false;

        var lower = rawText.ToLowerInvariant();

        // Sinais fortes de tabela nutricional (cada um sozinho é suficiente)
        if (lower.Contains("informação nutricional") || lower.Contains("informacao nutricional")) return true;
        if (lower.Contains("nutricional"))   return true;
        if (lower.Contains("valor energético") || lower.Contains("valor energetico"))             return true;
        if (Regex.IsMatch(lower, @"\d+\s*kcal"))    return true;
        if (lower.Contains("porção de") || lower.Contains("porcao de"))                           return true;

        // Sinais moderados: precisam de ao menos 2 para confirmar
        int signals = 0;
        if (lower.Contains("gordura"))      signals++;
        if (lower.Contains("proteína") || lower.Contains("proteina")) signals++;
        if (lower.Contains("carboidrato"))  signals++;
        if (lower.Contains("sódio") || lower.Contains("sodio"))       signals++;
        if (lower.Contains("fibra"))        signals++;
        if (lower.Contains("porção") || lower.Contains("porcao"))     signals++;
        if (Regex.IsMatch(lower, @"\d+\s*g\b"))   signals++;  // valores em gramas
        if (Regex.IsMatch(lower, @"\d+\s*mg\b"))  signals++;  // valores em mg

        return signals >= 2;
    }

    private static bool IsBeverageCategory(string norm) =>
        norm.Contains("suco") || norm.Contains("néctar") || norm.Contains("nectar")
        || norm.Contains("refresco") || norm.Contains("limonada")
        || norm.Contains("bebida à base") || norm.Contains("bebida a base");

    private static bool IsRiceCategory(string norm) => norm.Contains("arroz");

    private static bool IsStapleWithAmbiguousPreparation(string norm) =>
        norm.Contains("arroz") || norm.Contains("feijão") || norm.Contains("feijao")
        || norm.Contains("macarrão") || norm.Contains("macarrao") || norm.Contains("massa");

    private static void AddDistinct(List<string> list, string item)
    {
        if (!list.Any(x => string.Equals(x, item, StringComparison.OrdinalIgnoreCase)))
            list.Add(item);
    }

    private static string AppendBasis(string? basis, string note)
    {
        if (string.IsNullOrWhiteSpace(basis)) return note;
        if (basis.Contains(note, StringComparison.OrdinalIgnoreCase)) return basis;
        return $"{basis}. {note}";
    }

    private static bool ShouldUseCategoryAsProductName(string? productName, string? category)
    {
        if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(category)) return true;
        var normP = Norm(productName);
        var normC = Norm(category);
        if (normP == normC) return false;
        return normP is "biscoito" or "bolacha" or "arroz" or "pão" or "queijo" or "iogurte" or "achocolatado" or "cereal"
            || (!normP.Contains(' ') && normC.Contains(normP, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildCategoryAwareProductName(string? category, List<string>? claims)
    {
        if (string.IsNullOrWhiteSpace(category)) return "Produto alimentício";
        var norm = Norm(category);
        var c = claims ?? [];

        if (norm.Contains("arroz"))
        {
            var rice = c.Any(x => x.Contains("integral", StringComparison.OrdinalIgnoreCase)) || norm.Contains("integral") ? "Arroz Integral"
                : c.Any(x => x.Contains("parboilizado", StringComparison.OrdinalIgnoreCase)) || norm.Contains("parboilizado") ? "Arroz Parboilizado"
                : "Arroz Branco";
            if (c.Any(x => x.Contains("tipo 1", StringComparison.OrdinalIgnoreCase)) || norm.Contains("tipo 1")) return $"{rice} Tipo 1";
            if (c.Any(x => x.Contains("tipo 2", StringComparison.OrdinalIgnoreCase)) || norm.Contains("tipo 2")) return $"{rice} Tipo 2";
            return rice;
        }

        if (norm.Contains("feijão") || norm.Contains("feijao")) return "Feijão";
        if (norm.Contains("macarrão") || norm.Contains("macarrao") || norm.Contains("massa")) return "Macarrão";

        // Title case
        return string.Join(" ", category.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }

    private static string? DetectQualitativePrincipalOffender(string? category)
    {
        var c = Norm(category);
        if (c.Contains("achocolatado") || c.Contains("sobremesa") || c.Contains("refrigerante")) return "açúcar";
        if (c.Contains("biscoito recheado") || c.Contains("biscoito") || c.Contains("bolacha") || c.Contains("chocolate")) return "açúcar e gordura";
        if (c.Contains("queijo ralado")) return "sódio e gordura";
        if (c.Contains("queijo") || c.Contains("requeijão") || c.Contains("cream cheese")) return "gordura e sódio";
        if (c.Contains("embutido") || c.Contains("salsicha") || c.Contains("linguiça") || c.Contains("linguica")) return "sódio e gordura";
        if (c.Contains("macarrão instantâneo") || c.Contains("miojo")) return "sódio";
        if (c.Contains("salgadinho")) return "sódio e gordura";
        return null;
    }

    private static string FormatRisks(List<string> risks) =>
        string.Join(" e ", risks.Select(r => r switch
        {
            "alto_acucar" => "alto teor de açúcar",
            "alto_sodio" => "alto teor de sódio",
            "alta_gordura" => "alta gordura",
            "ultraprocessado" => "produto ultraprocessado",
            "aditivos_quimicos" => "presença de aditivos químicos",
            _ => r.Replace("_", " ")
        }).Take(3));

    private static void OverrideIfWeakerOrMissing(HealthProfileResult p, string status, string reason)
    {
        if (string.IsNullOrWhiteSpace(p.Status) || p.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase))
        { p.Status = status; p.Reason = reason; }
    }

    private static void DowngradeIfWarranted(HealthProfileResult p, string proposed, string reason)
    {
        var order = new[] { "favoravel", "adequado", "consumo_moderado", "fraco", "nao_recomendado", "nao_indicado" };
        var cur = Array.FindIndex(order, s => string.Equals(s, p.Status, StringComparison.OrdinalIgnoreCase));
        var prop = Array.FindIndex(order, s => string.Equals(s, proposed, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(p.Status) || p.Status.Equals("indeterminado", StringComparison.OrdinalIgnoreCase)
            || (cur >= 0 && prop > cur))
        { p.Status = proposed; p.Reason = reason; }
    }

    private static bool HasUsableAnalysis(NutritionAnalysisContext context) =>
        !string.IsNullOrWhiteSpace(context.CategoryNormalized ?? context.CategoryRaw)
        || HasQuantitativeData(context.FinalNutritionProfile)
        || HasUsableClassification(context.HealthProfiles);

    private static bool HasQuantitativeData(EstimatedNutritionProfileDto? p) =>
        p != null && ((p.CaloriesPer100g.HasValue || p.CaloriesPer100ml.HasValue) || p.EstimatedSugarPer100g.HasValue || p.EstimatedProteinPer100g.HasValue
            || p.EstimatedSodiumPer100g.HasValue || p.EstimatedFiberPer100g.HasValue || p.EstimatedFatPer100g.HasValue);

    private static bool HasUsableClassification(ProductClassificationDto? c) =>
        c != null && new[] { c.Diabetic?.Status, c.BloodPressure?.Status, c.WeightLoss?.Status, c.MuscleGain?.Status }
            .Any(s => !string.IsNullOrWhiteSpace(s) && !s.Equals("indeterminado", StringComparison.OrdinalIgnoreCase));

    private static NutritionalInfo? BuildNutritionalInfo(Guid productId, EstimatedNutritionProfileDto? profile, string? packageWeight)
    {
        if (profile == null || !HasQuantitativeData(profile)) return null;
        var info = new NutritionalInfo(productId);
        info.UpdateMacros(
            calories: ToDecimal(profile.CaloriesPer100g ?? profile.CaloriesPer100ml), totalFat: ToDecimal(profile.EstimatedFatPer100g),
            sodium: ToDecimal(profile.EstimatedSodiumPer100g), fiber: ToDecimal(profile.EstimatedFiberPer100g),
            sugars: ToDecimal(profile.EstimatedSugarPer100g), protein: ToDecimal(profile.EstimatedProteinPer100g));
        info.UpdateServing(profile.NutritionUnit == "ml" ? "100 ml" : "100 g", null);
        return info;
    }

    private static decimal? ToDecimal(double? v) => v.HasValue ? Convert.ToDecimal(v.Value) : null;

    private static AnalysisClassification MapAnalysisClassification(int score, ProductClassificationDto? c) => score switch
    {
        >= 80 => AnalysisClassification.Excellent,
        >= 60 => AnalysisClassification.Safe,
        >= 40 => AnalysisClassification.Moderate,
        >= 25 => AnalysisClassification.Caution,
        _ => AnalysisClassification.Avoid
    };

    private static ConfidenceLevel MapConfidenceLevel(ConfidenceDetailsDto? d, AnalysisMode mode)
    {
        var vals = new[] { d?.ProductIdentification, d?.VisibleClaimsExtraction, d?.EstimatedNutritionProfile, d?.Classification }
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (vals.Count == 0) return mode == AnalysisMode.FullNutritionLabel ? ConfidenceLevel.Medium : ConfidenceLevel.Low;
        var avg = vals.Average();
        return avg >= 0.8 ? ConfidenceLevel.High : avg >= 0.55 ? ConfidenceLevel.Medium : ConfidenceLevel.Low;
    }

    /// <summary>
    /// Conta quantos valores críticos foram extraídos do parser
    /// </summary>
    private static int CountExtractedValues(LabelWise.Domain.Entities.ParsedNutritionResult parsed)
    {
        int count = 0;
        if (parsed.Calories.HasValue) count++;
        if (parsed.Protein.HasValue) count++;
        if (parsed.Fat.HasValue) count++;
        if (parsed.Carbs.HasValue) count++;
        if (parsed.Sodium.HasValue) count++;
        return count;
    }

    /// <summary>
    /// Sanitiza o valor de sódio extraído pelo OCR estruturado.
    ///
    /// O <see cref="StructuredTableOcrParser"/> é propenso a erros de atribuição de coluna
    /// para sódio em tabelas com múltiplas colunas (100ml / 100g / porção / %VD).
    /// Um valor &lt; 5 mg com macros presentes (carboidratos ou proteína) é evidência
    /// de leitura da coluna errada — retorna null para que o GPT-4.1 ou o fallback
    /// por categoria forneçam o valor correto.
    /// </summary>
    private static double? SanitizeOcrSodium(double? ocrSodium, double? carbs, double? protein)
    {
        if (!ocrSodium.HasValue)
            return null;

        // Sódio plausível — aceitar diretamente
        if (ocrSodium.Value >= 5)
            return ocrSodium;

        // Sódio < 5 mg: suspeito apenas se o produto tem macros que provam que é alimento real
        bool hasMacros = (carbs.HasValue && carbs.Value > 2) || (protein.HasValue && protein.Value > 0.5);
        if (hasMacros)
            return null; // remove o valor incorreto → GPT/fallback assume

        return ocrSodium; // produto sem macros (ex.: água) pode ter sódio muito baixo
    }

    /// <summary>
    /// Conta quantos valores críticos foram extraídos do parser estruturado
    /// </summary>
    private static int CountExtractedValuesStructured(StructuredNutritionResult parsed)
    {
        int count = 0;
        if (parsed.Calories.HasValue) count++;
        if (parsed.Protein.HasValue) count++;
        if (parsed.Fat.HasValue) count++;
        if (parsed.Carbs.HasValue) count++;
        if (parsed.Sodium.HasValue) count++;
        return count;
    }
}
public enum ColumnType
{
    Per100Ml,
    Per100g,
    Portion
}

public class DetectedColumn
{
    public ColumnType Type { get; set; }
}

public class ParsedNutritionResult
{
    public EstimatedNutritionProfileDto Profile { get; set; } = new();
}
