using Newtonsoft.Json;

namespace Stat.Itok.Core
{
    public class GlobalConfig
    {
        public EmailConfig EmailConfig { get; set; }
        public string StorageAccountConnStr { get; set; }
        public string CosmosDbConnStr { get; set; }
        public string CosmosDbPkPrefix { get; set; } = "dev";
        public string CosmosContainerName { get; set; } = "container";
        public string CosmosDbName { get; set; } = "store";

        public string NSOAppStoreLink { get; set; } =
            "https://apps.apple.com/us/app/nintendo-switch-online/id1234806557";

        public string SplatNet3Url { get; set; } = "https://api.lp1.av5ja.srv.nintendo.net";
        public string GraphQLUrl => $"{SplatNet3Url}/api/graphql";
        public string NintendoAuthorizeUrl { get; set; } = "https://accounts.nintendo.com/connect/1.0.0/authorize";

        public string NintendoSessionTokenUrl { get; set; } =
            "https://accounts.nintendo.com/connect/1.0.0/api/session_token";

        public string NintendoAccessTokenUrl { get; set; } = "https://accounts.nintendo.com/connect/1.0.0/api/token";
        public string NintendoUserInfoUrl { get; set; } = "https://api.accounts.nintendo.com/2.0.0/users/me";
        public string NintendoAccountLoginUrl { get; set; } = "https://api-lp1.znc.srv.nintendo.net/v3/Account/Login";

        public string NintendoWebServiceTokenUrl { get; set; } =
            "https://api-lp1.znc.srv.nintendo.net/v2/Game/GetWebServiceToken";

        public string IMinkFCalcApi { get; set; } = "https://api.imink.app/f";
        public string StatInkBattleApi { get; set; } = "https://stat.ink/api/v3/battle";
        public string StatInkSalmonApi { get; set; } = "https://stat.ink/api/v3/salmon";
        public string StatInkUUIDListApi { get; set; } = "https://stat.ink/api/v3/s3s/uuid-list";
        public string StatInkFullGearApi { get; set; } = "https://stat.ink/api/v3/ability?full=1";
        public string StatInkSalmonFullWeaponApi { get; set; } = "https://stat.ink/api/v3/salmon/weapon?full=1";
        public FallbackConfig FallbackConfig { get; set; }
        public string JobSysBase { get; set; } = "https://tasks.itok.xyz";
        public int MaxNeedBuildFromBeginCount { get; set; } = 12 * 24;
    }

    public class FallbackConfig
    {
        public string WebViewVersion { get; set; } = "1.0.0-5644e7a2";
        public string NSOAppVersion { get; set; } = "2.3.1";
    }

    public static class StatItokConstants
    {
        public const string StatVersion = "0.3.2";
        public const string JobRunTaskQueueName = "job-run-task";
        public const string QueryContinuationHeaderName = "x-stat-itok-continuation";
    }

    public class NinMiscConfig
    {
        [JsonProperty("graphQL")]
        public NinGraphQL GraphQL { get; set; } = new NinGraphQL();

        [JsonProperty("version")]
        public string WebViewVersion { get; set; }

        [JsonProperty("nsoVer")]
        public string NSOAppVersion { get; set; }
    }

    public class NinGraphQL
    {
        [JsonProperty("apis")]
        public Dictionary<string, string> APIs { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Defai
    /// </summary>
    public class EmailConfig
    {
        public string Server { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FromEmail { get; set; } = "stat.itok";
        public string AdminEmail { get; set; }
        public int Port { get; set; } = 465;
    }
}