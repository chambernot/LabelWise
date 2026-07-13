using System;
using LabelWise.Application.DTOs.Nutrition;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LabelWise.Infrastructure.Persistence.Mongo;

public class ImageAnalysisCacheDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("imageHash")]
    public string ImageHash { get; set; } = string.Empty;

    [BsonElement("perceptualHash")]
    public long PerceptualHash { get; set; }

    [BsonElement("perceptualHashes")]
    public List<long> PerceptualHashes { get; set; } = [];

    [BsonElement("cacheVersion")]
    public string? CacheVersion { get; set; }

    [BsonElement("response")]
    public EstimatedNutritionProfileDto Response { get; set; } = null!;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}