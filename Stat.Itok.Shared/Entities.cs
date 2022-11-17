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
    public record JobRunHistoryLite
    {
        public string JobConfigId { get; set; }
        public DateTimeOffset? StartAt { get; set; }
        public DateTimeOffset? EndAt { get; set; }
        public TaskStatus? Status { get; set; }
        public PreCheckResult PreCheckResult { get; set; }
        public string Info { get; set; }
        public Dictionary<string, string> BattleIdDict { get; set; }
    }

    public record JobConfigLite
    {
        public string Id { get; set; }
        public NinAuthContext NinAuthContext { get; set; } = new NinAuthContext();
        public bool Enabled { get; set; } = true;
        public DateTimeOffset? LastUpdateTime { get; set; } = DateTimeOffset.Now;
        public IList<string> EnabledQueries { get; set; } = new List<string>();
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
}
