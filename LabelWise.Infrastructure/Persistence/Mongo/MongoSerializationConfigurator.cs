using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace LabelWise.Infrastructure.Persistence.Mongo;

public static class MongoSerializationConfigurator
{
    private static int _configured;

    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 1)
        {
            return;
        }

        BsonSerializer.RegisterSerializer(new GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard));
        BsonSerializer.RegisterSerializer(new NullableSerializer<Guid>(new GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard)));
    }
}
