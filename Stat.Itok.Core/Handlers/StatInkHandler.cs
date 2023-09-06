using Mediator;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Stat.Itok.Core.ApiClients;
using Stat.Itok.Core.Helpers;

namespace Stat.Itok.Core.Handlers;

public class StatInkHandler : HandlerBase,
    IRequestHandler<ReqPostBattle, StatInkPostBodySuccess>,
    IRequestHandler<ReqPostSalmon, StatInkPostBodySuccess>,
    IRequestHandler<ReqGetGearsInfo, Dictionary<string, string>>,
    IRequestHandler<ReqGetSalmonWeaponsInfo, Dictionary<string, string>>,
    IRequestHandler<ReqTestStatApiKey, ApiResp<string>>

{
    private readonly IStatInkApi _api;
    private readonly ILogger<StatInkHandler> _logger;

    public StatInkHandler(IStatInkApi api, ILogger<StatInkHandler> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async ValueTask<StatInkPostBodySuccess> Handle(ReqPostBattle request, CancellationToken cancellationToken)
    {
        var strResp = await RunWithDefaultPolicy(_api.PostBattleAsync(request.ApiKey, request.Body));
        var jTokenResp = strResp.ThrowIfJsonPropNotFound("id", "url");
        return new StatInkPostBodySuccess()
        {
            Id = jTokenResp["id"]!.Value<string>(),
            Url = jTokenResp["url"]!.Value<string>(),
        };
    }

    public async ValueTask<Dictionary<string, string>> Handle(ReqGetGearsInfo request, CancellationToken cancellationToken)
    {
        var strResp = await RunWithDefaultPolicy(_api.GetGearKeyDictAsync());
        return JArray.Parse(strResp).SelectMany(x =>
        {
            var res = new List<(string, string)>();
            var key = x["key"].Value<string>();
            var children = x["name"].Children();
            foreach (var child in children)
            {
                var childProp = child as JProperty;
                res.Add(($"[{childProp!.Name}]{childProp.Value}", key));
            }

            return res;
        }).GroupBy(x => x.Item1).ToDictionary(x => x.Key, y => y.First().Item2);
    }

    public async ValueTask<Dictionary<string, string>> Handle(ReqGetSalmonWeaponsInfo request,
        CancellationToken cancellationToken)
    {
        var strResp = await RunWithDefaultPolicy(_api.GetSalmonWeaponKeyDictAsync());
        return JArray.Parse(strResp).SelectMany(x =>
        {
            var res = new List<(string, string)>();
            var key = x["key"].Value<string>();
            var children = x["name"].Children();
            foreach (var child in children)
            {
                var childProp = child as JProperty;
                res.Add(($"[{childProp!.Name}]{childProp.Value}", key));
            }

            return res;
        }).GroupBy(x => x.Item1).ToDictionary(x => x.Key, y => y.First().Item2);
    }

    public async ValueTask<StatInkPostBodySuccess> Handle(ReqPostSalmon request, CancellationToken cancellationToken)
    {
        var strResp = await RunWithDefaultPolicy(_api.PostSalmonAsync(request.ApiKey, request.Body));
        var jTokenResp = strResp.ThrowIfJsonPropNotFound("id", "url");
        return new StatInkPostBodySuccess()
        {
            Id = jTokenResp["id"]!.Value<string>(),
            Url = jTokenResp["url"]!.Value<string>(),
        };
    }

    public async ValueTask<ApiResp<string>> Handle(ReqTestStatApiKey request, CancellationToken cancellationToken)
    {
        try
        {
            var valid = await _api.TestApiKeyAsync(request.ApiKey);
            if (valid)
                return ApiResp.OkWith("OK");
            return ApiResp<string>.Error("NOT OK");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"error when do {nameof(ReqTestStatApiKey)}");
            return ApiResp<string>.Error("NOT OK");
        }
    }
}