using LabelWise.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LabelWise.Infrastructure.Persistence.Mongo;

[BsonIgnoreExtraElements]
public sealed class CategoryMappingDocument
{
    [BsonId]
    public int Id { get; set; }
    public string RawCategoryName { get; set; } = string.Empty;
    public string NormalizedCategoryCode { get; set; } = string.Empty;
    public decimal Confidence { get; set; } = 1.00m;
    public bool IsActive { get; set; } = true;
    public NutritionCategoryDocument? NormalizedCategory { get; set; }

    public CategoryMapping ToDomain()
    {
        return new CategoryMapping
        {
            Id = Id,
            RawCategoryName = RawCategoryName,
            NormalizedCategoryCode = NormalizedCategoryCode,
            Confidence = Confidence,
            IsActive = IsActive,
            NormalizedCategory = NormalizedCategory?.ToDomain()
        };
    }
}
