using System.Text.Json;
using EventSourcing.Abstractions.Sagas;
using EventSourcing.Core.Sagas;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EventSourcing.MongoDB;

/// <summary>
/// MongoDB implementation of saga store
/// </summary>
public class MongoSagaStore : ISagaStore
{
    private readonly IMongoDatabase _database;
    private const string CollectionName = "sagas";

    public MongoSagaStore(IMongoDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    private IMongoCollection<BsonDocument> GetCollection()
    {
        return _database.GetCollection<BsonDocument>(CollectionName);
    }

    public async Task SaveAsync<TData>(ISaga<TData> saga, CancellationToken cancellationToken = default)
        where TData : class
    {
        if (saga == null) throw new ArgumentNullException(nameof(saga));

        var collection = GetCollection();

        var document = new BsonDocument
        {
            ["_id"] = saga.SagaId,
            ["sagaName"] = saga.SagaName,
            ["data"] = BsonDocument.Parse(JsonSerializer.Serialize(saga.Data)),
            ["dataType"] = typeof(TData).AssemblyQualifiedName,
            ["status"] = saga.Status.ToString(),
            ["currentStepIndex"] = saga.CurrentStepIndex,
            ["updatedAt"] = DateTime.UtcNow
        };

        var filter = Builders<BsonDocument>.Filter.Eq("_id", saga.SagaId);
        await collection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<ISaga<TData>?> LoadAsync<TData>(string sagaId, CancellationToken cancellationToken = default)
        where TData : class
    {
        if (string.IsNullOrEmpty(sagaId)) throw new ArgumentNullException(nameof(sagaId));

        var collection = GetCollection();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", sagaId);
        var document = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

        if (document == null)
            return null;

        var dataJson = document["data"].ToJson();
        var data = JsonSerializer.Deserialize<TData>(dataJson);

        if (data == null)
            return null;

        var sagaName = document["sagaName"].AsString;
        var status = Enum.Parse<SagaStatus>(document["status"].AsString);
        var currentStepIndex = document["currentStepIndex"].AsInt32;

        var saga = new Saga<TData>(sagaName, data, sagaId);
        saga.RestoreState(status, currentStepIndex);

        return saga;
    }

    public async Task DeleteAsync(string sagaId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sagaId)) throw new ArgumentNullException(nameof(sagaId));

        var collection = GetCollection();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", sagaId);
        await collection.DeleteOneAsync(filter, cancellationToken);
    }
}
