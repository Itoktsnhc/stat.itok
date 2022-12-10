using Azure.Storage.Blobs.Specialized;
using Newtonsoft.Json;

namespace Stat.Itok.Core
{
    public class RemoteConfigStore
    {
        private readonly IStorageAccessor _store;

        public RemoteConfigStore(IStorageAccessor store)
        {
            _store = store;
        }

        class RConfig { }
        public async Task<NinMiscConfig> GetNinMiscConfigAsync()
        {
            var container = await _store.GetBlobContainerClientAsync<RConfig>();
            const string fileName = "nin_misc_config.json";
            var blob = container.GetBlockBlobClient(fileName);
            var res = await blob.DownloadContentAsync();
            if (res.GetRawResponse().IsError) throw new Exception($"no found {fileName} in {nameof(RConfig)}");
            var webViewData = JsonConvert.DeserializeObject<NinMiscConfig>(res.Value.Content.ToString());
            return webViewData;
        }

    }

}
