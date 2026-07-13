namespace LabelWise.Application.Models.Nutrition
{
    public sealed class NutritionSanitizationContext
    {
        public string? RawModelResponseText { get; init; }
        public NutritionReferenceCatalogSnapshot? ReferenceCatalog { get; init; }
    }
}
