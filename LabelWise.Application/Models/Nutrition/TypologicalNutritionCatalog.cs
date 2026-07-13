using System;
using System.Collections.Generic;

namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Perfil nutricional típico para uma tipologia alimentar.
/// Baseado em dados médios de referência nutricional, não em heurísticas de produto específico.
/// </summary>
public class TypologicalNutritionProfile
{
    public FoodTypology Typology { get; set; }
    public string Description { get; set; } = string.Empty;

    // Valores típicos por 100g (ou 100ml para líquidos)
    public NutrientRange Calories { get; set; } = new();
    public NutrientRange Protein { get; set; } = new();
    public NutrientRange Fat { get; set; } = new();
    public NutrientRange Carbohydrates { get; set; } = new();
    public NutrientRange Sugar { get; set; } = new();
    public NutrientRange Fiber { get; set; } = new();
    public NutrientRange Sodium { get; set; } = new();

    /// <summary>
    /// Nível de confiança do perfil (0.0 a 1.0).
    /// Perfis bem estabelecidos têm confiança alta.
    /// </summary>
    public double Confidence { get; set; } = 0.7;

    /// <summary>
    /// Indica se o perfil é baseado em dados reais ou estimativa genérica.
    /// </summary>
    public bool IsEstimated { get; set; } = true;
}

/// <summary>
/// Range de valores para um nutriente (mínimo, típico, máximo).
/// </summary>
public class NutrientRange
{
    /// <summary>
    /// Valor mínimo esperado para a tipologia.
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Valor típico/médio para a tipologia.
    /// </summary>
    public double Typical { get; set; }

    /// <summary>
    /// Valor máximo esperado para a tipologia.
    /// </summary>
    public double Max { get; set; }

    public NutrientRange() { }

    public NutrientRange(double min, double typical, double max)
    {
        Min = min;
        Typical = typical;
        Max = max;
    }

    /// <summary>
    /// Verifica se um valor está dentro do range esperado.
    /// </summary>
    public bool IsInRange(double value)
    {
        return value >= Min && value <= Max;
    }

    /// <summary>
    /// Ajusta um valor para o range se estiver fora.
    /// </summary>
    public double ClampToRange(double value)
    {
        return Math.Max(Min, Math.Min(Max, value));
    }
}

/// <summary>
/// Catálogo de perfis nutricionais por tipologia alimentar.
/// Fonte: dados médios de tabelas nutricionais brasileiras (TACO, IBGE, Anvisa).
/// </summary>
public static class TypologicalNutritionCatalog
{
    private static readonly Dictionary<FoodTypology, TypologicalNutritionProfile> _catalog;

    static TypologicalNutritionCatalog()
    {
        _catalog = new Dictionary<FoodTypology, TypologicalNutritionProfile>
        {
            // === LATICÍNIOS ===

            [FoodTypology.DairyCreamyFull] = new()
            {
                Typology = FoodTypology.DairyCreamyFull,
                Description = "Laticínios cremosos tradicionais (requeijão, cream cheese)",
                Calories = new(180, 220, 280),
                Protein = new(5, 8, 12),
                Fat = new(15, 20, 30),
                Carbohydrates = new(2, 4, 8),
                Sugar = new(0, 2, 5),
                Fiber = new(0, 0, 0),
                Sodium = new(300, 450, 700),
                Confidence = 0.85,
                IsEstimated = false
            },

            [FoodTypology.DairyCreamyLight] = new()
            {
                Typology = FoodTypology.DairyCreamyLight,
                Description = "Laticínios cremosos light/reduzidos",
                Calories = new(100, 140, 180),
                Protein = new(6, 10, 14),
                Fat = new(5, 8, 15),
                Carbohydrates = new(3, 5, 8),
                Sugar = new(0, 2, 4),
                Fiber = new(0, 0, 0),
                Sodium = new(250, 400, 600),
                Confidence = 0.8,
                IsEstimated = false
            },

            [FoodTypology.CheeseHard] = new()
            {
                Typology = FoodTypology.CheeseHard,
                Description = "Queijos duros e semi-duros (mussarela, parmesão)",
                Calories = new(250, 320, 400),
                Protein = new(20, 28, 35),
                Fat = new(15, 24, 32),
                Carbohydrates = new(0, 1, 3),
                Sugar = new(0, 0.5, 2),
                Fiber = new(0, 0, 0),
                Sodium = new(400, 650, 900),
                Confidence = 0.9,
                IsEstimated = false
            },

            [FoodTypology.CheeseGrated] = new()
            {
                Typology = FoodTypology.CheeseGrated,
                Description = "Queijos ralados (parmesão ralado)",
                Calories = new(350, 400, 450),
                Protein = new(30, 36, 42),
                Fat = new(20, 28, 35),
                Carbohydrates = new(0, 1, 4),
                Sugar = new(0, 0.5, 2),
                Fiber = new(0, 0, 0),
                Sodium = new(800, 1000, 1400),
                Confidence = 0.85,
                IsEstimated = false
            },

            [FoodTypology.YogurtNatural] = new()
            {
                Typology = FoodTypology.YogurtNatural,
                Description = "Iogurtes naturais sem açúcar adicionado",
                Calories = new(50, 70, 90),
                Protein = new(3, 4.5, 6),
                Fat = new(0, 2, 4),
                Carbohydrates = new(4, 6, 8),
                Sugar = new(4, 5, 6), // Açúcar natural do leite (lactose)
                Fiber = new(0, 0, 0),
                Sodium = new(40, 60, 90),
                Confidence = 0.85,
                IsEstimated = false
            },

            [FoodTypology.YogurtSweetened] = new()
            {
                Typology = FoodTypology.YogurtSweetened,
                Description = "Iogurtes com açúcar adicionado ou aromatizados",
                Calories = new(80, 100, 130),
                Protein = new(2.5, 3.5, 5),
                Fat = new(0, 1.5, 3),
                Carbohydrates = new(12, 16, 20),
                Sugar = new(10, 14, 18),
                Fiber = new(0, 0, 1),
                Sodium = new(40, 65, 100),
                Confidence = 0.8,
                IsEstimated = false
            },

            [FoodTypology.DessertDairy] = new()
            {
                Typology = FoodTypology.DessertDairy,
                Description = "Sobremesas lácteas (danette, petit suisse)",
                Calories = new(120, 150, 200),
                Protein = new(2, 3.5, 5),
                Fat = new(3, 5, 8),
                Carbohydrates = new(18, 23, 30),
                Sugar = new(15, 20, 25),
                Fiber = new(0, 0.5, 1),
                Sodium = new(50, 80, 120),
                Confidence = 0.85,
                IsEstimated = false
            },

            // === CARBOIDRATOS BASE ===

            [FoodTypology.GrainCereal] = new()
            {
                Typology = FoodTypology.GrainCereal,
                Description = "Cereais e grãos (arroz, feijão, lentilha)",
                Calories = new(320, 350, 380),
                Protein = new(6, 8, 12),
                Fat = new(0.5, 1.5, 3),
                Carbohydrates = new(70, 75, 82),
                Sugar = new(0, 0.8, 2),
                Fiber = new(1, 3, 8),
                Sodium = new(0, 10, 30),
                Confidence = 0.9,
                IsEstimated = false
            },

            [FoodTypology.Pasta] = new()
            {
                Typology = FoodTypology.Pasta,
                Description = "Massas e macarrão",
                Calories = new(340, 360, 380),
                Protein = new(10, 12, 14),
                Fat = new(1, 1.5, 3),
                Carbohydrates = new(70, 74, 78),
                Sugar = new(2, 3, 5),
                Fiber = new(2, 3, 5),
                Sodium = new(10, 20, 40),
                Confidence = 0.9,
                IsEstimated = false
            },

            [FoodTypology.BreadBasic] = new()
            {
                Typology = FoodTypology.BreadBasic,
                Description = "Pães e produtos panificados básicos",
                Calories = new(240, 270, 300),
                Protein = new(7, 9, 12),
                Fat = new(2, 4, 6),
                Carbohydrates = new(48, 52, 58),
                Sugar = new(3, 6, 10),
                Fiber = new(2, 3, 5),
                Sodium = new(350, 450, 600),
                Confidence = 0.85,
                IsEstimated = false
            },

            [FoodTypology.CerealBreakfast] = new()
            {
                Typology = FoodTypology.CerealBreakfast,
                Description = "Cereais matinais tradicionais",
                Calories = new(350, 380, 410),
                Protein = new(7, 10, 14),
                Fat = new(2, 4, 8),
                Carbohydrates = new(70, 78, 85),
                Sugar = new(5, 12, 18),
                Fiber = new(3, 6, 10),
                Sodium = new(150, 250, 400),
                Confidence = 0.8,
                IsEstimated = false
            },

            [FoodTypology.CerealSweetened] = new()
            {
                Typology = FoodTypology.CerealSweetened,
                Description = "Cereais matinais açucarados",
                Calories = new(370, 400, 430),
                Protein = new(5, 7, 10),
                Fat = new(2, 4, 8),
                Carbohydrates = new(75, 82, 88),
                Sugar = new(20, 30, 40),
                Fiber = new(1, 3, 6),
                Sodium = new(200, 350, 500),
                Confidence = 0.85,
                IsEstimated = false
            },

            // === ULTRAPROCESSADOS ===

            [FoodTypology.CookieFilled] = new()
            {
                Typology = FoodTypology.CookieFilled,
                Description = "Biscoitos e bolachas recheadas",
                Calories = new(450, 490, 530),
                Protein = new(4, 5.5, 7),
                Fat = new(15, 21, 28),
                Carbohydrates = new(65, 70, 75),
                Sugar = new(25, 32, 40),
                Fiber = new(1.5, 2.5, 4),
                Sodium = new(250, 380, 550),
                Confidence = 0.85,
                IsEstimated = false
            },

            [FoodTypology.CookiePlain] = new()
            {
                Typology = FoodTypology.CookiePlain,
                Description = "Biscoitos e bolachas simples/salgados",
                Calories = new(420, 460, 500),
                Protein = new(6, 8, 11),
                Fat = new(10, 16, 24),
                Carbohydrates = new(60, 68, 75),
                Sugar = new(5, 10, 18),
                Fiber = new(2, 3, 5),
                Sodium = new(400, 550, 800),
                Confidence = 0.8,
                IsEstimated = false
            },

            [FoodTypology.SnackSalty] = new()
            {
                Typology = FoodTypology.SnackSalty,
                Description = "Salgadinhos e snacks",
                Calories = new(480, 530, 580),
                Protein = new(4, 6, 9),
                Fat = new(25, 32, 38),
                Carbohydrates = new(50, 58, 65),
                Sugar = new(1, 3, 6),
                Fiber = new(2, 3.5, 6),
                Sodium = new(700, 950, 1300),
                Confidence = 0.85,
                IsEstimated = false
            },

            [FoodTypology.Chocolate] = new()
            {
                Typology = FoodTypology.Chocolate,
                Description = "Chocolates e barras",
                Calories = new(500, 545, 590),
                Protein = new(5, 7, 10),
                Fat = new(25, 32, 40),
                Carbohydrates = new(50, 58, 65),
                Sugar = new(40, 50, 58),
                Fiber = new(2, 4, 7),
                Sodium = new(40, 80, 150),
                Confidence = 0.85,
                IsEstimated = false
            },

            [FoodTypology.ChocolatePowder] = new()
            {
                Typology = FoodTypology.ChocolatePowder,
                Description = "Achocolatado em pó",
                Calories = new(360, 385, 410),
                Protein = new(3, 4, 6),
                Fat = new(1.5, 3, 5),
                Carbohydrates = new(82, 87, 92),
                Sugar = new(70, 77, 83),
                Fiber = new(2, 3.5, 6),
                Sodium = new(100, 180, 280),
                Confidence = 0.9,
                IsEstimated = false
            },

            // === BEBIDAS ===

            [FoodTypology.BeverageSweetened] = new()
            {
                Typology = FoodTypology.BeverageSweetened,
                Description = "Bebidas açucaradas (por 100ml)",
                Calories = new(38, 45, 55),
                Protein = new(0, 0, 0.5),
                Fat = new(0, 0, 0),
                Carbohydrates = new(9, 11, 14),
                Sugar = new(9, 10.8, 13),
                Fiber = new(0, 0, 0),
                Sodium = new(5, 12, 25),
                Confidence = 0.9,
                IsEstimated = false
            },

            [FoodTypology.BeverageZero] = new()
            {
                Typology = FoodTypology.BeverageZero,
                Description = "Bebidas zero/diet (por 100ml)",
                Calories = new(0, 0.5, 2),
                Protein = new(0, 0, 0),
                Fat = new(0, 0, 0),
                Carbohydrates = new(0, 0, 0.5),
                Sugar = new(0, 0, 0),
                Fiber = new(0, 0, 0),
                Sodium = new(5, 15, 35),
                Confidence = 0.95,
                IsEstimated = false
            },

            // === PROTEICOS ===

            [FoodTypology.ProteinEnriched] = new()
            {
                Typology = FoodTypology.ProteinEnriched,
                Description = "Produtos enriquecidos com proteína",
                Calories = new(300, 360, 420),
                Protein = new(15, 25, 35),
                Fat = new(2, 6, 12),
                Carbohydrates = new(30, 45, 60),
                Sugar = new(5, 15, 30),
                Fiber = new(1, 3, 8),
                Sodium = new(150, 280, 450),
                Confidence = 0.75,
                IsEstimated = false
            },

            // === GORDURAS ===

            [FoodTypology.OilFat] = new()
            {
                Typology = FoodTypology.OilFat,
                Description = "Óleos e gorduras",
                Calories = new(860, 900, 900),
                Protein = new(0, 0, 0.5),
                Fat = new(95, 100, 100),
                Carbohydrates = new(0, 0, 0),
                Sugar = new(0, 0, 0),
                Fiber = new(0, 0, 0),
                Sodium = new(0, 5, 20),
                Confidence = 0.95,
                IsEstimated = false
            },

            // === VARIANTES SAUDÁVEIS ===

            [FoodTypology.HealthierVariant] = new()
            {
                Typology = FoodTypology.HealthierVariant,
                Description = "Variantes integrais/light (valores genéricos)",
                Calories = new(200, 280, 360),
                Protein = new(6, 10, 15),
                Fat = new(3, 8, 15),
                Carbohydrates = new(40, 52, 65),
                Sugar = new(3, 8, 15),
                Fiber = new(5, 8, 12),
                Sodium = new(150, 300, 500),
                Confidence = 0.6,
                IsEstimated = true
            }
        };
    }

    /// <summary>
    /// Obtém o perfil nutricional para uma tipologia.
    /// </summary>
    public static TypologicalNutritionProfile? GetProfile(FoodTypology typology)
    {
        return _catalog.TryGetValue(typology, out var profile) ? profile : null;
    }

    /// <summary>
    /// Verifica se existe um perfil para a tipologia.
    /// </summary>
    public static bool HasProfile(FoodTypology typology)
    {
        return _catalog.ContainsKey(typology);
    }

    /// <summary>
    /// Obtém todos os perfis disponíveis.
    /// </summary>
    public static IReadOnlyDictionary<FoodTypology, TypologicalNutritionProfile> GetAllProfiles()
    {
        return _catalog;
    }
}
