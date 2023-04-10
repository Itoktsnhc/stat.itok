using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Utility;

namespace Stat.Itok.Core.Helpers;

public static class BattleHelper
{
    private static readonly Guid _battleNsGuid = Guid.Parse("b3a2dbf5-2c09-4792-b78c-00b548b70aeb");
    private static readonly Guid _salmonNsGuid = Guid.Parse("f1911910-605e-11ed-a622-7085c2057a9d");

    /// <summary>
    /// BuildStatInkBattleBody withOutGearInfo
    /// </summary>
    /// <param name="rawVsDetail">rawVsBattleDetail</param>
    /// <param name="rawBattleGroup"></param>
    /// <param name="userLang"></param>
    /// <param name="gearInfoDict"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public static StatInkBattleBody BuildStatInkBattleBody(string rawVsDetail,
        string rawBattleGroup,
        string userLang = "zh-CN",
        Dictionary<string, string> gearInfoDict = null
    )
    {
        gearInfoDict ??= new Dictionary<string, string>();
        var json = JToken.Parse(rawVsDetail);
        var battleGroupInfo = JToken.Parse(rawBattleGroup);

        var battle = json["data"]["vsHistoryDetail"];
        var body = new StatInkBattleBody();
        body.UUID = GetBattleIdForStatInk(battle["id"].TryWith<string>());

        //Rule
        var rule = ExtractStatInkRule(battle);
        if (rule == null) return null;
        body.Rule = rule.Value;
        //lobby,stage,playerInfo,result
        body.Lobby = ExtractStatInkLobby(battle);
        body.Stage = ExtractStatInkStage(battle);
        FillSelfPlayerInfo(battle, body);
        body.Result = ExtractStatInkResult(battle);

        //start -> end
        body.StartAt =
            DateTimeOffset.Parse(battle["playedTime"].TryWith<string>(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal).ToUnixTimeSeconds();
        body.EndAt = body.StartAt + battle["duration"].TryWith<int?>() ?? 300;

        //SCOREBOARD: our & other teams(their and third)
        FillScoreBoardAndPlayers(battle, body, userLang, gearInfoDict);

        //SPLATFEST 
        FillSplafest(battle, body);
        FillGameData(battle, battleGroupInfo, body);

        //MEDALS
        FillMedals(battle, body);
        //SCREENSHOTS 
        //TODO

        //Others
        body.Automated = StatInkBoolean.Yes;
        body.AgentVersion = StatItokConstants.StatVersion;
        return body;
    }

    private static void FillMedals(JToken battle, StatInkBattleBody body)
    {
        if (battle["awards"] != null)
        {
            foreach (var award in (battle["awards"] as JArray))
            {
                body.Medals.Add(award["name"].TryWith<string>());
            }
        }
    }

    private static void FillGameData(JToken battle, JToken parent, StatInkBattleBody body)
    {
        if (body.Lobby == StatInkLobby.Regular
            || body.Lobby == StatInkLobby.SplatFestChallenge
            || body.Lobby == StatInkLobby.SplatFestOpen)
        {
            try
            {
                body.OurTeamPercent = battle["myTeam"]["result"]["paintRatio"].TryWith<decimal?>() * 100;
                body.TheirTeamPercent =
                    (battle["otherTeams"] as JArray)[0]["result"]["paintRatio"].TryWith<decimal?>() * 100;
                if (body.Rule == StatInkRule.TriColor)
                {
                    body.ThirdTeamPercent =
                        (battle["otherTeams"] as JArray)[1]["result"]["paintRatio"].TryWith<decimal?>() * 100;
                }
            }
            catch (Exception)
            {
                //ignore_
            }

            body.OurTeamInked = body.OurTeamPlayers.Sum(x => x.Inked ?? 0);
            body.TheirTeamInked = body.TheirTeamPlayers.Sum(x => x.Inked ?? 0);
            if (body.Rule == StatInkRule.TriColor)
            {
                body.ThirdTeamInked = body.ThirdTeamPlayers.Sum(x => x.Inked ?? 0);
            }
        }

        if (body.Lobby == StatInkLobby.Private)
        {
            try
            {
                body.Knockout = battle["knockout"] == null || battle["knockout"].TryWith<string>() == "NEITHER"
                    ? StatInkBoolean.No
                    : StatInkBoolean.Yes;
            }
            catch (Exception)
            {
                //ignore
            }

            try
            {
                body.OurTeamCount = battle["myTeam"]["result"]["score"].TryWith<int?>();
                body.TheirTeamCount = (battle["otherTeams"] as JArray)[0]["result"]["score"].TryWith<int?>();
            }
            catch (Exception)
            {
                //ignore
            }

            try
            {
                body.OurTeamPercent = battle["myTeam"]["result"]["paintRatio"].TryWith<decimal?>() * 100;
                body.TheirTeamPercent =
                    (battle["otherTeams"] as JArray)[0]["result"]["paintRatio"].TryWith<decimal?>() * 100;
            }
            catch (Exception)
            {
                //ignore_
            }
        }

        if (body.Lobby == StatInkLobby.BankaraChallenge || body.Lobby == StatInkLobby.BankaraOpen)
        {
            try
            {
                body.OurTeamCount = battle["myTeam"]["result"]["score"].TryWith<int?>();
                body.TheirTeamCount = (battle["otherTeams"] as JArray)[0]["result"]["score"].TryWith<int?>();
            }
            catch (Exception)
            {
                //ignore
            }

            body.Knockout = battle["knockout"] == null || battle["knockout"].TryWith<string>() == "NEITHER"
                ? StatInkBoolean.No
                : StatInkBoolean.Yes;

            body.RankExpChange = battle["bankaraMatch"]["earnedUdemaePoint"].TryWith<int?>();
            var parentNodes = parent["historyDetails"]["nodes"] as JArray;
            for (var i = 0; i < parentNodes.Count; i++)
            {
                var child = parentNodes[i];
                if (child["id"].TryWith<string>() == battle["id"].TryWith<string>())
                {
                    var fullRank = Regex.Split(child["udemae"].TryWith<string>().ToLower(), "([0-9]+)");
                    var WasSPlusBefore = fullRank.Length > 1;
                    body.RankBefore = fullRank[0];
                    if (WasSPlusBefore)
                        body.RankBeforeSPlus = int.Parse(fullRank[1]);
                    body.RankAfter = body.RankBefore;
                    body.RankAfterSPlus = body.RankBeforeSPlus;
                    var challenge = parent["bankaraMatchChallenge"];
                    if (challenge != null && challenge.Type != JTokenType.Null)
                    {
                        var ranks = new[] { "c-", "c", "c+", "b-", "b", "b+", "a-", "a", "a+", "s" };
                        if (challenge["rank_up_battle"].TryWith<bool?>() == true)
                            body.RankUpBattle = StatInkBoolean.Yes;
                        else
                            body.RankUpBattle = StatInkBoolean.No;
                        if (challenge["udemaeAfter"] == null)
                        {
                            body.RankAfter = body.RankBefore;
                            if (WasSPlusBefore)
                                body.RankAfterSPlus = body.RankBeforeSPlus;
                        }
                        else
                        {
                            var udemaeAfter = challenge["udemaeAfter"].TryWith<string>()?.ToLower();
                            if (udemaeAfter == null || i != 0)
                            {
                                body.RankAfter = body.RankBefore;
                                if (WasSPlusBefore)
                                    body.RankAfterSPlus = body.RankBeforeSPlus;
                            }
                            else
                            {
                                var fullRankAfter =
                                    Regex.Split(udemaeAfter, "([0-9]+)");
                                body.RankAfter = fullRankAfter[0];
                                if (fullRankAfter.Length > 1)
                                    body.RankAfterSPlus = int.Parse(fullRankAfter[1]);
                            }
                        }

                        if (i == 0)
                        {
                            body.ChallengeWin = challenge["winCount"].TryWith<int?>();
                            body.ChallengeLose = challenge["loseCount"].TryWith<int?>();
                            body.RankExpChange ??= challenge["earnedUdemaePoint"].TryWith<int?>();
                        }
                    }

                    break;
                }
            }
        }

        if (body.Lobby == StatInkLobby.XMatch)
        {
            try
            {
                body.OurTeamCount = battle["myTeam"]["result"]["score"].TryWith<int?>();
                body.TheirTeamCount = (battle["otherTeams"] as JArray)[0]["result"]["score"].TryWith<int?>();
            }
            catch (Exception)
            {
                //ignore
            }

            body.Knockout = battle["knockout"] == null || battle["knockout"].TryWith<string>() == "NEITHER"
                ? StatInkBoolean.No
                : StatInkBoolean.Yes;
            var xMatchObj = battle["xMatch"];
            if (xMatchObj != null && xMatchObj.Type != JTokenType.None)
            {
                var lastXPower = xMatchObj["lastXPower"]?.TryWith<decimal?>();
                body.XPowerBefore = lastXPower;
            }

            var parentNodes = parent["historyDetails"]["nodes"] as JArray;
            for (var i = 0; i < parentNodes.Count; i++)
            {
                var child = parentNodes[i];
                if (child["id"].TryWith<string>() == battle["id"].TryWith<string>())
                {
                    var xMatchMeasurement = parent["xMatchMeasurement"];
                    if (i == 0 && xMatchMeasurement != null && xMatchMeasurement.Type != JTokenType.None)
                    {
                        var afterXPower = xMatchMeasurement["xPowerAfter"]?.TryWith<decimal?>();
                        body.XPowerAfter = afterXPower;
                        body.ChallengeWin = xMatchMeasurement["winCount"]?.TryWith<int?>();
                        body.ChallengeLose = xMatchMeasurement["loseCount"]?.TryWith<int?>();
                    }
                }
            }
        }
    }

    private static void FillSplafest(JToken battle, StatInkBattleBody body)
    {
        if (body.Lobby == StatInkLobby.SplatFestChallenge || body.Lobby == StatInkLobby.SplatFestOpen)
        {
            var times_battle = battle["festMatch"]["dragonMatchType"].TryWith<string>();
            if (times_battle == "DECUPLE") body.FestDragon = "10x";
            if (times_battle == "DRAGON") body.FestDragon = "100x";
            if (times_battle == "DOUBLE_DRAGON") body.FestDragon = "333x";
            body.ClountChange = battle["festMatch"]["contribution"].TryWith<int?>();
            if (body.Lobby == StatInkLobby.SplatFestChallenge)
                body.FestPower = battle["festMatch"]["myFestPower"].TryWith<decimal?>();

            body.OurTeamTheme = battle["myTeam"]["festTeamName"]?.TryWith<string>();

            var otherTeamsArray = battle["otherTeams"] as JArray;
            var theirTeam = otherTeamsArray[0];
            body.TheirTeamTheme = theirTeam["festTeamName"]?.TryWith<string>();

            if (body.Rule == StatInkRule.TriColor)
            {
                body.OurTeamRole = ConvertTriColorRole(battle["myTeam"]["tricolorRole"]?.TryWith<string>());
                body.TheirTeamRole = ConvertTriColorRole(theirTeam["tricolorRole"]?.TryWith<string>());
                if (otherTeamsArray.Count > 1)
                {
                    var thirdTeam = otherTeamsArray[1];
                    body.ThirdTeamTheme = thirdTeam["festTeamName"]?.TryWith<string>();
                    body.ThirdTeamRole = ConvertTriColorRole(thirdTeam["tricolorRole"]?.TryWith<string>());
                }
            }
        }
    }

    private static void FillScoreBoardAndPlayers(JToken battle, StatInkBattleBody body,
        string userLang, Dictionary<string, string> gearInfoDict)
    {
        var myTeam = battle["myTeam"]["players"] as JArray;
        body.OurTeamColor = ConvertAsColorStr(battle["myTeam"]["color"]);
        for (int i = 0; i < myTeam.Count; i++)
        {
            var playerJ = myTeam[i];
            body.OurTeamPlayers.Add(BuildStatInkPlayer(playerJ, i, userLang, gearInfoDict));
        }

        var otherTeamsArray = battle["otherTeams"] as JArray;
        var theirTeam = otherTeamsArray[0]["players"] as JArray;
        body.TheirTeamColor = ConvertAsColorStr(otherTeamsArray[0]["color"]);
        for (int i = 0; i < theirTeam.Count; i++)
        {
            var playerJ = theirTeam[i];
            body.TheirTeamPlayers.Add(BuildStatInkPlayer(playerJ, i, userLang, gearInfoDict));
        }

        if (body.Rule == StatInkRule.TriColor && otherTeamsArray.Count > 1)
        {
            var thirdTeam = otherTeamsArray[1]["players"] as JArray;
            body.ThirdTeamColor = ConvertAsColorStr(otherTeamsArray[1]["color"]);
            for (int i = 0; i < thirdTeam.Count; i++)
            {
                var playerJ = thirdTeam[i];
                body.ThirdTeamPlayers.Add(BuildStatInkPlayer(playerJ, i, userLang, gearInfoDict));
            }
        }
    }

    private static StatInkPlayer BuildStatInkPlayer(JToken playerJ, int idx,
        string userLang, Dictionary<string, string> gearInfoDict)
    {
        var player = new StatInkPlayer();
        player.Me = playerJ["isMyself"].TryWith<bool?>() == true
                    || playerJ["isMySelf"].TryWith<bool?>() == true
            ? StatInkBoolean.Yes
            : StatInkBoolean.No;
        player.Name = playerJ["name"].TryWith<string>();
        player.Number = playerJ["nameId"].TryWith<string>();
        player.SplashtagTitle = playerJ["byname"].TryWith<string>();
        player.Weapon = ParseWeaponId(playerJ["weapon"]["id"].TryWith<string>());
        player.Inked = playerJ["paint"].TryWith<int?>();
        player.RankInTeam = idx + 1;
        if (playerJ["result"] != null && playerJ["result"].Type != JTokenType.Null)
        {
            var res = playerJ["result"];
            player.KillOrAssist = res["kill"].TryWith<int?>();
            player.Assist = res["assist"].TryWith<int?>();
            player.Kill = player.KillOrAssist - player.Assist;
            player.Death = res["death"].TryWith<int?>();
            player.Special = res["special"].TryWith<int?>();
            player.Disconnected = StatInkBoolean.No;
        }
        else
        {
            player.Disconnected = StatInkBoolean.Yes;
        }

        player.Crown = playerJ["crown"].TryWith<bool?>() == true
            ? StatInkBoolean.Yes
            : playerJ["crown"].TryWith<bool?>() == false
                ? StatInkBoolean.No
                : null;

        FillGearsForPlayerJ(playerJ, player, userLang, gearInfoDict);


        return player;
    }

    private static void FillGearsForPlayerJ(JToken playerJ, StatInkPlayer player, string userLang,
        Dictionary<string, string> gearInfoDict)
    {
        if (playerJ["headGear"] != null && playerJ["headGear"].Type != JTokenType.Null)
        {
            player.Gears.HeadGear = new StatInkGear();
            var primaryGearPower = playerJ["headGear"]?["primaryGearPower"]?["name"]?.TryWith<string>();
            var subKey = $"[{userLang.Replace('-', '_')}]{primaryGearPower}";
            if (!string.IsNullOrEmpty(primaryGearPower)
                && gearInfoDict.ContainsKey(subKey))
            {
                player.Gears.HeadGear.PrimaryAbility = gearInfoDict[subKey];
            }

            var additionalGears = playerJ["headGear"]?["additionalGearPowers"] as JArray;
            foreach (var item in additionalGears!)
            {
                var ability = item["name"]?.TryWith<string>();
                subKey = $"[{userLang.Replace('-', '_')}]{ability}";
                if (!string.IsNullOrEmpty(ability)
                    && gearInfoDict.ContainsKey(subKey))
                {
                    player.Gears.HeadGear.SecondaryAbility.Add(gearInfoDict[subKey]);
                }
            }
        }

        if (playerJ["clothingGear"] != null && playerJ["clothingGear"].Type != JTokenType.Null)
        {
            player.Gears.Clothing = new StatInkGear();
            var primaryGearPower = playerJ["clothingGear"]?["primaryGearPower"]?["name"]?.TryWith<string>();
            var subKey = $"[{userLang.Replace('-', '_')}]{primaryGearPower}";
            if (!string.IsNullOrEmpty(primaryGearPower)
                && gearInfoDict.ContainsKey(subKey))
            {
                player.Gears.Clothing.PrimaryAbility = gearInfoDict[subKey];
            }

            var additionalGears = playerJ["clothingGear"]?["additionalGearPowers"] as JArray;
            foreach (var item in additionalGears!)
            {
                var ability = item["name"]?.TryWith<string>();
                subKey = $"[{userLang.Replace('-', '_')}]{ability}";
                if (!string.IsNullOrEmpty(ability)
                    && gearInfoDict.ContainsKey(subKey))
                {
                    player.Gears.Clothing.SecondaryAbility.Add(gearInfoDict[subKey]);
                }
            }
        }

        if (playerJ["shoesGear"] != null && playerJ["shoesGear"].Type != JTokenType.Null)
        {
            player.Gears.Shoes = new StatInkGear();
            var primaryGearPower = playerJ["shoesGear"]?["primaryGearPower"]?["name"]?.TryWith<string>();
            var subKey = $"[{userLang.Replace('-', '_')}]{primaryGearPower}";
            if (!string.IsNullOrEmpty(primaryGearPower)
                && gearInfoDict.ContainsKey(subKey))
            {
                player.Gears.Shoes.PrimaryAbility = gearInfoDict[subKey];
            }

            var additionalGears = playerJ["shoesGear"]?["additionalGearPowers"] as JArray;
            foreach (var item in additionalGears!)
            {
                var ability = item["name"]?.TryWith<string>();
                subKey = $"[{userLang.Replace('-', '_')}]{ability}";
                if (!string.IsNullOrEmpty(ability)
                    && gearInfoDict.ContainsKey(subKey))
                {
                    player.Gears.Shoes.SecondaryAbility.Add(gearInfoDict[subKey]);
                }
            }
        }
    }

    private static StatInkResult ExtractStatInkResult(JToken battle)
    {
        var result = battle["judgement"].TryWith<string>();
        if (result == "WIN") return StatInkResult.Win;
        if (result == "LOSE" || result == "DEEMED_LOSE") return StatInkResult.Lose;
        if (result == "EXEMPTED_LOSE") return StatInkResult.ExemptedLose;
        if (result == "DRAW") return StatInkResult.Draw;
        throw new NotSupportedException($"result:{result}");
    }

    private static void FillSelfPlayerInfo(JToken battle, StatInkBattleBody body)
    {
        var players = battle["myTeam"]["players"] as JArray;
        for (int i = 0; i < players.Count; i++)
        {
            var playerJ = players[i];
            if (playerJ["isMyself"].TryWith<bool?>() == true)
            {
                body.Weapon = ParseWeaponId(playerJ["weapon"]["id"].TryWith<string>());
                body.Inked = playerJ["paint"].TryWith<int?>();
                //body.Species = player["species"]; //this is not supported
                body.RankInTeam = i + 1;
                if (playerJ["result"] != null && playerJ["result"].Type != JTokenType.Null)
                {
                    var res = playerJ["result"];
                    body.KillOrAssist = res["kill"].TryWith<int?>();
                    body.Assist = res["assist"].TryWith<int?>();
                    body.Kill = body.KillOrAssist - body.Assist;
                    body.Death = res["death"].TryWith<int?>();
                    body.Special = res["special"].TryWith<int?>();
                }

                break;
            }
        }
    }

    private static string ParseWeaponId(string weaponId)
    {
        var idRaw = Encoding.UTF8.GetString(Convert.FromBase64String(weaponId));
        var id = idRaw.Replace("Weapon-", "");
        if (id.Length == 5 && id.First() == '2' && id.Substring(id.Length - 3) == "900")
            return null;
        return id;
    }

    private static string ExtractStatInkStage(JToken battle)
    {
        var stageId = ParseStageId(battle["vsStage"]["id"].TryWith<string>());
        switch (stageId)
        {
            case 1: return "yunohana";
            case 2: return "gonzui";
            case 3: return "yagara";
            case 4: return "mategai";

            case 6: return "namero";
            case 7: return "kusaya";

            case 9: return "hirame";
            case 10: return "masaba";
            case 11: return "kinmedai";
            case 12: return "mahimahi";
            case 13: return "amabi";
            case 14: return "chozame";
            case 15: return "zatou";
            case 16: return "sumeshi";

            default: return stageId.ToString(); // return stage_id as Alias
        }
    }

    private static int ParseStageId(string vsStageId)
    {
        var idRaw = Encoding.UTF8.GetString(Convert.FromBase64String(vsStageId));
        return int.Parse(idRaw.Replace("VsStage-", ""));
    }

    private static StatInkRule? ExtractStatInkRule(JToken battle)
    {
        var rule = battle["vsRule"]["rule"].TryWith<string>();
        if (rule == "TURF_WAR") return StatInkRule.Nawabari;
        if (rule == "AREA") return StatInkRule.Area;
        if (rule == "LOFT") return StatInkRule.Yagura;
        if (rule == "GOAL") return StatInkRule.Hoko;
        if (rule == "CLAM") return StatInkRule.Asari;
        if (rule == "TRI_COLOR") return StatInkRule.TriColor;
        return null;
    }

    private static string ConvertAsColorStr(JToken colorObj)
    {
        var r = (int?)(colorObj["r"].TryWith<decimal?>() * 255);
        var g = (int?)(colorObj["g"].TryWith<decimal?>() * 255);
        var b = (int?)(colorObj["b"].TryWith<decimal?>() * 255);
        var a = (int?)(colorObj["a"].TryWith<decimal?>() * 255);
        return $"{r?.ToString("x2")}{g?.ToString("x2")}{b?.ToString("x2")}{a?.ToString("x2")}";
    }

    private static string ConvertTriColorRole(string rawRole)
    {
        if (rawRole == "DEFENSE") return "defender";
        return "attacker";
    }

    private static StatInkLobby ExtractStatInkLobby(JToken battle)
    {
        var mode = battle["vsMode"]["mode"].TryWith<string>();
        if (mode == "REGULAR") return StatInkLobby.Regular;
        if (mode == "BANKARA")
        {
            var modeSubName = battle["bankaraMatch"]["mode"].TryWith<string>();
            if (modeSubName == "OPEN")
                return StatInkLobby.BankaraOpen;
            if (modeSubName == "CHALLENGE")
                return StatInkLobby.BankaraChallenge;
            throw new NotSupportedException($"{mode}:mode;modeSubName:{modeSubName}");
        }

        if (mode == "PRIVATE") return StatInkLobby.Private;
        if (mode == "FEST")
        {
            var vsModeId = ParseVsModeId(battle["vsMode"]["id"].TryWith<string>());
            if (vsModeId == 6 || vsModeId == 8) //open or tricolor
                return StatInkLobby.SplatFestOpen;
            if (vsModeId == 7)
                return StatInkLobby.SplatFestChallenge;
            throw new NotSupportedException($"{mode}:mode;vsModeId:{vsModeId}");
        }

        if (mode == "X_MATCH") return StatInkLobby.XMatch;

        throw new NotSupportedException($"{mode}:mode;");
    }

    private static int ParseVsModeId(string vsModeId)
    {
        var idRaw = Encoding.UTF8.GetString(Convert.FromBase64String(vsModeId));
        return int.Parse(idRaw.Replace("VsMode-", ""));
    }

    private static T TryWith<T>(this JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
            return default;
        return token.Value<T>();
    }

    public static IList<BattleGroupAndIds> ExtractBattleIds(string rawJsonResp, string queryHash)
    {
        var res = new List<BattleGroupAndIds>();
        try
        {
            var json = JToken.Parse(rawJsonResp);
            var queryName = (json["data"]?.FirstOrDefault() as JProperty)?.Name;
            if (string.IsNullOrWhiteSpace(queryName)) return res;
            foreach (var battleGroup in json["data"][queryName]["historyGroups"]["nodes"] as JArray)
            {
                var group = new BattleGroupAndIds()
                {
                    RawBattleGroup = battleGroup.ToString()
                };
                foreach (var battle in battleGroup["historyDetails"]["nodes"] as JArray)
                {
                    group.BattleIds.Add(battle["id"].TryWith<string>());
                }

                res.Add(group);
            }
        }
        catch (Exception)
        {
            //ignore
        }

        return res;
    }

    public static string GetBattleIdForStatInk(string rawBattleId)
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(rawBattleId));
        var dataSeg = decoded[^52..];
        var guidNs = decoded.StartsWith("CoopHistoryDetail")
            ? _salmonNsGuid
            : _battleNsGuid;
        var res = GuidUtility.Create(guidNs, dataSeg, 5);
        return res.ToString();
    }

    public static string GetPayloadTypeForStatInk(string rawBattleId)
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(rawBattleId));

        return decoded.StartsWith("CoopHistoryDetail")
            ? "salmon"
            : "battle";
    }

    public static StatInkSalmonBody BuildStatInkSalmonBody(string detailRes, string groupRawStr,
        string userLang,
        Dictionary<string, string> weaponInfo)
    {
        var job = JToken.Parse(detailRes)["data"]!["coopHistoryDetail"];
         var payload = new StatInkSalmonBody
        {
            Uuid = GetBattleIdForStatInk(job["id"]?.TryWith<string>())
        };
        var groupWrapper = JToken.Parse(groupRawStr);
        var jobMode = groupWrapper["mode"]?.TryWith<string>();
        if(!string.IsNullOrWhiteSpace(jobMode) && jobMode.StartsWith("PRIVATE_"))
        {
            payload.IsPrivate = StatInkBoolean.Yes;
        }
        var group = groupWrapper["historyDetails"]!["nodes"] as JArray;
       
        var jobRule = job["rule"]?.TryWith<string>();

        payload.DangerRate = job["dangerRate"]?.TryWith<double?>() * 100;

        var kingSmell = job["smellMeter"]?.TryWith<int?>();
        payload.KingSmell = kingSmell;

        var waveCleared = job["resultWave"]?.TryWith<int?>() - 1;
        payload.ClearWaves = waveCleared == -1 ? 3 : waveCleared;
        if (payload.ClearWaves < 0) payload.ClearWaves = null;
        else if (payload.ClearWaves != 3)
        {
            var waves = job["waveResults"] as JArray;
            if (waves?.Count > payload.ClearWaves)
            {
                var lastWave = waves[payload.ClearWaves];

                if (lastWave != null)
                {
                    if (lastWave["teamDeliverCount"]?.TryWith<int?>() >=
                        lastWave["deliverNorm"]?.TryWith<int?>())
                        payload.FailReason = "wipe_out";
                    if (lastWave["teamDeliverCount"]?.TryWith<int?>() <
                        lastWave["deliverNorm"]?.TryWith<int?>())
                        payload.FailReason = "time_limit";
                }
            }
        }

        if (job["bossResult"] != null && job["bossResult"].Type != JTokenType.Null)
        {
            //extra_wave
            payload.KingSalmonId = ParseCommonId(job["bossResult"]?["boss"]?["id"]?.TryWith<string>());
            payload.ClearExtra = job["bossResult"]?["hasDefeatBoss"]?.TryWith<bool?>() == true
                ? StatInkBoolean.Yes
                : StatInkBoolean.No;
        }

        var currentStageFullId = job["coopStage"]?["id"]?.TryWith<string>();
        //https://stat.ink/api-info/salmon-title3 prevGrade_Point
        if (payload.IsPrivate != StatInkBoolean.Yes)
        {
            if (job["afterGrade"] != null && job["afterGrade"].Type != JTokenType.Null)
            {
                payload.TitleAfter = ParseCommonId(job["afterGrade"]?["id"]?.TryWith<string>());
                payload.TitleExpAfter = job["afterGradePoint"]?.TryWith<int?>();
            }

            string prevJobId = null;
            if (job["previousHistoryDetail"] != null && job["previousHistoryDetail"].Type != JTokenType.Null)
            {
                prevJobId = job["previousHistoryDetail"]["id"]?.TryWith<string>();
            }

            if (!string.IsNullOrWhiteSpace(prevJobId) && group != null)
            {
                foreach (var prevJob in group)
                {
                    if (prevJob["id"]?.TryWith<string>() != prevJobId) continue;
                    var coopStageId = prevJob["coopStage"]?["id"]?.TryWith<string>();
                    //new stage
                    if (coopStageId != null && coopStageId != currentStageFullId)
                    {
                        payload.TitleBefore = payload.TitleAfter;
                        payload.TitleExpBefore = 40;
                    }
                    else
                    {
                        if (prevJob["afterGrade"] != null && prevJob["afterGrade"].Type != JTokenType.Null)
                        {
                            payload.TitleBefore = ParseCommonId(prevJob["afterGrade"]?["id"]?.TryWith<string>());
                            payload.TitleExpBefore = prevJob["afterGradePoint"]?.TryWith<int?>();
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(payload.TitleBefore)) payload.TitleBefore = payload.TitleAfter;
            if (payload.TitleExpBefore == null) payload.TitleExpBefore = payload.TitleExpAfter;
        }

        var gEggs = 0;
        var pEggs = job["myResult"]["deliverCount"]?.TryWith<int?>() ?? 0;
        if (job["memberResults"] is JArray deliverRes)
        {
            foreach (var deliver in deliverRes)
            {
                pEggs += deliver["deliverCount"]?.TryWith<int?>() ?? 0;
            }
        }

        if (job["waveResults"] is JArray waveResults)
        {
            foreach (var wave in waveResults)
            {
                gEggs += wave["teamDeliverCount"]?.TryWith<int?>() ?? 0;
            }
        }

        payload.GoldenEggs = gEggs;
        payload.PowerEggs = pEggs;
        if (job["scale"] != null && job["scale"].Type != JTokenType.Null)
        {
            payload.GoldScale = job["scale"]?["gold"]?.TryWith<int?>();
            payload.SilverScale = job["scale"]?["silver"]?.TryWith<int?>();
            payload.BronzeScale = job["scale"]?["bronze"]?.TryWith<int?>();
        }

        payload.JobScore = job["jobScore"]?.TryWith<int?>();
        payload.JobRate = job["jobRate"]?.TryWith<double?>();
        payload.JobBonus = job["jobBonus"]?.TryWith<int?>();
        payload.JobPoint = job["jobPoint"]?.TryWith<int?>();

        payload.Waves = new List<Wave>();
        foreach (var wave in job["waveResults"] as JArray)
        {
            var waveInfo = new Wave
            {
                Tide = wave["waterLevel"]?.TryWith<int?>() switch
                {
                    0 => "low",
                    2 => "high",
                    _ => "normal",
                },
                GoldenQuota = wave["deliverNorm"]?.TryWith<int?>(),
                GoldenDelivered = wave["teamDeliverCount"]?.TryWith<int?>(),
                GoldenAppearances = wave["goldenPopCount"]?.TryWith<int?>()
            };
            if (wave["eventWave"] != null && wave["eventWave"].Type != JTokenType.Null)
            {
                var eventId = ParseCommonId(wave["eventWave"]?["id"]?.TryWith<string>());
                if (eventId != null)
                {
                    waveInfo.Event = GetWaveEventName(eventId);
                }
            }

            waveInfo.SpecialUses ??= new Dictionary<string, int>();
            foreach (var specialWeapon in wave["specialWeapons"] as JArray)
            {
                var specialId = ParseCommonId(specialWeapon["id"]?.TryWith<string>());
                if (string.IsNullOrWhiteSpace(specialId)) continue;
                var specialKey = GetSpecialName(specialId);
                if (!waveInfo.SpecialUses.ContainsKey(specialKey))
                {
                    waveInfo.SpecialUses[specialKey] = 0;
                }

                waveInfo.SpecialUses[specialKey]++;
            }

            payload.Waves.Add(waveInfo);
        }

        payload.Players ??= new List<SalmonPlayer>();
        var players = new JArray { job["myResult"] };
        foreach (var memberRes in job["memberResults"])
        {
            players.Add(memberRes);
        }

        for (var idx = 0; idx < players.Count; idx++)
        {
            var jPlayer = players[idx];
            var playerInfo = new SalmonPlayer
            {
                Me = idx == 0 ? StatInkBoolean.Yes : StatInkBoolean.No,
                Name = jPlayer["player"]?["name"]?.TryWith<string>(),
                Number = jPlayer["player"]?["nameId"]?.TryWith<string>(),
                SplashTagTitle = jPlayer["player"]["byname"]?.TryWith<string>(),
                GoldenEggs = jPlayer["goldenDeliverCount"]?.TryWith<int?>(),
                GoldenAssist = jPlayer["goldenAssistCount"]?.TryWith<int?>(),
                PowerEggs = jPlayer["deliverCount"]?.TryWith<int?>(),
                Rescue = jPlayer["rescueCount"]?.TryWith<int?>(),
                Rescued = jPlayer["rescuedCount"]?.TryWith<int?>(),
                DefeatBoss = jPlayer["defeatEnemyCount"]?.TryWith<int?>()
            };
            if (playerInfo.GoldenEggs == 0 && playerInfo.PowerEggs == 0 &&
                playerInfo.Rescue == 0 && playerInfo.Rescued == 0 && playerInfo.DefeatBoss == 0)
            {
                playerInfo.Disconnected = StatInkBoolean.Yes;
            }

            playerInfo.Uniform = ParseCommonId(jPlayer["player"]?["uniform"]?["id"].TryWith<string>());
            if (jPlayer["specialWeapon"] != null && jPlayer["specialWeapon"].Type != JTokenType.Null)
            {
                var specialId = jPlayer["specialWeapon"]["weaponId"]?.TryWith<string>();
                if (!string.IsNullOrWhiteSpace(specialId))
                {
                    var specialName = GetSpecialName(specialId);
                    playerInfo.Special = specialName;
                }
                else
                {
                    specialId = ParseCommonId(jPlayer["specialWeapon"]["id"]?.TryWith<string>());
                    if (!string.IsNullOrWhiteSpace(specialId))
                    {
                        var specialName = GetSpecialName(specialId);
                        playerInfo.Special = specialName;
                    }
                }
            }

            playerInfo.Weapons ??= new List<string>();
            foreach (var weapon in jPlayer["weapons"] as JArray)
            {
                var weaponName = weapon["name"]?.TryWith<string>();
                var subKey = $"[{userLang.Replace('-', '_')}]{weaponName}";
                if (weaponInfo.ContainsKey(subKey))
                {
                    playerInfo.Weapons.Add(weaponInfo[subKey]);
                }
            }

            payload.Players.Add(playerInfo);
        }

        //https://stat.ink/api-info/boss-salmonid3
        payload.Bosses ??= new Dictionary<string, Boss>();
        foreach (var jBoss in job["enemyResults"] as JArray)
        {
            var boss = new Boss()
            {
                Appearances = jBoss["popCount"]?.TryWith<int?>(),
                Defeated = jBoss["teamDefeatCount"]?.TryWith<int?>(),
                DefeatedByMe = jBoss["defeatCount"]?.TryWith<int?>(),
            };
            payload.Bosses[GetBossName(ParseCommonId(jBoss["enemy"]?["id"]?.TryWith<string>()) ?? "unknown")] = boss;
        }

        payload.StartAt =
            DateTimeOffset.Parse(job["playedTime"].TryWith<string>(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal).ToUnixTimeSeconds();
        //payload.EndAt

        payload.IsBigRun = jobRule is "BIG_RUN" ? StatInkBoolean.Yes : StatInkBoolean.No;
        if (payload.IsBigRun != StatInkBoolean.Yes)
        {
            payload.Stage = ParseCommonId(currentStageFullId);
        }

        return payload;
    }

    private static string ParseCommonId(string idRaw)
    {
        if (string.IsNullOrWhiteSpace(idRaw)) return null;
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(idRaw));
        var parts = decoded.Split('-');
        return parts.Last();
    }

    private static string GetWaveEventName(string eventId)
    {
        return eventId switch
        {
            "1" => "rush",
            "2" => "goldie_seeking",
            "3" => "the_griller",
            "4" => "the_mothership",
            "5" => "fog",
            "6" => "cohock_charge",
            "7" => "giant_tornado",
            "8" => "mudmouth_eruption",
            _ => eventId
        };
    }

    private static string GetSpecialName(string specialId)
    {
        return specialId switch
        {
            "20006" => "nicedama",
            "20007" => "hopsonar",
            "20009" => "megaphone51",
            "20010" => "jetpack",
            "20012" => "kanitank",
            "20013" => "sameride",
            "20014" => "tripletornado",
            _ => specialId
        };
    }

    private static string GetBossName(string bossId)
    {
        return bossId switch
        {
            "4" => "bakudan",
            "5" => "katapad",
            "6" => "teppan",
            "7" => "hebi",
            "8" => "tower",
            "9" => "mogura",
            "10" => "koumori",
            "11" => "hashira",
            "12" => "diver",
            "13" => "tekkyu",
            "14" => "nabebuta",
            "15" => "kin_shake",
            "17" => "grill",
            "20" => "doro_shake",
            _ => bossId
        };
    }
}