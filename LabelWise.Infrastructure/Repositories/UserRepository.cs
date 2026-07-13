using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly MongoDbContext _context;

        public UserRepository(MongoDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return await _context.Users
                .Find(Builders<User>.Filter.Eq(x => x.Email, email.Trim()))
                .FirstOrDefaultAsync();
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .Find(Builders<User>.Filter.Eq(x => x.Id, id))
                .FirstOrDefaultAsync();
        }

        public async Task AddAsync(User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            await _context.Users.ReplaceOneAsync(
                Builders<User>.Filter.Eq(x => x.Id, user.Id),
                user,
                new ReplaceOptions { IsUpsert = true });
        }

        public Task SaveChangesAsync()
        {
            return Task.CompletedTask;
        }
    }
}
