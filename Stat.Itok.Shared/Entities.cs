using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stat.Itok.Shared
{
    public record NinAccessTokenInfo
    {
        public string AccessToken { get; set; }
        public string IdToken { get; set; }
    }

    public record NinUserInfo
    {
        public string Nickname { get; set; }
        public string Id { get; set; }
        public string Lang { get; set; }
        public string Country { get; set; }
        public string Birthday { get; set; }
    }

    public record NinTokenCopyInfo
    {
        public string CopyRedirectionUrl { get; set; }
        public string AuthCodeVerifier { get; set; }
        public string RedirectUrl { get; set; }
    }

    public record NinAuthContext
    {
        public NinTokenCopyInfo TokenCopyInfo { get; set; } = new NinTokenCopyInfo();
        public string SessionToken { get; set; }
        public string GameToken { get; set; }
        public string BulletToken { get; set; }
        public NinUserInfo UserInfo { get; set; } = new NinUserInfo();
        public NinAccessTokenInfo AccessTokenInfo { get; set; } = new NinAccessTokenInfo();
        public string PerGameToken { get; set; }
    }

    public enum PreCheckResult
    {
        Ok = 1,
        AutoRefreshed = 2,
        NeedBuildFromBegin = 3,
    }

    public record JobConfigLite
    {
        public string Id { get; set; }
        public NinAuthContext NinAuthContext { get; set; } = new NinAuthContext();
        public bool Enabled { get; set; } = true;
        public DateTimeOffset? LastUpdateTime { get; set; } = DateTimeOffset.Now;
        public List<string> EnabledQueries { get; set; } = new List<string>();
        public string ForcedUserLang { get; set; }
        public string StatInkApiKey { get; set; }
        public bool ForceOverride { get; set; } = false;
    }

    public class ApiResp
    {
        public static ApiResp<TData> OkWith<TData>(TData data)
        {
            return ApiResp<TData>.OkWith(data);
        }

        public static ApiResp<TData> Error<TData>(string errorMsg = "failed")
        {
            return ApiResp<TData>.Error(errorMsg);
        }
    }

    public class ApiResp<TData> : ApiResp
    {
        public ApiResp()
        {
        }

        public ApiResp(TData data, bool result = true, string msg = "success")
        {
            Data = data;
            Result = result;
            Msg = msg;
        }

        public string Msg { get; set; }
        public bool Result { get; set; }
        public TData Data { get; set; }

        public static ApiResp<TData> OkWith(TData data)
        {
            return new ApiResp<TData>(data);
        }

        public static ApiResp<TData> Error(string errorMsg = "failed")
        {
            return new ApiResp<TData>(default, false, errorMsg);
        }
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