using System;
using LabelWise.Application.Parsing;
using LabelWise.Domain.Enums;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos práticos de uso do parser melhorado de rótulos alimentares.
    /// </summary>
    public class ParserUsageExamples
    {
        // ═══════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 1: Parsing Bem-Sucedido (Alta Confiança)
        // ═══════════════════════════════════════════════════════════════════════════════
        public static void Example1_SuccessfulParsing()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 1: Parsing Bem-Sucedido");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var parser = new IngredientAllergenParser();

            var ocrText = @"
Chocolate em Pó
NESTLÉ
INGREDIENTES: cacau, açúcar, leite
CONTÉM: leite, soja
";

            var result = parser.Parse(ocrText);

            Console.WriteLine($"ProductName: {result.ProductName}");
            Console.WriteLine($"Brand: {result.Brand}");
            Console.WriteLine($"Ingredients: {string.Join(", ", result.Ingredients)}");
            Console.WriteLine($"ConfirmedAllergens: {string.Join(", ", result.ConfirmedAllergens)}");
            Console.WriteLine($"ParsingConfidence: {result.ParsingConfidence}");
            Console.WriteLine($"IsProductNameValidated: {result.IsProductNameValidated}");
            Console.WriteLine($"ValidationWarnings: {result.ValidationWarnings.Count}");

            // OUTPUT ESPERADO:
            // ProductName: Chocolate em Pó
            // Brand: NESTLÉ
            // Ingredients: cacau, açúcar, leite
            // ConfirmedAllergens: leite, soja
            // ParsingConfidence: High
            // IsProductNameValidated: True
            // ValidationWarnings: 0
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 2: Rótulo com Tabela Nutricional (Problema Original)
        // ═══════════════════════════════════════════════════════════════════════════════
        public static void Example2_LabelWithNutritionalTable()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 2: Rótulo com Tabela Nutricional");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var parser = new IngredientAllergenParser();

            var ocrText = @"
Biscoito Recheado
BAUDUCCO
INFORMAÇÃO NUTRICIONAL
Porção 30g (3 unidades)
Valor Energético 150 kcal
Carboidratos 20g
Proteínas 3g
Gorduras 5g
%VD 10%
INGREDIENTES: farinha de trigo, açúcar, gordura vegetal
CONTÉM: glúten, leite
";

            var result = parser.Parse(ocrText);

            Console.WriteLine($"ProductName: {result.ProductName}");
            Console.WriteLine($"Brand: {result.Brand}");
            Console.WriteLine($"Ingredients: {string.Join(", ", result.Ingredients)}");
            Console.WriteLine($"ConfirmedAllergens: {string.Join(", ", result.ConfirmedAllergens)}");
            Console.WriteLine($"ParsingConfidence: {result.ParsingConfidence}");

            // ✅ ANTES DA CORREÇÃO (ERRO):
            // ProductName: Porção 30g (3 unidades)  ❌
            // Brand: Valor Energético 150 kcal      ❌

            // ✅ DEPOIS DA CORREÇÃO (CORRETO):
            // ProductName: Biscoito Recheado        ✅
            // Brand: BAUDUCCO                       ✅
            // ParsingConfidence: High
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 3: Rótulo Sem Nome Válido (Retorna Null)
        // ═══════════════════════════════════════════════════════════════════════════════
        public static void Example3_NoValidProductName()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 3: Sem Nome Válido");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var parser = new IngredientAllergenParser();

            var ocrText = @"
INFORMAÇÃO NUTRICIONAL
Porção 30g
150 kcal
INGREDIENTES: farinha, açúcar
";

            var result = parser.Parse(ocrText);

            Console.WriteLine($"ProductName: {result.ProductName ?? "null"}");
            Console.WriteLine($"Brand: {result.Brand ?? "null"}");
            Console.WriteLine($"Ingredients: {string.Join(", ", result.Ingredients)}");
            Console.WriteLine($"ParsingConfidence: {result.ParsingConfidence}");
            Console.WriteLine($"ValidationWarnings:");
            foreach (var warning in result.ValidationWarnings)
            {
                Console.WriteLine($"  - {warning}");
            }

            // OUTPUT ESPERADO:
            // ProductName: null
            // Brand: null
            // Ingredients: farinha, açúcar
            // ParsingConfidence: Medium
            // ValidationWarnings:
            //   - Nenhum nome de produto válido encontrado
            //   - Nome do produto não identificado
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 4: Múltiplos Alergênicos com Classificação
        // ═══════════════════════════════════════════════════════════════════════════════
        public static void Example4_MultipleAllergensClassified()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 4: Múltiplos Alergênicos");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var parser = new IngredientAllergenParser();

            var ocrText = @"
Barra de Cereal
NATURE VALLEY
INGREDIENTES: aveia, mel, amendoim, castanhas
CONTÉM: glúten, amendoim, castanhas
PODE CONTER: leite, soja
";

            var result = parser.Parse(ocrText);

            Console.WriteLine($"ProductName: {result.ProductName}");
            Console.WriteLine($"Brand: {result.Brand}");
            Console.WriteLine($"ConfirmedAllergens: {string.Join(", ", result.ConfirmedAllergens)}");
            Console.WriteLine($"MayContainAllergens: {string.Join(", ", result.MayContainAllergens)}");
            Console.WriteLine($"ParsingConfidence: {result.ParsingConfidence}");

            // OUTPUT ESPERADO:
            // ProductName: Barra de Cereal
            // Brand: NATURE VALLEY
            // ConfirmedAllergens: glúten, amendoim, castanhas
            // MayContainAllergens: leite, soja
            // ParsingConfidence: High
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 5: Validação de Confiança Baseada em Qualidade
        // ═══════════════════════════════════════════════════════════════════════════════
        public static void Example5_ConfidenceValidation()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 5: Validação de Confiança");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var parser = new IngredientAllergenParser();

            // Cenário A: Alta qualidade
            var goodOcrText = @"
Produto Excelente
Marca Excelente
INGREDIENTES: farinha, açúcar, leite
CONTÉM: glúten, leite
";

            var goodResult = parser.Parse(goodOcrText);
            Console.WriteLine("Cenário A (Alta Qualidade):");
            Console.WriteLine($"  Confidence: {goodResult.ParsingConfidence}");
            Console.WriteLine($"  Warnings: {goodResult.ValidationWarnings.Count}");

            // Cenário B: Qualidade média (sem nome válido)
            var mediumOcrText = @"
INGREDIENTES: farinha, açúcar
CONTÉM: glúten
";

            var mediumResult = parser.Parse(mediumOcrText);
            Console.WriteLine("\nCenário B (Qualidade Média):");
            Console.WriteLine($"  Confidence: {mediumResult.ParsingConfidence}");
            Console.WriteLine($"  Warnings: {mediumResult.ValidationWarnings.Count}");

            // Cenário C: Baixa qualidade (texto vazio)
            var badOcrText = "";

            var badResult = parser.Parse(badOcrText);
            Console.WriteLine("\nCenário C (Baixa Qualidade):");
            Console.WriteLine($"  Confidence: {badResult.ParsingConfidence}");
            Console.WriteLine($"  Warnings: {badResult.ValidationWarnings.Count}");

            // OUTPUT ESPERADO:
            // Cenário A: Confidence = High, Warnings = 0
            // Cenário B: Confidence = Medium, Warnings = 2
            // Cenário C: Confidence = Low, Warnings = 1
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 6: Uso em Produção (Verificação de Confiança)
        // ═══════════════════════════════════════════════════════════════════════════════
        public static void Example6_ProductionUsage()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 6: Uso em Produção");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var parser = new IngredientAllergenParser();

            var ocrText = @"
Achocolatado em Pó
TODDY
INGREDIENTES: açúcar, cacau, leite
CONTÉM: leite, soja
";

            var result = parser.Parse(ocrText);

            // ✅ Verificar confiança antes de usar os dados
            if (result.ParsingConfidence == ConfidenceLevel.High)
            {
                Console.WriteLine("✅ Dados confiáveis - Prosseguir com análise");
                Console.WriteLine($"   ProductName: {result.ProductName}");
                Console.WriteLine($"   Ingredients: {result.Ingredients.Count}");
            }
            else if (result.ParsingConfidence == ConfidenceLevel.Medium)
            {
                Console.WriteLine("⚠️  Dados parcialmente confiáveis - Revisar warnings");
                Console.WriteLine($"   Warnings: {result.ValidationWarnings.Count}");
                foreach (var warning in result.ValidationWarnings)
                {
                    Console.WriteLine($"   - {warning}");
                }
            }
            else
            {
                Console.WriteLine("❌ Dados não confiáveis - Solicitar nova imagem");
                Console.WriteLine($"   Warnings: {result.ValidationWarnings.Count}");
            }

            // ✅ Verificar validação de nome de produto
            if (result.IsProductNameValidated)
            {
                Console.WriteLine("\n✅ Nome do produto validado e confiável");
            }
            else
            {
                Console.WriteLine("\n⚠️  Nome do produto não validado - usar com cautela");
            }

            // ✅ Usar dados validados
            if (result.ProductName != null)
            {
                // Salvar no banco de dados
                Console.WriteLine($"\n💾 Salvando: {result.ProductName}");
            }
            else
            {
                // Solicitar revisão manual
                Console.WriteLine("\n👤 Revisão manual necessária");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // EXEMPLO 7: Detectar Ruído de OCR
        // ═══════════════════════════════════════════════════════════════════════════════
        public static void Example7_OcrNoiseDetection()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXEMPLO 7: Detecção de Ruído de OCR");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var parser = new IngredientAllergenParser();

            var noisyOcrText = @"
|||###
Pr0dut0 Test3
INGREDIENTES: |cacau|, [açúcar], {leite}
";

            var result = parser.Parse(noisyOcrText);

            Console.WriteLine($"ProductName: {result.ProductName ?? "null"}");
            Console.WriteLine($"Ingredients (limpos): {string.Join(", ", result.Ingredients)}");
            Console.WriteLine($"ParsingConfidence: {result.ParsingConfidence}");
            Console.WriteLine($"ValidationWarnings:");
            foreach (var warning in result.ValidationWarnings)
            {
                Console.WriteLine($"  - {warning}");
            }

            // OUTPUT ESPERADO:
            // ProductName: null (linha com símbolos inválidos)
            // Ingredients: cacau, açúcar, leite (símbolos removidos)
            // ParsingConfidence: Low ou Medium
            // ValidationWarnings:
            //   - Nenhum nome de produto válido encontrado
            //   - Texto com alto nível de ruído (X%)
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // MAIN: Executar todos os exemplos
        // ═══════════════════════════════════════════════════════════════════════════════
        public static void Main(string[] args)
        {
            Console.WriteLine("\n🧪 EXEMPLOS DE USO DO PARSER MELHORADO\n");

            Example1_SuccessfulParsing();
            Console.WriteLine("\n");

            Example2_LabelWithNutritionalTable();
            Console.WriteLine("\n");

            Example3_NoValidProductName();
            Console.WriteLine("\n");

            Example4_MultipleAllergensClassified();
            Console.WriteLine("\n");

            Example5_ConfidenceValidation();
            Console.WriteLine("\n");

            Example6_ProductionUsage();
            Console.WriteLine("\n");

            Example7_OcrNoiseDetection();

            Console.WriteLine("\n✅ Todos os exemplos executados!");
        }
    }
}
