using MongoDB.Bson.Serialization.Attributes;

namespace LabelWise.Infrastructure.Persistence.Mongo;

[BsonIgnoreExtraElements]
public sealed class NutritionCategoryAliasDocument
{
    [BsonId]
    public int Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string AliasName { get; set; } = string.Empty;
    public string AliasNameNormalized { get; set; } = string.Empty;
    public decimal Confidence { get; set; } = 1.00m;
    public string MatchType { get; set; } = "exact";
    public bool IsActive { get; set; } = true;
    public int UsageCount { get; set; }
}
