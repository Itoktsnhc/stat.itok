using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stat.Itok.Core;
using Stat.Itok.Core.ApiClients;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stat.Itok.Core.Helpers;

namespace Stat.Itok.Tests
{
    [TestClass]
    public class StatInkApiTests
    {
        private readonly IServiceProvider _sp;

        public StatInkApiTests()
        {
            var svc = new ServiceCollection()
                .AddSingleton(_ => Options.Create(new GlobalConfig()))
                .AddHttpClient()
                .AddMemoryCache()
                .AddLogging();
            svc.AddHttpClient<IStatInkApiForTest, StatInkApi>()
                .ConfigurePrimaryHttpMessageHandler(x =>
                    new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    });
            _sp = svc.BuildServiceProvider();
        }

        [TestMethod]
        public async Task ReUploadBattleAsync()
        {
            var config = JsonConvert.DeserializeObject<JobConfig>(File.ReadAllText("./configs/user_auth_cfg.json"));
            var groupRaw = File.ReadAllText("./samples/salmon/3/list.json");
            var detailRaw = File.ReadAllText("./samples/salmon/3/detail_0.json");
            var weapon = JArray.Parse(File.ReadAllText("./configs/salmon_weapon.json")).SelectMany(x =>
            {
                var res = new List<(string, string)>();
                var key = x["key"].Value<string>();
                var children = x["name"].Children();
                foreach (var child in children)
                {
                    var childProp = child as JProperty;
                    res.Add(($"[{childProp!.Name}]{childProp.Value}", key));
                }

                return res;
            }).GroupBy(x => x.Item1).ToDictionary(x => x.Key, y => y.First().Item2);

            var body = BattleHelper.BuildStatInkSalmonBody(detailRaw, groupRaw, "zh-CN", weapon);
            var statInkApi = _sp.GetRequiredService<IStatInkApiForTest>();
            var resp = await statInkApi.PostSalmonAsync(config.StatInkApiKey, body);
            var respContent = await resp.Content.ReadAsStringAsync();
        }
    }
}