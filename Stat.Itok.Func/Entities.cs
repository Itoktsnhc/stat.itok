using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Newtonsoft.Json;
using Stat.Itok.Core;
using Stat.Itok.Shared;

namespace Stat.Itok.Func;



public record JobConfig : JobConfigLite, ITableEntity
{
    [IgnoreDataMember]
    public new NinAuthContext NinAuthContext
    {
        get
        {
            return
                string.IsNullOrEmpty(NinAuthContextStr) ?
                null
                : JsonConvert.DeserializeObject<NinAuthContext>(Helper.DecompressStr(NinAuthContextStr));
        }
        set
        {
            NinAuthContextStr = Helper.CompressStr(JsonConvert.SerializeObject(value));
        }
    }
    public string NinAuthContextStr { get; set; }

    [IgnoreDataMember]
    public new IList<string> EnabledQueries
    {
        get
        {
            return
                string.IsNullOrEmpty(EnabledQueriesStr) ?
                new List<string>()
                : JsonConvert.DeserializeObject<IList<string>>(Helper.DecompressStr(EnabledQueriesStr));
        }
        set
        {
            EnabledQueriesStr = Helper.CompressStr(JsonConvert.SerializeObject(value));
        }
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

public record JobRunHistory : JobRunHistoryLite, ITableEntity
{
    /// <summary>
    /// StatInk UUID -> Returned BattleId
    /// </summary>
    [IgnoreDataMember]
    public new Dictionary<string, string> BattleIdDict
    {
        get
        {
            return
                string.IsNullOrEmpty(BattleIdStr) ?
                new Dictionary<string, string>()
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(BattleIdStr);
        }
        set
        {
            BattleIdStr = JsonConvert.SerializeObject(value);
        }
    }

    public string BattleIdStr { get; set; }

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