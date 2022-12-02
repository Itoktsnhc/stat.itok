using Azure.Storage.Blobs.Specialized;
using JobTrackerX.Client;
using Mapster;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Stat.Itok.Core;
using Stat.Itok.Core.ApiClients;
using Stat.Itok.Core.Handlers;
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
                    StorageAccountConnStr = content["GlobalConfig__StorageAccountConnStr"]
                }))
                .AddHttpClient()
                .AddSingleton<StorageAccessSvc>()
                .AddHttpClient()
                .AddMemoryCache()
                .AddSingleton<IStorageAccessSvc, StorageAccessSvc>()
                .AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipeline<,>))
                .AddSingleton<IJobTrackerClient, JobTrackerClient>(x =>
                    new JobTrackerClient(x.GetRequiredService<IOptions<GlobalConfig>>().Value.JobSysBase))
                .AddSingleton<RemoteConfigStore>()
                .AddSingleton(sp =>
                {
                    var store = sp.GetRequiredService<RemoteConfigStore>();
                    return store.GetWebViewDataAsync().GetAwaiter().GetResult();
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
        public async Task TestPosionMsgAsync()
        {
            var scope = _sp.CreateScope();
            var sp = scope.ServiceProvider;
            var store = sp.GetRequiredService<IStorageAccessSvc>();
            var container = await store.GetBlobContainerClientAsync<PoisonQueueMsg>();
            var fileName = "44e4392e-6f7f-4215-af01-9bc991aa2ba5.payload";
            var content = container.GetBlockBlobClient(fileName);
            var resp = await content.DownloadContentAsync();
            var array = resp.Value.Content.ToArray();
            var jobRunTaskLite = JsonConvert.DeserializeObject<JobRunTaskLite>(
                Stat.Itok.Core.Helper.DecompressStr(
                    Encoding.UTF8.GetString(
                        Stat.Itok.Core.Helper.DecompressBytes(array))));

            var payloadTable = await store.GetTableClientAsync<JobRunTaskPayload>();
            var p = await payloadTable.GetEntityIfExistsAsync<JobRunTaskPayload>(jobRunTaskLite.Pk, jobRunTaskLite.Rk);
            var task = JsonConvert.DeserializeObject<BattleTaskPayload>(Helper.DecompressStr(p.Value.CompressedPayload));
            var jobConfig = await GetJobConfigAsync(store, task.JobConfigId);
            var _mediator = sp.GetRequiredService<IMediator>();
            var gearsInfo = await _mediator.Send(new ReqGetGearsInfo());
            var vsDetailDistoryQueryName = $"{nameof(QueryHash.VsHistoryDetail)}Query";
            var _queryHash = sp.GetRequiredService<NinWebViewData>().ApiDictWrapper.Dict;
            var jobConfigLite = jobConfig.Adapt<JobConfigLite>();
            jobConfigLite.CorrectUserInfoLang();

            var detailRes = await _mediator.Send(new ReqDoGraphQL()
            {
                AuthContext = jobConfigLite.NinAuthContext,
                QueryHash = _queryHash[vsDetailDistoryQueryName],
                VarName = "vsResultId",
                VarValue = task.BattleIdRawStr
            });
            var battleBody = StatHelper.BuildStatInkBattleBody(
            detailRes,
                task.BattleGroupRawStr,
                jobConfigLite.NinAuthContext.UserInfo.Lang, gearsInfo);

        }

        private async Task<JobConfig> GetJobConfigAsync(IStorageAccessSvc storage, string jobConfigId)
        {
            var jobConfigTable = await storage.GetTableClientAsync<JobConfig>();
            var resp = await jobConfigTable.GetEntityIfExistsAsync<JobConfig>(nameof(JobConfig), jobConfigId);
            if (!resp.HasValue) throw new Exception("Cannot FindJobConfig");
            return resp.Value;
        }
    }
}
