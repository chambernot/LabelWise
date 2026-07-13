using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly MongoDbContext _context;

    public UserProfileRepository(MongoDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Find(Builders<User>.Filter.Eq(x => x.Id, userId))
            .FirstOrDefaultAsync(cancellationToken);

        return user?.Profile;
    }
}
