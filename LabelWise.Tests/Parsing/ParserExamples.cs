using LabelWise.Domain.Parsing;

namespace LabelWise.Tests.Parsing;

/// <summary>
/// Exemplos de uso do NutritionTableParser
/// </summary>
public static class ParserExamples
{
    /// <summary>
    /// Exemplo 1: Texto OCR do requisito original
    /// </summary>
    public static void Example1_RealOcrText()
    {
        var ocrText = @"INFORMAÇÃO NUTRICIONAL
Porções por embalagem: Cerca de 4 · Porção: 30 g (5 unidades)

100 g 30 g %VD* 100 g 30 g %VD

Valor energético (kcal) 519 158 8
Carboidratos (g
14
Açúcares totais (g) 0,6
Açúcares adicionados (g) 1,3 0,4 1
Proteinas (g) 5.2 1,6 3
Gorduras totais (g) 33 10 15
Gorduras saturadas (g) 18 5.4 27
Gorduras trans (g) 0 0
Fibras alimentares (g) 8,7 2.6 11
Sódio (mg) 95 29 1";

        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("EXEMPLO 1: Texto OCR Real (Requisito Original)");
        Console.WriteLine("═══════════════════════════════════════════════════\n");

        var parser = new NutritionTableParser();
        var result = parser.Parse(ocrText);

        PrintResult(result);
    }

    /// <summary>
    /// Exemplo 2: Texto OCR com quebras de linha problemáticas
    /// </summary>
    public static void Example2_BrokenLines()
    {
        var ocrText = @"TABELA NUTRICIONAL
Valor energético (kcal)
436
12
Carboidratos (g)
46
14
Proteínas (g)
5,2
Gorduras totais (g)
33
Sódio (mg)
95";

        Console.WriteLine("\n═══════════════════════════════════════════════════");
        Console.WriteLine("EXEMPLO 2: OCR com Quebras de Linha Problemáticas");
        Console.WriteLine("═══════════════════════════════════════════════════\n");

        var parser = new NutritionTableParser();
        var result = parser.Parse(ocrText);

        PrintResult(result);
    }

    /// <summary>
    /// Exemplo 3: Texto OCR bagunçado (teste de robustez)
    /// </summary>
    public static void Example3_MessyOcr()
    {
        var ocrText = @"Info Nutric
Val energia 519 kcal
Carbo 46g
Prot 5,2
Gord 33
Na 95mg";

        Console.WriteLine("\n═══════════════════════════════════════════════════");
        Console.WriteLine("EXEMPLO 3: OCR Bagunçado (Teste de Robustez)");
        Console.WriteLine("═══════════════════════════════════════════════════\n");

        var parser = new NutritionTableParser();
        var result = parser.Parse(ocrText);

        PrintResult(result);
    }

    /// <summary>
    /// Exemplo 4: Teste de validação (calorias inconsistentes)
    /// </summary>
    public static void Example4_ValidationTest()
    {
        var ocrText = @"INFORMAÇÃO NUTRICIONAL
Valor energético: 519 kcal
Carboidratos: 14 g  <-- ERRO: deveria ser 46g
Proteínas: 5.2 g
Gorduras totais: 33 g
Sódio: 95 mg";

        Console.WriteLine("\n═══════════════════════════════════════════════════");
        Console.WriteLine("EXEMPLO 4: Validação Automática (Correção de Erro)");
        Console.WriteLine("═══════════════════════════════════════════════════\n");

        var parser = new NutritionTableParser();
        var result = parser.Parse(ocrText);

        PrintResult(result);
    }

    private static void PrintResult(NutritionData data)
    {
        Console.WriteLine("📊 RESULTADO DO PARSING:");
        Console.WriteLine($"   Estratégia: {data.ParsingStrategy ?? "N/A"}");
        Console.WriteLine($"   Confiança: {data.ConfidenceScore}/100");
        Console.WriteLine($"   Nutrientes: {data.ExtractedNutrientsCount}");
        Console.WriteLine($"   Unidade: {data.Unit}");
        Console.WriteLine();

        Console.WriteLine("📋 VALORES EXTRAÍDOS:");
        if (data.CaloriesPer100g.HasValue)
            Console.WriteLine($"   • Calorias: {data.CaloriesPer100g:F0} kcal");
        if (data.CarbsPer100g.HasValue)
            Console.WriteLine($"   • Carboidratos: {data.CarbsPer100g:F1} g");
        if (data.SugarPer100g.HasValue)
            Console.WriteLine($"   • Açúcares totais: {data.SugarPer100g:F1} g");
        if (data.AddedSugarPer100g.HasValue)
            Console.WriteLine($"   • Açúcares adicionados: {data.AddedSugarPer100g:F1} g");
        if (data.ProteinPer100g.HasValue)
            Console.WriteLine($"   • Proteínas: {data.ProteinPer100g:F1} g");
        if (data.FatPer100g.HasValue)
            Console.WriteLine($"   • Gorduras totais: {data.FatPer100g:F1} g");
        if (data.SaturatedFatPer100g.HasValue)
            Console.WriteLine($"   • Gorduras saturadas: {data.SaturatedFatPer100g:F1} g");
        if (data.FiberPer100g.HasValue)
            Console.WriteLine($"   • Fibras: {data.FiberPer100g:F1} g");
        if (data.SodiumPer100g.HasValue)
            Console.WriteLine($"   • Sódio: {data.SodiumPer100g:F0} mg");
        Console.WriteLine();

        if (data.Warnings.Count > 0)
        {
            Console.WriteLine("⚠️ WARNINGS:");
            foreach (var warning in data.Warnings)
            {
                Console.WriteLine($"   - {warning}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("✅ RESUMO:");
        Console.WriteLine($"   {data.GetSummary()}");
        Console.WriteLine();

        Console.WriteLine("🔍 STATUS:");
        Console.WriteLine($"   Dados mínimos: {(data.HasMinimumData ? "✅ SIM" : "❌ NÃO")}");
        Console.WriteLine($"   Completo: {(data.IsComplete ? "✅ SIM" : "❌ NÃO")}");
    }

    /// <summary>
    /// Executar todos os exemplos
    /// </summary>
    public static void RunAllExamples()
    {
        Example1_RealOcrText();
        Example2_BrokenLines();
        Example3_MessyOcr();
        Example4_ValidationTest();
    }
}
