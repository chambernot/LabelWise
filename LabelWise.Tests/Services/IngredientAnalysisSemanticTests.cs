using LabelWise.Application.DTOs.IngredientAnalysis;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Models.IngredientAnalysis;
using LabelWise.Infrastructure.Services;
using LabelWise.Infrastructure.Services.IngredientAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LabelWise.Tests.Services;

public sealed class IngredientAnalysisSemanticTests
{
    [Fact]
    public void MayContainMilk_ShouldNotBlockVeganOrMarkLactoseAsContained()
    {
        var context = new IngredientAnalysisContext
        {
            VisionExtraction = new IngredientExtractionResult
            {
                Claims = ["ALÉRGICOS: PODE CONTER LEITE"]
            },
            OcrText = "Ingredientes: água, sucralose. ALÉRGICOS: PODE CONTER LEITE. NÃO CONTÉM GLÚTEN."
        };

        var classifier = new IngredientClassifier();
        var ingredients = classifier.ClassifyIngredients(context);
        var claims = classifier.ClassifyClaims(context);
        var allergenRisks = new AllergenDetector().DetectRisks(context, ingredients, claims);
        var profiles = new DietProfileEngine().Evaluate(ingredients, allergenRisks, claims);

        Assert.DoesNotContain(allergenRisks, risk => risk.Name == "leite" && risk.RiskType == "contains");
        Assert.Contains(allergenRisks, risk => risk.Name == "leite" && risk.RiskType == "may_contain");
        Assert.False(profiles.Vegan.Compatible);
        Assert.Equal(DietCompatibilityStatuses.Uncertain, profiles.Vegan.CompatibilityStatus);
        Assert.Contains(EvidenceType.CrossContamination, profiles.Vegan.EvidenceTypes);
        Assert.False(profiles.LactoseFree.Compatible);
        Assert.Equal(DietCompatibilityStatuses.Uncertain, profiles.LactoseFree.CompatibilityStatus);
    }

    [Fact]
    public void ArtificialSweetenerReferenceToSugarPower_ShouldNotDetectAddedSugar()
    {
        var context = new IngredientAnalysisContext
        {
            OcrText = "Ingredientes: água, edulcorantes: ciclamato de sódio, sacarina sódica. 10 gotas equivalem ao poder adoçante do açúcar. NÃO CONTÉM GLÚTEN."
        };

        var classifier = new IngredientClassifier();
        var ingredients = classifier.ClassifyIngredients(context);
        var claims = classifier.ClassifyClaims(context);
        var profiles = new DietProfileEngine().Evaluate(ingredients, Array.Empty<AllergenRiskDto>(), claims);

        Assert.DoesNotContain(ingredients, ingredient => ingredient.Contains("açúcar", StringComparison.OrdinalIgnoreCase));
        Assert.False(profiles.DiabeticFriendly.Compatible);
        Assert.Equal(DietCompatibilityStatuses.Uncertain, profiles.DiabeticFriendly.CompatibilityStatus);
    }

    [Fact]
    public void PartialNutritionTableText_ShouldKeepMediumQualityAndPartialCompleteness()
    {
        var confidenceEngine = new IngredientConfidenceEngine();
        var completenessEvaluator = new AnalysisCompletenessEvaluator();

        var ocrConfidence = confidenceEngine.EvaluateImageQuality(preparedSuccessfully: true, rawTextLength: 140);
        var completeness = completenessEvaluator.Evaluate(ocrConfidence, rawTextLength: 140, ingredientCount: 2, claimCount: 0, warnings: []);

        Assert.Equal("medium", ocrConfidence);
        Assert.Equal("partial", completeness.Status);
    }

    [Fact]
    public void FragmentedOcrIngredients_ShouldMergeAndRemoveIncompleteDuplicates()
    {
        var context = new IngredientAnalysisContext
        {
            OcrText = "Ingredientes: edulcorantes: ciclamato de, edulcorantes: ciclamato de sódio, água. NÃO CONTÉM GLÚTEN."
        };

        var ingredients = new IngredientClassifier().ClassifyIngredients(context);

        Assert.Contains("ciclamato de sódio", ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("ciclamato de", ingredients, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContainsLactoseClaim_ShouldLikelyNotBlockVeganWithoutExplicitAnimalIngredient()
    {
        var context = new IngredientAnalysisContext
        {
            OcrText = "Ingredientes: água, aroma natural. ALÉRGICOS: CONTÉM LACTOSE."
        };

        var classifier = new IngredientClassifier();
        var ingredients = classifier.ClassifyIngredients(context);
        var claims = classifier.ClassifyClaims(context);
        var allergenRisks = new AllergenDetector().DetectRisks(context, ingredients, claims);
        var profiles = new DietProfileEngine().Evaluate(ingredients, allergenRisks, claims);

        Assert.Equal(DietCompatibilityStatuses.LikelyNotCompatible, profiles.Vegan.CompatibilityStatus);
        Assert.Equal(CompatibilityStatus.LikelyNotCompatible, profiles.Vegan.Status);
        Assert.Contains(EvidenceType.ClaimDetected, profiles.Vegan.EvidenceTypes);
    }

    [Fact]
    public void OcrCleaner_ShouldNotExposeUnknownMayContainGarbageAsClaim()
    {
        var cleaner = new OcrSemanticCleaner();
        var cleaned = cleaner.Clean("PODE CONTER ATTINY AVEIALAVELAS");
        var context = new IngredientAnalysisContext { OcrText = cleaned.Text };

        var claims = new IngredientClassifier().ClassifyClaims(context);

        Assert.DoesNotContain(claims, claim => claim.Contains("ATTINY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SemanticPipeline_ShouldReconstructNoisyMayContainClaim()
    {
        var cleaner = new OcrCleaningService(NullLogger<OcrCleaningService>.Instance);
        var reconstruction = new SemanticReconstructionService(NullLogger<SemanticReconstructionService>.Instance);
        var extractor = new RegulatoryClaimExtractor(NullLogger<RegulatoryClaimExtractor>.Instance);

        var cleaned = cleaner.Clean("PODE CONTER ATTINY AVEIALAVELAS");
        var reconstructed = reconstruction.Reconstruct(cleaned.Text);
        var claims = extractor.Extract(reconstructed.Text);

        Assert.Contains("PODE CONTER aveia", claims.MayContainClaims, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("PODE CONTER avelãs", claims.MayContainClaims, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(claims.AllClaims, claim => claim.Contains("ATTINY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SemanticClaimResolver_ShouldPreferContainsOverMayContainForSameAllergen()
    {
        var context = new IngredientAnalysisContext
        {
            OcrText = "ALÉRGICOS: CONTÉM GLÚTEN. PODE CONTER GLÚTEN. PODE CONTER LEITE. CONTÉM LACTOSE."
        };
        var classifier = new IngredientClassifier();
        var claims = classifier.ClassifyClaims(context);
        var risks = new AllergenDetector().DetectRisks(context, [], claims);
        var resolver = new SemanticClaimResolver(NullLogger<SemanticClaimResolver>.Instance);

        var resolved = resolver.Resolve(claims, risks);

        Assert.Contains(resolved.AllergenRisks, risk => risk.Name == "glúten" && risk.RiskType == "contains");
        Assert.DoesNotContain(resolved.AllergenRisks, risk => risk.Name == "glúten" && risk.RiskType is "may_contain" or "cross_contamination");
        Assert.Contains(resolved.AllergenRisks, risk => risk.Name == "leite" && risk.RiskType == "contains");
        Assert.DoesNotContain(resolved.AllergenRisks, risk => risk.Name == "leite" && risk.RiskType is "may_contain" or "cross_contamination");
        Assert.DoesNotContain(resolved.Claims, claim => claim.Equals("PODE CONTER GLÚTEN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IngredientParser_ShouldDetectPartialMacadamiaAndNormalizeIt()
    {
        var parser = new IngredientParserService(
            new IngredientClassifier(),
            new IngredientNormalizer(),
            NullLogger<IngredientParserService>.Instance);
        var context = new IngredientAnalysisContext { OcrText = "MACADAMIAS" };

        var result = parser.Parse(context);

        Assert.Contains("Macadâmia", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        var normalized = Assert.Single(result.NormalizedIngredients, item => item.Normalized == "Macadâmia");
        Assert.Equal("tree_nut", normalized.Category);
    }

    [Fact]
    public void RegulatoryClaimExtractor_ShouldKeepContainsMayContainAndFreeFromSeparated()
    {
        var extractor = new RegulatoryClaimExtractor(NullLogger<RegulatoryClaimExtractor>.Instance);

        var result = extractor.Extract("ALÉRGICOS: CONTÉM GLÚTEN. PODE CONTER AVEIA, AVELÃS. NÃO CONTÉM LACTOSE.");

        Assert.Contains("CONTÉM glúten", result.ContainsClaims, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("PODE CONTER aveia", result.MayContainClaims, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("PODE CONTER avelãs", result.MayContainClaims, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("NÃO CONTÉM lactose", result.FreeFromClaims, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SemanticSafetyValidator_ShouldKeepMilkDerivativeAsInferenceOnly()
    {
        var context = new IngredientAnalysisContext
        {
            OcrText = "Ingredientes: derivados de leite, aroma natural."
        };
        var structured = new StructuredTextLayer(NullLogger<StructuredTextLayer>.Instance).Build(context.OcrText);
        var classifier = new IngredientClassifier();
        var ingredients = classifier.ClassifyIngredients(context);
        var claims = classifier.ClassifyClaims(context);
        var risks = new AllergenDetector().DetectRisks(context, ingredients, claims);

        var safety = new SemanticSafetyValidator(NullLogger<SemanticSafetyValidator>.Instance).Validate(claims, risks, structured);

        Assert.DoesNotContain(safety.Claims, claim => claim.Contains("CONTÉM LACTOSE", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(safety.AllergenRisks, risk => risk.Name == "lactose" && risk.RiskType == "contains");
        Assert.Contains(safety.SemanticInferences, inference => inference.Type == "possible_lactose" && inference.Confidence == "low");
    }

    [Fact]
    public void StructuredTextLayer_ShouldClassifyNutritionDisclaimerAsNutritionTableNotClaim()
    {
        var structured = new StructuredTextLayer(NullLogger<StructuredTextLayer>.Instance)
            .Build("INFORMAÇÃO NUTRICIONAL. NÃO CONTÉM quantidades significativas de gorduras trans. Ingredientes: água, aveia.");
        var extractor = new RegulatoryClaimExtractor(NullLogger<RegulatoryClaimExtractor>.Instance);

        var claims = extractor.Extract(structured);

        Assert.Contains(structured.Blocks, block => block.RegionType == TextRegionType.NutritionTable && block.Text.Contains("quantidades significativas", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(structured.Blocks, block => block.RegionType == TextRegionType.RegulatoryClaim && block.Text.Contains("quantidades significativas", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(claims.AllClaims, claim => claim.Contains("quantidades significativas", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StructuredIngredientParser_ShouldIgnoreManufacturerRegion()
    {
        var structured = new StructuredTextLayer(NullLogger<StructuredTextLayer>.Instance)
            .Build("Ingredientes: água, aveia. PRODUZIDO E ENVASILHADO POR Indústria Exemplo Ltda. CNPJ 00.000.000/0001-00.");
        var parser = new IngredientParserService(
            new IngredientClassifier(),
            new IngredientNormalizer(),
            NullLogger<IngredientParserService>.Instance);

        var result = parser.Parse(new IngredientAnalysisContext(), structured);

        Assert.Contains("água", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("aveia", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Ingredients, ingredient => ingredient.Contains("produzido", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Ingredients, ingredient => ingredient.Contains("cnpj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DietProfilesWithoutExplicitClaims_ShouldCapPositiveCompatibilityConfidenceAtLow()
    {
        var profiles = new DietProfileEngine().Evaluate(
            ["água", "aveia"],
            Array.Empty<AllergenRiskDto>(),
            []);

        Assert.Equal("low", profiles.Vegan.Confidence);
        Assert.Equal("low", profiles.Vegetarian.Confidence);
        Assert.Equal("low", profiles.LactoseFree.Confidence);
        Assert.Equal(DietCompatibilityStatuses.LikelyCompatible, profiles.Vegan.CompatibilityStatus);
        Assert.Contains("Não foram identificados ingredientes animais", profiles.Vegan.Reasons[0]);
    }

    [Fact]
    public void ProcessingClassifier_ShouldExposeProcessingScore()
    {
        var classifier = new ProcessingLevelClassifier();
        var normalized = new IngredientNormalizer().Normalize(["água", "aromatizante", "emulsificante", "açúcar", "conservante"]);

        var result = classifier.Classify(["água", "aromatizante", "emulsificante", "açúcar", "conservante"], normalized, []);

        Assert.InRange(result.ProcessingScore, 51, 100);
        Assert.Equal("ultra_processed", result.Level);
    }

    [Fact]
    public void AssistantSummary_ShouldUseHumanReadableAllergenSummary()
    {
        var risks = new List<AllergenRiskDto>
        {
            new() { Name = "glúten", RiskType = "contains", AllergenSeverity = new AllergenSeverityDto { RegulatoryLevel = "high" } },
            new() { Name = "leite", RiskType = "contains", AllergenSeverity = new AllergenSeverityDto { RegulatoryLevel = "high" } },
            new() { Name = "castanhas", RiskType = "may_contain", AllergenSeverity = new AllergenSeverityDto { RegulatoryLevel = "high" } }
        };

        var summary = new AssistantSummaryBuilder().Build(
            new DietProfilesDto(),
            risks,
            [],
            new ProcessingLevelDto { Value = "processed" },
            new LabelWise.Application.DTOs.Nutrition.IngredientContextDto(),
            [],
            [],
            new AnalysisCompletenessDto { Status = "complete" },
            "medium");

        Assert.StartsWith("O produto contém glúten e leite.", summary.Text);
        Assert.Contains("possível contaminação cruzada com castanhas", summary.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IngredientRecovery_ShouldRecoverFromAnchor()
    {
        var structured = new StructuredTextLayer(NullLogger<StructuredTextLayer>.Instance)
            .Build("INFORMAÇÃO NUTRICIONAL ... Valor Energético 250kcal. INGR.: ÁGUA, AÇÚCAR, SUCO CONCENTRADO.");
        var context = new IngredientAnalysisContext
        {
            OcrText = "INFORMAÇÃO NUTRICIONAL ... Valor Energético 250kcal. INGR.: ÁGUA, AÇÚCAR, SUCO CONCENTRADO."
        };
        var parser = new IngredientParserService(
            new IngredientClassifier(),
            new IngredientNormalizer(),
            NullLogger<IngredientParserService>.Instance);
        var recovery = new IngredientRecoveryEngine(
            new IngredientAnchorDetector(),
            new IngredientGrammarValidator(),
            new IngredientRegionPromoter(),
            NullLogger<IngredientRecoveryEngine>.Instance);

        var result = parser.ParseWithRecovery(context, structured, recovery);

        Assert.True(result.Ingredients.Count >= 2, $"Expected at least 2 ingredients, found {result.Ingredients.Count}: {string.Join(", ", result.Ingredients)}");
        Assert.Contains(result.Ingredients, ingredient => ingredient.Contains("ÁGUA", StringComparison.OrdinalIgnoreCase) || ingredient.Contains("agua", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Ingredients, ingredient => ingredient.Contains("AÇÚCAR", StringComparison.OrdinalIgnoreCase) || ingredient.Contains("acucar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProcessingSignalRecovery_ShouldRecoverScoreFromMarkers()
    {
        var signalRecovery = new ProcessingSignalRecovery();
        var ingredients = new List<string> { "água", "aromatizante", "acidulante", "corante" };

        var recoveredScore = signalRecovery.RecoverProcessingScore(0, ingredients);

        Assert.InRange(recoveredScore, 30, 100);
    }

    [Fact]
    public void IngredientPurification_ShouldRemoveNutritionTableLeaks()
    {
        var purification = new IngredientPurificationLayer(
            new IngredientNoiseFilter(),
            new NutritionLeakBlocker(),
            new IngredientSemanticValidator());

        var result = purification.Purify([
            "7 6 Açúcares 8",
            "2 14 28 Sodio (mg) 4",
            "1 8",
            "Gorduras totais",
            "Gorduras saturadas",
            "Açúcar",
            "Acidulante ácido cítrico",
            "Aromatizante"
        ]);

        Assert.DoesNotContain(result.Ingredients, ingredient => ingredient.Contains("Açúcares", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Ingredients, ingredient => ingredient.Contains("Sodio", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Ingredients, ingredient => ingredient.Contains("Gorduras", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Ingredients, ingredient => ingredient == "1 8");
        Assert.Contains("Açúcar", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Acidulante ácido cítrico", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Aromatizante", result.Ingredients, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseWithRecovery_ShouldPurifyRecoveredNutritionNoise()
    {
        var structured = new StructuredTextLayer(NullLogger<StructuredTextLayer>.Instance)
            .Build("INFORMAÇÃO NUTRICIONAL 7 6 Açúcares 8 2 14 28 Sódio (mg) 4. INGR.: ÁGUA, AÇÚCAR, AROMATIZANTE, GORDURAS TOTAIS.");
        var context = new IngredientAnalysisContext
        {
            OcrText = "INFORMAÇÃO NUTRICIONAL 7 6 Açúcares 8 2 14 28 Sódio (mg) 4. INGR.: ÁGUA, AÇÚCAR, AROMATIZANTE, GORDURAS TOTAIS."
        };
        var parser = new IngredientParserService(
            new IngredientClassifier(),
            new IngredientNormalizer(),
            NullLogger<IngredientParserService>.Instance);
        var recovery = new IngredientRecoveryEngine(
            new IngredientAnchorDetector(),
            new IngredientGrammarValidator(),
            new IngredientRegionPromoter(),
            NullLogger<IngredientRecoveryEngine>.Instance);

        var result = parser.ParseWithRecovery(context, structured, recovery);

        Assert.Contains(result.Ingredients, ingredient => ingredient.Contains("ÁGUA", StringComparison.OrdinalIgnoreCase) || ingredient.Contains("agua", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Ingredients, ingredient => ingredient.Contains("AÇÚCAR", StringComparison.OrdinalIgnoreCase) || ingredient.Contains("acucar", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("AROMATIZANTE", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Ingredients, ingredient => ingredient.Contains("GORDURAS", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.NormalizedIngredients, ingredient => ingredient.Raw.Contains("Sódio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StructuredTextLayer_ShouldTerminateNutritionBlockWhenIngredientAnchorAppears()
    {
        var structured = new StructuredTextLayer(NullLogger<StructuredTextLayer>.Instance)
            .Build("INFORMAÇÃO NUTRICIONAL 7 6 Açúcares 8 2 14 28 Sódio (mg) 4 INGR.: ÁGUA, AÇÚCAR, SUCO CONCENTRADO DE MAÇÃ.");

        Assert.Contains(structured.Blocks, block => block.RegionType == TextRegionType.IngredientList && block.Text.Contains("ÁGUA", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(structured.Blocks, block => block.RegionType == TextRegionType.NutritionTable && block.Text.Contains("INGR", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OcrSemanticSanitizer_ShouldRemoveNumericResiduesAndRepairFoodTokens()
    {
        var sanitizer = new OCRSemanticSanitizer();

        var appleJuice = sanitizer.SanitizeIngredient("SUCO CONCENTRADO DE MAÇÃ 2");
        var antioxidant = sanitizer.SanitizeIngredient("ENTIOXIDANTE");
        var brokenParenthesis = sanitizer.SanitizeIngredient("(L-)");

        Assert.Equal("SUCO CONCENTRADO DE MAÇÃ", appleJuice.Text);
        Assert.Equal("ANTIOXIDANTE", antioxidant.Text);
        Assert.True(string.IsNullOrWhiteSpace(brokenParenthesis.Text));
        Assert.NotEmpty(appleJuice.Corrections);
        Assert.NotEmpty(antioxidant.Corrections);
    }

    [Fact]
    public void CompoundIngredientParser_ShouldSplitCategorySubstancePairs()
    {
        var parts = new CompoundIngredientParser().Parse("ESTABILIZANTE GOMA GUAR E ANTIOXIDANTE ÁCIDO ASCÓRBICO");

        Assert.Contains(parts, part => part.Category == "stabilizer" && part.Ingredient.Equals("GOMA GUAR", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parts, part => part.Category == "antioxidant" && part.Ingredient.Equals("ÁCIDO ASCÓRBICO", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IngredientParser_ShouldSplitCompoundIngredientsAndPreserveProcessingSignals()
    {
        var parser = new IngredientParserService(
            new IngredientClassifier(),
            new IngredientNormalizer(),
            NullLogger<IngredientParserService>.Instance);
        var context = new IngredientAnalysisContext
        {
            OcrText = "Ingredientes: água, estabilizante goma guar e antioxidante ácido ascórbico, suco concentrado de maçã 2."
        };

        var result = parser.Parse(context);
        var processing = new ProcessingLevelClassifier().Classify(result.Ingredients, result.NormalizedIngredients, []);

        Assert.Contains(result.Ingredients, ingredient => ingredient.Contains("goma guar", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Ingredients, ingredient => ingredient.Contains("ácido ascórbico", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Ingredients, ingredient => ingredient.Equals("suco concentrado de maçã", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Ingredients, ingredient => ingredient.Contains("maçã 2", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(processing.ProcessingScore, 30, 100);
    }

    [Fact]
    public void ConfidenceCalibration_ShouldDowngradeRepairedOrPartialEvidence()
    {
        var calibration = new ConfidenceCalibrationEngine();
        var completeness = new AnalysisCompletenessDto { Status = "partial", Reasons = ["Imagem parcial."] };

        var reliability = calibration.Evaluate(
            "medium",
            completeness,
            ["ANTIOXIDANTE"],
            [new OcrCorrectionDto { Original = "ENTIOXIDANTE", Corrected = "ANTIOXIDANTE" }],
            ["Imagem inclinada."],
            boundaryLeakDetected: true,
            hasPartialReconstruction: true);

        Assert.Equal("low", calibration.ApplyCeiling("high", repaired: true, inferred: false, partial: true, boundaryLeak: true));
        Assert.NotEqual("high", reliability.Confidence);
    }

    [Fact]
    public void IngredientProductionValidator_ShouldRejectNutritionAndOcrContamination()
    {
        var structured = new StructuredTextLayer(NullLogger<StructuredTextLayer>.Instance)
            .Build("INFORMAÇÃO NUTRICIONAL 7 6 Açúcares 8. INGR.: ÁGUA, AÇÚCAR, ENTIOXIDANTE.");

        var validation = new IngredientProductionValidator().Validate(
            ["7 6 Açúcares 8", "ÁGUA", "ENTIOXIDANTE", "(L-)"],
            structured);

        Assert.Contains("ÁGUA", validation.SanitizedIngredients, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ANTIOXIDANTE", validation.SanitizedIngredients, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(validation.SanitizedIngredients, ingredient => ingredient.Contains("Açúcares", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(validation.SanitizedIngredients, ingredient => ingredient.Contains("L-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FoodCompatibilityEngine_ShouldConservativelyBlockLactoseFreeOnMayContainMilk()
    {
        var engine = new FoodCompatibilityEngine(new DietProfileEngine());

        var profiles = engine.Evaluate(new FoodCompatibilityContext
        {
            Ingredients = ["água", "aroma natural"],
            Claims = ["PODE CONTER LEITE"],
            AllergenRisks = [new AllergenRiskDto { Name = "leite", RiskType = "may_contain", Confidence = "medium", Evidence = ["PODE CONTER LEITE"] }],
            RawOcrText = "Ingredientes: água, aroma natural. ALÉRGICOS: PODE CONTER LEITE."
        });

        Assert.False(profiles.LactoseFree.Compatible);
        Assert.Equal(DietCompatibilityStatuses.Uncertain, profiles.LactoseFree.CompatibilityStatus);
        Assert.False(profiles.Vegan.Compatible);
    }

    [Fact]
    public void IngredientTextSanitizer_ShouldRejectCorruptedOcrButKeepFoodTerms()
    {
        var sanitizer = new IngredientTextSanitizer();

        var corrupted = sanitizer.Sanitize("Beton užtrata");
        var misspelled = sanitizer.Sanitize("creme de bele");
        var valid = sanitizer.Sanitize("creme de leite");

        Assert.False(corrupted.Accepted);
        Assert.False(misspelled.Accepted);
        Assert.True(valid.Accepted);
    }

    [Fact]
    public void FoodIngredientTokenizer_ShouldSplitFoodConjunctionsAndIgnoreGenericDerivatives()
    {
        var tokenizer = new FoodIngredientTokenizer();

        var pistacheTrigo = tokenizer.Tokenize("PISTACHE E TRIGO");
        var leiteSoja = tokenizer.Tokenize("LEITE, SOJA E DERIVADOS");

        Assert.Contains(pistacheTrigo, token => token.Text.Equals("PISTACHE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(pistacheTrigo, token => token.Text.Equals("TRIGO", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(pistacheTrigo, token => token.Text.Contains("PISTACHE E TRIGO", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(leiteSoja, token => token.Text.Equals("LEITE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(leiteSoja, token => token.Text.Equals("SOJA", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(leiteSoja, token => token.Text.Equals("DERIVADOS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FoodOcrCleaningEngine_ShouldRemoveNonFoodNoiseAndRepairKnownOcrCorruption()
    {
        var engine = new FoodOcrCleaningEngine(
            new OcrCleaningService(NullLogger<OcrCleaningService>.Instance),
            new SemanticReconstructionService(NullLogger<SemanticReconstructionService>.Instance),
            new StructuredTextLayer(NullLogger<StructuredTextLayer>.Instance),
            new SemanticDeduplicationEngine(),
            NullLogger<FoodOcrCleaningEngine>.Instance);

        var result = engine.Clean("Ingredientes: água, NINGIIEE DERIVADOS DE LEITE. SAC 0800 123 456. www.exemplo.com");

        Assert.Contains("DERIVADOS DE LEITE", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SAC 0800", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("www", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.StructuredText.Blocks, block => block.RegionType == TextRegionType.IngredientList);
    }

    [Fact]
    public void FoodCanonicalizationEngine_ShouldReturnStableCanonicalKeys()
    {
        var engine = new FoodCanonicalizationEngine();

        Assert.Equal("dairy_protein", engine.Canonicalize("proteína láctea"));
        Assert.Equal("cashew", engine.Canonicalize("castanha-de-caju"));
        Assert.Equal("vitamin_c", engine.Canonicalize("ácido ascórbico"));
        Assert.Equal("processed_fat", engine.Canonicalize("gordura vegetal interesterificada"));
    }

    [Fact]
    public void IngredientNormalizer_ShouldReturnRiskAndAnimalOriginMetadata()
    {
        var normalized = new IngredientNormalizer().Normalize(["leite pasteurizado", "fermento lácteo", "maltodextrina", "goma xantana", "INS 223"]);

        Assert.Contains(normalized, item => item.Raw == "leite pasteurizado" && item.Normalized == "leite" && item.AnimalOrigin && item.DietaryRisk == "lactose_milk");
        Assert.Contains(normalized, item => item.Raw == "fermento lácteo" && item.Normalized == "leite" && item.AnimalOrigin);
        Assert.Contains(normalized, item => item.Raw == "maltodextrina" && item.Category == "processed_carbohydrate");
        Assert.Contains(normalized, item => item.Raw == "goma xantana" && item.Category == "stabilizer");
        Assert.Contains(normalized, item => item.Raw == "INS 223" && item.Category == "preservative");
    }

    [Fact]
    public void IngredientPurification_ShouldDropMarketingPrefixBeforeIngredientAnchor()
    {
        // Cenário real reportado em produção: OCR junta uma frase de marketing/explicação
        // com o anchor "INGREDIENTES:" e a primeira palavra real da lista, gerando o
        // candidato "açúcar. INGREDIENTES: água". Não pode haver vazamento estrutural.
        var purification = new IngredientPurificationLayer(
            new IngredientNoiseFilter(),
            new NutritionLeakBlocker(),
            new IngredientSemanticValidator());

        var result = purification.Purify([
            "açúcar. INGREDIENTES: água",
            "equivale ao poder adoçante do açúcar. INGREDIENTES: sorbitol",
            "ÁGUA, SUCRALOSE. INFORMAÇÃO NUTRICIONAL Valor energético 0 kcal",
            "ÁCIDO BENZOICO CONSERVAR EM LUGAR FRESCO"
        ]);

        Assert.Contains("água", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("sorbitol", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(result.Ingredients, ingredient => ingredient.Equals("ÁGUA", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Ingredients, ingredient => ingredient.Equals("SUCRALOSE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Ingredients, ingredient => ingredient.Equals("ÁCIDO BENZOICO", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(result.Ingredients, ingredient =>
            ingredient.Contains("INGREDIENTES", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Ingredients, ingredient =>
            ingredient.Contains("INFORMAÇÃO", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Ingredients, ingredient =>
            ingredient.Contains("CONSERVAR", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Ingredients, ingredient =>
            ingredient.Contains("equivale", StringComparison.OrdinalIgnoreCase));
        // O fragmento "açúcar." vinha de uma frase de marketing antes do anchor — não é ingrediente.
        Assert.DoesNotContain(result.Ingredients, ingredient =>
            ingredient.Trim(' ', '.', ',').Equals("açúcar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NutritionConsistencyValidator_ShouldRejectImpossibleNutritionValues()
    {
        var validator = new NutritionConsistencyValidator();

        var result = validator.Validate(new EstimatedNutritionProfileDto
        {
            CaloriesPer100g = 80,
            EstimatedCarbsPer100g = 20,
            EstimatedSugarPer100g = 25,
            EstimatedAddedSugarPer100g = 30,
            EstimatedFatPer100g = 5,
            EstimatedSaturatedFatPer100g = 8,
            EstimatedProteinPer100g = 20,
            EstimatedFiberPer100g = 80,
            ServingAmount = 0
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Açúcar total maior", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("Gordura saturada maior", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("Soma nutricional", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("Porção", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PostOcrPipeline_ShouldRespectNegationAndNotReturnContainsGluten()
    {
        var structured = new StructuredTextLayer(NullLogger<StructuredTextLayer>.Instance)
            .Build("Ingredientes: água, sorbitol, sucralose. ALÉRGICOS: NÃO CONTÉM GLÚTEN.");
        var claims = new RegulatoryClaimExtractor(NullLogger<RegulatoryClaimExtractor>.Instance).Extract(structured).AllClaims;
        var context = new IngredientAnalysisContext
        {
            OcrText = string.Join("\n", structured.Blocks.Select(block => block.Text)),
            VisionExtraction = new IngredientExtractionResult { Claims = claims }
        };

        var risks = new AllergenDetector().DetectRisks(context, [], claims);
        var detectedClaims = new ClaimDetector().Detect(claims);

        Assert.Contains("NÃO CONTÉM glúten", claims, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(claims, claim => claim.Equals("CONTÉM glúten", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(detectedClaims, claim => claim.Type == "gluten_free");
        Assert.DoesNotContain(risks, risk => risk.Name == "glúten" && risk.RiskType == "contains");
    }

    [Fact]
    public void DedicatedIngredientParser_ShouldPreserveLongSweetenerListAndExcludeClaims()
    {
        var structured = new StructuredTextLayer(NullLogger<StructuredTextLayer>.Instance)
            .Build("INGREDIENTES: água, sorbitol, edulcorantes: ciclamato de sódio, sacarina sódica, sucralose, conservantes: ácido benzoico, metilparabeno. NÃO CONTÉM GLÚTEN. INFORMAÇÃO NUTRICIONAL carboidratos 0g sódio 10mg.");
        var parser = new IngredientParserService(
            new IngredientClassifier(),
            new IngredientNormalizer(),
            NullLogger<IngredientParserService>.Instance);

        var result = parser.Parse(new IngredientAnalysisContext(), structured);

        Assert.Contains("água", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("sorbitol", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(result.Ingredients, ingredient => ingredient.Contains("ciclamato de sódio", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Ingredients, ingredient => ingredient.Contains("sacarina sódica", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("sucralose", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(result.Ingredients, ingredient => ingredient.Contains("ácido benzoico", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("metilparabeno", result.Ingredients, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Ingredients, ingredient => ingredient.Contains("glúten", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Ingredients, ingredient => ingredient.Contains("carboidratos", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Ingredients, ingredient => ingredient.Contains("sódio 10mg", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StructuredBlocks_ShouldExposeSemanticRegionsAndKeepNutritionOutOfIngredients()
    {
        var structured = new StructuredTextLayer(NullLogger<StructuredTextLayer>.Instance)
            .Build("INFORMAÇÃO NUTRICIONAL carboidratos 0g sódio 10mg. INGREDIENTES: água, sorbitol, sucralose. PODE CONTER LEITE.");

        Assert.Contains(structured.Blocks, block => block.SemanticRegion == "NUTRITION_BLOCK");
        Assert.Contains(structured.Blocks, block => block.SemanticRegion == "INGREDIENTS_BLOCK");
        Assert.Contains(structured.Blocks, block => block.SemanticRegion == "CLAIMS_BLOCK");
        Assert.DoesNotContain(structured.Blocks.Where(block => block.RegionType == TextRegionType.IngredientList), block => block.Text.Contains("carboidratos", StringComparison.OrdinalIgnoreCase));
    }
}
