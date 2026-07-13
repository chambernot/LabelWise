using System;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly IUserRepository _users;

        public UserProfileService(IUserRepository users)
        {
            _users = users;
        }

        public async Task<UserProfileDto?> GetForUserAsync(Guid userId)
        {
            var user = await _users.GetByIdAsync(userId);
            if (user == null) return null;

            var profile = user.Profile;
            if (profile == null)
            {
                // return default empty profile with Undefined goal
                return new UserProfileDto
                {
                    UserId = user.Id.ToString(),
                    Goal = GoalType.Undefined,
                    HasLactoseRestriction = false,
                    HasGlutenRestriction = false,
                    HasDiabetesConcern = false,
                    HasHypertensionConcern = false,
                    IsVegan = false,
                    IsVegetarian = false,
                    PreferredExplanationLevel = ExplanationLevel.Brief
                };
            }

            return new UserProfileDto
            {
                UserId = user.Id.ToString(),
                Goal = profile.Goal,
                HasLactoseRestriction = profile.LactoseIntolerance,
                HasGlutenRestriction = profile.GlutenFree,
                HasDiabetesConcern = profile.Diabetes,
                HasHypertensionConcern = profile.SodiumControl,
                // map goal-based flags
                IsVegan = profile.Goal == GoalType.Vegan,
                IsVegetarian = profile.Goal == GoalType.Vegetarian,
                PreferredExplanationLevel = profile.PreferredExplanation
            };
        }

        public async Task<UserProfileDto> UpdateForUserAsync(Guid userId, UserProfileDto dto)
        {
            var user = await _users.GetByIdAsync(userId);
            if (user == null) throw new InvalidOperationException("User not found");

            var profile = user.Profile;
            if (profile == null)
            {
                var newProfile = new UserProfile(user.Id, dto.Goal,
                    dto.HasLactoseRestriction, dto.HasGlutenRestriction,
                    dto.HasDiabetesConcern, dto.HasHypertensionConcern,
                    otherRestrictions: null,
                    preferredExplanation: dto.PreferredExplanationLevel);

                user.SetProfile(newProfile);
            }
            else
            {
                profile.UpdateGoal(dto.Goal);
                profile.SetRestrictions(dto.HasLactoseRestriction, dto.HasGlutenRestriction,
                    dto.HasDiabetesConcern, dto.HasHypertensionConcern, other: null);
                profile.SetPreferredExplanation(dto.PreferredExplanationLevel);
            }

            await _users.SaveChangesAsync();

            dto.UserId = user.Id.ToString();
            return dto;
        }
    }
}
