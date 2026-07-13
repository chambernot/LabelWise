using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Persistence.Mongo;

public sealed class MongoIndexInitializerHostedService : IHostedService
{
    private readonly MongoDbContext _context;
    private readonly ILogger<MongoIndexInitializerHostedService> _logger;

    public MongoIndexInitializerHostedService(MongoDbContext context, ILogger<MongoIndexInitializerHostedService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.Database.RunCommandAsync((Command<BsonDocument>)"{ ping: 1 }", cancellationToken: cancellationToken);

            await _context.Users.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<LabelWise.Domain.Entities.User>(Builders<LabelWise.Domain.Entities.User>.IndexKeys.Ascending(x => x.Email), new CreateIndexOptions { Unique = true, Name = "ux_users_email" })
            ], cancellationToken);

            await _context.AppUsers.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<LabelWise.Domain.Entities.AppUser>(Builders<LabelWise.Domain.Entities.AppUser>.IndexKeys.Ascending(x => x.DeviceId), new CreateIndexOptions { Unique = true, Name = "ux_app_users_device_id" })
            ], cancellationToken);

            await _context.Products.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<LabelWise.Domain.Entities.Product>(Builders<LabelWise.Domain.Entities.Product>.IndexKeys.Ascending(x => x.Barcode), new CreateIndexOptions { Sparse = true, Name = "ix_products_barcode" })
            ], cancellationToken);

            await _context.Analyses.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<LabelWise.Domain.Entities.ProductAnalysis>(Builders<LabelWise.Domain.Entities.ProductAnalysis>.IndexKeys.Descending(x => x.AnalyzedAt), new CreateIndexOptions { Name = "ix_analyses_analyzed_at" }),
                new CreateIndexModel<LabelWise.Domain.Entities.ProductAnalysis>(Builders<LabelWise.Domain.Entities.ProductAnalysis>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.AnalyzedAt), new CreateIndexOptions { Sparse = true, Name = "ix_analyses_user_history" }),
                new CreateIndexModel<LabelWise.Domain.Entities.ProductAnalysis>(Builders<LabelWise.Domain.Entities.ProductAnalysis>.IndexKeys.Ascending(x => x.DeviceId).Descending(x => x.AnalyzedAt), new CreateIndexOptions { Sparse = true, Name = "ix_analyses_device_history" })
            ], cancellationToken);

            await _context.AnalysisSessions.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<LabelWise.Domain.Entities.ProductAnalysisSession>(Builders<LabelWise.Domain.Entities.ProductAnalysisSession>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.StartedAt), new CreateIndexOptions { Sparse = true, Name = "ix_analysis_sessions_user" }),
                new CreateIndexModel<LabelWise.Domain.Entities.ProductAnalysisSession>(Builders<LabelWise.Domain.Entities.ProductAnalysisSession>.IndexKeys.Ascending(x => x.Status).Descending(x => x.StartedAt), new CreateIndexOptions { Name = "ix_analysis_sessions_status" })
            ], cancellationToken);

            await _context.ProductCaptures.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<LabelWise.Domain.Entities.ProductCapture>(Builders<LabelWise.Domain.Entities.ProductCapture>.IndexKeys.Ascending(x => x.SessionId).Descending(x => x.CapturedAt), new CreateIndexOptions { Name = "ix_product_captures_session" }),
                new CreateIndexModel<LabelWise.Domain.Entities.ProductCapture>(Builders<LabelWise.Domain.Entities.ProductCapture>.IndexKeys.Ascending(x => x.ProductId).Ascending(x => x.CaptureType).Descending(x => x.CapturedAt), new CreateIndexOptions { Sparse = true, Name = "ix_product_captures_product_type" })
            ], cancellationToken);

            await _context.ValidatedProducts.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<LabelWise.Domain.Entities.ValidatedProduct>(Builders<LabelWise.Domain.Entities.ValidatedProduct>.IndexKeys.Ascending(x => x.ProductId), new CreateIndexOptions { Unique = true, Name = "ux_validated_products_product_id" }),
                new CreateIndexModel<LabelWise.Domain.Entities.ValidatedProduct>(Builders<LabelWise.Domain.Entities.ValidatedProduct>.IndexKeys.Ascending(x => x.ValidatedBarcode), new CreateIndexOptions { Sparse = true, Name = "ix_validated_products_barcode" })
            ], cancellationToken);

            await _context.KnownProducts.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<LabelWise.Domain.Entities.KnownProduct>(Builders<LabelWise.Domain.Entities.KnownProduct>.IndexKeys.Ascending(x => x.Barcode), new CreateIndexOptions { Sparse = true, Name = "ix_known_products_barcode" }),
                new CreateIndexModel<LabelWise.Domain.Entities.KnownProduct>(Builders<LabelWise.Domain.Entities.KnownProduct>.IndexKeys.Ascending(x => x.Category).Descending(x => x.IdentificationCount), new CreateIndexOptions { Name = "ix_known_products_category" }),
                new CreateIndexModel<LabelWise.Domain.Entities.KnownProduct>(Builders<LabelWise.Domain.Entities.KnownProduct>.IndexKeys.Text(x => x.SearchText), new CreateIndexOptions { Name = "ix_known_products_search_text" })
            ], cancellationToken);

            await _context.CategoryNutritionProfiles.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<CategoryNutritionProfileDocument>(Builders<CategoryNutritionProfileDocument>.IndexKeys.Ascending(x => x.CategoryCode), new CreateIndexOptions { Unique = true, Name = "ux_category_profiles_category_code" })
            ], cancellationToken);

            await _context.NutritionCategoryAliases.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<NutritionCategoryAliasDocument>(Builders<NutritionCategoryAliasDocument>.IndexKeys.Ascending(x => x.CategoryCode), new CreateIndexOptions { Name = "ix_nutrition_category_aliases_category_code" }),
                new CreateIndexModel<NutritionCategoryAliasDocument>(Builders<NutritionCategoryAliasDocument>.IndexKeys.Ascending(x => x.AliasNameNormalized).Ascending(x => x.CategoryCode), new CreateIndexOptions { Unique = true, Name = "ux_nutrition_category_aliases_normalized" })
            ], cancellationToken);

            await _context.CategoryMappings.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<CategoryMappingDocument>(Builders<CategoryMappingDocument>.IndexKeys.Ascending(x => x.RawCategoryName).Descending(x => x.Confidence), new CreateIndexOptions { Name = "ix_category_mappings_raw_name" })
            ], cancellationToken);

            await _context.NutritionCategories.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<NutritionCategoryDocument>(Builders<NutritionCategoryDocument>.IndexKeys.Ascending(x => x.Code), new CreateIndexOptions { Unique = true, Name = "ux_nutrition_categories_code" })
            ], cancellationToken);

            await _context.ImageAnalysisCache.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<ImageAnalysisCacheDocument>(Builders<ImageAnalysisCacheDocument>.IndexKeys.Ascending(x => x.ImageHash), new CreateIndexOptions { Name = "ix_image_analysis_cache_image_hash" }),
                new CreateIndexModel<ImageAnalysisCacheDocument>(Builders<ImageAnalysisCacheDocument>.IndexKeys.Ascending(x => x.CacheVersion).Descending(x => x.CreatedAt), new CreateIndexOptions { Name = "ix_image_analysis_cache_version_created_at" }),
                new CreateIndexModel<ImageAnalysisCacheDocument>(Builders<ImageAnalysisCacheDocument>.IndexKeys.Descending(x => x.CreatedAt), new CreateIndexOptions { Name = "ix_image_analysis_cache_created_at" })
            ], cancellationToken);

            await _context.NutritionConversations.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<LabelWise.Application.Models.NutritionConversation.NutritionConversationSession>(Builders<LabelWise.Application.Models.NutritionConversation.NutritionConversationSession>.IndexKeys.Ascending(x => x.ConversationId), new CreateIndexOptions { Unique = true, Name = "ux_nutrition_conversations_conversation_id" }),
                new CreateIndexModel<LabelWise.Application.Models.NutritionConversation.NutritionConversationSession>(Builders<LabelWise.Application.Models.NutritionConversation.NutritionConversationSession>.IndexKeys.Ascending(x => x.AnalysisId).Descending(x => x.CreatedAt), new CreateIndexOptions { Name = "ix_nutrition_conversations_analysis" }),
                new CreateIndexModel<LabelWise.Application.Models.NutritionConversation.NutritionConversationSession>(Builders<LabelWise.Application.Models.NutritionConversation.NutritionConversationSession>.IndexKeys.Ascending(x => x.DeviceId).Descending(x => x.CreatedAt), new CreateIndexOptions { Sparse = true, Name = "ix_nutrition_conversations_device" })
            ], cancellationToken);

            _logger.LogInformation("MongoDB conectado e índices garantidos para o banco {DatabaseName}.", _context.Settings.DatabaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao inicializar conexão/índices do MongoDB.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
