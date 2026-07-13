using System;
using System.Collections.Generic;
using System.Linq;

namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Normaliza categorias detectadas em tipologias alimentares padronizadas.
/// Baseado em sinais nutricionais e características do produto, não em heurísticas específicas.
/// </summary>
public static class CategoryNormalizer
{
    /// <summary>
    /// Normaliza uma categoria detectada para uma tipologia alimentar padronizada.
    /// </summary>
    /// <param name="detectedCategory">Categoria detectada pela análise visual ou OCR.</param>
    /// <param name="visibleClaims">Alegações visíveis no produto (ex: "light", "integral", "zero").</param>
    /// <param name="partialNutrition">Dados nutricionais parciais se disponíveis.</param>
    /// <returns>Tipologia alimentar padronizada.</returns>
    public static FoodTypology Normalize(
        string? detectedCategory,
        List<string>? visibleClaims = null,
        PartialNutritionData? partialNutrition = null)
    {
        if (string.IsNullOrWhiteSpace(detectedCategory))
        {
            return FoodTypology.Unknown;
        }

        var categoryLower = detectedCategory.ToLowerInvariant();
        var claims = (visibleClaims ?? new List<string>())
            .Select(c => c.ToLowerInvariant())
            .ToList();

        // === LATICÍNIOS ===

        if (IsCheeseGrated(categoryLower, claims))
            return FoodTypology.CheeseGrated;

        if (IsCheeseHard(categoryLower, claims))
            return FoodTypology.CheeseHard;

        if (IsDairyCreamyLight(categoryLower, claims))
            return FoodTypology.DairyCreamyLight;

        if (IsDairyCreamy(categoryLower, claims))
            return FoodTypology.DairyCreamyFull;

        if (IsDessertDairy(categoryLower, claims))
            return FoodTypology.DessertDairy;

        if (IsYogurtSweetened(categoryLower, claims))
            return FoodTypology.YogurtSweetened;

        if (IsYogurtNatural(categoryLower, claims))
            return FoodTypology.YogurtNatural;

        // === CARBOIDRATOS BASE ===

        if (IsCerealSweetened(categoryLower, claims))
            return FoodTypology.CerealSweetened;

        if (IsCerealBreakfast(categoryLower, claims))
            return FoodTypology.CerealBreakfast;

        if (IsPasta(categoryLower, claims))
            return FoodTypology.Pasta;

        if (IsBreadBasic(categoryLower, claims))
            return FoodTypology.BreadBasic;

        if (IsGrainCereal(categoryLower, claims))
            return FoodTypology.GrainCereal;

        // === ULTRAPROCESSADOS ===

        if (IsChocolatePowder(categoryLower, claims))
            return FoodTypology.ChocolatePowder;

        if (IsChocolate(categoryLower, claims))
            return FoodTypology.Chocolate;

        if (IsCookieFilled(categoryLower, claims))
            return FoodTypology.CookieFilled;

        if (IsCookiePlain(categoryLower, claims))
            return FoodTypology.CookiePlain;

        if (IsSnackSalty(categoryLower, claims))
            return FoodTypology.SnackSalty;

        // === BEBIDAS ===

        if (IsBeverageZero(categoryLower, claims))
            return FoodTypology.BeverageZero;

        if (IsBeverageSweetened(categoryLower, claims))
            return FoodTypology.BeverageSweetened;

        // === PROTEICOS ===

        if (IsProteinEnriched(categoryLower, claims, partialNutrition))
            return FoodTypology.ProteinEnriched;

        // === GORDURAS ===

        if (IsOilFat(categoryLower, claims))
            return FoodTypology.OilFat;

        // === VARIANTES SAUDÁVEIS ===

        if (IsHealthierVariant(categoryLower, claims))
            return FoodTypology.HealthierVariant;

        // === CONDIMENTOS ===

        if (IsCondiment(categoryLower, claims))
            return FoodTypology.Condiment;

        return FoodTypology.Unknown;
    }

    #region Laticínios

    private static bool IsDairyCreamy(string category, List<string> claims)
    {
        var creamyKeywords = new[] { "requeijão", "cream cheese", "catupiry", "cremoso", "processado" };
        return creamyKeywords.Any(k => category.Contains(k));
    }

    private static bool IsDairyCreamyLight(string category, List<string> claims)
    {
        if (!IsDairyCreamy(category, claims))
            return false;

        var lightKeywords = new[] { "light", "reduzido", "baixo teor", "menos gordura" };
        return claims.Any(c => lightKeywords.Any(k => c.Contains(k))) ||
               lightKeywords.Any(k => category.Contains(k));
    }

    private static bool IsCheeseHard(string category, List<string> claims)
    {
        var hardCheeseKeywords = new[] 
        { 
            "mussarela", "parmesão", "cheddar", "provolone", "gouda", 
            "minas padrão", "coalho", "queijo prato"
        };
        
        // Se é cremoso, não é queijo duro
        if (IsDairyCreamy(category, claims))
            return false;

        return category.Contains("queijo") && 
               (hardCheeseKeywords.Any(k => category.Contains(k)) || 
                category == "queijo");
    }

    private static bool IsCheeseGrated(string category, List<string> claims)
    {
        var gratedKeywords = new[] { "ralado", "parmesão ralado", "queijo ralado" };
        return gratedKeywords.Any(k => category.Contains(k));
    }

    private static bool IsYogurtNatural(string category, List<string> claims)
    {
        if (!category.Contains("iogurte"))
            return false;

        var sweetenedKeywords = new[] { "sabor", "morango", "frutas", "açúcar", "mel" };
        return !sweetenedKeywords.Any(k => category.Contains(k) || claims.Any(c => c.Contains(k)));
    }

    private static bool IsYogurtSweetened(string category, List<string> claims)
    {
        if (!category.Contains("iogurte"))
            return false;

        var sweetenedKeywords = new[] { "sabor", "morango", "frutas", "açúcar", "mel", "polpa" };
        return sweetenedKeywords.Any(k => category.Contains(k) || claims.Any(c => c.Contains(k)));
    }

    private static bool IsDessertDairy(string category, List<string> claims)
    {
        var dessertKeywords = new[] 
        { 
            "sobremesa", "pudim", "mousse", "petit suisse", 
            "danette", "chandelle", "flan"
        };
        
        return dessertKeywords.Any(k => category.Contains(k));
    }

    #endregion

    #region Carboidratos Base

    private static bool IsGrainCereal(string category, List<string> claims)
    {
        var grainKeywords = new[] 
        { 
            "arroz", "feijão", "lentilha", "grão-de-bico", 
            "ervilha", "quinoa", "aveia"
        };
        
        return grainKeywords.Any(k => category.Contains(k));
    }

    private static bool IsPasta(string category, List<string> claims)
    {
        var pastaKeywords = new[] { "macarrão", "massa", "espaguete", "penne", "lasanha" };
        return pastaKeywords.Any(k => category.Contains(k));
    }

    private static bool IsBreadBasic(string category, List<string> claims)
    {
        var breadKeywords = new[] { "pão", "panificado", "baguete", "bisnaga" };
        return breadKeywords.Any(k => category.Contains(k));
    }

    private static bool IsCerealBreakfast(string category, List<string> claims)
    {
        if (!category.Contains("cereal"))
            return false;

        var sweetenedKeywords = new[] { "açúcar", "mel", "chocolate", "sabor" };
        return !sweetenedKeywords.Any(k => category.Contains(k) || claims.Any(c => c.Contains(k)));
    }

    private static bool IsCerealSweetened(string category, List<string> claims)
    {
        if (!category.Contains("cereal"))
            return false;

        var sweetenedKeywords = new[] { "açúcar", "mel", "chocolate", "sabor", "sucrilhos" };
        return sweetenedKeywords.Any(k => category.Contains(k) || claims.Any(c => c.Contains(k)));
    }

    #endregion

    #region Ultraprocessados

    private static bool IsCookieFilled(string category, List<string> claims)
    {
        var cookieKeywords = new[] { "biscoito", "bolacha" };
        var filledKeywords = new[] { "recheado", "recheio", "wafer" };

        return cookieKeywords.Any(k => category.Contains(k)) &&
               filledKeywords.Any(k => category.Contains(k) || claims.Any(c => c.Contains(k)));
    }

    private static bool IsCookiePlain(string category, List<string> claims)
    {
        var cookieKeywords = new[] { "biscoito", "bolacha", "cream cracker", "maria" };
        
        if (!cookieKeywords.Any(k => category.Contains(k)))
            return false;

        // Se é recheado, não é simples
        var filledKeywords = new[] { "recheado", "recheio", "wafer" };
        return !filledKeywords.Any(k => category.Contains(k) || claims.Any(c => c.Contains(k)));
    }

    private static bool IsSnackSalty(string category, List<string> claims)
    {
        var snackKeywords = new[] 
        { 
            "salgadinho", "chips", "doritos", "cheetos", "ruffles", 
            "fandangos", "snack"
        };
        
        return snackKeywords.Any(k => category.Contains(k));
    }

    private static bool IsChocolate(string category, List<string> claims)
    {
        // Não confundir com achocolatado em pó
        if (IsChocolatePowder(category, claims))
            return false;

        var chocolateKeywords = new[] { "chocolate", "barra de chocolate", "bombom" };
        return chocolateKeywords.Any(k => category.Contains(k));
    }

    private static bool IsChocolatePowder(string category, List<string> claims)
    {
        var powderKeywords = new[] { "achocolatado", "chocolate em pó", "nescau", "toddy" };
        return powderKeywords.Any(k => category.Contains(k));
    }

    #endregion

    #region Bebidas

    private static bool IsBeverageSweetened(string category, List<string> claims)
    {
        var beverageKeywords = new[] 
        { 
            "refrigerante", "suco", "bebida", "néctar", 
            "refresco", "energético"
        };

        if (!beverageKeywords.Any(k => category.Contains(k)))
            return false;

        // Se é zero/diet, não é açucarada
        return !IsBeverageZero(category, claims);
    }

    private static bool IsBeverageZero(string category, List<string> claims)
    {
        var beverageKeywords = new[] { "refrigerante", "suco", "bebida" };
        var zeroKeywords = new[] { "zero", "diet", "sem açúcar" };

        return beverageKeywords.Any(k => category.Contains(k)) &&
               zeroKeywords.Any(k => category.Contains(k) || claims.Any(c => c.Contains(k)));
    }

    #endregion

    #region Proteicos

    private static bool IsProteinEnriched(
        string category, 
        List<string> claims,
        PartialNutritionData? nutrition)
    {
        // Sinais de produto proteico
        var proteinKeywords = new[] 
        { 
            "whey", "proteico", "protein", "alto teor de proteína",
            "iogurte proteico", "barra proteica", "shake proteico"
        };

        var hasProteinClaim = proteinKeywords.Any(k => 
            category.Contains(k) || claims.Any(c => c.Contains(k)));

        // Se tem dados nutricionais, validar
        if (nutrition?.Protein != null)
        {
            return nutrition.Protein >= 15; // > 15g/100g = produto proteico
        }

        return hasProteinClaim;
    }

    #endregion

    #region Outros

    private static bool IsOilFat(string category, List<string> claims)
    {
        var oilKeywords = new[] 
        { 
            "óleo", "azeite", "manteiga", "margarina", 
            "banha", "gordura"
        };
        
        return oilKeywords.Any(k => category.Contains(k));
    }

    private static bool IsHealthierVariant(string category, List<string> claims)
    {
        var healthyKeywords = new[] 
        { 
            "integral", "light", "fitness", "zero", "diet",
            "sem açúcar", "sem gordura", "reduzido"
        };

        return healthyKeywords.Any(k => claims.Any(c => c.Contains(k)));
    }

    private static bool IsCondiment(string category, List<string> claims)
    {
        var condimentKeywords = new[] 
        { 
            "sal", "açúcar", "tempero", "molho", "catchup", 
            "mostarda", "maionese", "vinagre"
        };
        
        return condimentKeywords.Any(k => category.Contains(k));
    }

    #endregion
}

/// <summary>
/// Dados nutricionais parciais para auxiliar na normalização de categoria.
/// </summary>
public class PartialNutritionData
{
    public double? Protein { get; set; }
    public double? Fat { get; set; }
    public double? Sugar { get; set; }
    public double? Sodium { get; set; }
    public double? Fiber { get; set; }
}
