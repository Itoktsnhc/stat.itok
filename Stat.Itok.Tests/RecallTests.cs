using Azure;
using Azure.Data.Tables;
using JobTrackerX.Client;
using Mapster;
using MediatR;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Stat.Itok.Core;
using Stat.Itok.Core.ApiClients;
using Stat.Itok.Core.Handlers;
using Stat.Itok.Shared;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Stat.Itok.Tests
{
    [TestClass]
    public class RecallTests
    {
        private readonly IServiceProvider _sp;

        public RecallTests()
        {
            var content = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("./configs/settings.json"));
            var svc = new ServiceCollection()
                .AddSingleton(_ => Options.Create(new GlobalConfig()
                {
                    StorageAccountConnStr = content["GlobalConfig__StorageAccountConnStr"],
                    CosmosDbConnStr = content["GlobalConfig__CosmosDbConnStr"],
                    CosmosDbPkPrefix = content["GlobalConfig__CosmosDbPkPrefix"]
                }))
                .AddSingleton<IJobTrackerClient, JobTrackerClient>(_ =>
                {
                    return new JobTrackerClient("http://jobtracker.itok.xyz/");
                })
                .AddHttpClient()
                .AddSingleton<StorageAccessor>()
                .AddSingleton<CosmosDbAccessor>()
                .AddHttpClient()
                .AddMemoryCache()
                .AddSingleton<IStorageAccessor, StorageAccessor>()
                .AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipeline<,>))
                .AddSingleton<IJobTrackerClient, JobTrackerClient>(x =>
                    new JobTrackerClient(x.GetRequiredService<IOptions<GlobalConfig>>().Value.JobSysBase))
                .AddSingleton<RemoteConfigStore>()
                .AddSingleton(sp =>
                {
                    var store = sp.GetRequiredService<RemoteConfigStore>();
                    return store.GetNinMiscConfigAsync().GetAwaiter().GetResult();
                })
                .AddMediatR(typeof(NintendoPrivateHandlers))
                .AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipeline<,>))
                .AddMemoryCache()
                .AddLogging();
            svc.AddHttpClient<INintendoApi, NintendoApi>()
                .ConfigurePrimaryHttpMessageHandler(_ =>
                    new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    });
            svc.AddHttpClient<IImInkApi, ImInkApi>()
                .ConfigurePrimaryHttpMessageHandler(_ =>
                    new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    });
            svc.AddHttpClient<IStatInkApi, StatInkApi>()
                .ConfigurePrimaryHttpMessageHandler(_ =>
                    new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    });
            _sp = svc.BuildServiceProvider();
        }

        [TestMethod]
        public async Task Mirgration()
        {
            var store = _sp.GetRequiredService<StorageAccessor>();
            var cosmos = _sp.GetRequiredService<CosmosDbAccessor>();
            var table = await store.GetTableClientAsync<JobConfig>();
            var res = table.QueryAsync<EncryptedJobConfig>(x => x.PartitionKey == nameof(JobConfig) && x.Enabled);
            await foreach (var item in res)
            {
                var jobConfig = item.Adapt<JobConfig>();
                jobConfig.EnabledQueries = JsonConvert.DeserializeObject<List<string>>(Helper.DecompressStr(item.EnabledQueriesStr));
                jobConfig.NinAuthContext = JsonConvert.DeserializeObject<NinAuthContext>(Helper.DecompressStr(item.NinAuthContextStr));
                var resp = await cosmos.UpsertEntityInStoreAsync<JobConfig>(jobConfig.Id, jobConfig);
            }
        }

        [TestMethod]
        public async Task DeleteOldElement()
        {
            var cosmos = _sp.GetRequiredService<CosmosDbAccessor>();
            var container = cosmos.GetContainer<JobConfig>();
            var ids = File.ReadAllLines("./configs/doc_id_list.txt");
            foreach (var id in ids)
            {
                await container.DeleteItemAsync<JobConfig>(id, new Microsoft.Azure.Cosmos.PartitionKey("prod.JobConfig"));
            }
        }
        [TestMethod]
        public void MyTestMethod()
        {
            var payloadNameList = Directory.GetFiles("D:\\_repos\\stat.itok\\Stat.Itok.Tests\\msg\\");
            foreach (var payload in payloadNameList)
            {
                var content = JsonConvert.DeserializeObject<JobRunTaskLite>(Helper.DecompressStr(File.ReadAllText(payload)));

            }
        }

        [TestMethod]
        public async Task DeleteOldElements()
        {
            var cosmos = _sp.GetRequiredService<CosmosDbAccessor>();
            var container = cosmos.GetContainer<BattleTaskPayload>();
            var query = new QueryDefinition(
                    query: "SELECT * FROM store AS s WHERE s.partitionKey = 'prod.BattleTaskPayload' and s._ts >=1670895947"
                );
            using var filteredFeed = container.GetItemQueryIterator<PureIdDto>(
                queryDefinition: query
            );
            var i = 0;
            while (filteredFeed.HasMoreResults)
            {
                FeedResponse<PureIdDto> response = await filteredFeed.ReadNextAsync();
                foreach (var item in response.Resource)
                {
                    var resp = await container.DeleteItemAsync<PureIdDto>(item.Id, new Microsoft.Azure.Cosmos.PartitionKey("prod.BattleTaskPayload"));
                    i++;
                }
            }
            Console.WriteLine(i);
        }

        private class PureIdDto
        {
            public string Id { get; set; }
        }
    }

    public record EncryptedJobConfig : JobConfig, ITableEntity
    {
        public string NinAuthContextStr { get; set; }
        public string EnabledQueriesStr { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
