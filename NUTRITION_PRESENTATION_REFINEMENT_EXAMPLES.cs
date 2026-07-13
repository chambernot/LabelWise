using System.Collections.Generic;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Presentation;
using LabelWise.Domain.Enums;

namespace LabelWise.Documentation.Examples
{
    /// <summary>
    /// Exemplos comparativos: antes e depois do refinamento da apresentação nutricional.
    /// </summary>
    public class NutritionPresentationRefinementExamples
    {
        /// <summary>
        /// Exemplo 1: Achocolatado em Pó com Alto Teor de Açúcar
        /// </summary>
        public static void Example1_AchocolatadoAltoacucar()
        {
            // ====================================
            // DADOS DE ENTRADA
            // ====================================
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Achocolatado em Pó Fortificado",
                Brand = "Toddy",
                Category = "Achocolatado em pó",
                PackageWeight = "400g",
                AnalysisMode = AnalysisMode.FullNutritionLabel,
                
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 380,
                    EstimatedSugarPer100g = 76,        // 🔴 MUITO ALTO
                    EstimatedProteinPer100g = 4,
                    EstimatedSodiumPer100g = 150,
                    EstimatedFiberPer100g = 3,
                    EstimatedFatPer100g = 2.5,
                    Basis = "Leitura da tabela nutricional presente no rótulo"
                },
                
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult 
                    { 
                        Status = "nao_recomendado", 
                        Reason = "Alto teor de açúcar (76g/100g); não adequado para diabéticos" 
                    },
                    BloodPressure = new HealthProfileResult 
                    { 
                        Status = "consumo_moderado", 
                        Reason = "Teor moderado de sódio (150mg/100g); consumo ocasional" 
                    },
                    WeightLoss = new HealthProfileResult 
                    { 
                        Status = "nao_recomendado", 
                        Reason = "Alta densidade calórica (380kcal/100g) e alto teor de açúcar" 
                    },
                    MuscleGain = new HealthProfileResult 
                    { 
                        Status = "fraco", 
                        Reason = "Baixo teor proteico (4g/100g); não relevante para ganho muscular" 
                    }
                }
            };

            // ====================================
            // PROCESSAMENTO COM MOTOR REFINADO
            // ====================================
            var presentation = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // ====================================
            // RESULTADO REFINADO
            // ====================================
            /*
            {
                "nutritionalScore": {
                    "value": 38,                                    ✓ Cap aplicado (max 48 para achocolatado)
                    "label": "Evitar consumo frequente",           ✓ Label claro (não "Moderado")
                    "status": "ruim",
                    "color": "#f97316",
                    "reason": "Açúcar: 76g/100g. Alto teor de açúcar. Reservar para ocasiões especiais."
                },
                
                "summary": "Achocolatado em Pó Fortificado contém teor extremamente elevado de açúcar (76g/100g). Dados extraídos da tabela nutricional presente no rótulo. Não recomendado para diabéticos ou quem busca controle de peso.",
                ✓ Direto e objetivo
                ✓ Destaca ofensor principal com valor
                ✓ Fornece contexto (tabela nutricional)
                ✓ Recomendação clara
                
                "alerts": [
                    "⚠️ Nível extremamente elevado de açúcar. Pode comprometer seriamente o controle glicêmico.",
                    "🚫 Não recomendado para diabéticos devido ao alto teor de açúcar.",
                    "🚫 Não recomendado para emagrecimento: alto teor de açúcar e calorias vazias."
                ],
                ✓ Alertas específicos por perfil de saúde
                ✓ Impacto explícito do ofensor
                ✓ Recomendações acionáveis
                
                "mainOffender": {
                    "nutrient": "Açúcar",
                    "value": 76,
                    "unit": "g",
                    "severity": 95,
                    "impactMessage": "Nível extremamente elevado de açúcar. Pode comprometer seriamente o controle glicêmico."
                }
            }
            */

            // ====================================
            // COMPARAÇÃO: ANTES vs DEPOIS
            // ====================================
            
            // ANTES (Sistema Antigo):
            // Score: 52 ("Moderado") ❌ Muito otimista
            // Summary: "Produto com perfil intermediário para a categoria" ❌ Genérico
            // Alerts: [] ❌ Sem alertas específicos
            
            // DEPOIS (Sistema Refinado):
            // Score: 38 ("Evitar consumo frequente") ✓ Realista
            // Summary: "...contém teor extremamente elevado de açúcar (76g/100g)..." ✓ Específico
            // Alerts: 3 alertas contextualizados ✓ Acionáveis
        }

        /// <summary>
        /// Exemplo 2: Sobremesa Láctea Doce
        /// </summary>
        public static void Example2_SobremesaLactea()
        {
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Chandelle Mousse de Chocolate",
                Brand = "Nestlé",
                Category = "Sobremesa láctea",
                PackageWeight = "120g",
                AnalysisMode = AnalysisMode.FullNutritionLabel,
                
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 320,
                    EstimatedSugarPer100g = 28,        // 🔴 ALTO
                    EstimatedProteinPer100g = 2.8,
                    EstimatedSodiumPer100g = 100,
                    EstimatedFiberPer100g = 0.5,
                    EstimatedFatPer100g = 10,
                    Basis = "Leitura parcial da tabela nutricional (calorias, açúcares e gorduras extraídos)"
                },
                
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "nao_recomendado" },
                    BloodPressure = new HealthProfileResult { Status = "adequado" },
                    WeightLoss = new HealthProfileResult { Status = "nao_recomendado" },
                    MuscleGain = new HealthProfileResult { Status = "fraco" }
                }
            };

            var presentation = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // RESULTADO:
            // Score: 39 (cap de 42 aplicado para sobremesa láctea) ✓
            // Label: "Evitar consumo frequente" ✓
            // Summary: "Chandelle Mousse de Chocolate apresenta alto teor de açúcar (28g/100g)..." ✓
            // MainOffender: Açúcar (28g/100g, severidade 85) ✓
        }

        /// <summary>
        /// Exemplo 3: Queijo Cottage (Alto Proteína - Positivo)
        /// </summary>
        public static void Example3_QueijoCottage()
        {
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Queijo Cottage Light",
                Brand = "Vigor",
                Category = "Queijo fresco",
                PackageWeight = "200g",
                AnalysisMode = AnalysisMode.FullNutritionLabel,
                
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 98,
                    EstimatedSugarPer100g = 3,         // ✓ BAIXO
                    EstimatedProteinPer100g = 22,      // ✓ ALTO (bônus +8)
                    EstimatedSodiumPer100g = 400,
                    EstimatedFiberPer100g = 0,
                    EstimatedFatPer100g = 2,
                    Basis = "Leitura da tabela nutricional presente no rótulo"
                },
                
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "adequado" },
                    BloodPressure = new HealthProfileResult { Status = "consumo_moderado" },
                    WeightLoss = new HealthProfileResult { Status = "adequado" },
                    MuscleGain = new HealthProfileResult { Status = "adequado" }
                }
            };

            var presentation = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // RESULTADO:
            // Score: 72 (bônus por alta proteína) ✓
            // Label: "Boa escolha" ✓
            // Summary: "Queijo Cottage Light oferece bom aporte proteico (22g/100g)..." ✓
            // MainOffender: null (nenhum ofensor crítico) ✓
            // Alerts: ["⚠️ Hipertensos devem limitar o consumo devido ao sódio."] ✓
        }

        /// <summary>
        /// Exemplo 4: Produto Sem Tabela Nutricional (Estimativa)
        /// </summary>
        public static void Example4_SemTabelaNutricional()
        {
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Arroz Branco Tipo 1",
                Category = "Arroz branco",
                PackageWeight = "5kg",
                AnalysisMode = AnalysisMode.FrontOfPackageOnly,    // SEM tabela nutricional
                
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 360,
                    EstimatedSugarPer100g = 0.5,
                    EstimatedProteinPer100g = 7,
                    EstimatedSodiumPer100g = 5,
                    EstimatedFiberPer100g = 1,
                    EstimatedFatPer100g = 0.5,
                    Basis = "Estimativa padronizada por 100g para a categoria (tabela nutricional não visível)"
                },
                
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "consumo_moderado" },
                    BloodPressure = new HealthProfileResult { Status = "adequado" },
                    WeightLoss = new HealthProfileResult { Status = "consumo_moderado" },
                    MuscleGain = new HealthProfileResult { Status = "consumo_moderado" }
                }
            };

            var presentation = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // RESULTADO:
            // Summary: "Arroz Branco Tipo 1 caracterizado como fonte primária de carboidratos. Análise baseada na categoria do produto devido à ausência de tabela nutricional legível." ✓
            // Alerts: ["ℹ️ Valores nutricionais estimados por categoria. Para análise precisa, capture a tabela nutricional."] ✓
        }

        /// <summary>
        /// Exemplo 5: Salgadinho (Alto Sódio)
        /// </summary>
        public static void Example5_SalgadinhoAltoSodio()
        {
            var analysis = new NutritionAnalysisResponseDto
            {
                Success = true,
                ProductName = "Salgadinho de Milho",
                Category = "Salgadinho",
                AnalysisMode = AnalysisMode.FullNutritionLabel,
                
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 520,
                    EstimatedSugarPer100g = 2,
                    EstimatedProteinPer100g = 6,
                    EstimatedSodiumPer100g = 1100,     // 🔴 MUITO ALTO
                    EstimatedFiberPer100g = 3,
                    EstimatedFatPer100g = 32,          // 🔴 ALTO
                    Basis = "Leitura da tabela nutricional"
                },
                
                Classification = new ProductClassificationDto
                {
                    Diabetic = new HealthProfileResult { Status = "consumo_moderado" },
                    BloodPressure = new HealthProfileResult { Status = "nao_recomendado" },
                    WeightLoss = new HealthProfileResult { Status = "nao_recomendado" },
                    MuscleGain = new HealthProfileResult { Status = "fraco" }
                }
            };

            var presentation = NutritionPresentationEngine.ProcessForPresentation(analysis);

            // RESULTADO:
            // MainOffender: Sódio (1100mg/100g, severidade 90) ou Gordura (32g/100g, severidade 85)
            // Score: <= 35 (cap aplicado) ✓
            // Label: "Evitar" ✓
            // Alerts: Inclui alerta para hipertensos ✓
        }

        /// <summary>
        /// Exemplo 6: Comparação Direta - Labels User-Friendly
        /// </summary>
        public static void Example6_LabelsComparison()
        {
            // ==========================================
            // ANTES (Labels Genéricos)
            // ==========================================
            /*
            Score 85-100: "Muito saudável"       → OK
            Score 70-84:  "Boa escolha"          → OK
            Score 50-69:  "Atenção"              → ❌ Genérico
            Score 30-49:  "Consumo ocasional"    → ❌ Vago
            Score 0-29:   "Evitar consumo"       → OK
            */

            // ==========================================
            // DEPOIS (Labels Claros e Acionáveis)
            // ==========================================
            /*
            Score 80-100: "Excelente escolha"           → ✓ Mais positivo
            Score 65-79:  "Boa escolha"                 → ✓ OK
            Score 50-64:  "Consumo com atenção"         → ✓ Mais específico
            Score 35-49:  "Evitar consumo frequente"    → ✓ Recomendação clara
            Score 0-34:   "Evitar"                      → ✓ Direto
            */
        }

        /// <summary>
        /// Exemplo 7: Reason no Score - Informação Completa
        /// </summary>
        public static void Example7_ScoreReason()
        {
            // ==========================================
            // ANTES
            // ==========================================
            /*
            {
                "value": 45,
                "label": "Consumo ocasional",
                "reason": ""                  ❌ Vazio
            }
            */

            // ==========================================
            // DEPOIS
            // ==========================================
            /*
            {
                "value": 38,
                "label": "Evitar consumo frequente",
                "reason": "Açúcar: 75g/100g. Alto teor de açúcar. Reservar para ocasiões especiais."
                ✓ Inclui ofensor principal
                ✓ Inclui valor específico
                ✓ Inclui recomendação
            }
            */
        }

        /// <summary>
        /// Exemplo 8: Caps por Categoria - Calibração Precisa
        /// </summary>
        public static void Example8_CategoryCaps()
        {
            // ==========================================
            // CAPS APLICADOS
            // ==========================================
            /*
            Achocolatado:       max 48 pontos
            Sobremesa láctea:   max 42 pontos
            Biscoito recheado:  max 38 pontos
            Refrigerante:       max 30 pontos
            Salgadinho:         max 35 pontos
            Chocolate:          max 45 pontos
            
            Ofensor >= 85:      max 45 pontos (qualquer categoria)
            */

            // ANTES: Achocolatado com alto açúcar → Score 52 ❌
            // DEPOIS: Achocolatado com alto açúcar → Score 38-48 (com cap) ✓
        }
    }
}
