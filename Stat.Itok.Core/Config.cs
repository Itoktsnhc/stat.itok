namespace Stat.Itok.Core
{
    public class GlobalConfig
    {
        public string SubPrefix { get; set; } = "dev";
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
    }

    public class FallbackConfig
    {
        public string WebViewVersion { get; set; } = "1.0.0-5644e7a2";
        public string NSOAppVersion { get; set; } = "2.3.1";
    }

    public static class StatItokConstants
    {
        public const string StatVersion = "0.1.0";
    }

    public static class QueryHash
    {
        public static Dictionary<string, string> BattleQueries;
        public static Dictionary<string, string> CoopQueries;

        static QueryHash()
        {
            BattleQueries = new()
            {
                {nameof(LatestBattleHistories), LatestBattleHistories},
                {nameof(RegularBattleHistories), RegularBattleHistories},
                {nameof(BankaraBattleHistories), BankaraBattleHistories},
                {nameof(PrivateBattleHistories), PrivateBattleHistories},
            };
            CoopQueries = new()
            {
                {nameof(CoopResult), CoopResult}
            };
        }

        public const string HomeQuery = "dba47124d5ec3090c97ba17db5d2f4b3";

        public const string LatestBattleHistories = "7d8b560e31617e981cf7c8aa1ca13a00";
        public const string RegularBattleHistories = "f6e7e0277e03ff14edfef3b41f70cd33";
        public const string BankaraBattleHistories = "c1553ac75de0a3ea497cdbafaa93e95b";
        public const string PrivateBattleHistories = "38e0529de8bc77189504d26c7a14e0b8";
        public const string VsHistoryDetail = "2b085984f729cd51938fc069ceef784a";

        public const string CoopResult = "817618ce39bcf5570f52a97d73301b30";
        public const string CoopHistoryDetail = "f3799a033f0a7ad4b1b396f9a3bafb1e";
    }
}