using System.Net;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Stat.Itok.Core
{
    public class CosmosEntity<TData>
    {
        public string Id { get; set; }
        public TData Data { get; set; }
        public string PartitionKey { get; set; }
    }

    public static class CosmosEntity
    {
        public static string GetPartitionKey<TData>(string pkPrefix)
        {
            return $"{pkPrefix}.{typeof(TData).Name}";
        }

        public static CosmosEntity<TData> CreateFrom<TData>(string id, TData data, string pkPrefix)
        {
            return new CosmosEntity<TData>()
            {
                Id = id,
                Data = data,
                PartitionKey = $"{pkPrefix}.{typeof(TData).Name}"
            };
        }
    }

    public interface ICosmosAccessor
    {
        Task<ItemResponse<CosmosEntity<TEntity>>> UpsertEntityInStoreAsync<TEntity>(string entityId,
            TEntity entity);

        Container GetContainer<TEntity>();
        Task<TEntity> GetEntityIfExistAsync<TEntity>(string id);
    }

    public class CosmosDbAccessor : ICosmosAccessor
    {
        private readonly IOptions<GlobalConfig> _options;
        private readonly CosmosClient _client;

        public CosmosDbAccessor(IOptions<GlobalConfig> options)
        {
            _options = options;
            _client = new CosmosClient(
                connectionString: _options.Value.CosmosDbConnStr,
                clientOptions: new CosmosClientOptions()
                {
                    SerializerOptions = new CosmosSerializationOptions()
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                    }
                });
        }

        public async Task<ItemResponse<CosmosEntity<TEntity>>> UpsertEntityInStoreAsync<TEntity>(string entityId,
            TEntity entity)
        {
            var cName = _options.Value.CosmosContainerName;
            var container = _client.GetContainer(_options.Value.CosmosDbName, cName);
            return await container.UpsertItemAsync(CosmosEntity.CreateFrom(Helper.BuildCosmosRealId<TEntity>(entityId),
                entity, _options.Value.CosmosDbPkPrefix));
        }

        public Container GetContainer<TEntity>()
        {
            var cName = _options.Value.CosmosContainerName;
            return _client.GetContainer(_options.Value.CosmosDbName, cName);
        }

        public async Task<TEntity> GetEntityIfExistAsync<TEntity>(string id)
        {
            var container = _client.GetContainer(_options.Value.CosmosDbName, _options.Value.CosmosContainerName);
            var resp = await container.ReadItemAsync<CosmosEntity<TEntity>>(Helper.BuildCosmosRealId<TEntity>(id),
                new PartitionKey(CosmosEntity.GetPartitionKey<TEntity>(_options.Value.CosmosDbPkPrefix)));
            if (resp.StatusCode == HttpStatusCode.OK) return resp.Resource.Data;
            return default;
        }
    }

    public interface IStorageAccessor
    {
        Task<QueueClient> GeJobRunTaskQueueClientAsync();
        Task<BlobContainerClient> GetBlobContainerClientAsync<T>();
    }

    public class StorageAccessor : IStorageAccessor
    {
        private readonly IOptions<GlobalConfig> _options;

        public StorageAccessor(IOptions<GlobalConfig> options)
        {
            _options = options;
        }

        public async Task<TableClient> GetTableClientAsync(string tableName)
        {
            var serviceClient = new TableServiceClient(_options.Value.CosmosDbConnStr);
            var tableClient = serviceClient.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();
            return tableClient;
        }

        public async Task<BlobContainerClient> GetBlobContainerClientAsync<T>()
        {
            var containerName = typeof(T).Name.ToLowerInvariant();
            var serviceClient = new BlobServiceClient(_options.Value.StorageAccountConnStr);
            var containerClient = serviceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            return containerClient;
        }

        public async Task<BlobContainerClient> GetBlobClientAsync(string containerName)
        {
            var serviceClient = new BlobServiceClient(_options.Value.StorageAccountConnStr);
            var containerClient = serviceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            return containerClient;
        }


        public async Task<TableClient> GetTableClientAsync<T>()
        {
            var tableName = $"{typeof(T).Name}";
            var serviceClient = new TableServiceClient(_options.Value.CosmosDbConnStr);
            var tableClient = serviceClient.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();
            return tableClient;
        }

        public async Task<QueueClient> GeJobRunTaskQueueClientAsync()
        {
            var serviceClient = new QueueServiceClient(Environment.GetEnvironmentVariable("WorkerQueueConnStr"));
            var queueClient = serviceClient.GetQueueClient(StatItokConstants.JobRunTaskQueueName);
            await queueClient.CreateIfNotExistsAsync();
            return queueClient;
        }
    }
}