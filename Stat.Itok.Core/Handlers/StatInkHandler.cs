using MediatR;
using Newtonsoft.Json.Linq;
using Stat.Itok.Core.ApiClients;
using Stat.Itok.Core.Helpers;

namespace Stat.Itok.Core.Handlers;

public class StatInkHandler : HandlerBase,
    IRequestHandler<ReqPostBattle, StatInkPostBodySuccess>,
    IRequestHandler<ReqPostSalmon, StatInkPostBodySuccess>,
    IRequestHandler<ReqGetGearsInfo, Dictionary<string, string>>,
    IRequestHandler<ReqGetSalmonWeaponsInfo, Dictionary<string, string>>

{
    private readonly IStatInkApi _api;

    public StatInkHandler(IStatInkApi api)
    {
        _api = api;
    }

    public async Task<StatInkPostBodySuccess> Handle(ReqPostBattle request, CancellationToken cancellationToken)
    {
        var strResp = await RunWithDefaultPolicy(_api.PostBattleAsync(request.ApiKey, request.Body));
        var jTokenResp = strResp.ThrowIfJsonPropNotFound("id", "url");
        return new StatInkPostBodySuccess()
        {
            Id = jTokenResp["id"]!.Value<string>(),
            Url = jTokenResp["url"]!.Value<string>(),
        };
    }

    public async Task<Dictionary<string, string>> Handle(ReqGetGearsInfo request, CancellationToken cancellationToken)
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

    public async Task<Dictionary<string, string>> Handle(ReqGetSalmonWeaponsInfo request, CancellationToken cancellationToken)
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

    public async Task<StatInkPostBodySuccess> Handle(ReqPostSalmon request, CancellationToken cancellationToken)
    {
        var strResp = await RunWithDefaultPolicy(_api.PostSalmonAsync(request.ApiKey, request.Body));
        var jTokenResp = strResp.ThrowIfJsonPropNotFound("id", "url");
        return new StatInkPostBodySuccess()
        {
            Id = jTokenResp["id"]!.Value<string>(),
            Url = jTokenResp["url"]!.Value<string>(),
        };
    }

}