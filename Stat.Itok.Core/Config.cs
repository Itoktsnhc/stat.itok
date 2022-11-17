namespace Stat.Itok.Core
{
    public class GlobalConfig
    {
        public string StorageAccountConnStr { get; set; }

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
        public string StatInkUUIDListApi { get; set; } = "https://stat.ink/api/v3/s3s/uuid-list";
        public string StatInkFullGearApi { get; set; } = "https://stat.ink/api/v3/ability?full=1";
        public FallbackConfig FallbackConfig { get; set; }
        public string JobSysBase { get; set; } = "http://jobtracker.itok.xyz";
    }

    public class FallbackConfig
    {
        public string WebViewVersion { get; set; } = "1.0.0-5644e7a2";
        public string NSOAppVersion { get; set; } = "2.3.1";
    }

    public static class StatItokConstants
    {
        public const string StatVersion = "0.1.0";
        public const string JobRunTaskQueueName = "job-run-task";
    }
}