using System.Net;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using Microsoft.Azure.Cosmos;

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

        public static string BuildCosmosRealId<TData>(string id, string prefix)
        {
            return $"${prefix}.{typeof(TData).Name}__{id}";
        }

        public static CosmosEntity<TData> CreateFrom<TData>(string id, TData data, string pkPrefix)
        {
            return new CosmosEntity<TData>()
            {
                Id = id,
                Data = data,
                PartitionKey = GetPartitionKey<TData>(pkPrefix)
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
            return await container.UpsertItemAsync(CosmosEntity.CreateFrom(
                CosmosEntity.BuildCosmosRealId<TEntity>(entityId, _options.Value.CosmosDbPkPrefix),
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
            try
            {
                var resp = await container.ReadItemAsync<CosmosEntity<TEntity>>(
                    CosmosEntity.BuildCosmosRealId<TEntity>(id, _options.Value.CosmosDbPkPrefix),
                    new PartitionKey(CosmosEntity.GetPartitionKey<TEntity>(_options.Value.CosmosDbPkPrefix)));
                return resp.Resource.Data;
            }
            catch (Exception)
            {
                return default;
            }
        }
    }

    public interface IStorageAccessor
    {
        Task<QueueClient> GetJobRunTaskQueueClientAsync();
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
            var serviceClient = new TableServiceClient(_options.Value.StorageAccountConnStr);
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
            var serviceClient = new TableServiceClient(_options.Value.StorageAccountConnStr);
            var tableClient = serviceClient.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();
            return tableClient;
        }

        public async Task<QueueClient> GetJobRunTaskQueueClientAsync()
        {
            var serviceClient = new QueueServiceClient(_options.Value.StorageAccountConnStr);
            var queueClient = serviceClient.GetQueueClient(StatItokConstants.JobRunTaskQueueName);
            await queueClient.CreateIfNotExistsAsync();
            return queueClient;
        }
    }
}