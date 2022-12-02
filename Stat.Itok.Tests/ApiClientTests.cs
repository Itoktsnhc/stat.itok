using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stat.Itok.Core;
using Stat.Itok.Core.ApiClients;
using System.Net;

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
                            { "PhotoAlbumRefetchQuery","123"}
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
        public async Task TestGetWebViewVerion()
        {
            var api = _sp.GetRequiredService<INintendoApiForTest>();
            var ver = await api.GetWebViewVersionAsync();
            Assert.IsNotNull(ver);
        }

        [TestMethod]
        public async Task TestGetTokenPasteUrl()
        {
            var api = _sp.GetRequiredService<INintendoApiForTest>();
            var authCode = StatHelper.BuildRandomSizedBased64Str(32);
            var authCodeVerifier = StatHelper.BuildRandomSizedBased64Str(64);
            var verifyUrl = await api.GetTokenCopyUrlAsync(authCode, authCodeVerifier);
            Assert.IsNotNull(verifyUrl);
        }
    }
}