// ════════════════════════════════════════════════════════════════════════════════
// EXEMPLOS DE USO - NUTRITION TABLE CAPTURE TYPE
// ════════════════════════════════════════════════════════════════════════════════

// Este arquivo demonstra exemplos de requisições e respostas para o endpoint
// /api/pipeline/analyze com diferentes tipos de captura.

// ═══════════════════════════════════════════════════════════════════════════════
// EXEMPLO 1: TABELA NUTRICIONAL (CaptureType = 3)
// ═══════════════════════════════════════════════════════════════════════════════

// REQUEST:
// POST /api/pipeline/analyze
// Content-Type: multipart/form-data
// 
// file: [imagem da tabela nutricional]
// captureType: 3  (NutritionTable)

// TEXTO OCR EXTRAÍDO (exemplo):
var ocrTextNutritionTable = @"
INFORMAÇÃO NUTRICIONAL
Porção de 30g (1 unidade)
Porções por embalagem: 10

                        Qtde. por porção    %VD*
Valor energético        120 kcal            6%
Carboidratos           22 g                 7%
Açúcares totais         8 g                 -
Açúcares adicionados    5 g                10%
Proteínas               3 g                 6%
Gorduras totais       3,5 g                 6%
Gorduras saturadas    1,5 g                 7%
Gorduras trans          0 g                 -
Fibra alimentar         2 g                 8%
Sódio                 150 mg                6%
Cálcio                120 mg               12%

* % Valores Diários com base em uma dieta de 2.000 kcal.
";

// RESPOSTA (DEPOIS DAS MELHORIAS):
var responseNutritionTable = new
{
    success = true,
    captureType = "NutritionTable",
    overallConfidence = 0.82,
    
    finalAnalysis = new
    {
        productName = "Análise Parcial",
        brand = (string?)null,
        classification = "Partial",
        confidenceLevel = "Alto",
        generalScore = 0.75,
        personalizedScore = 0.75,
        
        // FLAGS DE ANÁLISE PARCIAL
        isPartialAnalysis = true,
        captureType = "NutritionTable",
        missingSteps = new[] { "IngredientsList", "FrontPackaging" },
        
        // NUTRIENTES ESTRUTURADOS
        nutritionalFacts = new
        {
            servingSize = "30 g",
            calories = 120.0,
            totalFat = 3.5,
            saturatedFat = 1.5,
            transFat = 0.0,
            totalCarbohydrate = 22.0,
            sugars = 8.0,
            addedSugars = 5.0,
            protein = 3.0,
            sodium = 150.0,
            dietaryFiber = 2.0,
            calcium = 120.0,
            extractedFieldsCount = 12,
            isComplete = true,
            dailyValuePercentages = new Dictionary<string, double>
            {
                ["Calories"] = 6,
                ["Carbohydrates"] = 7,
                ["Protein"] = 6,
                ["TotalFat"] = 6,
                ["Sodium"] = 6,
                ["Fiber"] = 8,
                ["Calcium"] = 12
            }
        },
        
        // SUMÁRIOS
        summary = "📊 **Tabela nutricional identificada com sucesso.** Foram extraídos 12 valores nutricionais. Envie a lista de ingredientes ou frente da embalagem para uma análise completa do produto.",
        shortSummary = "Tabela nutricional lida (75/100). Envie ingredientes para análise completa.",
        
        // ALERTAS RELEVANTES
        alerts = new[]
        {
            "⚠️ Alto teor de açúcares adicionados: 5g por porção",
            "ℹ️ Análise parcial (NutritionTable). Complete com mais capturas para resultado final."
        },
        
        // RECOMENDAÇÕES
        recommendations = new[]
        {
            "📋 Envie foto da lista de ingredientes para verificar aditivos e conservantes",
            "📦 Envie foto da frente da embalagem para identificar o produto"
        }
    },
    
    metadata = new
    {
        processingId = Guid.NewGuid(),
        totalProcessingTimeMs = 1250.5,
        ocrProvider = "Azure Computer Vision Read API"
    }
};


// ═══════════════════════════════════════════════════════════════════════════════
// EXEMPLO 2: LISTA DE INGREDIENTES (CaptureType = 4)
// ═══════════════════════════════════════════════════════════════════════════════

// REQUEST:
// POST /api/pipeline/analyze
// file: [imagem da lista de ingredientes]
// captureType: 4  (IngredientsList)

// TEXTO OCR EXTRAÍDO:
var ocrTextIngredients = @"
INGREDIENTES: Farinha de trigo enriquecida com ferro e ácido fólico, 
açúcar, óleo vegetal, cacau em pó, leite em pó, sal, emulsificante 
lecitina de soja, aromatizantes.

CONTÉM GLÚTEN, DERIVADOS DE LEITE E SOJA.
PODE CONTER AMENDOIM E CASTANHAS.
";

// RESPOSTA:
var responseIngredients = new
{
    success = true,
    captureType = "IngredientsList",
    overallConfidence = 0.78,
    
    finalAnalysis = new
    {
        productName = "Análise Parcial",
        classification = "Partial",
        confidenceLevel = "Médio",
        generalScore = 0.68,
        
        isPartialAnalysis = true,
        captureType = "IngredientsList",
        missingSteps = new[] { "NutritionTable", "FrontPackaging" },
        
        extractedIngredients = new[]
        {
            "Farinha de trigo enriquecida com ferro e ácido fólico",
            "Açúcar",
            "Óleo vegetal",
            "Cacau em pó",
            "Leite em pó",
            "Sal",
            "Emulsificante lecitina de soja",
            "Aromatizantes"
        },
        
        extractedAllergens = new[]
        {
            "Glúten",
            "Derivados de leite",
            "Soja",
            "Amendoim (pode conter)",
            "Castanhas (pode conter)"
        },
        
        summary = "📋 **Lista de ingredientes identificada.** Encontrados 8 ingredientes e 5 alérgenos. Envie a tabela nutricional para uma análise completa.",
        
        alerts = new[]
        {
            "⚠️ ALÉRGENOS DETECTADOS: Glúten, Derivados de leite, Soja",
            "⚠️ PODE CONTER: Amendoim, Castanhas",
            "ℹ️ Análise parcial (IngredientsList). Complete com mais capturas para resultado final."
        },
        
        recommendations = new[]
        {
            "📊 Envie foto da tabela nutricional para análise de macronutrientes",
            "📦 Envie foto da frente da embalagem para identificar o produto"
        }
    }
};


// ═══════════════════════════════════════════════════════════════════════════════
// EXEMPLO 3: DECLARAÇÃO DE ALÉRGENOS (CaptureType = 5)
// ═══════════════════════════════════════════════════════════════════════════════

// REQUEST:
// POST /api/pipeline/analyze
// file: [imagem da declaração de alérgenos]
// captureType: 5  (AllergenStatement)

// TEXTO OCR:
var ocrTextAllergens = @"
ALÉRGICOS:
CONTÉM: TRIGO, LEITE, OVO E SOJA.
PODE CONTER: AMENDOIM, CASTANHA DE CAJU, CASTANHA-DO-PARÁ, AVELÃ E NOZES.

NÃO CONTÉM GLÚTEN.
";

// RESPOSTA:
var responseAllergens = new
{
    success = true,
    captureType = "AllergenStatement",
    overallConfidence = 0.85,
    
    finalAnalysis = new
    {
        classification = "Partial",
        isPartialAnalysis = true,
        captureType = "AllergenStatement",
        missingSteps = new[] { "NutritionTable", "IngredientsList", "FrontPackaging" },
        
        extractedAllergens = new[]
        {
            "Trigo",
            "Leite",
            "Ovo",
            "Soja",
            "Amendoim (pode conter)",
            "Castanha de caju (pode conter)",
            "Castanha-do-pará (pode conter)",
            "Avelã (pode conter)",
            "Nozes (pode conter)"
        },
        
        summary = "⚠️ **Declaração de alérgenos identificada.** Detectados 9 alérgenos (4 confirmados). Envie a tabela nutricional e ingredientes para análise completa.",
        
        alerts = new[]
        {
            "⚠️ ALÉRGENOS DETECTADOS: Trigo, Leite, Ovo, Soja",
            "⚠️ PODE CONTER: Amendoim, Castanha de caju, Castanha-do-pará, Avelã, Nozes",
            "✅ NÃO CONTÉM GLÚTEN"
        }
    }
};


// ═══════════════════════════════════════════════════════════════════════════════
// EXEMPLO 4: EMBALAGEM FRONTAL (CaptureType = 2)
// ═══════════════════════════════════════════════════════════════════════════════

// REQUEST:
// POST /api/pipeline/analyze
// file: [imagem da frente da embalagem]
// captureType: 2  (FrontPackaging)

// TEXTO OCR:
var ocrTextFront = @"
NESTLÉ
NESCAU
Achocolatado em Pó

FONTE DE VITAMINAS
SEM CONSERVANTES

400g
";

// RESPOSTA:
var responseFront = new
{
    success = true,
    captureType = "FrontPackaging",
    overallConfidence = 0.88,
    
    finalAnalysis = new
    {
        productName = "Nescau Achocolatado em Pó",
        brand = "Nestlé",
        classification = "Partial",
        isPartialAnalysis = true,
        captureType = "FrontPackaging",
        missingSteps = new[] { "NutritionTable", "IngredientsList" },
        
        summary = "📦 **Embalagem frontal identificada.** Produto: Nescau Achocolatado em Pó. Marca: Nestlé. Envie a tabela nutricional e ingredientes para análise completa.",
        
        // Claims identificados
        alerts = new[]
        {
            "✅ Claim: Fonte de vitaminas",
            "✅ Claim: Sem conservantes"
        }
    }
};


// ═══════════════════════════════════════════════════════════════════════════════
// CÓDIGO C# PARA CHAMAR A API
// ═══════════════════════════════════════════════════════════════════════════════

public class NutritionTableCaptureExample
{
    private readonly HttpClient _httpClient;
    
    public NutritionTableCaptureExample(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    /// <summary>
    /// Envia uma imagem de tabela nutricional para análise parcial.
    /// </summary>
    public async Task<CapturedImageAnalysisResponse?> AnalyzeNutritionTableAsync(
        string imagePath)
    {
        using var content = new MultipartFormDataContent();
        
        // Adicionar arquivo
        var fileBytes = await File.ReadAllBytesAsync(imagePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "file", Path.GetFileName(imagePath));
        
        // Adicionar CaptureType = NutritionTable (3)
        content.Add(new StringContent("3"), "captureType");
        
        // Chamar API
        var response = await _httpClient.PostAsync("/api/pipeline/analyze", content);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<CapturedImageAnalysisResponse>();
    }
    
    /// <summary>
    /// Processa resposta de análise parcial de tabela nutricional.
    /// </summary>
    public void ProcessPartialAnalysisResponse(CapturedImageAnalysisResponse response)
    {
        if (response.FinalAnalysis == null)
        {
            Console.WriteLine("❌ Análise falhou");
            return;
        }
        
        var analysis = response.FinalAnalysis;
        
        // Verificar se é análise parcial
        if (analysis.IsPartialAnalysis)
        {
            Console.WriteLine($"📊 Análise Parcial ({analysis.CaptureType})");
            Console.WriteLine($"   Score: {analysis.GeneralScore:P0}");
            Console.WriteLine($"   Confiança: {analysis.ConfidenceLevel}");
            
            // Exibir nutrientes extraídos
            if (analysis.NutritionalFacts != null)
            {
                var facts = analysis.NutritionalFacts;
                Console.WriteLine("\n📋 Nutrientes Extraídos:");
                if (facts.Calories.HasValue)
                    Console.WriteLine($"   Calorias: {facts.Calories} kcal");
                if (facts.TotalCarbohydrate.HasValue)
                    Console.WriteLine($"   Carboidratos: {facts.TotalCarbohydrate} g");
                if (facts.Protein.HasValue)
                    Console.WriteLine($"   Proteínas: {facts.Protein} g");
                if (facts.TotalFat.HasValue)
                    Console.WriteLine($"   Gorduras: {facts.TotalFat} g");
                if (facts.Sodium.HasValue)
                    Console.WriteLine($"   Sódio: {facts.Sodium} mg");
                
                Console.WriteLine($"\n   Total de campos: {facts.ExtractedFieldsCount}");
                Console.WriteLine($"   Tabela completa: {facts.IsComplete}");
            }
            
            // Exibir próximos passos
            if (analysis.MissingSteps?.Count > 0)
            {
                Console.WriteLine("\n📷 Próximas capturas necessárias:");
                foreach (var step in analysis.MissingSteps)
                {
                    Console.WriteLine($"   • {step}");
                }
            }
        }
        else
        {
            // Análise completa
            Console.WriteLine($"✅ Análise Completa: {analysis.ProductName}");
            Console.WriteLine($"   Classificação: {analysis.Classification}");
        }
    }
}


// ═══════════════════════════════════════════════════════════════════════════════
// TESTES UNITÁRIOS SUGERIDOS
// ═══════════════════════════════════════════════════════════════════════════════

public class NutritionTableParserTests
{
    [Fact]
    public void Parse_ValidNutritionTable_ExtractsAllNutrients()
    {
        // Arrange
        var parser = new NutritionTableParser();
        var ocrText = @"
            Porção de 30g
            Valor energético 120 kcal
            Carboidratos 22 g
            Proteínas 3 g
            Gorduras totais 3,5 g
            Sódio 150 mg
        ";
        
        // Act
        var result = parser.Parse(ocrText);
        
        // Assert
        Assert.Equal("30 g", result.ServingSize);
        Assert.Equal(120, result.Calories);
        Assert.Equal(22, result.TotalCarbohydrate);
        Assert.Equal(3, result.Protein);
        Assert.Equal(3.5, result.TotalFat);
        Assert.Equal(150, result.Sodium);
        Assert.True(result.HasNutritionData);
        Assert.True(result.IsComplete);
        Assert.Equal(ConfidenceLevel.High, result.Confidence);
    }
    
    [Fact]
    public void Parse_PartialNutritionTable_SetsAppropriateConfidence()
    {
        // Arrange
        var parser = new NutritionTableParser();
        var ocrText = @"
            Calorias 100 kcal
            Proteína 5 g
        ";
        
        // Act
        var result = parser.Parse(ocrText);
        
        // Assert
        Assert.Equal(100, result.Calories);
        Assert.Equal(5, result.Protein);
        Assert.True(result.HasNutritionData);
        Assert.False(result.IsComplete); // Faltam carboidratos, gorduras, sódio
        Assert.Equal(ConfidenceLevel.Low, result.Confidence); // Apenas 2 campos
    }
    
    [Fact]
    public void Parse_BrazilianFormat_HandlesCommaDecimalSeparator()
    {
        // Arrange
        var parser = new NutritionTableParser();
        var ocrText = @"
            Gorduras totais 15,5 g
            Açúcares 8,25 g
        ";
        
        // Act
        var result = parser.Parse(ocrText);
        
        // Assert
        Assert.Equal(15.5, result.TotalFat);
        Assert.Equal(8.25, result.Sugars);
    }
}
