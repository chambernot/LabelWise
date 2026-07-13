using LabelWise.Domain.Entities;
using LabelWise.Application.Models.NutritionConversation;
using MongoDB.Driver;
using Microsoft.Extensions.Options;

namespace LabelWise.Infrastructure.Persistence.Mongo;

public sealed class MongoDbContext
{
    public MongoDbContext(IMongoDatabase database, IOptions<MongoDbSettings> settings)
    {
        Database = database ?? throw new ArgumentNullException(nameof(database));
        Settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public MongoDbSettings Settings { get; }
    public IMongoDatabase Database { get; }

    public IMongoCollection<User> Users => Database.GetCollection<User>(MongoCollectionNames.Users);
    public IMongoCollection<AppUser> AppUsers => Database.GetCollection<AppUser>(MongoCollectionNames.AppUsers);
    public IMongoCollection<Product> Products => Database.GetCollection<Product>(MongoCollectionNames.Products);
    public IMongoCollection<ProductAnalysis> Analyses => Database.GetCollection<ProductAnalysis>(MongoCollectionNames.Analyses);
    public IMongoCollection<ProductAnalysisSession> AnalysisSessions => Database.GetCollection<ProductAnalysisSession>(MongoCollectionNames.AnalysisSessions);
    public IMongoCollection<ProductCapture> ProductCaptures => Database.GetCollection<ProductCapture>(MongoCollectionNames.ProductCaptures);
    public IMongoCollection<ValidatedProduct> ValidatedProducts => Database.GetCollection<ValidatedProduct>(MongoCollectionNames.ValidatedProducts);
    public IMongoCollection<KnownProduct> KnownProducts => Database.GetCollection<KnownProduct>(MongoCollectionNames.KnownProducts);
    public IMongoCollection<NutritionCategoryDocument> NutritionCategories => Database.GetCollection<NutritionCategoryDocument>(MongoCollectionNames.NutritionCategories);
    public IMongoCollection<NutritionCategoryAliasDocument> NutritionCategoryAliases => Database.GetCollection<NutritionCategoryAliasDocument>(MongoCollectionNames.NutritionCategoryAliases);
    public IMongoCollection<CategoryNutritionProfileDocument> CategoryNutritionProfiles => Database.GetCollection<CategoryNutritionProfileDocument>(MongoCollectionNames.CategoryNutritionProfiles);
    public IMongoCollection<CategoryMappingDocument> CategoryMappings => Database.GetCollection<CategoryMappingDocument>(MongoCollectionNames.CategoryMappings);
    public IMongoCollection<ImageAnalysisCacheDocument> ImageAnalysisCache => Database.GetCollection<ImageAnalysisCacheDocument>(MongoCollectionNames.ImageAnalysisCache);
    public IMongoCollection<NutritionAnalysisCacheDocument> NutritionAnalysisCache => Database.GetCollection<NutritionAnalysisCacheDocument>(MongoCollectionNames.NutritionAnalysisCache);
    public IMongoCollection<NutritionConversationSession> NutritionConversations => Database.GetCollection<NutritionConversationSession>(MongoCollectionNames.NutritionConversations);
}
