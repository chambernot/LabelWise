// ═══════════════════════════════════════════════════════════════════════════════
// MULTIDIMENSIONAL CONFIDENCE SYSTEM - USAGE EXAMPLES
// ═══════════════════════════════════════════════════════════════════════════════
// Este arquivo demonstra o uso do sistema de confiança multidimensional

using LabelWise.Application.Confidence;
using LabelWise.Application.Parsing;
using LabelWise.Application.QualityGate;

namespace LabelWise.Examples;

public class MultidimensionalConfidenceExamples
{
    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 1: Cálculo básico de confiança
    // ═══════════════════════════════════════════════════════════════════════
    
    public void BasicConfidenceCalculation()
    {
        // Dados de entrada
        var parseResult = new IngredientAllergenParseResult
        {
            ProductName = "Biscoito Recheado Oreo",
            Brand = "Mondelez",
            Ingredients = ["farinha de trigo", "açúcar", "gordura vegetal", "cacau"],
            Allergens = ["glúten", "leite"],
            Nutrition = new NutritionInfo
            {
                Calories = 150,
                TotalFat = 7,
                SaturatedFat = 3,
                Sugars = 12,
                Sodium = 100,
                Protein = 2
            }
        };

        // Avaliar qualidade do OCR
        var ocrAssessor = new OcrQualityAssessor();
        var ocrMetrics = ocrAssessor.AssessQuality(
            "INGREDIENTES: FARINHA DE TRIGO AÇÚCAR GORDURA VEGETAL CACAU...",
            confidence: 0.87);

        // Avaliar qualidade do parsing
        var parsingAssessor = new ParsingQualityAssessor();
        var parsingMetrics = parsingAssessor.AssessQuality(parseResult);

        // Calcular confiança multidimensional
        var calculator = new MultidimensionalConfidenceCalculator();
        var confidence = calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

        // Resultados
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("📊 CONFIANÇA MULTIDIMENSIONAL");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine($"🏷️  Identificação do Produto: {confidence.ProductIdentification.Score}");
        Console.WriteLine($"    • Nome identificado: {confidence.ProductIdentification.ProductNameIdentified}");
        Console.WriteLine($"    • Marca identificada: {confidence.ProductIdentification.BrandIdentified}");
        Console.WriteLine();
        Console.WriteLine($"📖 Leitura do Rótulo: {confidence.LabelReading.Score}");
        Console.WriteLine($"    • OCR Score: {confidence.LabelReading.OcrScore:P0}");
        Console.WriteLine($"    • Ingredientes Score: {confidence.LabelReading.IngredientsScore:P0}");
        Console.WriteLine($"    • Nutrientes Score: {confidence.LabelReading.NutrientsScore:P0}");
        Console.WriteLine($"    • Alérgenos Score: {confidence.LabelReading.AllergensScore:P0}");
        Console.WriteLine();
        Console.WriteLine($"📈 Análise Final: {confidence.FinalAnalysis.Score}");
        Console.WriteLine($"    • Classificação confiável: {confidence.FinalAnalysis.ClassificationReliable}");
        Console.WriteLine($"    • Penalização aplicada: {confidence.FinalAnalysis.PenaltyApplied:P0}");
        Console.WriteLine();
        Console.WriteLine($"✅ GERAL: {confidence.OverallConfidence}");
        Console.WriteLine($"   Quality Gate: {(confidence.QualityGatePassed ? "PASSOU" : "NÃO PASSOU")}");
        Console.WriteLine($"   Resumo: {confidence.QualitySummary}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 2: Ajuste de classificação baseado em confiança
    // ═══════════════════════════════════════════════════════════════════════

    public void ClassificationAdjustmentExample()
    {
        // Cenário: Produto com Safe mas confiança baixa
        var parseResult = new IngredientAllergenParseResult
        {
            ProductName = "Produto Desconhecido", // Nome não identificado!
            Ingredients = ["item1", "item2"],
            Allergens = []
        };

        var ocrAssessor = new OcrQualityAssessor();
        var ocrMetrics = ocrAssessor.AssessQuality("texto parcial", 0.55);

        var parsingAssessor = new ParsingQualityAssessor();
        var parsingMetrics = parsingAssessor.AssessQuality(parseResult);

        var calculator = new MultidimensionalConfidenceCalculator();
        var confidence = calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

        // Ajustar classificação
        var adjuster = new ConfidenceBasedClassificationAdjuster();
        var adjustment = adjuster.AdjustClassification("Safe", confidence);

        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("🔄 AJUSTE DE CLASSIFICAÇÃO");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine($"Classificação Original: {adjustment.OriginalClassification}");
        Console.WriteLine($"Classificação Ajustada: {adjustment.AdjustedClassification}");
        Console.WriteLine($"Foi Ajustada: {adjustment.WasAdjusted}");
        Console.WriteLine($"Motivo: {adjustment.AdjustmentReason}");
        Console.WriteLine($"Regra Aplicada: {adjustment.AdjustmentRule}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");

        // Output esperado:
        // Classificação Original: Safe
        // Classificação Ajustada: Incomplete
        // Foi Ajustada: True
        // Motivo: Produto não identificado com segurança
        // Regra Aplicada: ProductNotIdentified
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 3: Quality Gate completo
    // ═══════════════════════════════════════════════════════════════════════

    public void FullQualityGateExample()
    {
        // Criar resultado de análise simulado
        var analysisResult = new ProductAnalysisResultDto
        {
            ProductName = "Cereal Matinal",
            Brand = "Kellogg's",
            Classification = "Safe",
            GeneralScore = 0.78,
            PersonalizedScore = 0.82,
            Summary = "Boa opção para café da manhã. Pode consumir regularmente.",
            ExtractedIngredients = ["milho", "açúcar", "sal", "vitaminas"],
            ExtractedAllergens = ["glúten"]
        };

        var parseResult = new IngredientAllergenParseResult
        {
            ProductName = "Cereal Matinal",
            Brand = "Kellogg's",
            Ingredients = ["milho", "açúcar", "sal", "vitaminas"],
            Allergens = ["glúten"],
            Nutrition = new NutritionInfo
            {
                Calories = 110,
                TotalCarbohydrate = 25,
                Sugars = 8,
                Sodium = 150
            }
        };

        // Aplicar Quality Gate Multidimensional
        var qualityGate = new MultidimensionalQualityGate();
        var result = qualityGate.ApplyQualityGate(
            analysisResult,
            extractedText: "INGREDIENTES: MILHO AÇÚCAR SAL VITAMINAS CONTÉM GLÚTEN",
            ocrConfidence: 0.85,
            parseResult);

        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("🎯 QUALITY GATE MULTIDIMENSIONAL");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine($"Passou no Quality Gate: {result.Passed}");
        Console.WriteLine();
        Console.WriteLine("📊 Confiança por Dimensão:");
        Console.WriteLine($"   • Identificação: {result.Confidence.ProductIdentification.Score}");
        Console.WriteLine($"   • Leitura: {result.Confidence.LabelReading.Score}");
        Console.WriteLine($"   • Análise: {result.Confidence.FinalAnalysis.Score}");
        Console.WriteLine($"   • GERAL: {result.Confidence.OverallConfidence}");
        Console.WriteLine();
        Console.WriteLine("📝 Ajustes Aplicados:");
        Console.WriteLine($"   Classificação: {result.ClassificationAdjustment.OriginalClassification} → {result.AdjustedClassification}");
        Console.WriteLine($"   General Score: {result.ScoreAdjustment.OriginalGeneralScore:F2} → {result.AdjustedGeneralScore:F2}");
        Console.WriteLine($"   Penalidade: {result.ScoreAdjustment.PenaltyApplied:P0}");
        Console.WriteLine();
        Console.WriteLine("📄 Resumo Ajustado:");
        Console.WriteLine($"   {result.AdjustedShortSummary}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 4: Comparação Before/After
    // ═══════════════════════════════════════════════════════════════════════

    public void BeforeAfterComparison()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("🔄 COMPARAÇÃO: BEFORE vs AFTER");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        
        // CENÁRIO 1: Produto não identificado
        Console.WriteLine();
        Console.WriteLine("📌 CENÁRIO 1: Produto não identificado");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine("BEFORE (Sistema Antigo):");
        Console.WriteLine("   classification: \"Safe\"");
        Console.WriteLine("   confidenceLevel: \"Médio\"");
        Console.WriteLine("   productName: \"Produto Desconhecido\"");
        Console.WriteLine("   generalScore: 0.75");
        Console.WriteLine();
        Console.WriteLine("AFTER (Sistema Novo):");
        Console.WriteLine("   classification: \"Incomplete\"");
        Console.WriteLine("   confidenceLevel: \"Baixo\"");
        Console.WriteLine("   confidenceDetails: {");
        Console.WriteLine("      productIdentification: { score: 0.25, level: \"VeryLow\" }");
        Console.WriteLine("      adjustments: { reason: \"Produto não identificado\" }");
        Console.WriteLine("   }");

        // CENÁRIO 2: Nutrientes incompletos
        Console.WriteLine();
        Console.WriteLine("📌 CENÁRIO 2: Nutrientes incompletos");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine("BEFORE:");
        Console.WriteLine("   classification: \"Safe\"");
        Console.WriteLine("   confidenceLevel: \"Alto\"");
        Console.WriteLine("   nutritionalFields: 2/10");
        Console.WriteLine();
        Console.WriteLine("AFTER:");
        Console.WriteLine("   classification: \"Caution\"");
        Console.WriteLine("   confidenceLevel: \"Médio\"");
        Console.WriteLine("   confidenceDetails: {");
        Console.WriteLine("      labelReading: { nutrientsIncomplete: true, score: 0.58 }");
        Console.WriteLine("      adjustments: { penaltyApplied: 0.20 }");
        Console.WriteLine("   }");

        // CENÁRIO 3: Múltiplos alérgenos
        Console.WriteLine();
        Console.WriteLine("📌 CENÁRIO 3: Múltiplos alérgenos detectados");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine("BEFORE:");
        Console.WriteLine("   classification: \"Safe\"");
        Console.WriteLine("   allergens: [\"glúten\", \"leite\", \"soja\", \"ovos\"]");
        Console.WriteLine();
        Console.WriteLine("AFTER:");
        Console.WriteLine("   classification: \"Caution\"");
        Console.WriteLine("   confidenceDetails: {");
        Console.WriteLine("      labelReading: { allergensClearlyDetected: true, allergensCount: 4 }");
        Console.WriteLine("      adjustments: { reason: \"Múltiplos alérgenos detectados (4)\" }");
        Console.WriteLine("   }");

        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXEMPLO 5: Uso com código de barras
    // ═══════════════════════════════════════════════════════════════════════

    public void BarcodeConfidenceExample()
    {
        var parseResult = new IngredientAllergenParseResult
        {
            ProductName = "Produto X",
            Ingredients = ["ingrediente1"],
            Allergens = []
        };

        var ocrAssessor = new OcrQualityAssessor();
        var ocrMetrics = ocrAssessor.AssessQuality("texto", 0.70);

        var parsingAssessor = new ParsingQualityAssessor();
        var parsingMetrics = parsingAssessor.AssessQuality(parseResult);

        var calculator = new MultidimensionalConfidenceCalculator();

        // SEM código de barras
        var withoutBarcode = calculator.Calculate(parseResult, ocrMetrics, parsingMetrics);

        // COM código de barras
        var withBarcode = calculator.Calculate(parseResult, ocrMetrics, parsingMetrics, "7891234567890");

        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("📱 IMPACTO DO CÓDIGO DE BARRAS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine($"Sem Barcode: {withoutBarcode.ProductIdentification.Score}");
        Console.WriteLine($"Com Barcode: {withBarcode.ProductIdentification.Score}");
        Console.WriteLine($"Diferença: +{(withBarcode.ProductIdentification.Score.Value - withoutBarcode.ProductIdentification.Score.Value):P0}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// EXEMPLO DE RESPOSTA JSON DA API
// ═══════════════════════════════════════════════════════════════════════════════

/*
{
  "analysisResult": {
    "productName": "Biscoito Recheado Oreo",
    "brand": "Mondelez",
    "classification": "Caution",
    "confidenceLevel": "Médio",
    "confidenceDetails": {
      "productIdentification": {
        "score": 0.88,
        "level": "Medium",
        "details": "Produto identificado adequadamente.",
        "factors": {
          "productNameIdentified": true,
          "productNameScore": 0.85,
          "brandIdentified": true,
          "brandScore": 0.85,
          "barcodeIdentified": false,
          "identificationSource": "OCR (Nome + Marca)"
        }
      },
      "labelReading": {
        "score": 0.72,
        "level": "Medium",
        "details": "4 ingredientes | 6/10 campos nutricionais | 2 alérgenos",
        "factors": {
          "ocrScore": 0.85,
          "ingredientsScore": 0.70,
          "nutrientsScore": 0.68,
          "allergensScore": 0.90
        }
      },
      "finalAnalysis": {
        "score": 0.76,
        "level": "Medium",
        "details": "Score base: 78%, Penalização: 5%, Score ajustado: 74%",
        "factors": {
          "classificationReliable": true,
          "originalScore": 0.78,
          "adjustedScore": 0.74,
          "penaltyApplied": 0.05
        }
      },
      "overall": {
        "score": 0.76,
        "level": "Medium",
        "qualityGatePassed": true,
        "summary": "⚠️ Análise com algumas limitações - verifique os detalhes"
      },
      "labelReadingDetails": {
        "ocrConfidence": 0.87,
        "hasExcessiveNoise": false,
        "noiseRatio": 0.05,
        "ingredientsExtracted": true,
        "validIngredientsCount": 4,
        "ingredientsHaveExcessiveNoise": false,
        "nutrientsExtracted": true,
        "nutritionalFieldsCount": 6,
        "nutritionalCompletenessRatio": 0.60,
        "nutrientsIncomplete": false,
        "allergensClearlyDetected": true,
        "allergensCount": 2
      },
      "adjustments": {
        "classificationWasAdjusted": false,
        "originalClassification": "Caution",
        "adjustedClassification": "Caution",
        "classificationAdjustmentReason": "",
        "originalGeneralScore": 0.78,
        "adjustedGeneralScore": 0.74,
        "originalPersonalizedScore": 0.75,
        "adjustedPersonalizedScore": 0.71,
        "scorePenaltyApplied": 0.05,
        "confidenceAlerts": []
      }
    },
    "generalScore": 0.74,
    "personalizedScore": 0.71,
    "summary": "Consumir com moderação devido ao alto teor de açúcar.",
    "shortSummary": "⚠️ Atenção necessária (74/100). Verifique ingredientes.",
    "alerts": ["Alto teor de açúcar (12g)"],
    "recommendations": ["Prefira versões com menos açúcar"],
    "extractedIngredients": ["farinha de trigo", "açúcar", "gordura vegetal", "cacau"],
    "extractedAllergens": ["glúten", "leite"]
  }
}
*/
