using System.ComponentModel.DataAnnotations;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs
{
    public class UserProfileDto
    {
        public string? UserId { get; set; }

        [Required]
        public GoalType Goal { get; set; }

        public bool HasLactoseRestriction { get; set; }
        public bool HasGlutenRestriction { get; set; }
        public bool HasDiabetesConcern { get; set; }
        public bool HasHypertensionConcern { get; set; }

        public bool IsVegan { get; set; }
        public bool IsVegetarian { get; set; }

        [Required]
        public ExplanationLevel PreferredExplanationLevel { get; set; }
    }
}
