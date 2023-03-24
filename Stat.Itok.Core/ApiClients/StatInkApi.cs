using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stat.Itok.Core.ApiClients
{
    public interface IStatInkApi
    {
        Task<IList<string>> GetUuidListAsync(string apiKey);
        Task<HttpResponseMessage> PostBattlesAsync(string apiKey, StatInkBattleBody battle);
        Task<HttpResponseMessage> PostSalmonAsync(string apiKey, StatInkSalmonBody salmon);
        Task<HttpResponseMessage> GetGearKeyDictAsync();
        Task<HttpResponseMessage> GetSalmonWeaponKeyDictAsync();
        Task DeleteBattleAsync(string apiKey, string battleId);
    }

    public interface IStatInkApiForTest : IStatInkApi
    {
    }

    public class StatInkApi : IStatInkApiForTest
    {
        private readonly HttpClient _client;
        private readonly IOptions<GlobalConfig> _options;

        public StatInkApi(HttpClient client, IOptions<GlobalConfig> options)
        {
            _client = client;
            _options = options;
        }

        public async Task<IList<string>> GetUuidListAsync(string apiKey)
        {
            var req = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(_options.Value.StatInkUUIDListApi)
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            var rawResp = await _client.SendAsync(req);
            rawResp.EnsureSuccessStatusCode();
            var resp = await rawResp.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(resp))
                return JsonConvert.DeserializeObject<IList<string>>(resp);
            return new List<string>();
        }

        public async Task<HttpResponseMessage> GetGearKeyDictAsync()
        {
            var rawResp = await _client.GetAsync(_options.Value.StatInkFullGearApi);
            return rawResp;
        }

        //https://github.com/fetus-hina/stat.ink/wiki/Spl3-API:-Delete-v3-battle#request
        public async Task DeleteBattleAsync(string apiKey, string battleId)
        {
            var req = new HttpRequestMessage()
            {
                RequestUri = new Uri($"{_options.Value.StatInkBattleApi}/{battleId}"),
                Method = HttpMethod.Delete
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            var rawResp = await _client.SendAsync(req);
            rawResp.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// https://github.com/fetus-hina/stat.ink/wiki/Spl3-API:-Post-v3-battle
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="battle"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> PostBattlesAsync(string apiKey, StatInkBattleBody battle)
        {
            var req = new HttpRequestMessage()
            {
                RequestUri = new Uri(_options.Value.StatInkBattleApi),
                Method = HttpMethod.Post
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            req.Content = new StringContent(JsonConvert.SerializeObject(battle), Encoding.UTF8, "application/json");
            var rawResp = await _client.SendAsync(req);
            return rawResp;
        }

        /// <summary>
        /// https://github.com/fetus-hina/stat.ink/wiki/Spl3-API:-Salmon-%EF%BC%8D-Post
        /// </summary>
        public async Task<HttpResponseMessage> PostSalmonAsync(string apiKey, StatInkSalmonBody salmon)
        {
            var req = new HttpRequestMessage()
            {
                RequestUri = new Uri(_options.Value.StatInkSalmonApi),
                Method = HttpMethod.Post
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            req.Content = new StringContent(JsonConvert.SerializeObject(salmon), Encoding.UTF8, "application/json");
            var rawResp = await _client.SendAsync(req);
            return rawResp;
        }

        public async Task<HttpResponseMessage> GetSalmonWeaponKeyDictAsync()
        {
            var rawResp = await _client.GetAsync(_options.Value.StatInkSalmonFullWeaponApi);
            return rawResp;
        }
    }
}