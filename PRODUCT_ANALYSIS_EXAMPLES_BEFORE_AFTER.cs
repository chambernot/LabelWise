// ═══════════════════════════════════════════════════════════════════════════════
// EXEMPLOS DE ANÁLISE: BEFORE vs AFTER
// Demonstração das melhorias no sistema de análise de produtos LabelWise
// ═══════════════════════════════════════════════════════════════════════════════

using LabelWise.Application.DTOs;
using System.Collections.Generic;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos práticos demonstrando as melhorias implementadas no sistema
    /// </summary>
    public class ProductAnalysisBeforeAfterExamples
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 1: BISCOITO RECHEADO (Produto Ultraprocessado)
        // ═══════════════════════════════════════════════════════════════════════════

        public static ProductAnalysisResultDto BeforeBiscoito = new()
        {
            ProductName = "Biscoito Recheado Chocolate",
            Brand = "Porção 30g (3 unidades)", // ❌ INCORRETO - dados da tabela nutricional
            
            GeneralScore = 0.70,        // ❌ Score muito alto
            PersonalizedScore = 0.70,   // ❌ Score muito alto
            Classification = "Safe",     // ❌ Classificação otimista demais
            
            ShortSummary = "Produto seguro (nota 7/10). Pode consumir com tranquilidade.", // ❌ Frases otimistas
            
            ExtractedAllergens = new List<string> 
            { 
                "glúten", "leite", "soja", "amendoim", "castanha" // ❌ Tudo misturado
            },
            
            Alerts = new List<string>
            {
                "Declared allergen: glúten",
                "Declared allergen: leite"
                // ❌ Faltam alertas sobre ultraprocessamento
            },
            
            Recommendations = new List<string>
            {
                "Read the ingredient list carefully and compare serving sizes.",
                "This product seems compatible with your profile." // ❌ Muito otimista
            }
        };

        public static ProductAnalysisResultDto AfterBiscoito = new()
        {
            ProductName = "Biscoito Recheado Chocolate",
            Brand = null, // ✅ Corrigido - não captura dados da tabela
            
            GeneralScore = 0.35,        // ✅ Score realista
            PersonalizedScore = 0.30,   // ✅ Score realista considerando perfil
            Classification = "Avoid",    // ✅ Classificação correta
            
            ShortSummary = "Não recomendado (nota 3/10). Evitar este produto.", // ✅ Linguagem realista
            
            // ✅ Alergênicos separados (nova funcionalidade)
            ExtractedAllergens = new List<string> 
            { 
                "glúten", "leite", "soja", "amendoim", "castanha"
            },
            // Propriedades adicionais (não mostradas aqui mas disponíveis):
            // ConfirmedAllergens = ["glúten", "leite", "soja"]
            // MayContainAllergens = ["amendoim", "castanha"]
            
            Alerts = new List<string>
            {
                "⚠️ Contém gordura hidrogenada - associada a riscos cardiovasculares",
                "⚠️ Contém 3 tipos de aditivos químicos",
                "⚠️ Contém açúcares de alto índice glicêmico (xarope, maltodextrina, etc.)",
                "⚠️ Alto teor de açúcar combinado com baixa fibra",
                "⚠️ Lista extensa de ingredientes (18 itens) - indicador de ultraprocessamento",
                "🚨 PRODUTO ULTRAPROCESSADO - Consumir esporadicamente",
                "Declared allergen: glúten",
                "Declared allergen: leite",
                "Declared allergen: soja"
            },
            
            Recommendations = new List<string>
            {
                "⚠️ Leia atentamente a lista de ingredientes e alertas identificados",
                "🚫 Evite este produto - não recomendado para seu perfil",
                "🍬 Alto teor de açúcar - limite o consumo",
                "⚠️ Baixo teor de fibras - complemente com outros alimentos",
                "⚠️ Produto contém alergênicos declarados - verifique compatibilidade"
            }
        };

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 2: IOGURTE NATURAL (Produto Saudável)
        // ═══════════════════════════════════════════════════════════════════════════

        public static ProductAnalysisResultDto BeforeIogurte = new()
        {
            ProductName = "Iogurte Natural",
            Brand = "Informação Nutricional", // ❌ Capturou header da tabela
            
            GeneralScore = 0.80,
            PersonalizedScore = 0.85,
            Classification = "Safe",
            
            ShortSummary = "Produto seguro (nota 8/10). Pode consumir com tranquilidade.",
            
            ExtractedAllergens = new List<string> { "leite" },
            
            Alerts = new List<string>
            {
                "Declared allergen: leite"
            },
            
            Recommendations = new List<string>
            {
                "Read the ingredient list carefully and compare serving sizes.",
                "This product seems compatible with your profile."
            }
        };

        public static ProductAnalysisResultDto AfterIogurte = new()
        {
            ProductName = "Iogurte Natural",
            Brand = "YogurBrand", // ✅ Marca correta identificada
            
            GeneralScore = 0.85,
            PersonalizedScore = 0.82,
            Classification = "Safe", // ✅ Mantido como "Safe" - produto realmente saudável
            
            ShortSummary = "Produto adequado (nota 8/10). Compatível com consumo regular.",
            
            ExtractedAllergens = new List<string> { "leite" },
            // ConfirmedAllergens = ["leite"]
            // MayContainAllergens = []
            
            Alerts = new List<string>
            {
                "Declared allergen: leite"
            },
            
            Recommendations = new List<string>
            {
                "⚠️ Leia atentamente a lista de ingredientes e alertas identificados",
                "✓ Produto adequado: Compatível com perfil saudável",
                "⚠️ Produto contém alergênicos declarados - verifique compatibilidade"
            }
        };

        // ═══════════════════════════════════════════════════════════════════════════
        // EXEMPLO 3: SUCO INDUSTRIALIZADO (Produto Processado)
        // ═══════════════════════════════════════════════════════════════════════════

        public static ProductAnalysisResultDto BeforeSuco = new()
        {
            ProductName = "Suco de Laranja",
            Brand = "200ml",
            
            GeneralScore = 0.65,
            PersonalizedScore = 0.60,
            Classification = "Safe",
            
            ShortSummary = "Produto seguro (nota 6/10). Pode consumir com tranquilidade.",
            
            ExtractedAllergens = new List<string>(),
            
            Alerts = new List<string>
            {
                "Contains lactose or milk-derived ingredients"
            },
            
            Recommendations = new List<string>
            {
                "Consume with caution and monitor portions."
            }
        };

        public static ProductAnalysisResultDto AfterSuco = new()
        {
            ProductName = "Suco de Laranja",
            Brand = "SucoMax",
            
            GeneralScore = 0.50,
            PersonalizedScore = 0.45,
            Classification = "Caution", // ✅ Mais realista para suco industrializado
            
            ShortSummary = "Atenção necessária (nota 5/10). Consumir esporadicamente.",
            
            ExtractedAllergens = new List<string>(),
            
            Alerts = new List<string>
            {
                "⚠️ Contém açúcares de alto índice glicêmico (xarope, maltodextrina, etc.)",
                "⚠️ Alto teor de açúcar combinado com baixa fibra",
                "⚠️ Contém 2 tipos de aditivos químicos"
            },
            
            Recommendations = new List<string>
            {
                "⚠️ Leia atentamente a lista de ingredientes e alertas identificados",
                "⚠️ Consumo esporádico: Não recomendado para consumo frequente",
                "🍬 Alto teor de açúcar - limite o consumo",
                "⚠️ Baixo teor de fibras - complemente com outros alimentos"
            }
        };

        // ═══════════════════════════════════════════════════════════════════════════
        // TABELA COMPARATIVA DE SCORES
        // ═══════════════════════════════════════════════════════════════════════════

        public static string GetComparativeTable()
        {
            return @"
╔═══════════════════════════════════════════════════════════════════════════════╗
║                    COMPARAÇÃO DE SCORES: BEFORE vs AFTER                       ║
╠═══════════════════════════════════════════════════════════════════════════════╣
║                                                                                ║
║  PRODUTO               │ BEFORE                │ AFTER                         ║
║  ─────────────────────────────────────────────────────────────────────────────║
║  Biscoito Recheado     │ 0.70 (Safe)          │ 0.35 (Avoid) ✅               ║
║                        │ "Pode consumir..."    │ "Evitar este produto"         ║
║  ─────────────────────────────────────────────────────────────────────────────║
║  Iogurte Natural       │ 0.85 (Safe)          │ 0.85 (Safe) ✅                ║
║                        │ Brand: "Info Nutri"   │ Brand: "YogurBrand"           ║
║  ─────────────────────────────────────────────────────────────────────────────║
║  Suco Industrializado  │ 0.65 (Safe)          │ 0.50 (Caution) ✅             ║
║                        │ "Produto seguro"      │ "Consumir esporadicamente"    ║
║                                                                                ║
╠═══════════════════════════════════════════════════════════════════════════════╣
║  MELHORIAS PRINCIPAIS:                                                         ║
║  ✅ Scores mais realistas para ultraprocessados                               ║
║  ✅ Extração correta de marca (não captura tabela nutricional)                ║
║  ✅ Separação de alergênicos (contém vs pode conter)                          ║
║  ✅ Linguagem realista e não otimista                                          ║
║  ✅ Alertas detalhados sobre ultraprocessamento                                ║
╚═══════════════════════════════════════════════════════════════════════════════╝
";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // INGREDIENTES QUE ACIONAM ALERTAS DE ULTRAPROCESSAMENTO
        // ═══════════════════════════════════════════════════════════════════════════

        public static class UltraProcessedTriggers
        {
            public static readonly string[] HydrogenatedFat = new[]
            {
                "gordura vegetal hidrogenada",
                "gordura hidrogenada",
                "óleo hidrogenado",
                "parcialmente hidrogenada"
            };

            public static readonly string[] ArtificialAdditives = new[]
            {
                "aromatizante",
                "corante",
                "emulsificante",
                "espessante",
                "estabilizante",
                "realçador de sabor",
                "conservante",
                "acidulante"
            };

            public static readonly string[] HighGlycemic = new[]
            {
                "xarope de glicose",
                "xarope de milho",
                "maltodextrina",
                "dextrose",
                "açúcar invertido"
            };

            public static string GetPenaltyExplanation()
            {
                return @"
PENALIDADES APLICADAS POR ULTRAPROCESSAMENTO:

1. GORDURA HIDROGENADA:
   - Penalidade: -0.25 (generalScore e personalizedScore)
   - Alerta: ⚠️ Riscos cardiovasculares

2. MÚLTIPLOS ADITIVOS (≥3):
   - Penalidade: -0.15
   - Alerta: ⚠️ Contém X tipos de aditivos químicos

3. AÇÚCARES DE ALTO ÍNDICE GLICÊMICO:
   - Penalidade: -0.10
   - Alerta: ⚠️ Xarope, maltodextrina, etc.

4. ALTO AÇÚCAR + BAIXA FIBRA:
   - Penalidade: -0.15
   - Alerta: ⚠️ Combo ruim para saúde

5. MUITOS INGREDIENTES (>15):
   - Penalidade: -0.10
   - Alerta: ⚠️ Indicador de ultraprocessamento

6. SCORE DE ULTRAPROCESSAMENTO ≥5:
   - Penalidade: Força classification para 'Avoid'
   - Alerta: 🚨 PRODUTO ULTRAPROCESSADO
";
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // NOVOS LIMIARES DE CLASSIFICAÇÃO
        // ═══════════════════════════════════════════════════════════════════════════

        public static class ClassificationThresholds
        {
            public const double SAFE_MIN_AVG = 0.80;     // Before: 0.75
            public const double SAFE_MIN_INDIVIDUAL = 0.70;  // Novo requisito

            public const double MODERATE_MIN_AVG = 0.65; // Before: 0.60
            public const double MODERATE_MIN_INDIVIDUAL = 0.50; // Novo requisito

            public const double CAUTION_MIN_AVG = 0.50;  // Before: 0.40
            
            public const double AVOID_THRESHOLD = 0.35;  // Novo limiar

            public static string GetClassificationLogic()
            {
                return @"
LÓGICA DE CLASSIFICAÇÃO (AFTER):

1. SAFE (avgScore ≥ 0.80 E minScore ≥ 0.70):
   - Ambos scores devem ser altos
   - ShortSummary: 'Produto adequado (nota X/10). Compatível com consumo regular.'

2. MODERATE (avgScore ≥ 0.65 E minScore ≥ 0.50):
   - Aceitável com moderação
   - ShortSummary: 'Consumo moderado (nota X/10). Atenção à frequência e porções.'

3. CAUTION (avgScore ≥ 0.50):
   - Atenção necessária
   - ShortSummary: 'Atenção necessária (nota X/10). Consumir esporadicamente.'

4. AVOID (avgScore < 0.50):
   - Evitar
   - ShortSummary: 'Não recomendado (nota X/10). Evitar este produto.'

NOTA: Usa o MENOR score entre geral e personalizado (mais conservador)
";
            }
        }
    }
}
