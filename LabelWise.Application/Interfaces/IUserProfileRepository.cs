using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces
{
    public interface IUserProfileRepository
    {
        Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
