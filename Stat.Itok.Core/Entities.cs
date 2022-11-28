using System.Runtime.Serialization;
using Azure.Data.Tables;
using Azure;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Stat.Itok.Core;

// ReSharper disable once InconsistentNaming
public record IMinkFCalcApiResp
{
    public string F { get; set; }
    public string RequestId { get; set; }
    public string Timestamp { get; set; }
}

public enum BattleType
{
    Vs,
    Coop
}

public class BattleGroupAndIds
{
    public string RawBattleGroup { get; set; }
    public IList<string> BattleIds { get; set; } = new List<string>();
}

public enum StatInkBoolean
{
    [EnumMember(Value = "no")]
    No,

    [EnumMember(Value = "yes")]
    Yes,
}

public enum StatInkLobby
{
    [EnumMember(Value = "regular")]
    Regular,

    [EnumMember(Value = "bankara_challenge")]
    BankaraChallenge,

    [EnumMember(Value = "bankara_open")]
    BankaraOpen,

    [EnumMember(Value = "splatfest_challenge")]
    SplatFestChallenge,

    [EnumMember(Value = "splatfest_open")]
    SplatFestOpen,

    [EnumMember(Value = "private")]
    Private,
}

public enum StatInkResult
{
    [EnumMember(Value = "win")]
    Win,

    [EnumMember(Value = "lose")]
    Lose,

    [EnumMember(Value = "draw")]
    Draw,

    [EnumMember(Value = "exempted_lose")]
    ExemptedLose,
}

public enum StatInkRule
{
    [EnumMember(Value = "nawabari")]
    Nawabari,

    [EnumMember(Value = "area")]
    Area,

    [EnumMember(Value = "hoko")]
    Hoko,

    [EnumMember(Value = "yagura")]
    Yagura,

    [EnumMember(Value = "asari")]
    Asari,
}

public record StatInkBattleBody
{
    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("test")]
    public StatInkBoolean Test { get; set; }

    [JsonProperty("uuid")]
    public string UUID { get; set; }

    [JsonProperty("image_judge")]
    public string ImageJudge { get; set; }

    [JsonProperty("image_result")]
    public string ImageResult { get; set; }

    [JsonProperty("image_gear")]
    public string ImageGear { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("lobby")]
    public StatInkLobby Lobby { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("rule")]
    public StatInkRule Rule { get; set; }

    [JsonProperty("stage")]
    public string Stage { get; set; }

    [JsonProperty("weapon")]
    public string Weapon { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("result")]
    public StatInkResult Result { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("knockout")]
    public StatInkBoolean Knockout { get; set; }

    [JsonProperty("rank_in_team")]
    public int? RankInTeam { get; set; }

    [JsonProperty("kill")]
    public int? Kill { get; set; }

    [JsonProperty("assist")]
    public int? Assist { get; set; }

    [JsonProperty("kill_or_assist")]
    public int? KillOrAssist { get; set; }

    [JsonProperty("death")]
    public int? Death { get; set; }

    [JsonProperty("special")]
    public int? Special { get; set; }

    [JsonProperty("inked")]
    public int? Inked { get; set; }

    [JsonProperty("medals")]
    public List<string> Medals { get; set; } = new List<string>();

    [JsonProperty("our_team_inked")]
    public int? OurTeamInked { get; set; }

    [JsonProperty("their_team_inked")]
    public int? TheirTeamInked { get; set; }

    [JsonProperty("our_team_percent")]
    public decimal? OurTeamPercent { get; set; }

    [JsonProperty("their_team_percent")]
    public decimal? TheirTeamPercent { get; set; }

    [JsonProperty("our_team_count")]
    public int? OurTeamCount { get; set; }

    [JsonProperty("their_team_count")]
    public int? TheirTeamCount { get; set; }

    [JsonProperty("level_before")]
    public int? LevelBefore { get; set; }

    [JsonProperty("level_after")]
    public int? LevelAfter { get; set; }

    [JsonProperty("rank_before")]
    public string RankBefore { get; set; }

    [JsonProperty("rank_before_s_plus")]
    public int? RankBeforeSPlus { get; set; }

    [JsonProperty("rank_before_exp")]
    public string RankBeforeExp { get; set; }

    [JsonProperty("rank_after")]
    public string RankAfter { get; set; }

    [JsonProperty("rank_after_s_plus")]
    public int? RankAfterSPlus { get; set; }

    [JsonProperty("rank_after_exp")]
    public string RankAfterExp { get; set; }

    [JsonProperty("rank_exp_change")]
    public int? RankExpChange { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("rank_up_battle")]
    public StatInkBoolean RankUpBattle { get; set; }

    [JsonProperty("challenge_win")]
    public int? ChallengeWin { get; set; }

    [JsonProperty("challenge_lose")]
    public int? ChallengeLose { get; set; }

    [JsonProperty("fest_power")]
    public decimal? FestPower { get; set; }

    [JsonProperty("fest_dragon")]
    public string FestDragon { get; set; }

    [JsonProperty("clout_before")]
    public int? ClountBefore { get; set; }

    [JsonProperty("clout_after")]
    public int? ClountAfter { get; set; }

    [JsonProperty("clout_change")]
    public int? ClountChange { get; set; }

    [JsonProperty("cash_before")]
    public int? CashBefore { get; set; }

    [JsonProperty("cash_after")]
    public int? CashAfter { get; set; }

    [JsonProperty("our_team_players")]
    public List<StatInkPlayer> OurTeamPlayers { get; set; } = new();

    [JsonProperty("their_team_players")]
    public List<StatInkPlayer> TheirTeamPlayers { get; set; } = new();

    [JsonProperty("note")]
    public string Note { get; set; }

    [JsonProperty("private_note")]
    public string PrivateNote { get; set; }

    [JsonProperty("link_url")]
    public string LinkUrl { get; set; }

    [JsonProperty("agent")]
    public string Agent { get; set; } = "itok.stat";

    [JsonProperty("agent_version")]
    public string AgentVersion { get; set; }

    [JsonProperty("agent_variables")]
    public Dictionary<string, string> AgentVariables { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("automated")]
    public StatInkBoolean Automated { get; set; }

    [JsonProperty("start_at")]
    public long StartAt { get; set; }

    [JsonProperty("end_at")]
    public long EndAt { get; set; }
}

public class StatInkPlayer
{
    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("me")]
    public StatInkBoolean Me { get; set; }

    [JsonProperty("rank_in_team")]
    public int? RankInTeam { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("number")]
    public string Number { get; set; }

    [JsonProperty("splashtag_title")]
    public string SplashtagTitle { get; set; }

    [JsonProperty("weapon")]
    public string Weapon { get; set; }

    [JsonProperty("inked")]
    public int? Inked { get; set; }

    [JsonProperty("kill")]
    public int? Kill { get; set; }

    [JsonProperty("assist")]
    public int? Assist { get; set; }

    [JsonProperty("kill_or_assist")]
    public int? KillOrAssist { get; set; }

    [JsonProperty("death")]
    public int? Death { get; set; }

    [JsonProperty("special")]
    public int? Special { get; set; }

    [JsonProperty("gears")]
    public StatInkGears Gears { get; set; } = new StatInkGears();

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("disconnected")]
    public StatInkBoolean Disconnected { get; set; }
}

public class StatInkGears
{
    [JsonProperty("headgear")]
    public StatInkGear HeadGear { get; set; }

    [JsonProperty("clothing")]
    public StatInkGear Clothing { get; set; }

    [JsonProperty("shoes")]
    public StatInkGear Shoes { get; set; }
}

public class StatInkGear
{
    [JsonProperty("primary_ability")]
    public string PrimaryAbility { get; set; }

    [JsonProperty("secondary_abilities")]
    public List<string> SecondaryAbility { get; set; } = new List<string>();
}

public class StatInkPostBattleSuccess
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }
}

public record JobConfig : JobConfigLite, ITableEntity
{
    [IgnoreDataMember]
    public new NinAuthContext NinAuthContext
    {
        get
        {
            return
                string.IsNullOrEmpty(NinAuthContextStr)
                    ? null
                    : JsonConvert.DeserializeObject<NinAuthContext>(Helper.DecompressStr(NinAuthContextStr));
        }
        set { NinAuthContextStr = Helper.CompressStr(JsonConvert.SerializeObject(value)); }
    }

    public string NinAuthContextStr { get; set; }

    [IgnoreDataMember]
    public new List<string> EnabledQueries
    {
        get
        {
            return
                string.IsNullOrEmpty(EnabledQueriesStr)
                    ? new List<string>()
                    : JsonConvert.DeserializeObject<List<string>>(Helper.DecompressStr(EnabledQueriesStr));
        }
        set { EnabledQueriesStr = Helper.CompressStr(JsonConvert.SerializeObject(value)); }
    }

    public string EnabledQueriesStr { get; set; }

    /// <summary>
    /// nameof(JobConfig)
    /// </summary>
    public string PartitionKey { get; set; }

    /// <summary>
    /// $"nin_user_{this.NinAuthContext.UserInfo.Id}";
    /// </summary>
    public string RowKey { get; set; }

    public DateTimeOffset? Timestamp { get; set; } = DateTimeOffset.Now;
    public ETag ETag { get; set; }
}

public record JobRun : ITableEntity
{
    public long TrackedId { get; set; }
    public string JobConfigId { get; set; }

    /// <summary>
    /// JobConfigId
    /// </summary>
    public string PartitionKey { get; set; }

    /// <summary>
    /// invertedTimeKey
    /// </summary>
    public string RowKey { get; set; }

    public DateTimeOffset? Timestamp { get; set; } = DateTimeOffset.Now;
    public ETag ETag { get; set; }
}

public class JobRunTaskPayload : ITableEntity
{
    public string CompressedPayload { get; set; }
    public string PartitionKey { get; set; }
    public string RowKey  { get; set; }
    public DateTimeOffset? Timestamp  { get; set; }
    public ETag ETag  { get; set; }
}

public class JobRunTaskLite
{
    public long TrackedId { get; set; }
    public string Pk { get; set; }
    public string Rk { get; set; }
}


public class BattleTaskPayload
{
    public string JobConfigId { get; set; }
    public long JobRunTrackedId { get; set; }
    public long TrackedId { get; set; }
    public string BattleGroupRawStr { get; set; }
    public string BattleIdRawStr { get; set; }
}

public class RawBattleCacheEntity
{
    public string JobConfigId { get; set; }
    public string StatInkBattleId { get; set; }
    public string StatInkWebBattleId { get; set; }
    public string StatInkWebUrl { get; set; }
    public string BattleIdRawStr { get; set; }
    public string BattleGroupRawStr { get; set; }
    public string BattleDetailRawStr { get; set; }
}