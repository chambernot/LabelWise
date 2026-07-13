using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces
{
    public interface IAppUserRepository
    {
        Task<AppUser?> GetByDeviceIdAsync(string deviceId);
        Task CreateAsync(AppUser appUser);
        Task UpdateAsync(AppUser appUser);
        Task SaveChangesAsync();
    }
}
