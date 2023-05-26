using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stat.Itok.Core;
using Stat.Itok.Core.ApiClients;
using System.Net;

namespace Stat.Itok.Tests
{
    [TestClass]
    public class ImInkApiTests
    {
        private readonly IServiceProvider _sp;

        public ImInkApiTests()
        {
            var svc = new ServiceCollection()
                .AddSingleton(_ => Options.Create(new GlobalConfig()))
                .AddHttpClient()
                .AddMemoryCache()
                .AddLogging();
            svc.AddHttpClient<IImInkApi, ImInkApi>()
                .ConfigurePrimaryHttpMessageHandler(x =>
                    new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    });
            _sp = svc.BuildServiceProvider();
        }

        [TestMethod]
        public async Task TestCallFCalcApi()
        {
            var api = _sp.GetRequiredService<IImInkApi>();
            var resp = await api.CallFCalcApiAsync(
                "eyJhbGciOiJSUzI1NiIsImprdSI6Imh0dHBzOi8vYWNjb3VudHMubmludGVuZG8uY29tLzEuMC4wL2NlcnRpZmljYXRlcyIsImtpZCI6Ijg3Yjk5ZDVkLWVlZTUtNDcyMC1iZTRiLWU5MGU5NTgxOGU0MiJ9.eyJhdF9oYXNoIjoiUDkwUU1oNFI1M3hKYzBwM2pXdTFhZyIsImlhdCI6MTY2Nzg4Nzg1OSwiZXhwIjoxNjY3ODg4NzU5LCJpc3MiOiJodHRwczovL2FjY291bnRzLm5pbnRlbmRvLmNvbSIsImp0aSI6IjdhZTI3MDBmLTliNGUtNGI1MS1iNDFmLWUxZDQzNmM5M2M4ZSIsInN1YiI6IjFjZWZhMDY3NmQ1YWJhMjciLCJjb3VudHJ5IjoiVVMiLCJhdWQiOiI3MWI5NjNjMWI3YjZkMTE5IiwidHlwIjoiaWRfdG9rZW4ifQ.e3w0VcA5ao8W1Bam9aEsofG8u4OMZlhAgZcHZk14Jj9uP5lRvmxsH4oTqB-GQ7k3f5UMq1HeAo4ODnKh8ALEStN1evfqKjg26Pu-pB0XZTkvabiLv8iaPhxM0OtzFh_BBr5Egl38r50UTk3GkO6xiWHdpojLwvNKr6nvneGlTcJwsrkJQW6r6izg20ZIt-gXDQrDqiePa29Q7Wd2gFESVdLt5HwOCOrLLKxodL6mywwalWycKUj1XnEXgQ4JtBjGhbxLEnSvUYwmfCVZP5FXwWydnQs5Uj-Mks2i8xtCRzvGcja1L78nQ4xd9oH4iNhNDdhJA3En_DL-op97zWikfg"
                , 1, "itoktsnhc");
            Assert.IsNotNull(resp);
            Assert.IsNotNull(resp.Timestamp);
            Assert.IsNotNull(resp.RequestId);
            Assert.IsNotNull(resp.F);
        }
    }
}