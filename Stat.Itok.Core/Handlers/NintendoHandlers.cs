using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Stat.Itok.Core.ApiClients;
using Stat.Itok.Core.Helpers;

namespace Stat.Itok.Core.Handlers;

public class NintendoPublicHandlers : HandlerBase,
    IRequestHandler<ReqGetTokenCopyInfo, NinTokenCopyInfo>,
    IRequestHandler<ReqGenAuthContext, NinAuthContext>,
    IRequestHandler<ReqDoGraphQL, string>,
    IRequestHandler<ReqReGenAuthContext, NinAuthContext>,
    IRequestHandler<ReqPreCheck, RespPreCheck>,
    IRequestHandler<ReqGetNinMiscConfig, NinMiscConfig>
{
    private readonly IMediator _mediator;
    private readonly INintendoApi _api;
    private readonly ILogger<NintendoPublicHandlers> _logger;
    private readonly Dictionary<string, string> _queryDict;

    public NintendoPublicHandlers(
        IMediator mediator, INintendoApi api, ILogger<NintendoPublicHandlers> logger,
        NinMiscConfig webViewData)
    {
        _mediator = mediator;
        _api = api;
        _logger = logger;
        _queryDict = webViewData.GraphQL.APIs ?? new Dictionary<string, string>();
    }


    public async Task<NinTokenCopyInfo> Handle(ReqGetTokenCopyInfo request, CancellationToken cancellationToken)
    {
        var authCode = StatInkHelper.BuildRandomSizedBased64Str(32);
        var authCodeVerifier = StatInkHelper.BuildRandomSizedBased64Str(64);
        var strResp = await RunWithDefaultPolicy(_api.GetTokenCopyUrlAsync(authCode, authCodeVerifier), true);
        return new NinTokenCopyInfo()
        {
            CopyRedirectionUrl = strResp,
            AuthCodeVerifier = authCodeVerifier,
        };
    }

    public async Task<NinAuthContext> Handle(ReqGenAuthContext request, CancellationToken cancellationToken)
    {
        var sessionToken = await _mediator.Send(new ReqGetSessionToken()
        {
            TokenCopyInfo = request.TokenCopyInfo
        }, cancellationToken);
        var context = new NinAuthContext()
        {
            SessionToken = sessionToken,
            TokenCopyInfo = request.TokenCopyInfo
        };
        return await _mediator.Send(new ReqReGenAuthContext
        {
            NinAuthContext = context
        }, cancellationToken);
    }

    public async Task<NinAuthContext> Handle(ReqReGenAuthContext request, CancellationToken cancellationToken)
    {
        var accessTokenInfo = await _mediator.Send(new ReqGetAccessToken()
        {
            SessionToken = request.NinAuthContext.SessionToken,
        }, cancellationToken);
        var user = await _mediator.Send(new ReqGetUserInfo()
        {
            AccessTokenInfo = accessTokenInfo
        }, cancellationToken);
        var preGameTokenResp = await _mediator.Send(new ReqGetPreGameToken()
        {
            User = user,
            AccessTokenInfo = accessTokenInfo
        }, cancellationToken);
        var preGameToken = preGameTokenResp.PreGameToken;
        var coralUserId = preGameTokenResp.CoralUserId;
        var gameToken = await _mediator.Send(new ReqGetGameToken()
        {
            User = user,
            PreGameToken = preGameToken,
            CoralUserId = coralUserId
        }, cancellationToken);
        var bulletToken = await _mediator.Send(new ReqGetBulletGameToken()
        {
            User = user,
            GameToken = gameToken
        }, cancellationToken);

        var ctx = new NinAuthContext
        {
            TokenCopyInfo = request.NinAuthContext.TokenCopyInfo,
            SessionToken = request.NinAuthContext.SessionToken,
            AccessTokenInfo = accessTokenInfo,
            UserInfo = user,
            PreGameToken = preGameToken,
            GameToken = gameToken,
            BulletToken = bulletToken
        };
        return ctx;
    }

    public async Task<string> Handle(ReqDoGraphQL request, CancellationToken cancellationToken)
    {
        var strResp = await RunWithDefaultPolicy(_api.SendGraphQLRequestAsync(request.AuthContext.GameToken,
            request.AuthContext.BulletToken, request.AuthContext.UserInfo, request.QueryHash, request.VarName,
            request.VarValue));
        return strResp;
    }

    public async Task<RespPreCheck> Handle(ReqPreCheck request, CancellationToken cancellationToken)
    {
        var checkResp = PreCheckResult.Ok;
        NinAuthContext newAuthContext = request.AuthContext;
        try
        {
            var homeQueryName = nameof(QueryHash.HomeQuery);
            _ = await _mediator.Send(new ReqDoGraphQL()
            {
                AuthContext = request.AuthContext,
                QueryHash = _queryDict[homeQueryName]
            }, cancellationToken);
        }
        catch (Exception)
        {
            try
            {
                newAuthContext = await _mediator.Send(new ReqReGenAuthContext()
                {
                    NinAuthContext = request.AuthContext
                });
                checkResp = PreCheckResult.AutoRefreshed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error When Refresh NinAuthContext");
                checkResp = PreCheckResult.NeedBuildFromBegin;
            }
        }

        return new RespPreCheck
        {
            AuthContext = newAuthContext,
            Result = checkResp
        };
    }

    public async Task<NinMiscConfig> Handle(ReqGetNinMiscConfig request, CancellationToken cancellationToken)
    {
        return await _api.GetNinMiscConfigAsync();
    }
}

public class NintendoPrivateHandlers : HandlerBase,
    IRequestHandler<ReqGetSessionToken, string>,
    IRequestHandler<ReqGetAccessToken, NinAccessTokenInfo>,
    IRequestHandler<ReqGetUserInfo, NinUserInfo>,
    IRequestHandler<ReqGetPreGameToken, RespPerGameToken>,
    IRequestHandler<ReqGetGameToken, string>,
    IRequestHandler<ReqGetBulletGameToken, string>
{
    private readonly INintendoApi _api;

    public NintendoPrivateHandlers(INintendoApi api)
    {
        _api = api;
    }

    public async Task<string> Handle(ReqGetSessionToken request, CancellationToken cancellationToken)
    {
        var strResp = await RunWithDefaultPolicy(_api.GetSessionTokenAsync(request.TokenCopyInfo.RedirectUrl,
            request.TokenCopyInfo.AuthCodeVerifier));
        var jTokenResp = strResp.ThrowIfJsonPropNotFound("session_token");

        return jTokenResp["session_token"]!.Value<string>();
    }

    public async Task<NinAccessTokenInfo> Handle(ReqGetAccessToken request, CancellationToken cancellationToken)
    {
        var strResp = await RunWithDefaultPolicy(_api.GetAccessTokenInfoAsync(request.SessionToken));
        var jTokenResp = strResp.ThrowIfJsonPropNotFound("access_token", "id_token");
        return new NinAccessTokenInfo
        {
            AccessToken = jTokenResp["access_token"]!.Value<string>(),
            IdToken = jTokenResp["id_token"]!.Value<string>()
        };
    }

    public async Task<NinUserInfo> Handle(ReqGetUserInfo request, CancellationToken cancellationToken)
    {
        var strResp = await RunWithDefaultPolicy(_api.GetUserInfoAsync(request.AccessTokenInfo.AccessToken));
        var jTokenResp = strResp.ThrowIfJsonPropNotFound("id", "country", "language", "nickname", "birthday");
        return new NinUserInfo
        {
            Id = jTokenResp["id"]!.Value<string>(),
            Country = jTokenResp["country"]!.Value<string>(),
            Lang = jTokenResp["language"]!.Value<string>(),
            Nickname = jTokenResp["nickname"]!.Value<string>(),
            Birthday = jTokenResp["birthday"]!.Value<string>()
        };
    }

    public async Task<RespPerGameToken> Handle(ReqGetPreGameToken request, CancellationToken cancellationToken)
    {
        var strResp =
            await RunWithDefaultPolicy(_api.GetPreGameTokenAsync(request.AccessTokenInfo.AccessToken, request.User));
        var preGameTokenJResp =
            strResp.ThrowIfJsonPropChainNotFound(new[] {"result", "webApiServerCredential", "accessToken"});
        var coralUserIdJResp =
            strResp.ThrowIfJsonPropChainNotFound(new[] {"result", "user", "id"});
        return new RespPerGameToken()
        {
            PreGameToken = preGameTokenJResp["result"]!["webApiServerCredential"]!["accessToken"]!.Value<string>(),
            CoralUserId = coralUserIdJResp["result"]!["user"]!["id"]!.Value<string>()
        };
    }

    public async Task<string> Handle(ReqGetGameToken request, CancellationToken cancellationToken)
    {
        var strResp =
            await RunWithDefaultPolicy(_api.GetGameTokenAsync(request.PreGameToken, request.User, request.CoralUserId));
        var jTokenResp =
            strResp.ThrowIfJsonPropChainNotFound(new[] {"result", "accessToken"});

        return jTokenResp["result"]!["accessToken"]!.Value<string>();
    }

    public async Task<string> Handle(ReqGetBulletGameToken request, CancellationToken cancellationToken)
    {
        var strResp =
            await RunWithDefaultPolicy(_api.GetBulletTokenAsync(request.GameToken, request.User));
        var jTokenResp =
            strResp.ThrowIfJsonPropNotFound("bulletToken");

        return jTokenResp["bulletToken"]!.Value<string>();
    }
}