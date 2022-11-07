using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Data.Tables;
using MediatR;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stat.Itok.Core;
using Stat.Itok.Core.Handlers;

namespace Stat.Itok.Func.Functions;

public class JobExecutor
{
    private readonly IMediator _mediator;
    private readonly ILogger<NinAuthFunctions> _logger;
    private readonly IStorageAccessSvc _storage;
    private readonly IOptions<GlobalConfig> _options;

    public JobExecutor(
        IMediator mediator,
        ILogger<NinAuthFunctions> logger,
        IStorageAccessSvc storage,
        IOptions<GlobalConfig> options)
    {
        _mediator = mediator;
        _logger = logger;
        _storage = storage;
        _options = options;
    }

    [FunctionName("JobExecutor")]
    public async Task RunAsync([TimerTrigger("0 */10 * * * *", RunOnStartup = true)] TimerInfo timerInfo)
    {
        _logger.LogWarning("JobExecutor RUNNING");
        var jobConfigTable = await _storage.GetTableClientAsync<JobConfig>();
        var queryRes = jobConfigTable.QueryAsync<JobConfig>(x => x.PartitionKey == nameof(JobConfig) && x.Enabled);
        var jobs = new List<JobConfig>();
        await foreach (var item in queryRes)
        {
            jobs.Add(item);
        }
        _logger.LogInformation("JobExecutor GOT {N} Records", jobs.Count);
        if (jobs.Count <= 0) return;
        var gearsInfoDict = await _mediator.Send(new ReqGetGearsInfo());
        foreach (var job in jobs)
        {
            var newJob = await ExecuteJobAsync(job, gearsInfoDict);
            try
            {
                await jobConfigTable.UpsertEntityAsync(newJob);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when UpdateJob after execute");
            }
        }
    }

    private async Task<JobConfig> ExecuteJobAsync(JobConfig jobConfig, Dictionary<string, string> gearsInfo)
    {
        var hisTable = await _storage.GetTableClientAsync<JobRunHistory>();
        var lastHis = await GetLastestJobRunHistoryAsync(jobConfig, hisTable);
        var lastBattleIdDict = lastHis == null ? new Dictionary<string, string>() : lastHis.BattleIdDict;
        var jobRunHis = new JobRunHistory
        {
            StartAt = DateTimeOffset.Now,
            Status = TaskStatus.WaitingToRun,
            JobConfigId = jobConfig.Id,
        };
        try
        {
            var checkRes = await _mediator.Send(new ReqPreCheck() { AuthContext = jobConfig.NinAuthContext });
            if (checkRes.Result == PreCheckResult.NeedBuildFromBegin)
            {
                jobRunHis.PreCheckResult = PreCheckResult.NeedBuildFromBegin;
                jobRunHis.Status = TaskStatus.Faulted;
                throw new Exception("PreCheckResult.NeedBuildFromBegin");
            }
            jobConfig.NinAuthContext = checkRes.AuthContext;
            jobRunHis.Status = TaskStatus.RanToCompletion;// assum ok result;
            var newBattleIdDict = new Dictionary<string, string>();
            foreach (var queryName in jobConfig.EnabledQueries)
            {
                var battleIdDict = await UploadPlayHistoriesAsync(queryName, lastBattleIdDict, jobRunHis, jobConfig, gearsInfo);
                foreach (var (bodyBattleId, WebBattleId) in battleIdDict)
                {
                    newBattleIdDict[bodyBattleId] = WebBattleId;
                }
            }
            jobRunHis.BattleIdDict = newBattleIdDict;
        }
        catch (Exception ex)
        {
            jobRunHis.Status = TaskStatus.Faulted;
            _logger.LogError(ex, "Error when Execution Job");
        }
        finally
        {
            jobRunHis.EndAt = DateTimeOffset.Now;
            await UploadJobRunHistoryAsync(jobRunHis, hisTable);
        }
        return jobConfig;
    }

    private async Task<Dictionary<string, string>> UploadPlayHistoriesAsync(string queryName,
        Dictionary<string, string> hisBattleIds,
        JobRunHistory jobRunHis,
        JobConfig jobConfig, Dictionary<string, string> gearsInfoDict)
    {
        try
        {
            switch (queryName)
            {
                case nameof(QueryHash.BankaraBattleHistories):
                    return await DoVsBattleUploadAsync(QueryHash.BankaraBattleHistories, hisBattleIds, jobRunHis, jobConfig, gearsInfoDict);
                case nameof(QueryHash.RegularBattleHistories):
                    return await DoVsBattleUploadAsync(QueryHash.RegularBattleHistories, hisBattleIds, jobRunHis, jobConfig, gearsInfoDict);
                default:
                    _logger.LogError("NoSupportedQuery:{querName}", queryName);
                    jobRunHis.Status = TaskStatus.Faulted;
                    break;
            }
        }
        catch (Exception ex)
        {
            jobRunHis.Status = TaskStatus.Faulted;
            _logger.LogError(ex, "Error when do query:{queryName}", queryName);
        }
        return new Dictionary<string, string>();
    }

    private async Task<Dictionary<string, string>> DoVsBattleUploadAsync(
        string queryHash,
        Dictionary<string, string> hisBattleIdDict,
        JobRunHistory jobRunHis,
        JobConfig jobConfig,
        Dictionary<string, string> gearInfoDict)
    {
        var newBattleIdDict = new Dictionary<string, string>();
        var groupRes = await _mediator.Send(new ReqDoGraphQL()
        {
            AuthContext = jobConfig.NinAuthContext,
            QueryHash = queryHash,
        });
        var battleAndIds = StatHelper.ExtractBattleIds(groupRes, queryHash);
        foreach (var battleGroup in battleAndIds)
        {
            foreach (var battleId in battleGroup.BattleIds)
            {
                var statInkBattleId = StatHelper.GetBattleIdForStatInk(battleId);
                if (!jobConfig.ForceOverride && hisBattleIdDict.ContainsKey(statInkBattleId))
                {
                    newBattleIdDict[statInkBattleId] = hisBattleIdDict[statInkBattleId];
                    _logger.LogInformation("Skipping {battleId} due to previous upload");
                    continue;
                }
                var detailRes = await _mediator.Send(new ReqDoGraphQL()
                {
                    AuthContext = jobConfig.NinAuthContext,
                    QueryHash = QueryHash.VsHistoryDetail,
                    VarName = "vsResultId",
                    VarValue = battleId
                });

                var battleBody = StatHelper.BuildStatInkBattleBody(
                    detailRes,
                    battleGroup.RawBattleGroup,
                    jobConfig.NinAuthContext.UserInfo.Lang, gearInfoDict);
                var resp = await _mediator.Send(new ReqPostBattle()
                {
                    ApiKey = jobConfig.StatInkApiKey,
                    Body = battleBody,
                });
                newBattleIdDict[statInkBattleId] = resp.Id;
            }
        }
        return newBattleIdDict;
    }

    private async Task<JobRunHistory> GetLastestJobRunHistoryAsync(JobConfig job, TableClient hisTable)
    {
        await foreach (var his in hisTable.QueryAsync<JobRunHistory>(x => x.PartitionKey == job.RowKey))
        {
            return his;
        }
        return null;
    }


    private async Task UploadJobRunHistoryAsync(JobRunHistory his, TableClient hisTable)
    {
        try
        {
            his.PartitionKey = his.JobConfigId;
            his.RowKey = (DateTime.MaxValue.Ticks - his.StartAt!.Value.Ticks).ToString();
            await hisTable.UpsertEntityAsync(his);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when UploadJobRunHistory");
        }
    }
}