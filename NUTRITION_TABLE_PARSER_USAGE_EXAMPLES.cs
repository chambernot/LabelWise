// ═══════════════════════════════════════════════════════════════════════════════════════
// NUTRITION TABLE PARSER - EXEMPLOS DE USO PRÁTICO
// ═══════════════════════════════════════════════════════════════════════════════════════

using LabelWise.Application.Parsing.Strategies;
using LabelWise.Domain.Enums;
using System;
using System.Linq;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos práticos de uso do parser refinado de tabela nutricional.
    /// </summary>
    public class NutritionTableParserExamples
    {
        // ═══════════════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 1: PARSING BÁSICO
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public static void Example1_BasicParsing()
        {
            var parser = new NutritionTableParser();

            var ocrText = @"
Porção: 30g
Valor energético 140 kcal
Carboidratos 21 g
Proteínas 1,5 g
Gorduras totais 5,5 g
Sódio 95 mg
";

            var result = parser.Parse(ocrText);

            if (result.HasNutritionData)
            {
                Console.WriteLine("✅ Dados nutricionais extraídos:");
                Console.WriteLine($"   Porção: {result.ServingSize}");
                Console.WriteLine($"   Calorias: {result.Calories} kcal");
                Console.WriteLine($"   Carboidratos: {result.TotalCarbohydrate}g");
                Console.WriteLine($"   Proteínas: {result.Protein}g");
                Console.WriteLine($"   Gorduras: {result.TotalFat}g");
                Console.WriteLine($"   Sódio: {result.Sodium}mg");
                Console.WriteLine($"\n📊 Campos extraídos: {result.ExtractedFieldsCount}");
                Console.WriteLine($"🎯 Confiança: {result.Confidence}");
            }
            else
            {
                Console.WriteLine("❌ Não foi possível extrair dados nutricionais.");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 2: VERIFICAR DADOS COMPLETOS
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public static void Example2_CheckComplete()
        {
            var parser = new NutritionTableParser();

            var ocrText = @"
Porção: 200ml
Valor energético 120 kcal
Carboidratos 18 g
Açúcares totais 16 g
Lactose 9,5 g
Proteínas 6,5 g
Gorduras totais 2,8 g
Gorduras saturadas 1,8 g
Gorduras trans 0 g
Fibra alimentar 0 g
Sódio 85 mg
Cálcio 240 mg
";

            var result = parser.Parse(ocrText);

            // Verificar se tabela está completa
            if (result.IsComplete)
            {
                Console.WriteLine("✅ Tabela nutricional completa!");
                Console.WriteLine($"   Todos os macros principais + sódio presentes");
            }
            else
            {
                Console.WriteLine("⚠️ Tabela parcial");
                Console.WriteLine($"   {result.ExtractedFieldsCount} campos extraídos");
            }

            // Verificar campos específicos importantes
            if (result.Lactose.HasValue)
            {
                Console.WriteLine($"   ℹ️ Contém lactose: {result.Lactose}g");
            }

            if (result.Calcium.HasValue)
            {
                Console.WriteLine($"   ℹ️ Fonte de cálcio: {result.Calcium}mg");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 3: DETECTAR WARNINGS DE VALIDAÇÃO
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public static void Example3_ValidationWarnings()
        {
            var parser = new NutritionTableParser();

            var ocrText = @"
Porção: 30g
Valor energético 150 kcal
Carboidratos 20 g
Açúcares totais 10 g
Açúcares adicionados 15 g
Proteínas 2 g
";

            var result = parser.Parse(ocrText);

            // Verificar warnings
            if (result.ValidationWarnings.Any())
            {
                Console.WriteLine("⚠️ Avisos de validação:");
                foreach (var warning in result.ValidationWarnings)
                {
                    Console.WriteLine($"   • {warning}");
                }
            }
            else
            {
                Console.WriteLine("✅ Nenhum aviso de validação");
            }

            // Decisão baseada em warnings
            if (result.ValidationWarnings.Count >= 3)
            {
                Console.WriteLine("❌ Muitos warnings - dados podem estar incorretos");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 4: SUPLEMENTOS (CREATINA)
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public static void Example4_Supplements()
        {
            var parser = new NutritionTableParser();

            var ocrText = @"
CREATINA 100% PURA
Porção: 3g (1 colher medida)
Aproximadamente 100 porções por embalagem

Valor energético 0 kcal
Carboidratos 0 g
Proteínas 0 g
Gorduras totais 0 g
Creatina 3 g
";

            var result = parser.Parse(ocrText);

            // Creatina é convertida automaticamente para mg
            if (result.Creatine.HasValue)
            {
                Console.WriteLine("💪 Suplemento de Creatina detectado:");
                Console.WriteLine($"   Creatina: {result.Creatine}mg (= {result.Creatine / 1000}g)");
                Console.WriteLine($"   Porção: {result.ServingSize}");
                Console.WriteLine($"   Porções por embalagem: {result.ServingsPerContainer}");
            }

            // Calcular dose diária se tomar 2 scoops
            if (result.Creatine.HasValue)
            {
                var dailyDose = (result.Creatine.Value / 1000) * 2; // 2 scoops
                Console.WriteLine($"   💊 Dose diária (2 scoops): {dailyDose}g");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 5: OCR QUEBRADO
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public static void Example5_BrokenOcr()
        {
            var parser = new NutritionTableParser();

            // Texto OCR com quebras ruins
            var ocrText = @"
INFORMAÇÃO
NUTRICIONAL
Porção de
30g
Valor
energético
150
kcal
Carboidratos
22,5
g
Proteínas
2
g
Gorduras
totais
6
g
";

            var result = parser.Parse(ocrText);

            Console.WriteLine("🔧 Parser lidou com OCR quebrado:");
            Console.WriteLine($"   Confiança: {result.Confidence}");
            Console.WriteLine($"   Campos extraídos: {result.ExtractedFieldsCount}");

            if (result.HasNutritionData)
            {
                Console.WriteLine("   ✅ Conseguiu extrair dados mesmo com texto quebrado!");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 6: CALCULAR % DE MACROS
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public static void Example6_CalculateMacroPercentages()
        {
            var parser = new NutritionTableParser();

            var ocrText = @"
Porção: 30g
Valor energético 140 kcal
Carboidratos 21 g
Proteínas 1,5 g
Gorduras totais 5,5 g
";

            var result = parser.Parse(ocrText);

            if (result.Calories.HasValue && 
                result.TotalCarbohydrate.HasValue && 
                result.Protein.HasValue && 
                result.TotalFat.HasValue)
            {
                // Calcular calorias de cada macro
                var carbCals = result.TotalCarbohydrate.Value * 4;
                var proteinCals = result.Protein.Value * 4;
                var fatCals = result.TotalFat.Value * 9;

                var totalCalcs = carbCals + proteinCals + fatCals;

                Console.WriteLine("📊 Distribuição de macronutrientes:");
                Console.WriteLine($"   Carboidratos: {(carbCals / totalCalcs * 100):F1}%");
                Console.WriteLine($"   Proteínas: {(proteinCals / totalCalcs * 100):F1}%");
                Console.WriteLine($"   Gorduras: {(fatCals / totalCalcs * 100):F1}%");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 7: CLASSIFICAR PRODUTO POR MACROS
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public static void Example7_ClassifyProduct()
        {
            var parser = new NutritionTableParser();

            var ocrText = @"
Porção: 30g
Carboidratos 21 g
Açúcares totais 12 g
Proteínas 1,5 g
Gorduras totais 5,5 g
Gorduras saturadas 2,5 g
Gorduras trans 0 g
Fibra alimentar 0,6 g
Sódio 95 mg
";

            var result = parser.Parse(ocrText);

            // Classificar produto
            string classification = "Desconhecido";

            if (result.TotalCarbohydrate.HasValue && result.Sugars.HasValue)
            {
                var sugarRatio = result.Sugars.Value / result.TotalCarbohydrate.Value;

                if (sugarRatio > 0.5)
                {
                    classification = "🍪 Rico em açúcar";
                }
            }

            if (result.TotalFat.HasValue && result.SaturatedFat.HasValue)
            {
                var saturatedRatio = result.SaturatedFat.Value / result.TotalFat.Value;

                if (saturatedRatio > 0.5)
                {
                    classification += " + Alto em gordura saturada";
                }
            }

            if (result.TransFat.HasValue && result.TransFat.Value > 0)
            {
                classification += " ⚠️ CONTÉM GORDURA TRANS";
            }

            Console.WriteLine($"Classificação do produto: {classification}");
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 8: VERIFICAR ADEQUAÇÃO PARA DIETA
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public static void Example8_CheckDietSuitability()
        {
            var parser = new NutritionTableParser();

            var ocrText = @"
Porção: 200ml
Valor energético 120 kcal
Carboidratos 18 g
Açúcares totais 16 g
Lactose 9,5 g
Proteínas 6,5 g
Gorduras totais 2,8 g
Gorduras saturadas 1,8 g
Sódio 85 mg
";

            var result = parser.Parse(ocrText);

            // Verificar adequação para diferentes dietas
            Console.WriteLine("🍽️ Adequação para dietas:");

            // Low-carb (< 10g carbs por porção)
            if (result.TotalCarbohydrate.HasValue)
            {
                var isLowCarb = result.TotalCarbohydrate.Value < 10;
                Console.WriteLine($"   Low-carb: {(isLowCarb ? "✅" : "❌")} ({result.TotalCarbohydrate}g)");
            }

            // Low-fat (< 3g fat por porção)
            if (result.TotalFat.HasValue)
            {
                var isLowFat = result.TotalFat.Value < 3;
                Console.WriteLine($"   Low-fat: {(isLowFat ? "✅" : "❌")} ({result.TotalFat}g)");
            }

            // High-protein (> 5g protein por porção)
            if (result.Protein.HasValue)
            {
                var isHighProtein = result.Protein.Value > 5;
                Console.WriteLine($"   High-protein: {(isHighProtein ? "✅" : "❌")} ({result.Protein}g)");
            }

            // Low-sodium (< 140mg por porção)
            if (result.Sodium.HasValue)
            {
                var isLowSodium = result.Sodium.Value < 140;
                Console.WriteLine($"   Low-sodium: {(isLowSodium ? "✅" : "❌")} ({result.Sodium}mg)");
            }

            // Lactose-free
            if (result.Lactose.HasValue)
            {
                var isLactoseFree = result.Lactose.Value == 0;
                Console.WriteLine($"   Sem lactose: {(isLactoseFree ? "✅" : "❌")} ({result.Lactose}g)");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 9: EXPORTAR PARA JSON
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public static void Example9_ExportToJson()
        {
            var parser = new NutritionTableParser();

            var ocrText = @"
Porção: 30g
Valor energético 140 kcal
Carboidratos 21 g
Proteínas 1,5 g
Gorduras totais 5,5 g
Sódio 95 mg
";

            var result = parser.Parse(ocrText);

            // Criar objeto para serialização
            var nutritionData = new
            {
                ServingSize = result.ServingSize,
                Calories = result.Calories,
                Macronutrients = new
                {
                    Carbohydrates = result.TotalCarbohydrate,
                    Protein = result.Protein,
                    TotalFat = result.TotalFat,
                    SaturatedFat = result.SaturatedFat,
                    TransFat = result.TransFat,
                    Fiber = result.DietaryFiber
                },
                Micronutrients = new
                {
                    Sodium = result.Sodium,
                    Calcium = result.Calcium
                },
                Quality = new
                {
                    ExtractedFields = result.ExtractedFieldsCount,
                    Confidence = result.Confidence.ToString(),
                    IsComplete = result.IsComplete,
                    Warnings = result.ValidationWarnings
                }
            };

            // Serializar (exemplo simplificado)
            Console.WriteLine("📄 Dados nutricionais estruturados:");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(nutritionData, 
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 10: COMPARAR DOIS PRODUTOS
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public static void Example10_CompareProducts()
        {
            var parser = new NutritionTableParser();

            var product1Ocr = @"Porção: 30g
Valor energético 140 kcal
Carboidratos 21 g
Açúcares 12 g
Proteínas 1,5 g
Gorduras totais 5,5 g";

            var product2Ocr = @"Porção: 30g
Valor energético 110 kcal
Carboidratos 15 g
Açúcares 5 g
Proteínas 2,5 g
Gorduras totais 4 g";

            var product1 = parser.Parse(product1Ocr);
            var product2 = parser.Parse(product2Ocr);

            Console.WriteLine("📊 Comparação de produtos (por porção de 30g):");
            Console.WriteLine($"                    Produto 1    Produto 2    Melhor");
            Console.WriteLine($"   Calorias:        {product1.Calories}kcal      {product2.Calories}kcal      {(product2.Calories < product1.Calories ? "Produto 2" : "Produto 1")}");
            Console.WriteLine($"   Açúcares:        {product1.Sugars}g         {product2.Sugars}g          {(product2.Sugars < product1.Sugars ? "Produto 2 ✅" : "Produto 1")}");
            Console.WriteLine($"   Proteínas:       {product1.Protein}g         {product2.Protein}g          {(product2.Protein > product1.Protein ? "Produto 2 ✅" : "Produto 1")}");
            Console.WriteLine($"   Gorduras:        {product1.TotalFat}g         {product2.TotalFat}g          {(product2.TotalFat < product1.TotalFat ? "Produto 2 ✅" : "Produto 1")}");
        }

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // MAIN - EXECUTAR TODOS OS EXEMPLOS
        // ═══════════════════════════════════════════════════════════════════════════════════════

        public static void Main()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");
            Console.WriteLine("NUTRITION TABLE PARSER - EXEMPLOS DE USO");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

            Console.WriteLine("--- EXEMPLO 1: PARSING BÁSICO ---");
            Example1_BasicParsing();
            Console.WriteLine();

            Console.WriteLine("--- EXEMPLO 2: VERIFICAR DADOS COMPLETOS ---");
            Example2_CheckComplete();
            Console.WriteLine();

            Console.WriteLine("--- EXEMPLO 3: VALIDAÇÃO ---");
            Example3_ValidationWarnings();
            Console.WriteLine();

            Console.WriteLine("--- EXEMPLO 4: SUPLEMENTOS ---");
            Example4_Supplements();
            Console.WriteLine();

            Console.WriteLine("--- EXEMPLO 5: OCR QUEBRADO ---");
            Example5_BrokenOcr();
            Console.WriteLine();

            Console.WriteLine("--- EXEMPLO 6: % DE MACROS ---");
            Example6_CalculateMacroPercentages();
            Console.WriteLine();

            Console.WriteLine("--- EXEMPLO 7: CLASSIFICAR PRODUTO ---");
            Example7_ClassifyProduct();
            Console.WriteLine();

            Console.WriteLine("--- EXEMPLO 8: ADEQUAÇÃO PARA DIETA ---");
            Example8_CheckDietSuitability();
            Console.WriteLine();

            Console.WriteLine("--- EXEMPLO 9: EXPORTAR JSON ---");
            Example9_ExportToJson();
            Console.WriteLine();

            Console.WriteLine("--- EXEMPLO 10: COMPARAR PRODUTOS ---");
            Example10_CompareProducts();
            Console.WriteLine();

            Console.WriteLine("═══════════════════════════════════════════════════════════════════");
            Console.WriteLine("✅ TODOS OS EXEMPLOS EXECUTADOS COM SUCESSO!");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        }
    }
}
