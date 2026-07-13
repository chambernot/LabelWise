// ═══════════════════════════════════════════════════════════════════════════════════════
// PARSING CONSISTENCY FIX - EXAMPLES
// Demonstra as correções de consistência no parsing de rótulos LabelWise
// ═══════════════════════════════════════════════════════════════════════════════════════

using LabelWise.Application.Parsing;
using LabelWise.Application.Parsing.Strategies;

namespace LabelWise.Examples;

/// <summary>
/// Exemplos demonstrando as correções de parsing para:
/// 1. Alérgenos negativos (NÃO CONTÉM)
/// 2. Limpeza de ruído OCR
/// 3. Nutrientes de suplementos (Creatina)
/// </summary>
public static class ParsingConsistencyExamples
{
    // ═══════════════════════════════════════════════════════════════════════════════════════
    // EXEMPLO 1: SUPLEMENTO CREATINA
    // ═══════════════════════════════════════════════════════════════════════════════════════
    
    public static void ExemploCreatina()
    {
        var textoOcr = @"
            "" Creapure'
            INFORMAÇÃO NUTRICIONAL
            Porção: 3 g (1 dosador)
            Valor energético 0 kcal
            Carboidratos 0 g
            Proteínas 0 g
            Gorduras totais 0 g
            Sódio 0 mg
            Creatina (mg) 3.000
            INGREDIENTES: Creatina monohidratada
            NÃO CONTÉM GLÚTEN
        ";

        var parser = new IngredientAllergenParser();
        var resultado = parser.Parse(textoOcr);

        // RESULTADO ESPERADO:
        // ✅ ProductName: "Creapure" (sem aspas)
        // ✅ Brand: null (não "INFORMAÇÃO NUTRICIONAL")
        // ✅ Nutrition.Creatine: 3000 mg
        // ✅ Nutrition.ServingSize: "3 g"
        // ✅ Allergens: [] (vazio)
        // ✅ ConfirmedAllergens: [] (vazio)
        // ✅ MayContainAllergens: [] (vazio)
        // 
        // Observação: DoesNotContainAllergens não está exposto diretamente,
        // mas o importante é que "glúten" NÃO aparece em Allergens ou ConfirmedAllergens

        Console.WriteLine("═══ EXEMPLO 1: SUPLEMENTO CREATINA ═══");
        Console.WriteLine($"ProductName: {resultado.ProductName}");
        Console.WriteLine($"Brand: {resultado.Brand ?? "(null)"}");
        Console.WriteLine($"Nutrition.ServingSize: {resultado.Nutrition?.ServingSize}");
        Console.WriteLine($"Nutrition.Creatine: {resultado.Nutrition?.Creatine} mg");
        Console.WriteLine($"Nutrition.Calories: {resultado.Nutrition?.Calories} kcal");
        Console.WriteLine($"Allergens: [{string.Join(", ", resultado.Allergens)}]");
        Console.WriteLine($"ConfirmedAllergens: [{string.Join(", ", resultado.ConfirmedAllergens)}]");
        Console.WriteLine($"HasNutritionData: {resultado.HasNutritionData}");
        Console.WriteLine($"NutritionFieldsCount: {resultado.Nutrition?.FilledFieldsCount ?? 0}");
        Console.WriteLine();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // EXEMPLO 2: ALÉRGENOS MISTOS (CONTÉM + NÃO CONTÉM)
    // ═══════════════════════════════════════════════════════════════════════════════════════
    
    public static void ExemploAlergenosMistos()
    {
        var textoOcr = @"
            Biscoito Integral
            INGREDIENTES: Farinha de trigo integral, açúcar, óleo vegetal
            CONTÉM GLÚTEN E DERIVADOS DE LEITE
            PODE CONTER SOJA E AMENDOIM
            NÃO CONTÉM LACTOSE
        ";

        var parser = new IngredientAllergenParser();
        var resultado = parser.Parse(textoOcr);

        // RESULTADO ESPERADO:
        // ✅ ConfirmedAllergens: ["glúten", "leite"] (ou variantes)
        // ✅ MayContainAllergens: ["soja", "amendoim"]
        // ✅ Allergens: ["glúten", "leite", "soja", "amendoim"] (SEM "lactose")
        //
        // "lactose" NÃO deve aparecer em nenhuma lista positiva

        Console.WriteLine("═══ EXEMPLO 2: ALÉRGENOS MISTOS ═══");
        Console.WriteLine($"ProductName: {resultado.ProductName}");
        Console.WriteLine($"Allergens: [{string.Join(", ", resultado.Allergens)}]");
        Console.WriteLine($"ConfirmedAllergens: [{string.Join(", ", resultado.ConfirmedAllergens)}]");
        Console.WriteLine($"MayContainAllergens: [{string.Join(", ", resultado.MayContainAllergens)}]");
        Console.WriteLine($"CriticalTerms: [{string.Join(", ", resultado.CriticalTerms)}]");
        Console.WriteLine();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // EXEMPLO 3: PARSER DE ALÉRGENOS ESTRATÉGICO
    // ═══════════════════════════════════════════════════════════════════════════════════════
    
    public static void ExemploAllergenParserEstrategico()
    {
        var textoOcr = @"
            ALÉRGICOS: CONTÉM GLÚTEN
            NÃO CONTÉM LACTOSE
            PODE CONTER TRAÇOS DE AMENDOIM
        ";

        var parser = new AllergenParser();
        var resultado = parser.Parse(textoOcr);

        // RESULTADO ESPERADO:
        // ✅ ConfirmedAllergens: ["glúten"]
        // ✅ MayContainAllergens: ["amendoim"]
        // ✅ DoesNotContainAllergens: ["lactose"]
        //
        // "lactose" deve estar APENAS em DoesNotContainAllergens

        Console.WriteLine("═══ EXEMPLO 3: ALLERGEN PARSER ESTRATÉGICO ═══");
        Console.WriteLine($"ConfirmedAllergens: [{string.Join(", ", resultado.ConfirmedAllergens)}]");
        Console.WriteLine($"MayContainAllergens: [{string.Join(", ", resultado.MayContainAllergens)}]");
        Console.WriteLine($"DoesNotContainAllergens: [{string.Join(", ", resultado.DoesNotContainAllergens)}]");
        Console.WriteLine($"ExtractedPhrases: [{string.Join("; ", resultado.ExtractedPhrases)}]");
        Console.WriteLine($"Confidence: {resultado.Confidence}");
        Console.WriteLine();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // EXEMPLO 4: LIMPEZA DE RUÍDO OCR NO NOME
    // ═══════════════════════════════════════════════════════════════════════════════════════
    
    public static void ExemploLimpezaRuidoOcr()
    {
        var textoOcr = @"
            \"" Whey Protein*
            //MARCA: Optimum\\
            INGREDIENTES: Whey protein concentrado
        ";

        var parser = new IngredientAllergenParser();
        var resultado = parser.Parse(textoOcr);

        // RESULTADO ESPERADO:
        // ✅ ProductName: "Whey Protein" (sem aspas, asterisco, etc.)
        // ✅ Brand: "MARCA: Optimum" ou similar (sem barras)

        Console.WriteLine("═══ EXEMPLO 4: LIMPEZA DE RUÍDO OCR ═══");
        Console.WriteLine($"ProductName: {resultado.ProductName}");
        Console.WriteLine($"Brand: {resultado.Brand ?? "(null)"}");
        Console.WriteLine($"IsProductNameValidated: {resultado.IsProductNameValidated}");
        Console.WriteLine();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // EXEMPLO 5: TABELA NUTRICIONAL DE SUPLEMENTO COMPLETA
    // ═══════════════════════════════════════════════════════════════════════════════════════
    
    public static void ExemploTabelaNutricionalSuplemento()
    {
        var textoOcr = @"
            INFORMAÇÃO NUTRICIONAL
            Porção: 5 g (1 scoop)
            Porções por embalagem: 60
            
            Valor energético 20 kcal
            Carboidratos 0 g
            Proteínas 5 g
            Gorduras totais 0 g
            Sódio 10 mg
            Creatina 5.000 mg
            Cafeína 200 mg
        ";

        var parser = new NutritionTableParser();
        var resultado = parser.Parse(textoOcr);

        // RESULTADO ESPERADO:
        // ✅ ServingSize: "5 g"
        // ✅ ServingsPerContainer: 60
        // ✅ Calories: 20
        // ✅ Protein: 5
        // ✅ Creatine: 5000
        // ✅ Caffeine: 200
        // ✅ HasNutritionData: true
        // ✅ ExtractedFieldsCount: 7+

        Console.WriteLine("═══ EXEMPLO 5: TABELA NUTRICIONAL DE SUPLEMENTO ═══");
        Console.WriteLine($"ServingSize: {resultado.ServingSize}");
        Console.WriteLine($"ServingsPerContainer: {resultado.ServingsPerContainer}");
        Console.WriteLine($"Calories: {resultado.Calories} kcal");
        Console.WriteLine($"Protein: {resultado.Protein} g");
        Console.WriteLine($"Sodium: {resultado.Sodium} mg");
        Console.WriteLine($"Creatine: {resultado.Creatine} mg");
        Console.WriteLine($"Caffeine: {resultado.Caffeine} mg");
        Console.WriteLine($"HasNutritionData: {resultado.HasNutritionData}");
        Console.WriteLine($"ExtractedFieldsCount: {resultado.ExtractedFieldsCount}");
        Console.WriteLine($"IsComplete: {resultado.IsComplete}");
        Console.WriteLine($"Confidence: {resultado.Confidence}");
        Console.WriteLine();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // EXEMPLO 6: CENÁRIO PROBLEMÁTICO ORIGINAL (CREATINA CREAPURE)
    // ═══════════════════════════════════════════════════════════════════════════════════════
    
    public static void ExemploCenarioOriginal()
    {
        // Este é o cenário exato que foi reportado como problemático
        var textoOcr = @"
            \"" Creapure'
            
            INFORMAÇÃO NUTRICIONAL
            Porção: 3 g (1 dosador)
            
            Quantidade por porção    %VD(*)
            Valor energético         0 kcal    0%
            Carboidratos             0 g       0%
            Proteínas                0 g       0%
            Gorduras totais          0 g       0%
            - Gorduras saturadas     0 g       0%
            - Gorduras trans         0 g       **
            Fibra alimentar          0 g       0%
            Sódio                    0 mg      0%
            
            Creatina (mg)            3.000
            
            INGREDIENTES: Creatina monohidratada (Creapure®)
            
            NÃO CONTÉM GLÚTEN
            
            (*) % Valores Diários de referência com base em uma dieta de 2.000 kcal
            (**) VD não estabelecido
        ";

        var parser = new IngredientAllergenParser();
        var resultado = parser.Parse(textoOcr);

        Console.WriteLine("═══ EXEMPLO 6: CENÁRIO ORIGINAL (CREAPURE) ═══");
        Console.WriteLine();
        Console.WriteLine("RESULTADO ANTERIOR (PROBLEMÁTICO):");
        Console.WriteLine("  productName: \"\\\" Creapure'\"");
        Console.WriteLine("  brand: \"INFORMAGAO NUTRICIONAL\"");
        Console.WriteLine("  nutritionalFacts: null");
        Console.WriteLine("  extractedAllergens: [\"gluten\"]");
        Console.WriteLine();
        Console.WriteLine("RESULTADO APÓS CORREÇÃO:");
        Console.WriteLine($"  productName: \"{resultado.ProductName}\"");
        Console.WriteLine($"  brand: \"{resultado.Brand ?? "(null)"}\"");
        Console.WriteLine($"  nutritionalFacts: {(resultado.HasNutritionData ? "populated" : "null")}");
        Console.WriteLine($"    servingSize: {resultado.Nutrition?.ServingSize}");
        Console.WriteLine($"    calories: {resultado.Nutrition?.Calories}");
        Console.WriteLine($"    creatine: {resultado.Nutrition?.Creatine}");
        Console.WriteLine($"    fieldsCount: {resultado.Nutrition?.FilledFieldsCount ?? 0}");
        Console.WriteLine($"  extractedAllergens: [{string.Join(", ", resultado.Allergens)}]");
        Console.WriteLine($"  confirmedAllergens: [{string.Join(", ", resultado.ConfirmedAllergens)}]");
        Console.WriteLine($"  ingredients: [{string.Join(", ", resultado.Ingredients)}]");
        Console.WriteLine($"  parsingConfidence: {resultado.ParsingConfidence}");
        Console.WriteLine($"  validationWarnings: [{string.Join(", ", resultado.ValidationWarnings)}]");
        Console.WriteLine();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // MÉTODO PRINCIPAL PARA EXECUTAR TODOS OS EXEMPLOS
    // ═══════════════════════════════════════════════════════════════════════════════════════
    
    public static void Main()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   PARSING CONSISTENCY FIX - DEMONSTRAÇÃO DE EXEMPLOS                      ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        ExemploCreatina();
        ExemploAlergenosMistos();
        ExemploAllergenParserEstrategico();
        ExemploLimpezaRuidoOcr();
        ExemploTabelaNutricionalSuplemento();
        ExemploCenarioOriginal();

        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   FIM DA DEMONSTRAÇÃO                                                      ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════╝");
    }
}
