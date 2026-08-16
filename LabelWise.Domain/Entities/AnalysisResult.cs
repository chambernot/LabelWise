using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Domain.Entities
{
    public record AnalysisResult(
    string Summary,
    int ConfidenceScore,
    List<string> Tags,
    bool IsValid
);
}
