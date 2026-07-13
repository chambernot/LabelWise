using System;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;

namespace LabelWise.Application.Interfaces
{
    public interface IUserProfileService
    {
        Task<UserProfileDto?> GetForUserAsync(Guid userId);
        Task<UserProfileDto> UpdateForUserAsync(Guid userId, UserProfileDto dto);
    }
}
