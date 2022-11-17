using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace Stat.Itok.Core
{
    public interface IStorageAccessSvc
    {
        Task<TableClient> GetTableClientAsync(string tableName);
        Task<TableClient> GetTableClientAsync<T>();
        Task<QueueClient> GeJobRunTaskQueueClientAsync();
    }

    public class StorageAccessSvc : IStorageAccessSvc
    {
        private readonly IOptions<GlobalConfig> _options;

        public StorageAccessSvc(IOptions<GlobalConfig> options)
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
        
        public async Task<QueueClient> GeJobRunTaskQueueClientAsync()
        {
            var serviceClient = new QueueServiceClient(Environment.GetEnvironmentVariable("WorkerQueueConnStr"));
            var queueClient = serviceClient.GetQueueClient(StatItokConstants.JobRunTaskQueueName);
            await queueClient.CreateIfNotExistsAsync();
            return queueClient;
        }

    }
}