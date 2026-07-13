// NUTRITION_ACCEPTANCE_FIX_EXAMPLES.cs
// Exemplos práticos mostrando o antes e depois da correção

using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Infrastructure.Services;

namespace LabelWise.Examples
{
    /// <summary>
    /// Exemplos práticos da correção da lógica de aceitação da análise nutricional.
    /// </summary>
    public class NutritionAcceptanceFixExamples
    {
        // ================================================================
        // EXEMPLO 1: Resposta com Category e Nutrition, mas ProductName null
        // ================================================================

        /// <summary>
        /// ❌ ANTES DA CORREÇÃO:
        /// Sistema rejeitava como falha total porque productName estava null,
        /// mesmo tendo category e nutrition profile válidos.
        /// </summary>
        public static NutritionAnalysisResponseDto Example1_Before()
        {
            return new NutritionAnalysisResponseDto
            {
                Success = false, // ❌ Marcado como falha!
                ProductName = null, // ❌ Null invalida resposta
                Category = "biscoito recheado", // ✅ Presente
                Brand = "Marca X",
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 450, // ✅ Presente
                    EstimatedSugarPer100g = 35, // ✅ Presente
                    EstimatedProteinPer100g = 5,
                    Basis = "Estimativa baseada em análise visual"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new ConsumerProfileClassificationDto
                    {
                        Status = "evitar", // ✅ Útil
                        Reason = "Alto teor de açúcar"
                    },
                    BloodPressure = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não disponível"
                    },
                    WeightLoss = new ConsumerProfileClassificationDto
                    {
                        Status = "evitar",
                        Reason = "Alto teor calórico"
                    },
                    MuscleGain = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não disponível"
                    }
                },
                ErrorMessage = "Could not interpret the nutrition analysis from the image.", // ❌ Erro genérico
                Score = new NutritionScoreDto
                {
                    Score = 45, // ❌ Score incoerente para falha!
                    Status = "regular",
                    Color = "yellow",
                    Label = "Regular"
                },
                AnalysisMode = Domain.Enums.AnalysisMode.FrontOfPackageOnly
            };
        }

        /// <summary>
        /// ✅ DEPOIS DA CORREÇÃO:
        /// Sistema aceita como sucesso, aplica fallback de productName e calcula score corretamente.
        /// </summary>
        public static NutritionAnalysisResponseDto Example1_After()
        {
            return new NutritionAnalysisResponseDto
            {
                Success = true, // ✅ Sucesso!
                ProductName = "Biscoito Recheado", // ✅ Fallback aplicado!
                Category = "biscoito recheado",
                Brand = "Marca X",
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 450,
                    EstimatedSugarPer100g = 35,
                    EstimatedProteinPer100g = 5,
                    Basis = "Estimativa baseada em análise visual"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new ConsumerProfileClassificationDto
                    {
                        Status = "evitar",
                        Reason = "Alto teor de açúcar"
                    },
                    BloodPressure = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não disponível"
                    },
                    WeightLoss = new ConsumerProfileClassificationDto
                    {
                        Status = "evitar",
                        Reason = "Alto teor calórico"
                    },
                    MuscleGain = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não disponível"
                    }
                },
                ErrorMessage = null, // ✅ Sem erro!
                Score = new NutritionScoreDto
                {
                    Score = 32, // ✅ Score calculado corretamente!
                    Status = "ruim",
                    Color = "red",
                    Label = "Ruim"
                },
                AnalysisMode = Domain.Enums.AnalysisMode.FrontOfPackageOnly
            };
        }

        // ================================================================
        // EXEMPLO 2: Resposta com SOMENTE Category
        // ================================================================

        /// <summary>
        /// ❌ ANTES DA CORREÇÃO:
        /// Sistema rejeitava mesmo tendo category disponível.
        /// </summary>
        public static NutritionAnalysisResponseDto Example2_Before()
        {
            return new NutritionAnalysisResponseDto
            {
                Success = false, // ❌ Falha
                ProductName = null,
                Category = "arroz branco tipo 1", // ✅ Único dado útil
                Brand = null,
                EstimatedNutritionProfile = null,
                Classification = null,
                ErrorMessage = "Could not interpret the nutrition analysis from the image.",
                Score = new NutritionScoreDto
                {
                    Score = 0,
                    Status = "indeterminado",
                    Color = "gray",
                    Label = "Análise insuficiente"
                },
                AnalysisMode = Domain.Enums.AnalysisMode.FrontOfPackageOnly
            };
        }

        /// <summary>
        /// ✅ DEPOIS DA CORREÇÃO:
        /// Sistema aceita como sucesso e aplica fallback de productName.
        /// </summary>
        public static NutritionAnalysisResponseDto Example2_After()
        {
            return new NutritionAnalysisResponseDto
            {
                Success = true, // ✅ Sucesso!
                ProductName = "Arroz Branco Tipo 1", // ✅ Fallback aplicado!
                Category = "arroz branco tipo 1",
                Brand = null,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    Basis = "Estimativa por categoria visual, sem leitura da tabela nutricional oficial"
                    // Outros campos null, mas isso é OK
                },
                Classification = new ProductClassificationDto
                {
                    // Todos com status "indeterminado", mas preenchidos pelo validator
                    Diabetic = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    },
                    BloodPressure = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    },
                    WeightLoss = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    },
                    MuscleGain = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    }
                },
                ErrorMessage = null, // ✅ Sem erro!
                Score = new NutritionScoreDto
                {
                    Score = 50, // ✅ Score calculado (neutro para arroz)
                    Status = "regular",
                    Color = "yellow",
                    Label = "Regular"
                },
                AnalysisMode = Domain.Enums.AnalysisMode.FrontOfPackageOnly
            };
        }

        // ================================================================
        // EXEMPLO 3: Resposta TOTALMENTE Vazia (Falha Real)
        // ================================================================

        /// <summary>
        /// ❌ ANTES DA CORREÇÃO:
        /// Sistema marcava como falha, mas score estava incoerente.
        /// </summary>
        public static NutritionAnalysisResponseDto Example3_Before()
        {
            return new NutritionAnalysisResponseDto
            {
                Success = false, // ✅ Correto
                ProductName = null,
                Category = null, // ❌ Nenhum dado
                Brand = null,
                EstimatedNutritionProfile = null,
                Classification = null,
                ErrorMessage = "Could not interpret the nutrition analysis from the image.",
                Score = new NutritionScoreDto
                {
                    Score = 28, // ❌ Score incoerente! Deveria ser 0
                    Status = "ruim",
                    Color = "red",
                    Label = "Ruim"
                },
                AnalysisMode = Domain.Enums.AnalysisMode.FrontOfPackageOnly
            };
        }

        /// <summary>
        /// ✅ DEPOIS DA CORREÇÃO:
        /// Sistema marca como falha E score é 0 (indeterminado).
        /// </summary>
        public static NutritionAnalysisResponseDto Example3_After()
        {
            return new NutritionAnalysisResponseDto
            {
                Success = false, // ✅ Correto
                ProductName = null,
                Category = null,
                Brand = null,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    Basis = "Estimativa por categoria visual, sem leitura da tabela nutricional oficial"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    },
                    BloodPressure = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    },
                    WeightLoss = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    },
                    MuscleGain = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    }
                },
                ErrorMessage = "Não foi possível interpretar dados úteis da imagem", // ✅ Mensagem atualizada
                Score = new NutritionScoreDto
                {
                    Score = 0, // ✅ Score correto para falha!
                    Status = "indeterminado",
                    Color = "gray",
                    Label = "Análise insuficiente"
                },
                AnalysisMode = Domain.Enums.AnalysisMode.FrontOfPackageOnly
            };
        }

        // ================================================================
        // EXEMPLO 4: Resposta com Nutrition Profile mas sem Category
        // ================================================================

        /// <summary>
        /// ❌ ANTES DA CORREÇÃO:
        /// Sistema rejeitava mesmo tendo nutrition profile útil.
        /// </summary>
        public static NutritionAnalysisResponseDto Example4_Before()
        {
            return new NutritionAnalysisResponseDto
            {
                Success = false, // ❌ Falha
                ProductName = null,
                Category = null, // Não tem
                Brand = null,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 380, // ✅ Dado útil!
                    EstimatedSugarPer100g = 40, // ✅ Dado útil!
                    EstimatedProteinPer100g = 6,
                    Basis = "Leitura parcial da tabela nutricional"
                },
                Classification = null,
                ErrorMessage = "Could not interpret the nutrition analysis from the image.",
                Score = new NutritionScoreDto
                {
                    Score = 0,
                    Status = "indeterminado",
                    Color = "gray",
                    Label = "Análise insuficiente"
                },
                AnalysisMode = Domain.Enums.AnalysisMode.FullNutritionLabel
            };
        }

        /// <summary>
        /// ✅ DEPOIS DA CORREÇÃO:
        /// Sistema aceita como sucesso porque tem nutrition profile.
        /// ProductName fica null (sem fallback porque não tem category).
        /// </summary>
        public static NutritionAnalysisResponseDto Example4_After()
        {
            return new NutritionAnalysisResponseDto
            {
                Success = true, // ✅ Sucesso!
                ProductName = null, // Sem fallback (sem category)
                Category = null,
                Brand = null,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    CaloriesPer100g = 380,
                    EstimatedSugarPer100g = 40,
                    EstimatedProteinPer100g = 6,
                    Basis = "Leitura parcial da tabela nutricional"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    },
                    BloodPressure = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    },
                    WeightLoss = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    },
                    MuscleGain = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não pôde ser determinada a partir da imagem."
                    }
                },
                ErrorMessage = null, // ✅ Sem erro!
                Score = new NutritionScoreDto
                {
                    Score = 35, // ✅ Score calculado com base em nutrition!
                    Status = "ruim",
                    Color = "red",
                    Label = "Ruim"
                },
                AnalysisMode = Domain.Enums.AnalysisMode.FullNutritionLabel
            };
        }

        // ================================================================
        // EXEMPLO 5: Resposta com Classification mas sem Category/Nutrition
        // ================================================================

        /// <summary>
        /// ❌ ANTES DA CORREÇÃO:
        /// Sistema rejeitava mesmo tendo classification útil.
        /// </summary>
        public static NutritionAnalysisResponseDto Example5_Before()
        {
            return new NutritionAnalysisResponseDto
            {
                Success = false, // ❌ Falha
                ProductName = null,
                Category = null,
                Brand = null,
                EstimatedNutritionProfile = null,
                Classification = new ProductClassificationDto
                {
                    Diabetic = new ConsumerProfileClassificationDto
                    {
                        Status = "evitar", // ✅ Dado útil!
                        Reason = "Produto com alto teor de açúcar adicionado"
                    },
                    BloodPressure = new ConsumerProfileClassificationDto
                    {
                        Status = "consumo_moderado", // ✅ Dado útil!
                        Reason = "Teor moderado de sódio"
                    },
                    WeightLoss = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não disponível"
                    },
                    MuscleGain = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não disponível"
                    }
                },
                ErrorMessage = "Could not interpret the nutrition analysis from the image.",
                Score = new NutritionScoreDto
                {
                    Score = 0,
                    Status = "indeterminado",
                    Color = "gray",
                    Label = "Análise insuficiente"
                },
                AnalysisMode = Domain.Enums.AnalysisMode.FrontOfPackageOnly
            };
        }

        /// <summary>
        /// ✅ DEPOIS DA CORREÇÃO:
        /// Sistema aceita como sucesso porque tem classification útil.
        /// </summary>
        public static NutritionAnalysisResponseDto Example5_After()
        {
            return new NutritionAnalysisResponseDto
            {
                Success = true, // ✅ Sucesso!
                ProductName = null,
                Category = null,
                Brand = null,
                EstimatedNutritionProfile = new EstimatedNutritionProfileDto
                {
                    Basis = "Estimativa por categoria visual, sem leitura da tabela nutricional oficial"
                },
                Classification = new ProductClassificationDto
                {
                    Diabetic = new ConsumerProfileClassificationDto
                    {
                        Status = "evitar",
                        Reason = "Produto com alto teor de açúcar adicionado"
                    },
                    BloodPressure = new ConsumerProfileClassificationDto
                    {
                        Status = "consumo_moderado",
                        Reason = "Teor moderado de sódio"
                    },
                    WeightLoss = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não disponível"
                    },
                    MuscleGain = new ConsumerProfileClassificationDto
                    {
                        Status = "indeterminado",
                        Reason = "Classificação não disponível"
                    }
                },
                ErrorMessage = null, // ✅ Sem erro!
                Score = new NutritionScoreDto
                {
                    Score = 25, // ✅ Score calculado com base em classification!
                    Status = "ruim",
                    Color = "red",
                    Label = "Ruim"
                },
                AnalysisMode = Domain.Enums.AnalysisMode.FrontOfPackageOnly
            };
        }

        // ================================================================
        // TABELA RESUMO DE EXEMPLOS
        // ================================================================

        /*
        ┌────────────┬──────────────────────────────────┬─────────────────┬─────────────────┐
        │  Exemplo   │          Dados Presentes          │  Antes (Falha)  │  Depois (OK)    │
        ├────────────┼──────────────────────────────────┼─────────────────┼─────────────────┤
        │ Exemplo 1  │ Category + Nutrition             │ ❌ success=false│ ✅ success=true │
        │            │ (productName null)               │ ❌ score=45     │ ✅ score=32     │
        │            │                                  │                 │ ✅ fallback OK  │
        ├────────────┼──────────────────────────────────┼─────────────────┼─────────────────┤
        │ Exemplo 2  │ Só Category                      │ ❌ success=false│ ✅ success=true │
        │            │                                  │ ❌ score=0      │ ✅ score=50     │
        │            │                                  │                 │ ✅ fallback OK  │
        ├────────────┼──────────────────────────────────┼─────────────────┼─────────────────┤
        │ Exemplo 3  │ Nenhum dado                      │ ❌ success=false│ ❌ success=false│
        │            │ (Falha real)                     │ ❌ score=28     │ ✅ score=0      │
        ├────────────┼──────────────────────────────────┼─────────────────┼─────────────────┤
        │ Exemplo 4  │ Só Nutrition Profile             │ ❌ success=false│ ✅ success=true │
        │            │                                  │ ❌ score=0      │ ✅ score=35     │
        ├────────────┼──────────────────────────────────┼─────────────────┼─────────────────┤
        │ Exemplo 5  │ Só Classification                │ ❌ success=false│ ✅ success=true │
        │            │                                  │ ❌ score=0      │ ✅ score=25     │
        └────────────┴──────────────────────────────────┴─────────────────┴─────────────────┘
        */
    }
}
