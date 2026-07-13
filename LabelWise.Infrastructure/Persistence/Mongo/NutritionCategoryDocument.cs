using LabelWise.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LabelWise.Infrastructure.Persistence.Mongo;

[BsonIgnoreExtraElements]
public sealed class NutritionCategoryDocument
{
    [BsonId]
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ParentCode { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public NutritionCategory ToDomain()
    {
        return new NutritionCategory
        {
            Id = Id,
            Code = Code,
            Name = Name,
            Description = Description,
            ParentCode = ParentCode,
            IsActive = IsActive,
            DisplayOrder = DisplayOrder
        };
    }
}
