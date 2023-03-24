using System.Numerics;
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

    [EnumMember(Value = "xmatch")]
    XMatch
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

    [EnumMember(Value = "tricolor")]
    TriColor
}

public record StatInkBattleBody
{
    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("test")]
    public StatInkBoolean Test { get; set; } = StatInkBoolean.No;

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

    [JsonProperty("third_team_inked")]
    public int? ThirdTeamInked { get; set; }

    [JsonProperty("our_team_percent")]
    public decimal? OurTeamPercent { get; set; }

    [JsonProperty("their_team_percent")]
    public decimal? TheirTeamPercent { get; set; }

    [JsonProperty("third_team_percent")]
    public decimal? ThirdTeamPercent { get; set; }

    [JsonProperty("our_team_count")]
    public int? OurTeamCount { get; set; }

    [JsonProperty("their_team_count")]
    public int? TheirTeamCount { get; set; }

    //[JsonProperty("third_team_count")]
    //public decimal? ThirdTeamCount { get; set; }

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

    [JsonProperty("third_team_players")]
    public List<StatInkPlayer> ThirdTeamPlayers { get; set; } = new();

    [JsonProperty("note")]
    public string Note { get; set; }

    [JsonProperty("private_note")]
    public string PrivateNote { get; set; }

    [JsonProperty("link_url")]
    public string LinkUrl { get; set; }

    [JsonProperty("agent")]
    public string Agent { get; set; } = "stat.itok";

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

    [JsonProperty("x_power_before")]
    public decimal? XPowerBefore { get; set; }

    [JsonProperty("x_power_after")]
    public decimal? XPowerAfter { get; set; }

    [JsonProperty("our_team_color")]
    public string OurTeamColor { get; set; }

    [JsonProperty("their_team_color")]
    public string TheirTeamColor { get; set; }

    [JsonProperty("third_team_color")]
    public string ThirdTeamColor { get; set; }

    [JsonProperty("our_team_role")]
    public string OurTeamRole { get; set; }

    [JsonProperty("their_team_role")]
    public string TheirTeamRole { get; set; }

    [JsonProperty("third_team_role")]
    public string ThirdTeamRole { get; set; }

    [JsonProperty("our_team_theme")]
    public string OurTeamTheme { get; set; }

    [JsonProperty("their_team_theme")]
    public string TheirTeamTheme { get; set; }

    [JsonProperty("third_team_theme")]
    public string ThirdTeamTheme { get; set; }
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

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("crown")]
    public StatInkBoolean? Crown { get; set; }
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

public class StatInkPostBodySuccess
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }
}

public enum RunBattleTaskStatus
{
    Ok = 0,
    BattleBodyIsNull = 1
}

public record JobRun
{
    public long TrackedId { get; set; }
    public string JobConfigId { get; set; }
}

public class JobRunTaskLite
{
    public long TrackedId { get; set; }
    public string PayloadId { get; set; }
}

public class BattleTaskPayload
{
    public string JobConfigId { get; set; }
    public long JobRunTrackedId { get; set; }
    public long TrackedId { get; set; }
    public string BattleGroupRawStr { get; set; }
    public string BattleIdRawStr { get; set; }
}

public class BattleTaskDebugContext
{
    public string FilePath => $"{JobConfigId}/{StatInkBattleId}.json";
    public string JobConfigId { get; set; }
    public string StatInkBattleId { get; set; }
    public StatInkBattleBody StatInkBattleBody { get; set; }
    public StatInkSalmonBody StatInkSalmonBody { get; set; }
    public StatInkPostBodySuccess StatInkPostBodySuccess { get; set; }
    public string BattleIdRawStr { get; set; }
    public string BattleGroupRawStr { get; set; }
    public string BattleDetailRawStr { get; set; }
    public string PayloadType { get; set; }
}

public class PoisonQueueMsg
{
}

/// <summary>
/// https://github.com/fetus-hina/stat.ink/wiki/Spl3-API:-Salmon-%EF%BC%8D-Post
/// </summary>
public record StatInkSalmonBody
{
    [JsonProperty("uuid")]
    public string Uuid { get; set; }

    [JsonProperty("private")]
    [JsonConverter(typeof(StringEnumConverter))]
    public StatInkBoolean IsPrivate { get; set; }

    [JsonProperty("big_run")]
    [JsonConverter(typeof(StringEnumConverter))]
    public StatInkBoolean IsBigRun { get; set; }

    [JsonProperty("stage")]
    public string Stage { get; set; }

    [JsonProperty("danger_rate")]
    public double? DangerRate { get; set; }

    [JsonProperty("clear_waves")]
    public int? ClearWaves { get; set; }

    [JsonProperty("fail_reason")]
    public string FailReason { get; set; }

    [JsonProperty("king_smell")]
    public int? KingSmell { get; set; }

    [JsonProperty("king_salmonid")]
    public string KingSalmonId { get; set; }

    [JsonProperty("clear_extra")]
    [JsonConverter(typeof(StringEnumConverter))]
    public StatInkBoolean ClearExtra { get; set; }

    [JsonProperty("title_before")]
    public string TitleBefore { get; set; }

    [JsonProperty("title_exp_before")]
    public int? TitleExpBefore { get; set; }

    [JsonProperty("title_after")]
    public string TitleAfter { get; set; }

    [JsonProperty("title_exp_after")]
    public int? TitleExpAfter { get; set; }

    [JsonProperty("golden_eggs")]
    public int? GoldenEggs { get; set; }

    [JsonProperty("power_eggs")]
    public int? PowerEggs { get; set; }

    [JsonProperty("gold_scale")]
    public int? GoldScale { get; set; }

    [JsonProperty("silver_scale")]
    public int? SilverScale { get; set; }
    
    [JsonProperty("bronze_scale")]
    public int? BronzeScale { get; set; }

    [JsonProperty("job_point")]
    public int? JobPoint { get; set; }

    [JsonProperty("job_score")]
    public int? JobScore { get; set; }

    [JsonProperty("job_rate")]
    public double? JobRate { get; set; }

    [JsonProperty("job_bonus")]
    public int? JobBonus { get; set; }

    [JsonProperty("waves")]
    public List<Wave> Waves { get; set; }

    [JsonProperty("players")]
    public List<SalmonPlayer> Players { get; set; }

    [JsonProperty("bosses")]
    public Dictionary<string, Boss> Bosses { get; set; }

    [JsonProperty("note")]
    public string Note { get; set; }

    [JsonProperty("private_note")]
    public string PrivateNote { get; set; }

    [JsonProperty("link_url")]
    public string LinkUrl { get; set; }

    [JsonProperty("agent")]
    public string Agent { get; set; } = "stat.itok";

    [JsonProperty("agent_version")]
    public string AgentVersion { get; set; } = StatItokConstants.StatVersion;

    [JsonProperty("agent_variables")]
    public Dictionary<string, string> AgentVariables { get; set; }

    [JsonProperty("automated")]
    [JsonConverter(typeof(StringEnumConverter))]
    public StatInkBoolean Automated { get; set; } = StatInkBoolean.Yes;

    [JsonProperty("start_at")]
    public long? StartAt { get; set; }

    [JsonProperty("end_at")]
    public long? EndAt { get; set; }
}

public record Boss
{
    [JsonProperty("appearances")]
    public int? Appearances { get; set; }

    [JsonProperty("defeated")]
    public int? Defeated { get; set; }

    [JsonProperty("defeated_by_me")]
    public int? DefeatedByMe { get; set; }
}

public record Wave
{
    [JsonProperty("tide")]
    public string Tide { get; set; }

    [JsonProperty("event")]
    public string Event { get; set; }

    [JsonProperty("golden_quota")]
    public int? GoldenQuota { get; set; }

    [JsonProperty("golden_delivered")]
    public int? GoldenDelivered { get; set; }

    [JsonProperty("golden_appearances")]
    public int? GoldenAppearances { get; set; }

    [JsonProperty("special_uses")]
    public Dictionary<string, int> SpecialUses { get; set; }
}

public record SalmonPlayer
{
    [JsonProperty("me")]
    [JsonConverter(typeof(StringEnumConverter))]
    public StatInkBoolean Me { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("number")]
    public string Number { get; set; }

    [JsonProperty("splashtag_title")]
    public string SplashTagTitle { get; set; }

    [JsonProperty("uniform")]
    public string Uniform { get; set; }

    [JsonProperty("special")]
    public string Special { get; set; }

    [JsonProperty("weapons")]
    public List<string> Weapons { get; set; }

    [JsonProperty("golden_eggs")]
    public int? GoldenEggs { get; set; }

    [JsonProperty("golden_assist")]
    public int? GoldenAssist { get; set; }

    [JsonProperty("power_eggs")]
    public int? PowerEggs { get; set; }

    [JsonProperty("rescue")]
    public int? Rescue { get; set; }

    [JsonProperty("rescued")]
    public int? Rescued { get; set; }

    [JsonProperty("defeat_boss")]
    public int? DefeatBoss { get; set; }

    [JsonProperty("disconnected")]
    [JsonConverter(typeof(StringEnumConverter))]
    public StatInkBoolean Disconnected { get; set; } = StatInkBoolean.No;
}