using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stat.Itok.Core;
using Stat.Itok.Core.ApiClients;
using System.Net;
using Newtonsoft.Json;
using Stat.Itok.Core.Helpers;

namespace Stat.Itok.Tests
{
    [TestClass]
    public class NintendoApiClientTests
    {
        private readonly IServiceProvider _sp;

        public NintendoApiClientTests()
        {
            var svc = new ServiceCollection()
                .AddSingleton(_ => Options.Create(new GlobalConfig()))
                .AddHttpClient()
                .AddMemoryCache()
                .AddSingleton(_ => new NinMiscConfig()
                {
                    NSOAppVersion = "1.2.3",
                    WebViewVersion = "1.0",
                    GraphQL = new NinGraphQL
                    {
                        APIs = new Dictionary<string, string>
                        {
                            {"PhotoAlbumRefetchQuery", "123"}
                        }
                    }
                })
                .AddLogging();
            svc.AddHttpClient<INintendoApiForTest, NintendoApi>()
                .ConfigurePrimaryHttpMessageHandler(x =>
                    new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    });
            svc.AddHttpClient<IImInkApi, ImInkApi>()
                .ConfigurePrimaryHttpMessageHandler(x =>
                    new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    });
            _sp = svc.BuildServiceProvider();
        }

        [TestMethod]
        public async Task TestGetNSOAppVersion()
        {
            var api = _sp.GetRequiredService<INintendoApiForTest>();
            var ver = await api.GetNSOAppVersionAsync();
            Assert.IsNotNull(ver);
        }

        [TestMethod]
        public async Task TestGetWebViewVersion()
        {
            var api = _sp.GetRequiredService<INintendoApiForTest>();
            var ver = await api.GetWebViewVersionAsync();
            Assert.IsNotNull(ver);
        }

        [TestMethod]
        public async Task TestGetTokenPasteUrl()
        {
            var api = _sp.GetRequiredService<INintendoApiForTest>();
            var authCode = StatInkHelper.BuildRandomSizedBased64Str(32);
            var authCodeVerifier = StatInkHelper.BuildRandomSizedBased64Str(64);
            var verifyUrl = await api.GetTokenCopyUrlAsync(authCode, authCodeVerifier);
            Assert.IsNotNull(verifyUrl);
        }

        [TestMethod]
        public async Task TestSalmonQueryAsync()
        {
            var api = _sp.GetRequiredService<INintendoApiForTest>();
            var authConfig = JsonConvert.DeserializeObject<JobConfig>(File.ReadAllText("./configs/user_auth_cfg.json"));
            var authContext = authConfig.NinAuthContext;
            var resp = await api.SendGraphQLRequestAsync(authContext.GameToken, authContext.BulletToken,
                authContext.UserInfo, "379f0d9b78b531be53044bcac031b34b", "coopHistoryDetailId","Q29vcEhpc3RvcnlEZXRhaWwtdS1xcTJlajJ3ZG9rM3k1d2V1N25tbToyMDIzMDMyM1QxNDMwMTZfYmExYzYyMzUtNTRkZC00MWJmLWJhZWQtOTExODI4OWI0M2I1");
            var content = await resp.Content.ReadAsStringAsync();
        }
    }
}