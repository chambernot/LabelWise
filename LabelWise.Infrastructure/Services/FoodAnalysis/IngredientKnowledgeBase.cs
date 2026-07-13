using LabelWise.Domain.Enums;

namespace LabelWise.Infrastructure.Services.FoodAnalysis;

/// <summary>
/// Base de conhecimento alimentar com informações canônicas sobre ingredientes.
/// </summary>
public sealed class IngredientKnowledgeBase
{
    private readonly Dictionary<string, FoodEntity> _entities = new(StringComparer.OrdinalIgnoreCase);

    public IngredientKnowledgeBase()
    {
        InitializeEntities();
    }

    public FoodEntity? GetEntity(string ingredientName)
    {
        if (_entities.TryGetValue(ingredientName, out var entity))
            return entity;

        // Tentar encontrar por alias
        return _entities.Values.FirstOrDefault(e => 
            e.Aliases.Contains(ingredientName, StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> GetAllergenRelations(string ingredientName)
    {
        var entity = GetEntity(ingredientName);
        return entity?.AllergenRelations ?? Array.Empty<string>();
    }

    public bool IsVeganCompatible(string ingredientName)
    {
        var entity = GetEntity(ingredientName);
        return entity?.IsVegan ?? true; // Default true se não conhecemos
    }

    public bool IsVegetarianCompatible(string ingredientName)
    {
        var entity = GetEntity(ingredientName);
        return entity?.IsVegetarian ?? true;
    }

    public bool ContainsLactose(string ingredientName)
    {
        var entity = GetEntity(ingredientName);
        return entity?.ContainsLactose ?? false;
    }

    public bool ContainsGluten(string ingredientName)
    {
        var entity = GetEntity(ingredientName);
        return entity?.ContainsGluten ?? false;
    }

    private void InitializeEntities()
    {
        // Lácteos
        AddEntity(new FoodEntity
        {
            CanonicalName = "leite",
            Aliases = ["leite de vaca", "leite integral", "leite desnatado", "leite semidesnatado"],
            IsVegan = false,
            IsVegetarian = true,
            ContainsLactose = true,
            AllergenRelations = ["leite", "lactose"],
            ProcessingImpact = ProcessingLevel.MinimallyProcessed
        });

        AddEntity(new FoodEntity
        {
            CanonicalName = "queijo",
            Aliases = ["queijo minas", "queijo prato", "queijo mussarela", "queijo parmesão"],
            IsVegan = false,
            IsVegetarian = true,
            ContainsLactose = true,
            AllergenRelations = ["leite", "lactose"],
            ProcessingImpact = ProcessingLevel.Processed
        });

        AddEntity(new FoodEntity
        {
            CanonicalName = "manteiga",
            Aliases = ["manteiga sem sal", "manteiga com sal"],
            IsVegan = false,
            IsVegetarian = true,
            ContainsLactose = true,
            AllergenRelations = ["leite", "lactose"],
            ProcessingImpact = ProcessingLevel.ProcessedCulinaryIngredients
        });

        // Glúten
        AddEntity(new FoodEntity
        {
            CanonicalName = "farinha de trigo",
            Aliases = ["farinha de trigo enriquecida", "farinha de trigo integral"],
            IsVegan = true,
            IsVegetarian = true,
            ContainsGluten = true,
            AllergenRelations = ["glúten", "trigo"],
            ProcessingImpact = ProcessingLevel.MinimallyProcessed
        });

        AddEntity(new FoodEntity
        {
            CanonicalName = "aveia",
            Aliases = ["flocos de aveia", "farinha de aveia"],
            IsVegan = true,
            IsVegetarian = true,
            ContainsGluten = false, // Aveia pura não tem glúten, mas pode ter contaminação
            AllergenRelations = ["aveia"],
            ProcessingImpact = ProcessingLevel.MinimallyProcessed
        });

        // Ovos
        AddEntity(new FoodEntity
        {
            CanonicalName = "ovo",
            Aliases = ["ovos", "ovo de galinha", "albumina", "clara de ovo"],
            IsVegan = false,
            IsVegetarian = true,
            AllergenRelations = ["ovo"],
            ProcessingImpact = ProcessingLevel.MinimallyProcessed
        });

        // Carnes
        AddEntity(new FoodEntity
        {
            CanonicalName = "carne bovina",
            Aliases = ["carne de boi", "carne", "boi"],
            IsVegan = false,
            IsVegetarian = false,
            AllergenRelations = [],
            ProcessingImpact = ProcessingLevel.MinimallyProcessed
        });

        AddEntity(new FoodEntity
        {
            CanonicalName = "frango",
            Aliases = ["carne de frango", "peito de frango"],
            IsVegan = false,
            IsVegetarian = false,
            AllergenRelations = [],
            ProcessingImpact = ProcessingLevel.MinimallyProcessed
        });

        AddEntity(new FoodEntity
        {
            CanonicalName = "peixe",
            Aliases = ["pescado"],
            IsVegan = false,
            IsVegetarian = false,
            AllergenRelations = ["peixe"],
            ProcessingImpact = ProcessingLevel.MinimallyProcessed
        });

        // Oleaginosas
        AddEntity(new FoodEntity
        {
            CanonicalName = "amendoim",
            Aliases = ["pasta de amendoim"],
            IsVegan = true,
            IsVegetarian = true,
            AllergenRelations = ["amendoim"],
            ProcessingImpact = ProcessingLevel.MinimallyProcessed
        });

        AddEntity(new FoodEntity
        {
            CanonicalName = "castanha",
            Aliases = ["castanha de caju", "castanha do pará", "castanhas"],
            IsVegan = true,
            IsVegetarian = true,
            AllergenRelations = ["castanha"],
            ProcessingImpact = ProcessingLevel.MinimallyProcessed
        });

        // Açúcares
        AddEntity(new FoodEntity
        {
            CanonicalName = "açúcar",
            Aliases = ["açúcar refinado", "açúcar cristal", "açúcar demerara"],
            IsVegan = true,
            IsVegetarian = true,
            DiabeticImpact = "high",
            GlycemicRisk = "high",
            ProcessingImpact = ProcessingLevel.ProcessedCulinaryIngredients
        });

        AddEntity(new FoodEntity
        {
            CanonicalName = "xarope de milho",
            Aliases = ["xarope de glicose", "glucose de milho"],
            IsVegan = true,
            IsVegetarian = true,
            DiabeticImpact = "high",
            GlycemicRisk = "high",
            ProcessingImpact = ProcessingLevel.UltraProcessed
        });

        // Aditivos
        AddEntity(new FoodEntity
        {
            CanonicalName = "glutamato monossódico",
            Aliases = ["msg", "e621", "realçador de sabor"],
            IsVegan = true,
            IsVegetarian = true,
            ProcessingImpact = ProcessingLevel.UltraProcessed
        });

        AddEntity(new FoodEntity
        {
            CanonicalName = "gordura hidrogenada",
            Aliases = ["gordura vegetal hidrogenada", "óleo hidrogenado"],
            IsVegan = true,
            IsVegetarian = true,
            ProcessingImpact = ProcessingLevel.UltraProcessed
        });
    }

    private void AddEntity(FoodEntity entity)
    {
        _entities[entity.CanonicalName] = entity;
        
        foreach (var alias in entity.Aliases)
        {
            _entities[alias] = entity;
        }
    }
}

/// <summary>
/// Entidade alimentar canônica com todas as informações relevantes.
/// </summary>
public sealed class FoodEntity
{
    public required string CanonicalName { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public bool IsVegan { get; init; }
    public bool IsVegetarian { get; init; }
    public bool ContainsLactose { get; init; }
    public bool ContainsGluten { get; init; }
    public IReadOnlyList<string> AllergenRelations { get; init; } = [];
    public ProcessingLevel ProcessingImpact { get; init; } = ProcessingLevel.Unknown;
    public string? DiabeticImpact { get; init; }
    public string? InsulinImpact { get; init; }
    public string? GlycemicRisk { get; init; }
}
