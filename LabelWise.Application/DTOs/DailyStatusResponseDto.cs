using System;
using System.Collections.Generic;

namespace LabelWise.Application.DTOs.Nutrition;

public class DailyStatusResponseDto
{
    public string UserId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public MacroSummaryDto Target { get; set; } = new(0, 0, 0, 0);
    public MacroSummaryDto Consumed { get; set; } = new(0, 0, 0, 0);
    public MacroSummaryDto Remaining { get; set; } = new(0, 0, 0, 0);
    public List<string> Suggestions { get; set; } = new();

    public DailyStatusResponseDto() { }

    public DailyStatusResponseDto(
        string userId,
        DateTime date,
        MacroSummaryDto target,
        MacroSummaryDto consumed,
        MacroSummaryDto remaining,
        List<string> suggestions)
    {
        UserId = userId;
        Date = date;
        Target = target;
        Consumed = consumed;
        Remaining = remaining;
        Suggestions = suggestions;
    }
}