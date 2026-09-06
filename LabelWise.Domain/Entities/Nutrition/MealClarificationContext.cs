using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace LabelWise.Domain.Entities.Nutrition;

public class MealClarificationContext
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = string.Empty;
    public string OriginalTextInput { get; set; } = string.Empty;
    public string? OriginalBase64Image { get; set; }
    public string ClarificationQuestion { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public MealClarificationContext() { }

    public MealClarificationContext(string userId, string originalTextInput, string? originalBase64Image, string clarificationQuestion)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId;
        OriginalTextInput = originalTextInput;
        OriginalBase64Image = originalBase64Image;
        ClarificationQuestion = clarificationQuestion;
        CreatedAt = DateTime.UtcNow;
    }
}