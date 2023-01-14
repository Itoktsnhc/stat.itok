using System;
using System.Dynamic;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NeoSmart.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stat.Itok.Core.Utility;

namespace Stat.Itok.Core
{
    public static class Helper
    {
        public static string CompressStr(string input, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var bytes = encoding.GetBytes(input);
            return Convert.ToBase64String(CompressBytes(bytes));
        }

        public static string DecompressStr(string input, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var bytes = Convert.FromBase64String(input);
            return encoding.GetString(DecompressBytes(bytes));
        }

        public static byte[] CompressBytes(byte[] bytes)
        {
            using var outputStream = new MemoryStream();
            using (var compressStream = new BrotliStream(outputStream, CompressionLevel.SmallestSize))
            {
                compressStream.Write(bytes, 0, bytes.Length);
            }

            return outputStream.ToArray();
        }

        public static byte[] DecompressBytes(byte[] bytes)
        {
            using var inputStream = new MemoryStream(bytes);
            using var outputStream = new MemoryStream();
            using (var decompressStream = new BrotliStream(inputStream, CompressionMode.Decompress))
            {
                decompressStream.CopyTo(outputStream);
            }

            return outputStream.ToArray();
        }
    }

    public static class StatHelper
    {
        private static readonly Random _random = new Random();
        private static readonly Guid _splatoon3NsGuid = Guid.Parse("b3a2dbf5-2c09-4792-b78c-00b548b70aeb");

        public static NinMiscConfig ParseNinWebViewData(string str)
        {
            var webViewData = new NinMiscConfig();

            var versionMatchRes = Regex.Match(str,
                "=.(?<revision>[0-9a-f]{40}).*revision_info_not_set.*=.(?<version>\\d+\\.\\d+\\.\\d+)-");
            if (versionMatchRes.Groups.Count < 3) return null;
            var versionRange = versionMatchRes.Groups["version"];

            var revisionRange = versionMatchRes.Groups["revision"];
            var revision = str.Substring(revisionRange.Index, 8);
            webViewData.WebViewVersion = $"{versionRange.Value}-{revision}";
            var graphQLMatchRes = Regex.Matches(str,
                "params:\\{id:.(?<id>[0-9a-f]{32}).,metadata:\\{\\},name:.(?<name>[a-zA-Z0-9_]+).,");

            foreach (Match match in graphQLMatchRes)
            {
                if (!match.Success || match.Groups.Count < 3)
                {
                    continue;
                }

                webViewData.GraphQL.APIs[match.Groups["name"].Value] = match.Groups["id"].Value;
            }

            return webViewData;
        }

        public static string BuildRandomSizedBased64Str(int size)
        {
            var arr = new byte[size];
            _random.NextBytes(arr);
            return UrlBase64.Encode(arr);
        }

        public static void CorrectUserInfoLang(this JobConfig jobConfig)
        {
            if (string.IsNullOrEmpty(jobConfig.ForcedUserLang))
            {
                jobConfig.ForcedUserLang = jobConfig.NinAuthContext.UserInfo.Lang;
            }

            jobConfig.NinAuthContext.UserInfo.Lang = jobConfig.ForcedUserLang;
        }

        public static JToken ThrowIfJsonPropNotFound(this string json, params string[] propNames)
        {
            JToken jToken;
            try
            {
                jToken = JToken.Parse(json);
            }
            catch (Exception e)
            {
                throw new Exception($"parse json Error, rawJson is: {json}", e);
            }

            foreach (var propName in propNames)
            {
                if (jToken[propName] == null || jToken[propName].Type == JTokenType.Null)
                {
                    throw new Exception($"[{propName}] is null, rawJson is: {json}");
                }
            }

            return jToken;
        }

        public static JToken ThrowIfJsonPropChainNotFound(this string json, string[] propNames)
        {
            JToken jToken;
            try
            {
                jToken = JToken.Parse(json);
            }
            catch (Exception e)
            {
                throw new Exception($"parse json Error, rawJson is: {json}", e);
            }

            var curToken = jToken;
            foreach (var propName in propNames)
            {
                if (curToken[propName] == null || curToken[propName].Type == JTokenType.Null)
                {
                    throw new Exception($"[{propName}] is null, rawJson is: {json}");
                }

                curToken = curToken[propName];
            }

            return jToken;
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

        public static string FirstCharToLower(this string input) =>
            input switch
            {
                null => throw new ArgumentNullException(nameof(input)),
                "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
                _ => string.Concat(input[0].ToString().ToLower(), input.AsSpan(1))
            };

        public static string GetBattleIdForStatInk(string rawBattleId)
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(rawBattleId));
            var res = GuidUtility.Create(_splatoon3NsGuid, decoded.Substring(decoded.Length - 52), 5);
            return res.ToString();
        }

        public static string BuildGraphQLBody(string queryHash, string name = null, string value = null)
        {
            dynamic body = new ExpandoObject();
            dynamic extensions = new ExpandoObject();
            dynamic persistedQuery = new ExpandoObject();
            dynamic variables = new ExpandoObject();
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                ((IDictionary<string, object>)variables)[name] = value;
            persistedQuery.sha256Hash = queryHash;
            persistedQuery.version = 1;
            extensions.persistedQuery = persistedQuery;
            body.extensions = extensions;
            body.variables = variables;
            return JsonConvert.SerializeObject(body);
        }

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
                (long)(DateTimeOffset.Parse(battle["playedTime"].TryWith<string>(), CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal)
                        - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.FromHours(0))).TotalSeconds;
            body.EndAt = body.StartAt + battle["duration"].TryWith<int?>() ?? 300;

            //SCOREBOARD: our & other teams
            FillScoreBoard(battle, body, userLang, gearInfoDict);

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
                }
                catch (Exception)
                {
                    //ignore_
                }

                body.OurTeamInked = body.OurTeamPlayers.Sum(x => x.Inked ?? 0);
                body.TheirTeamInked = body.TheirTeamPlayers.Sum(x => x.Inked ?? 0);
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
            }
        }

        private static void FillScoreBoard(JToken battle, StatInkBattleBody body,
            string userLang, Dictionary<string, string> gearInfoDict)
        {
            var myTeam = battle["myTeam"]["players"] as JArray;
            for (int i = 0; i < myTeam.Count; i++)
            {
                var playerJ = myTeam[i];
                body.OurTeamPlayers.Add(BuildStatInkPlayer(playerJ, i, userLang, gearInfoDict));
            }

            var otherTeams = (battle["otherTeams"] as JArray)[0]["players"] as JArray;
            for (int i = 0; i < otherTeams.Count; i++)
            {
                var playerJ = otherTeams[i];
                body.TheirTeamPlayers.Add(BuildStatInkPlayer(playerJ, i, userLang, gearInfoDict));
            }
        }

        private static StatInkPlayer BuildStatInkPlayer(JToken playerJ, int idx,
            string userLang, Dictionary<string, string> gearInfoDict)
        {
            var player = new StatInkPlayer();
            player.Me = playerJ["isMySelf"].TryWith<bool?>() == true ? StatInkBoolean.Yes : StatInkBoolean.No;
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

                default: throw new NotSupportedException("NO STAGE PARSED");
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
                if (vsModeId == 6)
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
    }

    namespace Utility
    {
        /// <summary>
        /// Helper methods for working with <see cref="Guid"/>. https://gist.github.com/ChrisMcKee/599264d776878bea8a611493b5e28143
        /// </summary>
        public static class GuidUtility
        {
            /// <summary>
            /// Creates a name-based UUID using the algorithm from RFC 4122 §4.3.
            /// </summary>
            /// <param name="namespaceId">The ID of the namespace.</param>
            /// <param name="name">The name (within that namespace).</param>
            /// <returns>A UUID derived from the namespace and name.</returns>
            /// <remarks>See <a href="http://code.logos.com/blog/2011/04/generating_a_deterministic_guid.html">Generating a deterministic GUID</a>.</remarks>
            public static Guid Create(Guid namespaceId, string name)
            {
                return GuidUtility.Create(namespaceId, name, 5);
            }

            /// <summary>
            /// Creates a name-based UUID using the algorithm from RFC 4122 §4.3.
            /// </summary>
            /// <param name="namespaceId">The ID of the namespace.</param>
            /// <param name="name">The name (within that namespace).</param>
            /// <param name="version">The version number of the UUID to create; this value must be either
            /// 3 (for MD5 hashing) or 5 (for SHA-1 hashing) or 6 (for SHA-256 hashing).</param>
            /// <returns>A UUID derived from the namespace and name.</returns>
            /// <remarks>See <a href="http://code.logos.com/blog/2011/04/generating_a_deterministic_guid.html">Generating a deterministic GUID</a>.</remarks>
            public static Guid Create(Guid namespaceId, string name, int version)
            {
                if (name == null)
                    throw new ArgumentNullException("name");
                if (version != 3 && version != 5 && version != 6)
                    throw new ArgumentOutOfRangeException("version",
                        "version must be either 3 (md5) or 5 (sha1), or 6 (sha256).");

                // convert the name to a sequence of octets (as defined by the standard or conventions of its namespace) (step 3)
                // ASSUME: UTF-8 encoding is always appropriate
                byte[] nameBytes = Encoding.UTF8.GetBytes(name);

                // convert the namespace UUID to network order (step 3)
                byte[] namespaceBytes = namespaceId.ToByteArray();
                GuidUtility.SwapByteOrder(namespaceBytes);

                // comput the hash of the name space ID concatenated with the name (step 4)
                byte[] hash;
                using (var incrementalHash = version == 3 ? IncrementalHash.CreateHash(HashAlgorithmName.MD5) :
                       version == 5 ? IncrementalHash.CreateHash(HashAlgorithmName.SHA1) :
                       IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    incrementalHash.AppendData(namespaceBytes);
                    incrementalHash.AppendData(nameBytes);
                    hash = incrementalHash.GetHashAndReset();
                    /*algorithm.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0);
                    algorithm.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
                    hash = algorithm.Hash;*/ //todo verify correctness;
                }

                // most bytes from the hash are copied straight to the bytes of the new GUID (steps 5-7, 9, 11-12)
                byte[] newGuid = new byte[16];
                Array.Copy(hash, 0, newGuid, 0, 16);

                // set the four most significant bits (bits 12 through 15) of the time_hi_and_version field to the appropriate 4-bit version number from Section 4.1.3 (step 8)
                newGuid[6] = (byte)((newGuid[6] & 0x0F) | (version << 4));

                // set the two most significant bits (bits 6 and 7) of the clock_seq_hi_and_reserved to zero and one, respectively (step 10)
                newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

                // convert the resulting UUID to local byte order (step 13)
                GuidUtility.SwapByteOrder(newGuid);
                return new Guid(newGuid);
            }

            /// <summary>
            /// The namespace for fully-qualified domain names (from RFC 4122, Appendix C).
            /// </summary>
            public static readonly Guid DnsNamespace = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

            /// <summary>
            /// The namespace for URLs (from RFC 4122, Appendix C).
            /// </summary>
            public static readonly Guid UrlNamespace = new Guid("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

            /// <summary>
            /// The namespace for ISO OIDs (from RFC 4122, Appendix C).
            /// </summary>
            public static readonly Guid IsoOidNamespace = new Guid("6ba7b812-9dad-11d1-80b4-00c04fd430c8");

            // Converts a GUID (expressed as a byte array) to/from network order (MSB-first).
            internal static void SwapByteOrder(byte[] guid)
            {
                GuidUtility.SwapBytes(guid, 0, 3);
                GuidUtility.SwapBytes(guid, 1, 2);
                GuidUtility.SwapBytes(guid, 4, 5);
                GuidUtility.SwapBytes(guid, 6, 7);
            }

            private static void SwapBytes(byte[] guid, int left, int right)
            {
                byte temp = guid[left];
                guid[left] = guid[right];
                guid[right] = temp;
            }
        }
    }
}