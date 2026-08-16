using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Domain.Entities
{
    public record ChatAnalysisResponse(
   string MessageResponse,
   List<string> DetectedItems,
   bool RequiresFollowUp
);
}
