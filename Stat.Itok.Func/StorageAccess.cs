using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Stat.Itok.Core;
using System.Threading.Tasks;

namespace Stat.Itok.Func
{
    public interface IStorageAccessSvc
    {
        Task<BlobContainerClient> GetBlobClientAsync(string containerName);
        Task<BlobContainerClient> GetBlobClientAsync<T>();
        Task<TableClient> GetTableClientAsync(string tableName);
        Task<TableClient> GetTableClientAsync<T>();
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
            var tableName = $"{_options.Value.SubPrefix}000{typeof(T).Name}";
            var serviceClient = new TableServiceClient(_options.Value.StorageAccountConnStr);
            var tableClient = serviceClient.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();
            return tableClient;
        }

        public async Task<BlobContainerClient> GetBlobClientAsync<T>()
        {
            var containerName = $"{_options.Value.SubPrefix}000{typeof(T).Name}";
            var serviceClient = new BlobServiceClient(_options.Value.StorageAccountConnStr);
            var containerClient = serviceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            return containerClient;
        }

    }

}
