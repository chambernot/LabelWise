using System;
using LabelWise.Application.DTOs.Nutrition;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LabelWise.Infrastructure.Persistence.Mongo;

public class NutritionAnalysisCacheDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;

    [BsonElement("response")]
    public EstimatedNutritionProfileDto Response { get; set; } = null!;

    [BsonElement("confidenceScore")]
    public double ConfidenceScore { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}