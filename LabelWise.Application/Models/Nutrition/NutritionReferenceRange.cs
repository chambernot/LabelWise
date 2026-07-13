namespace LabelWise.Application.Models.Nutrition
{
    public sealed record NutritionMetricRange(double Minimum, double Maximum, double Average)
    {
        public bool Contains(double value) => value >= Minimum && value <= Maximum;
    }

    public sealed record NutritionReferenceRange(
        string Key,
        string DisplayName,
        NutritionMetricRange CarbsPer100g,
        NutritionMetricRange SugarPer100g,
        NutritionMetricRange ProteinPer100g,
        NutritionMetricRange FatPer100g,
        NutritionMetricRange SodiumPer100g,
        NutritionMetricRange CaloriesPer100g,
        NutritionMetricRange FiberPer100g);

    public static class NutritionReferenceRanges
    {
        private static readonly NutritionReferenceRange GenericPackagedFood = new(
            Key: "generic-packaged-food",
            DisplayName: "alimentos embalados",
            CarbsPer100g: new NutritionMetricRange(0, 85, 35),
            SugarPer100g: new NutritionMetricRange(0, 30, 8),
            ProteinPer100g: new NutritionMetricRange(0, 25, 7),
            FatPer100g: new NutritionMetricRange(0, 35, 8),
            SodiumPer100g: new NutritionMetricRange(0, 1200, 200),
            CaloriesPer100g: new NutritionMetricRange(0, 600, 250),
            FiberPer100g: new NutritionMetricRange(0, 12, 2));

        private static readonly IReadOnlyDictionary<string, NutritionReferenceRange> Catalog =
            new Dictionary<string, NutritionReferenceRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["dairy-solid"] = new NutritionReferenceRange(
                    Key: "dairy-solid",
                    DisplayName: "laticínios sólidos",
                    CarbsPer100g: new NutritionMetricRange(0, 8, 3),
                    SugarPer100g: new NutritionMetricRange(0, 2, 1),
                    ProteinPer100g: new NutritionMetricRange(10, 35, 24),
                    FatPer100g: new NutritionMetricRange(10, 35, 24),
                    SodiumPer100g: new NutritionMetricRange(200, 1400, 650),
                    CaloriesPer100g: new NutritionMetricRange(180, 450, 320),
                    FiberPer100g: new NutritionMetricRange(0, 1, 0)),
                ["dairy-liquid"] = new NutritionReferenceRange(
                    Key: "dairy-liquid",
                    DisplayName: "laticínios líquidos",
                    CarbsPer100g: new NutritionMetricRange(0, 18, 9),
                    SugarPer100g: new NutritionMetricRange(0, 18, 9),
                    ProteinPer100g: new NutritionMetricRange(2, 12, 5),
                    FatPer100g: new NutritionMetricRange(0, 10, 3),
                    SodiumPer100g: new NutritionMetricRange(20, 200, 80),
                    CaloriesPer100g: new NutritionMetricRange(40, 150, 90),
                    FiberPer100g: new NutritionMetricRange(0, 2, 0)),
                ["beverage"] = new NutritionReferenceRange(
                    Key: "beverage",
                    DisplayName: "bebidas",
                    CarbsPer100g: new NutritionMetricRange(0, 18, 8),
                    SugarPer100g: new NutritionMetricRange(0, 12, 6),
                    ProteinPer100g: new NutritionMetricRange(0, 3, 0),
                    FatPer100g: new NutritionMetricRange(0, 1, 0),
                    SodiumPer100g: new NutritionMetricRange(0, 180, 30),
                    CaloriesPer100g: new NutritionMetricRange(0, 80, 35),
                    FiberPer100g: new NutritionMetricRange(0, 2, 0)),
                ["snack"] = new NutritionReferenceRange(
                    Key: "snack",
                    DisplayName: "snacks",
                    CarbsPer100g: new NutritionMetricRange(25, 75, 55),
                    SugarPer100g: new NutritionMetricRange(0, 35, 12),
                    ProteinPer100g: new NutritionMetricRange(3, 12, 6),
                    FatPer100g: new NutritionMetricRange(15, 40, 25),
                    SodiumPer100g: new NutritionMetricRange(150, 1200, 450),
                    CaloriesPer100g: new NutritionMetricRange(350, 600, 480),
                    FiberPer100g: new NutritionMetricRange(1, 8, 3)),
                ["cereal"] = new NutritionReferenceRange(
                    Key: "cereal",
                    DisplayName: "cereais e barras",
                    CarbsPer100g: new NutritionMetricRange(35, 80, 60),
                    SugarPer100g: new NutritionMetricRange(0, 35, 12),
                    ProteinPer100g: new NutritionMetricRange(5, 18, 10),
                    FatPer100g: new NutritionMetricRange(1, 20, 6),
                    SodiumPer100g: new NutritionMetricRange(0, 500, 180),
                    CaloriesPer100g: new NutritionMetricRange(300, 450, 360),
                    FiberPer100g: new NutritionMetricRange(2, 15, 6)),
                ["ready-meal"] = new NutritionReferenceRange(
                    Key: "ready-meal",
                    DisplayName: "refeições e massas",
                    CarbsPer100g: new NutritionMetricRange(15, 75, 35),
                    SugarPer100g: new NutritionMetricRange(0, 12, 3),
                    ProteinPer100g: new NutritionMetricRange(4, 18, 9),
                    FatPer100g: new NutritionMetricRange(1, 20, 6),
                    SodiumPer100g: new NutritionMetricRange(0, 900, 220),
                    CaloriesPer100g: new NutritionMetricRange(100, 420, 260),
                    FiberPer100g: new NutritionMetricRange(1, 10, 3))
            };

        public static NutritionReferenceCatalogSnapshot GetSnapshot()
        {
            return new NutritionReferenceCatalogSnapshot(GenericPackagedFood, new Dictionary<string, NutritionReferenceRange>(Catalog, StringComparer.OrdinalIgnoreCase));
        }

        public static NutritionReferenceRange Resolve(string? category, string? productName, IEnumerable<string>? visibleClaims = null)
        {
            return ResolveInternal(category, productName, visibleClaims, GenericPackagedFood, Catalog);
        }

        internal static NutritionReferenceRange ResolveInternal(
            string? category,
            string? productName,
            IEnumerable<string>? visibleClaims,
            NutritionReferenceRange genericPackagedFood,
            IReadOnlyDictionary<string, NutritionReferenceRange> catalog)
        {
            var searchText = string.Join(' ', new[]
            {
                category,
                productName,
                visibleClaims == null ? null : string.Join(' ', visibleClaims)
            }.Where(v => !string.IsNullOrWhiteSpace(v))).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                return genericPackagedFood;
            }

            if (ContainsAny(searchText, "queijo", "parmes", "parmesão", "mussarela", "muçarela", "cheddar", "minas", "gouda", "requeijão", "cream cheese"))
            {
                return catalog["dairy-solid"];
            }

            if (ContainsAny(searchText, "iogurte", "leite", "kefir", "bebida láctea", "bebida lactea", "fermentado"))
            {
                return catalog["dairy-liquid"];
            }

            if (ContainsAny(searchText, "refrigerante", "suco", "néctar", "nectar", "chá", "cha", "energético", "energetico", "isotônico", "isotonico", "bebida"))
            {
                return catalog["beverage"];
            }

            if (ContainsAny(searchText, "biscoito", "bolacha", "snack", "salgadinho", "batata", "chips", "cracker"))
            {
                return catalog["snack"];
            }

            if (ContainsAny(searchText, "cereal", "granola", "barra", "aveia"))
            {
                return catalog["cereal"];
            }

            if (ContainsAny(searchText, "massa", "macarrão", "macarrao", "arroz", "feijão", "feijao", "lasanha", "refeição", "refeicao"))
            {
                return catalog["ready-meal"];
            }

            return genericPackagedFood;
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            return terms.Any(value.Contains);
        }
    }

    public sealed class NutritionReferenceCatalogSnapshot
    {
        private readonly NutritionReferenceRange _genericPackagedFood;
        private readonly IReadOnlyDictionary<string, NutritionReferenceRange> _catalog;

        internal NutritionReferenceCatalogSnapshot(
            NutritionReferenceRange genericPackagedFood,
            IReadOnlyDictionary<string, NutritionReferenceRange> catalog)
        {
            _genericPackagedFood = genericPackagedFood;
            _catalog = catalog;
        }

        public NutritionReferenceRange Resolve(string? category, string? productName, IEnumerable<string>? visibleClaims = null)
        {
            return NutritionReferenceRanges.ResolveInternal(category, productName, visibleClaims, _genericPackagedFood, _catalog);
        }
    }
}
