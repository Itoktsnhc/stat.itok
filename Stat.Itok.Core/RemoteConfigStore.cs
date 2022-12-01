using Azure.Storage.Blobs.Specialized;
using Newtonsoft.Json;

namespace Stat.Itok.Core
{
    public class RemoteConfigStore
    {
        private readonly IStorageAccessSvc _store;

        public RemoteConfigStore(IStorageAccessSvc store)
        {
            _store = store;
        }

        class RConfig { }
        public async Task<NinWebViewData> GetWebViewDataAsync()
        {
            var container = await _store.GetBlobContainerClientAsync<RConfig>();
            const string fileName = "splatnet3_webview_data.json";
            var blob = container.GetBlockBlobClient(fileName);
            var res = await blob.DownloadContentAsync();
            if (res.GetRawResponse().IsError) throw new Exception($"no found {fileName} in {nameof(RConfig)}");
            var webViewData = JsonConvert.DeserializeObject<NinWebViewData>(res.Value.Content.ToString());
            return webViewData;
        }

    }

}
