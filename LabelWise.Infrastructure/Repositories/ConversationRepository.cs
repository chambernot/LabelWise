using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.NutritionConversation;
using LabelWise.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Repositories;

public sealed class ConversationRepository : IConversationRepository
{
    private readonly MongoDbContext _context;

    public ConversationRepository(MongoDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task SaveAsync(NutritionConversationSession session, CancellationToken cancellationToken = default)
    {
        if (session is null)
            throw new ArgumentNullException(nameof(session));

        return _context.NutritionConversations.InsertOneAsync(session, cancellationToken: cancellationToken);
    }

    public Task<NutritionConversationSession?> GetAsync(
        string conversationId,
        string analysisId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<NutritionConversationSession>.Filter.Eq(x => x.ConversationId, conversationId)
                   & Builders<NutritionConversationSession>.Filter.Eq(x => x.AnalysisId, analysisId)
                   & Builders<NutritionConversationSession>.Filter.Eq(x => x.Status, "active");

        return _context.NutritionConversations
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task AppendMessagesAsync(
        string conversationId,
        string analysisId,
        IReadOnlyCollection<ConversationMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
            return Task.CompletedTask;

        var filter = Builders<NutritionConversationSession>.Filter.Eq(x => x.ConversationId, conversationId)
                   & Builders<NutritionConversationSession>.Filter.Eq(x => x.AnalysisId, analysisId)
                   & Builders<NutritionConversationSession>.Filter.Eq(x => x.Status, "active");

        var update = Builders<NutritionConversationSession>.Update
            .PushEach(x => x.Messages, messages)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        return _context.NutritionConversations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}
