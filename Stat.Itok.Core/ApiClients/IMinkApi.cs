using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Net.Http.Json;

namespace Stat.Itok.Core.ApiClients
{
    public interface IImInkApi
    {
        Task<IMinkFCalcApiResp> CallFCalcApiAsync(string idToken, int step, string userId,
            string coralUserId = null);
    }

    public class ImInkApi : IImInkApi
    {
        private readonly HttpClient _client;
        private readonly IOptions<GlobalConfig> _options;

        public ImInkApi(HttpClient client, IOptions<GlobalConfig> options)
        {
            _client = client;
            _options = options;
        }

        public async Task<IMinkFCalcApiResp> CallFCalcApiAsync(string idToken, int step, string userId,
            string coralUserId = null)
        {
            var req = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_options.Value.IMinkFCalcApi)
            };

            req.Headers.TryAddWithoutValidation("User-Agent", $"stat.itok/{StatItokConstants.StatVersion}");
            var bodyDict = new Dictionary<string, string>()
            {
                {"token", idToken},
                {"hash_method", step.ToString()},
                {"na_id", userId},
            };
            if (step == 2 && !string.IsNullOrWhiteSpace(coralUserId))
            {
                bodyDict["coral_user_id"] = coralUserId;
            }

            req.Content = JsonContent.Create(bodyDict);
            var rawResp = await _client.SendAsync(req);
            rawResp.EnsureSuccessStatusCode();
            var resp = await rawResp.Content.ReadAsStringAsync();
            var respJToken = JToken.Parse(resp);
            return new IMinkFCalcApiResp
            {
                F = respJToken["f"].Value<string>(),
                RequestId = respJToken["request_id"].Value<string>(),
                Timestamp = respJToken["timestamp"].Value<string>(),
            };
        }
    }
}