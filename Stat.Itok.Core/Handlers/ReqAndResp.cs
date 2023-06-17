using MediatR;

namespace Stat.Itok.Core.Handlers;

public record ReqPreCheck : IRequest<RespPreCheck>
{
    public NinAuthContext AuthContext { get; set; }
}

public record RespPreCheck
{
    public PreCheckResult Result { get; set; }
    public NinAuthContext AuthContext { get; set; }
}

public record ReqGetTokenCopyInfo : IRequest<NinTokenCopyInfo>;

public record ReqGenAuthContext : IRequest<NinAuthContext>
{
    public NinTokenCopyInfo TokenCopyInfo { get; set; }
}

public record ReqReGenAuthContext : IRequest<NinAuthContext>
{
    public NinAuthContext NinAuthContext { get; set; }
}

public record ReqGetSessionToken : IRequest<string>
{
    public NinTokenCopyInfo TokenCopyInfo { get; set; }
}

public record ReqGetAccessToken : IRequest<NinAccessTokenInfo>
{
    public string SessionToken { get; set; }
}

public record ReqGetUserInfo : IRequest<NinUserInfo>
{
    public NinAccessTokenInfo AccessTokenInfo { get; set; }
}

public record ReqGetPreGameToken : IRequest<RespPerGameToken>
{
    public NinAccessTokenInfo AccessTokenInfo { get; set; }
    public NinUserInfo User { get; set; }
}

public record RespPerGameToken
{
    public string PreGameToken { get; set; }
    public string CoralUserId { get; set; }
}

public record ReqGetGameToken : IRequest<string>
{
    public string PreGameToken { get; set; }
    public string CoralUserId { get; set; }
    public NinUserInfo User { get; set; }
}

public record ReqGetBulletGameToken : IRequest<string>
{
    public string GameToken { get; set; }
    public NinUserInfo User { get; set; }
}

// ReSharper disable once InconsistentNaming
public record ReqDoGraphQL : IRequest<string>
{
    public NinAuthContext AuthContext { get; set; }
    public string QueryHash { get; set; }
    public string VarName { get; set; }
    public string VarValue { get; set; }
}

public record ReqGetNinMiscConfig : IRequest<NinMiscConfig>
{
}

public record ReqPostBattle : IRequest<StatInkPostBodySuccess>
{
    public string ApiKey { get; set; }
    public StatInkBattleBody Body { get; set; }
}

public record ReqPostSalmon : IRequest<StatInkPostBodySuccess>
{
    public string ApiKey { get; set; }
    public StatInkSalmonBody Body { get; set; }
}

public record ReqTestStatApiKey : IRequest<ApiResp<string>>
{
    public string ApiKey { get; set; }
}

public record ReqGetGearsInfo : IRequest<Dictionary<string, string>>;

public record ReqGetSalmonWeaponsInfo : IRequest<Dictionary<string, string>>;