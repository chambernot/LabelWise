using System.Threading.Tasks;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(System.Guid id);
        Task AddAsync(User user);
        Task SaveChangesAsync();
    }
}
