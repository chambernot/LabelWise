namespace LabelWise.Application.DTOs.IngredientAnalysis;

public sealed class DietProfilesDto
{
    public DietProfileCompatibilityDto Vegan { get; set; } = new();
    public DietProfileCompatibilityDto Vegetarian { get; set; } = new();
    public DietProfileCompatibilityDto LactoseFree { get; set; } = new();
    public DietProfileCompatibilityDto GlutenFree { get; set; } = new();
    public DietProfileCompatibilityDto DiabeticFriendly { get; set; } = new();
}
