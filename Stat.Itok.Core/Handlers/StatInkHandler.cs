using MediatR;
using Newtonsoft.Json.Linq;
using Stat.Itok.Core.ApiClients;

namespace Stat.Itok.Core.Handlers;

public class StatInkHandler : HandlerBase,
    IRequestHandler<ReqPostBattle, StatInkPostBattleSuccess>,
    IRequestHandler<ReqGetGearsInfo, Dictionary<string, string>>
{
    private readonly IStatInkApi _api;

    public StatInkHandler(IStatInkApi api)
    {
        _api = api;
    }

    public async Task<StatInkPostBattleSuccess> Handle(ReqPostBattle request, CancellationToken cancellationToken)
    {
        var strResp = await RunWithDefaultPolicy(_api.PostBattlesAsync(request.ApiKey, request.Body));
        var jTokenResp = strResp.ThrowIfJsonPropNotFound("id", "url");
        return new StatInkPostBattleSuccess()
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
}