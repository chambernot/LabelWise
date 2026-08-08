using LabelWise.Domain.Enums;
using LabelWise.Domain.ValueObjects;

namespace LabelWise.Domain.Entities;

public class CardEvaluation
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string CardName { get; private set; }
    public bool IsAuthentic { get; private set; }
    public CardCondition Condition { get; private set; }
    public decimal EstimatedValue { get; private set; }
    public List<DefectMap> Defects { get; private set; } = new();

    public CardEvaluation(Guid userId, string cardName, bool isAuthentic, CardCondition condition, decimal estimatedValue, List<DefectMap> defects)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        CardName = cardName;
        IsAuthentic = isAuthentic;
        Condition = condition;
        EstimatedValue = estimatedValue;
        Defects = defects ?? new List<DefectMap>();
    }
}