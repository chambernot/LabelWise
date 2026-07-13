using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories
{
    public class AppUserRepository : IAppUserRepository
    {
        private readonly MongoDbContext _context;

        public AppUserRepository(MongoDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<AppUser?> GetByDeviceIdAsync(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return null;
            }

            return await _context.AppUsers
                .Find(Builders<AppUser>.Filter.Eq(x => x.DeviceId, deviceId.Trim()))
                .FirstOrDefaultAsync();
        }

        public async Task CreateAsync(AppUser appUser)
        {
            ArgumentNullException.ThrowIfNull(appUser);

            await _context.AppUsers.ReplaceOneAsync(
                Builders<AppUser>.Filter.Eq(x => x.Id, appUser.Id),
                appUser,
                new ReplaceOptions { IsUpsert = true });
        }

        public async Task UpdateAsync(AppUser appUser)
        {
            ArgumentNullException.ThrowIfNull(appUser);

            await _context.AppUsers.ReplaceOneAsync(
                Builders<AppUser>.Filter.Eq(x => x.Id, appUser.Id),
                appUser,
                new ReplaceOptions { IsUpsert = true });
        }

        public Task SaveChangesAsync()
        {
            return Task.CompletedTask;
        }
    }
}
