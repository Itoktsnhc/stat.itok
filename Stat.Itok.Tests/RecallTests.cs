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
using Stat.Itok.Core.Helpers;

namespace Stat.Itok.Tests
{
    [TestClass]
    public class RecallTests
    {
        private readonly IServiceProvider _sp;

        public RecallTests()
        {
            var content =
                JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("./configs/settings.json"));
            var svc = new ServiceCollection()
                .AddSingleton(_ => Options.Create(new GlobalConfig()
                {
                    StorageAccountConnStr = content["GlobalConfig__StorageAccountConnStr"],
                    CosmosDbConnStr = content["GlobalConfig__CosmosDbConnStr"],
                    CosmosDbPkPrefix = content["GlobalConfig__CosmosDbPkPrefix"],
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
                jobConfig.EnabledQueries =
                    JsonConvert.DeserializeObject<List<string>>(CommonHelper.DecompressStr(item.EnabledQueriesStr));
                jobConfig.NinAuthContext =
                    JsonConvert.DeserializeObject<NinAuthContext>(CommonHelper.DecompressStr(item.NinAuthContextStr));
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
                await container.DeleteItemAsync<JobConfig>(id,
                    new Microsoft.Azure.Cosmos.PartitionKey("prod.JobConfig"));
            }
        }

        [TestMethod]
        public void MyTestMethod()
        {
            var payloadNameList = Directory.GetFiles("D:\\_repos\\stat.itok\\Stat.Itok.Tests\\msg\\");
            foreach (var payload in payloadNameList)
            {
                var content =
                    JsonConvert.DeserializeObject<JobRunTaskLite>(
                        CommonHelper.DecompressStr(File.ReadAllText(payload)));
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
                    var resp = await container.DeleteItemAsync<PureIdDto>(item.Id,
                        new Microsoft.Azure.Cosmos.PartitionKey("prod.BattleTaskPayload"));
                    i++;
                }
            }

            Console.WriteLine(i);
        }

        [TestMethod]
        public async Task PerformIndexPolicyAsync()
        {
            var cosmos = _sp.GetRequiredService<CosmosDbAccessor>();
            var container = cosmos.GetContainer<BattleTaskPayload>();
            string sqlQueryText =
                "SELECT c.data.trackedId FROM c where c.partitionKey = 'prod.BattleTaskPayload' and c.data.jobConfigId = 'nin_user_1ae0221c54a7cba9'   order by c.data.trackedId desc offset 0 limit 10";

            QueryDefinition query = new QueryDefinition(sqlQueryText);

            FeedIterator<PureIdDto> resultSetIterator = container.GetItemQueryIterator<PureIdDto>(
                query, requestOptions: new QueryRequestOptions
                {
                    PopulateIndexMetrics = true
                });

            FeedResponse<PureIdDto> response = null;

            while (resultSetIterator.HasMoreResults)
            {
                response = await resultSetIterator.ReadNextAsync();
                Console.WriteLine(response.IndexMetrics);
            }
        }

        [TestMethod]
        public async Task RerunBattleTaskAsync()
        {
            var cosmos = _sp.GetRequiredService<CosmosDbAccessor>();
            var container = cosmos.GetContainer<BattleTaskPayload>();
            var ids = File.ReadAllLines("./configs/rerun_list.txt");
            foreach (var id in ids)
            {
                await container.DeleteItemAsync<PureIdDto>(id,
                    new Microsoft.Azure.Cosmos.PartitionKey("prod.BattleTaskPayload"));
            }
        }

        [TestMethod]
        public async Task ResetJobConfigEnableAndLimitAsync()
        {
            var cosmos = _sp.GetRequiredService<CosmosDbAccessor>();
            var container = cosmos.GetContainer<BattleTaskPayload>();
            var rawSQL =
                $"SELECT * FROM c where c.partitionKey= 'prod.JobConfig'";
            QueryDefinition query = new QueryDefinition(rawSQL);

            var resultSetIterator = container.GetItemQueryIterator<CosmosEntity<JobConfig>>(
                query);

            while (resultSetIterator.HasMoreResults)
            {
                var response = await resultSetIterator.ReadNextAsync();
                foreach (var entity in response)
                {
                    var jobConfig = entity.Data;
                    jobConfig.Enabled = true;
                    jobConfig.NeedBuildFromBeginCount = 0;
                    var resp = await cosmos.UpsertEntityInStoreAsync(jobConfig.Id, jobConfig);
                }
            }
        }

        [TestMethod]
        public async Task DeleteInvalidJobHisByTimeRangeAsync()
        {
            var cosmos = _sp.GetRequiredService<CosmosDbAccessor>();
            var container = cosmos.GetContainer<BattleTaskPayload>();
            var rawSQL =
                $"SELECT * FROM c where c.partitionKey = 'prod.BattleTaskPayload' and c.data.jobConfigId ='nin_user_1cefa0676d5aba27' and c._ts >1693838917\r\n";
            QueryDefinition query = new QueryDefinition(rawSQL);

            var resultSetIterator = container.GetItemQueryIterator<PureIdDto>(
                query);

            while (resultSetIterator.HasMoreResults)
            {
                var response = await resultSetIterator.ReadNextAsync();
                foreach (var entity in response)
                {
                    await container.DeleteItemAsync<PureIdDto>(entity.Id,
                     new Microsoft.Azure.Cosmos.PartitionKey("prod.BattleTaskPayload"));
                }
            }
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