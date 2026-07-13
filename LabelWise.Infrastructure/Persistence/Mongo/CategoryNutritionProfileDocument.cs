using LabelWise.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LabelWise.Infrastructure.Persistence.Mongo;

[BsonIgnoreExtraElements]
public sealed class CategoryNutritionProfileDocument
{
    [BsonId]
    public int Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public decimal? CaloriesPer100g { get; set; }
    public decimal? CaloriesMin { get; set; }
    public decimal? CaloriesMax { get; set; }
    public decimal? ProteinPer100g { get; set; }
    public decimal? ProteinMin { get; set; }
    public decimal? ProteinMax { get; set; }
    public decimal? FatPer100g { get; set; }
    public decimal? FatMin { get; set; }
    public decimal? FatMax { get; set; }
    public decimal? SaturatedFatPer100g { get; set; }
    public decimal? SaturatedFatMin { get; set; }
    public decimal? SaturatedFatMax { get; set; }
    public decimal? TransFatPer100g { get; set; }
    public decimal? TransFatMin { get; set; }
    public decimal? TransFatMax { get; set; }
    public decimal? CarbohydratesPer100g { get; set; }
    public decimal? CarbohydratesMin { get; set; }
    public decimal? CarbohydratesMax { get; set; }
    public decimal? SugarPer100g { get; set; }
    public decimal? SugarMin { get; set; }
    public decimal? SugarMax { get; set; }
    public decimal? FiberPer100g { get; set; }
    public decimal? FiberMin { get; set; }
    public decimal? FiberMax { get; set; }
    public decimal? SodiumPer100g { get; set; }
    public decimal? SodiumMin { get; set; }
    public decimal? SodiumMax { get; set; }
    public decimal ConfidenceLevel { get; set; } = 0.70m;
    public string? DataSource { get; set; }
    public int? ReferenceYear { get; set; }
    public int? SampleSize { get; set; }
    public string? Notes { get; set; }
    public bool IsLiquid { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public NutritionCategoryDocument? Category { get; set; }

    public CategoryNutritionProfile ToDomain()
    {
        return new CategoryNutritionProfile
        {
            Id = Id,
            CategoryCode = CategoryCode,
            CaloriesPer100g = CaloriesPer100g,
            CaloriesMin = CaloriesMin,
            CaloriesMax = CaloriesMax,
            ProteinPer100g = ProteinPer100g,
            ProteinMin = ProteinMin,
            ProteinMax = ProteinMax,
            FatPer100g = FatPer100g,
            FatMin = FatMin,
            FatMax = FatMax,
            SaturatedFatPer100g = SaturatedFatPer100g,
            SaturatedFatMin = SaturatedFatMin,
            SaturatedFatMax = SaturatedFatMax,
            TransFatPer100g = TransFatPer100g,
            TransFatMin = TransFatMin,
            TransFatMax = TransFatMax,
            CarbohydratesPer100g = CarbohydratesPer100g,
            CarbohydratesMin = CarbohydratesMin,
            CarbohydratesMax = CarbohydratesMax,
            SugarPer100g = SugarPer100g,
            SugarMin = SugarMin,
            SugarMax = SugarMax,
            FiberPer100g = FiberPer100g,
            FiberMin = FiberMin,
            FiberMax = FiberMax,
            SodiumPer100g = SodiumPer100g,
            SodiumMin = SodiumMin,
            SodiumMax = SodiumMax,
            ConfidenceLevel = ConfidenceLevel,
            DataSource = DataSource,
            ReferenceYear = ReferenceYear,
            SampleSize = SampleSize,
            Notes = Notes,
            IsLiquid = IsLiquid,
            IsActive = IsActive,
            Version = Version,
            Category = Category?.ToDomain()
        };
    }
}
