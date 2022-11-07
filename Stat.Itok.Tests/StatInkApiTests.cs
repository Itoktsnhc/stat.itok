using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stat.Itok.Core;
using Stat.Itok.Core.ApiClients;
using System.Net;

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
    }
}
