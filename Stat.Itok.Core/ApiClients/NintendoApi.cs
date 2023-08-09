using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using Mapster;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSmart.Utils;
using Stat.Itok.Shared;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Stat.Itok.Core.Helpers;

namespace Stat.Itok.Core.ApiClients
{
    public interface INintendoApi
    {
        /// <summary>
        /// 1
        /// </summary>
        /// <param name="authState">36-length</param>
        /// <param name="authCodeVerifier">64-length</param>
        /// <returns></returns>
        Task<HttpResponseMessage> GetTokenCopyUrlAsync(string authState, string authCodeVerifier);

        /// <summary>
        /// 2 need manul copy button url after 1 is used
        /// </summary>
        /// <param name="authRedirectUrl"></param>
        /// <param name="authCodeVerifier"></param>
        /// <returns></returns>
        Task<HttpResponseMessage> GetSessionTokenAsync(string authRedirectUrl, string authCodeVerifier);

        /// <summary>
        /// 3
        /// </summary>
        /// <param name="sessionToken"></param>
        /// <returns></returns>
        Task<HttpResponseMessage> GetAccessTokenInfoAsync(string sessionToken);

        /// <summary>
        /// 4
        /// </summary>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        Task<HttpResponseMessage> GetUserInfoAsync(string accessToken);

        /// <summary>
        /// 5
        /// </summary>
        /// <param name="accessIdToken"></param>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        Task<HttpResponseMessage> GetPreGameTokenAsync(string accessIdToken, NinUserInfo userInfo);

        /// <summary>
        /// 6, also known as gToken or web_svc_token
        /// </summary>
        /// <param name="stepOneIdToken"></param>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        Task<HttpResponseMessage> GetGameTokenAsync(string stepOneIdToken, NinUserInfo userInfo, string coralUserId);

        /// <summary>
        /// 7, also known as bulletToken
        /// </summary>
        /// <param name="stepTwoIdToken"></param>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        Task<HttpResponseMessage> GetBulletTokenAsync(string stepTwoIdToken, NinUserInfo userInfo);

        // ReSharper disable once InconsistentNaming
        Task<HttpResponseMessage> SendGraphQLRequestAsync(string gToken, string bulletToken, NinUserInfo user,
            string queryHash, string varName = null, string varValue = null);

        Task<NinMiscConfig> GetNinMiscConfigAsync(bool unCached = false);
    }

    public interface INintendoApiForTest : INintendoApi
    {
        Task<string> GetWebViewVersionAsync();

        // ReSharper disable once InconsistentNaming
        Task<string> GetNSOAppVersionAsync(bool forceRefresh = false);
    }

    public class NintendoApi : INintendoApiForTest
    {
        private readonly HttpClient _client;
        private readonly IOptions<GlobalConfig> _options;
        private readonly IMemoryCache _memCache;
        private readonly IImInkApi _inkApi;
        private readonly NinMiscConfig _defaultConfig;
        private readonly ILogger<NintendoApi> _logger;

        public NintendoApi(HttpClient client,
            IOptions<GlobalConfig> options,
            IMemoryCache memCache,
            IImInkApi inkApi,
            NinMiscConfig defaultWebViewData,
            ILogger<NintendoApi> logger
        )
        {
            _client = client;
            _options = options;
            _memCache = memCache;
            _inkApi = inkApi;
            _defaultConfig = defaultWebViewData;
            _logger = logger;
        }


        public async Task<string> GetNSOAppVersionAsync(bool forceRefresh = false)
        {
            var miscConfig = await GetNinMiscConfigAsync();
            return miscConfig.NSOAppVersion;
        }

        public async Task<NinMiscConfig> GetNinMiscConfigAsync(bool unCached = false)
        {
            if (!unCached &&
                _memCache.TryGetValue<NinMiscConfig>(nameof(GetNinMiscConfigAsync), out var cachedWebViewData))
            {
                return cachedWebViewData;
            }

            var combinedNinMiscConfig = _defaultConfig.Adapt<NinMiscConfig>();
            try
            {
                var liveData = new NinMiscConfig();
                try
                {
                    var jsUrl = await GetMainJsUrlPathAsync();
                    var jsContent = await GetWebViewRawContentAsync(jsUrl);
                    liveData = StatInkHelper.ParseNinWebViewData(jsContent);

                    var html = await _client.GetStringAsync(_options.Value.NSOAppStoreLink);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    var node = doc.DocumentNode.QuerySelector("p.whats-new__latest__version");
                    var str = node?.GetDirectInnerText();
                    liveData.NSOAppVersion = str?.Replace("Version", "").Trim();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"error when {nameof(GetNinMiscConfigAsync)}", ex);
                }

                if (!string.IsNullOrWhiteSpace(liveData.WebViewVersion))
                    combinedNinMiscConfig.WebViewVersion = liveData.WebViewVersion;
                if (!string.IsNullOrWhiteSpace(liveData.NSOAppVersion))
                    combinedNinMiscConfig.NSOAppVersion = liveData.NSOAppVersion;
                foreach (var (key, val) in liveData.GraphQL.APIs)
                {
                    combinedNinMiscConfig.GraphQL.APIs[key] = val;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("error when fetch or combine NinMiscConfig", ex);
            }

            _memCache.Set(nameof(GetNinMiscConfigAsync), combinedNinMiscConfig, TimeSpan.FromMinutes(5));

            return combinedNinMiscConfig;
        }

        public async Task<string> GetWebViewVersionAsync()
        {
            var webViewData = await GetNinMiscConfigAsync();
            return webViewData.WebViewVersion;
        }

        // ReSharper disable once UnusedMember.Local
        private async Task<string> GetWebViewRawContentAsync(string jsUrl)
        {
            var req = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(jsUrl)
            };
            var appHeader = new Dictionary<string, string>()
            {
                {"Accept", "*/*"},
                {"X-Requested-With", "com.nintendo.znca"},
                {"Sec-Fetch-Site", "none"},
                {"Sec-Fetch-Mode", "navigate"},
                {"Sec-Fetch-User", "?1"},
                {"Sec-Fetch-Dest", "document"},
                {"Referer", _options.Value.SplatNet3Url}
            };

            foreach (var (k, v) in appHeader)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }

            var cookieList = new List<string>() {"_dht=1"};
            req.Headers.TryAddWithoutValidation("Cookie", string.Join(';', cookieList));
            var resp = await _client.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var respContent = await resp.Content.ReadAsStringAsync();

            return respContent;
        }

        // ReSharper disable once UnusedMember.Local
        public async Task<string> GetMainJsUrlPathAsync()
        {
            var req = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(_options.Value.SplatNet3Url)
            };
            var appHeader = new Dictionary<string, string>()
            {
                {"Accept", "*/*"},
                {"Upgrade-Insecure-Requests", "1"},
                {"DNT", "1"},
                {"X-AppColorScheme", "DARK"},
                {"X-Requested-With", "com.nintendo.znca"},
                {"Sec-Fetch-Site", "none"},
                {"Sec-Fetch-Mode", "navigate"},
                {"Sec-Fetch-User", "?1"},
                {"Sec-Fetch-Dest", "document"},
            };

            foreach (var (k, v) in appHeader)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }

            var cookieList = new List<string>() {"_dht=1"};
            req.Headers.TryAddWithoutValidation("Cookie", string.Join(';', cookieList));
            var resp = await _client.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var respContent = await resp.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(respContent);
            var scriptNode = doc.DocumentNode.QuerySelector("script[src*='static']");
            if (scriptNode?.Attributes.Any(x => x.Name == "src") != true) return null;
            var jsPath = scriptNode.Attributes["src"]?.Value;
            if (string.IsNullOrEmpty(jsPath)) return null;

            return _options.Value.SplatNet3Url + jsPath;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="authState">36-length</param>
        /// <param name="authCodeVerifier">64-length</param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> GetTokenCopyUrlAsync(string authState, string authCodeVerifier)
        {
            using var sha256Hash = SHA256.Create();
            var authCodeCodeChallenge =
                UrlBase64.Encode(sha256Hash.ComputeHash(Encoding.ASCII.GetBytes(authCodeVerifier.Replace("=", ""))));
            var appHeader = new Dictionary<string, string>()
            {
                {"Host", "accounts.nintendo.com"},
                {"Connection", "keep-alive"},
                {"Cache-Control", "max-age=0"},
                {"Upgrade-Insecure-Requests", "1"},
                {
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/107.0.0.0 Safari/537.36 Edg/107.0.1418.35"
                },
                {"Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8n"},
                {"DNT", "1"},
                {"Accept-Encoding", "gzip,deflate,br"},
            };

            var paramDict = new Dictionary<string, string>
            {
                {"state", authState},
                {"redirect_uri", "npf71b963c1b7b6d119://auth"},
                {"client_id", "71b963c1b7b6d119"},
                {"scope", "openid user user.birthday user.mii user.screenName"},
                {"response_type", "session_token_code"},
                {"session_token_code_challenge", authCodeCodeChallenge.Replace("=", "")},
                {"session_token_code_challenge_method", "S256"},
                {"theme", "login_form"},
            };
            var req = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(
                    $"{_options.Value.NintendoAuthorizeUrl}?" +
                    $"{string.Join('&', paramDict.Select(x => $"{x.Key}={x.Value}"))}")
            };
            foreach (var (k, v) in appHeader)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }

            var rawResp = await _client.SendAsync(req);
            return rawResp;
        }

        public async Task<HttpResponseMessage> GetSessionTokenAsync(string authRedirectUrl, string authCodeVerifier)
        {
            var matchRes = Regex.Match(authRedirectUrl, "code=(.*)&");
            if (!matchRes.Success || matchRes.Groups.Count < 2) throw new Exception("no session code Match");
            var sessionTokenCode = matchRes.Groups[1].Value;
            var req = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_options.Value.NintendoSessionTokenUrl)
            };
            var nsoAppVer = await GetNSOAppVersionAsync();
            var appHeader = new Dictionary<string, string>()
            {
                {"User-Agent", $"OnlineLounge/{nsoAppVer} NASDKAPI Android"},
                {"Accept-Language", "en-US"},
                {"Accept", "application/json"},
                {"Host", "accounts.nintendo.com"},
                {"Connection", "Keep-Alive"},
                {"Accept-Encoding", "gzip"},
            };

            foreach (var (k, v) in appHeader)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }

            var bodyDict = new Dictionary<string, string>()
            {
                {"client_id", "71b963c1b7b6d119"},
                {"session_token_code", sessionTokenCode},
                {"session_token_code_verifier", authCodeVerifier.Replace("=", "")},
            };
            req.Content = new FormUrlEncodedContent(bodyDict);
            var rawResp = await _client.SendAsync(req);
            return rawResp;
        }

        public async Task<HttpResponseMessage> GetAccessTokenInfoAsync(string sessionToken)
        {
            var appHeader = new Dictionary<string, string>()
            {
                {"Host", "accounts.nintendo.com"},
                {"Accept-Encoding", "gzip"},
                {"User-Agent", $"Dalvik/2.1.0 (Linux; U; Android 7.1.2)"},
                {"Accept", "application/json"},
                {"Connection", "Keep-Alive"},
            };
            var req = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_options.Value.NintendoAccessTokenUrl)
            };
            foreach (var (k, v) in appHeader)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }

            var bodyDict = new Dictionary<string, string>()
            {
                {"client_id", "71b963c1b7b6d119"},
                {"session_token", sessionToken},
                {"grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer-session-token"},
            };
            req.Content = JsonContent.Create(bodyDict);
            var rawResp = await _client.SendAsync(req);
            return rawResp;
            // rawResp.EnsureSuccessStatusCode();
            // var respContent = await rawResp.Content.ReadAsStringAsync();
            // var idRespJToken = JToken.Parse(respContent);
            // return new NinAccessTokenInfo
            // {
            //     AccessToken = idRespJToken["access_token"].Value<string>(),
            //     IdToken = idRespJToken["id_token"].Value<string>()
            // };
        }

        public async Task<HttpResponseMessage> GetUserInfoAsync(string accessToken)
        {
            var appHeader = new Dictionary<string, string>()
            {
                {"User-Agent", $"NASDKAPI; Android"},
                {"Accept", "application/json"},
                {"Authorization", $"Bearer {accessToken}"},
                {"Host", "api.accounts.nintendo.com"},
                {"Connection", "Keep-Alive"},
                {"Accept-Encoding", "gzip"},
            };
            var req = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(_options.Value.NintendoUserInfoUrl)
            };
            foreach (var (k, v) in appHeader)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }

            var rawResp = await _client.SendAsync(req);
            return rawResp;
        }


        public async Task<HttpResponseMessage> GetPreGameTokenAsync(string accessIdToken, NinUserInfo userInfo)
        {
            var inkApiResp = await _inkApi.CallFCalcApiAsync(accessIdToken, 1, userInfo.Id);
            var req = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_options.Value.NintendoAccountLoginUrl)
            };
            var bodyParamDict = new Dictionary<string, string>()
            {
                {"f", inkApiResp.F},
                {"language", inkApiResp.F},
                {"naBirthday", userInfo.Birthday},
                {"naCountry", userInfo.Country},
                {"naIdToken", accessIdToken},
                {"requestId", inkApiResp.RequestId},
                {"timestamp", inkApiResp.Timestamp},
            };
            var bodyDict = new Dictionary<string, Dictionary<string, string>>()
            {
                {"parameter", bodyParamDict}
            };
            var nsoAppVer = await GetNSOAppVersionAsync();
            var appHeader = new Dictionary<string, string>()
            {
                {"User-Agent", $"com.nintendo.znca/{nsoAppVer}(Android/7.1.2)"},
                {"Accept-Encoding", "gzip"},
                {"Connection", "Keep-Alive"},
                {"X-ProductVersion", nsoAppVer},
                {"X-Platform", "Android"},
            };

            foreach (var (k, v) in appHeader)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }

            req.Content = JsonContent.Create(bodyDict);

            var rawResp = await _client.SendAsync(req);
            return rawResp;
        }

        public async Task<HttpResponseMessage> GetGameTokenAsync(string stepOneIdToken, NinUserInfo userInfo,
            string coralUserId)
        {
            var inkApiResp = await _inkApi.CallFCalcApiAsync(stepOneIdToken, 2, userInfo.Id, coralUserId);
            var req = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_options.Value.NintendoWebServiceTokenUrl)
            };
            var bodyParamDict = new Dictionary<string, object>()
            {
                {"f", inkApiResp.F},
                {"id", 4834290508791808},
                {"registrationToken", stepOneIdToken},
                {"requestId", inkApiResp.RequestId},
                {"timestamp", inkApiResp.Timestamp},
            };
            var bodyDict = new Dictionary<string, Dictionary<string, object>>()
            {
                {"parameter", bodyParamDict}
            };
            var nsoAppVer = await GetNSOAppVersionAsync();
            var appHeader = new Dictionary<string, string>()
            {
                {"User-Agent", $"com.nintendo.znca/{nsoAppVer}(Android/7.1.2)"},
                {"Accept-Encoding", "gzip"},
                {"Connection", "Keep-Alive"},
                {"Authorization", $"Bearer {stepOneIdToken}"},
                {"X-ProductVersion", nsoAppVer},
                {"X-Platform", "Android"},
            };

            foreach (var (k, v) in appHeader)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }

            req.Content = JsonContent.Create(bodyDict);

            var rawResp = await _client.SendAsync(req);
            return rawResp;
        }

        public async Task<HttpResponseMessage> GetBulletTokenAsync(string stepTwoIdToken, NinUserInfo userInfo)
        {
            var req = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"{_options.Value.SplatNet3Url}/api/bullet_tokens")
            };

            var appHeader = new Dictionary<string, string>()
            {
                {"Accept-Language", userInfo.Lang},
                {
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/107.0.0.0 Safari/537.36 Edg/107.0.1418.35"
                },
                {"X-Web-View-Ver", await GetWebViewVersionAsync()},
                {"Accept", "*/*"},
                {"Origin", _options.Value.SplatNet3Url},
                {"X-NACOUNTRY", "*/*"},
                {"X-Requested-With", "com.nintendo.znca"},
                {"Content-Type", "application/json"},
            };

            foreach (var (k, v) in appHeader)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }

            var cookieList = new List<string>() {"_dht=1", $"_gtoken={stepTwoIdToken}"};
            req.Headers.TryAddWithoutValidation("Cookie", string.Join(';', cookieList));

            var rawResp = await _client.SendAsync(req);
            return rawResp;
        }


        public async Task<HttpResponseMessage> SendGraphQLRequestAsync(
            string gToken, string bulletToken, NinUserInfo user,
            string queryHash, string varName = null, string varValue = null)
        {
            var req = new HttpRequestMessage()
            {
                RequestUri = new Uri(_options.Value.GraphQLUrl),
                Method = HttpMethod.Post,
            };
            var headers = new Dictionary<string, string>()
            {
                {"Authorization", $"Bearer {bulletToken}"},
                {"Accept-Language", user.Lang},
                {
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/107.0.0.0 Safari/537.36 Edg/107.0.1418.35"
                },
                {"X-Web-View-Ver", await GetWebViewVersionAsync()},
                {"Accept", "*/*"},
                {"Origin", _options.Value.SplatNet3Url},
                {"X-Requested-With", "com.nintendo.znca"},
                {
                    "Referer",
                    $"{_options.Value.SplatNet3Url}?lang={user.Lang}&na_country={user.Country}&na_lang={user.Lang}"
                },
                {"Accept-Encoding", "gzip, deflate"},
            };
            foreach (var (k, v) in headers)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }

            var cookieList = new List<string>() {$"_gtoken={gToken}"};
            req.Headers.TryAddWithoutValidation("Cookie", string.Join(';', cookieList));
            var queryBody = StatInkHelper.BuildGraphQLBody(queryHash, varName, varValue);
            req.Content = new StringContent(queryBody, Encoding.UTF8, "application/json");
            var rawResp = await _client.SendAsync(req);
            return rawResp;
        }
    }
}