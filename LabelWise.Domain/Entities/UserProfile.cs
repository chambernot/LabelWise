using System;
using LabelWise.Domain.Common;
using LabelWise.Domain.Enums;

namespace LabelWise.Domain.Entities
{
    public class UserProfile : AuditableEntity
    {
        public Guid UserId { get; private set; }
        public virtual User User { get; private set; } = null!;

        public GoalType Goal { get; private set; }

        // Simple flags for common restrictions, but keep a free-text field for custom ones
        public bool LactoseIntolerance { get; private set; }
        public bool GlutenFree { get; private set; }
        public bool Diabetes { get; private set; }
        public bool SodiumControl { get; private set; }

        public string? OtherRestrictions { get; private set; }
        public ExplanationLevel PreferredExplanation { get; private set; }

        protected UserProfile() { }

        public UserProfile(Guid userId, GoalType goal,
            bool lactoseIntolerance = false, bool glutenFree = false,
            bool diabetes = false, bool sodiumControl = false,
            string? otherRestrictions = null,
            ExplanationLevel preferredExplanation = ExplanationLevel.Brief)
        {
            UserId = userId;
            Goal = goal;
            LactoseIntolerance = lactoseIntolerance;
            GlutenFree = glutenFree;
            Diabetes = diabetes;
            SodiumControl = sodiumControl;
            OtherRestrictions = otherRestrictions;
            PreferredExplanation = preferredExplanation;
        }

        public void UpdateGoal(GoalType goal)
        {
            Goal = goal;
            SetUpdated();
        }

        public void SetRestrictions(bool lactose, bool gluten, bool diabetes, bool sodium, string? other = null)
        {
            LactoseIntolerance = lactose;
            GlutenFree = gluten;
            Diabetes = diabetes;
            SodiumControl = sodium;
            OtherRestrictions = other;
            SetUpdated();
        }

        public void SetPreferredExplanation(ExplanationLevel level)
        {
            PreferredExplanation = level;
            SetUpdated();
        }
    }
}
