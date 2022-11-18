using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Queues.Models;
using JobTrackerX.Client;
using JobTrackerX.SharedLibs;
using Mapster;
using MediatR;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Stat.Itok.Core;
using Stat.Itok.Core.Handlers;

namespace Stat.Itok.Func.Worker;

public class JobDispatcher
{
    private readonly IMediator _mediator;
    private readonly ILogger<JobDispatcher> _logger;
    private readonly IStorageAccessSvc _storage;
    private readonly IOptions<GlobalConfig> _options;
    private readonly IJobTrackerClient _jobTracker;
    private readonly IMemoryCache _memCache;

    public JobDispatcher(
        IMediator mediator,
        ILogger<JobDispatcher> logger,
        IStorageAccessSvc storage,
        IOptions<GlobalConfig> options,
        IJobTrackerClient jobTracker,
        IMemoryCache memCache)
    {
        _mediator = mediator;
        _logger = logger;
        _storage = storage;
        _options = options;
        _jobTracker = jobTracker;
        _memCache = memCache;
    }

    [FunctionName("JobDispatcher")]
    public async Task ActJobDispatcher([TimerTrigger("0 */3 * * * *", RunOnStartup = true)] TimerInfo timerInfo)
    {
        var jobConfigTable = await _storage.GetTableClientAsync<JobConfig>();
        var queryRes = jobConfigTable.QueryAsync<JobConfig>(x => x.PartitionKey == nameof(JobConfig) && x.Enabled);
        var jobs = new List<JobConfig>();
        await foreach (var item in queryRes)
        {
            jobs.Add(item);
        }

        _logger.LogInformation("JobExecutor GOT {N} Records", jobs.Count);
        if (jobs.Count <= 0) return;

        foreach (var job in jobs)
        {
            try
            {
                await DispatchJobRunAsync(job, jobConfigTable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when DispatchJobRunAsync");
            }
        }
    }

    private async Task DispatchJobRunAsync(JobConfig jobConfig, TableClient jobConfigTable)
    {
        var checkRes = await _mediator.Send(new ReqPreCheck { AuthContext = jobConfig.NinAuthContext });
        if (checkRes.Result == PreCheckResult.NeedBuildFromBegin)
        {
            throw new Exception("PreCheckResult.NeedBuildFromBegin");
        }

        jobConfig.NinAuthContext = checkRes.AuthContext;
        await jobConfigTable.UpsertEntityAsync(jobConfig);

        var jobRunTaskList = new List<BattleTaskPayload>();
        foreach (var queryName in jobConfig.EnabledQueries)
        {
            switch (queryName)
            {
                case nameof(QueryHash.BankaraBattleHistories):
                    jobRunTaskList.AddRange(await GetJobRunTasksAsync(QueryHash.BankaraBattleHistories, jobConfig));
                    break;
                case nameof(QueryHash.RegularBattleHistories):
                    jobRunTaskList.AddRange(await GetJobRunTasksAsync(QueryHash.RegularBattleHistories, jobConfig));
                    break;
                default:
                    _logger.LogError("NoSupportedQuery:{queryName}", queryName);
                    break;
            }
        }

        await DispatchJobRunTasksAsync(jobConfig, jobRunTaskList);
    }

    private async Task<IList<BattleTaskPayload>> GetJobRunTasksAsync(
        string queryHash, JobConfig jobConfig)
    {
        var groupRes = await _mediator.Send(new ReqDoGraphQL
        {
            AuthContext = jobConfig.NinAuthContext,
            QueryHash = queryHash,
        });
        var jobRunTaskList = StatHelper.ExtractBattleIds(groupRes, queryHash)
            .SelectMany(x => x.BattleIds.Select(y => new BattleTaskPayload
            {
                BattleGroupRawStr = x.RawBattleGroup,
                BattleIdRawStr = y,
                JobConfigId = jobConfig.Id
            })).ToList();
        return jobRunTaskList;
    }

    private async Task SetJobRunTaskPayloadAsync<T>(string pk, string rk, T payloadObj)
    {
        var payloadTable = await _storage.GetTableClientAsync<JobRunTaskPayload>();
        await payloadTable.UpsertEntityAsync(new JobRunTaskPayload()
        {
            CompressedPayload = Helper.CompressStr(JsonConvert.SerializeObject(payloadObj)),
            PartitionKey = pk,
            RowKey = rk
        });
    }

    private async Task DispatchJobRunTasksAsync(JobConfig jobConfig, IList<BattleTaskPayload> tasks)
    {
        tasks = await ExcludeExistBattlesAsync(jobConfig, tasks);
        if (!tasks.Any())
        {
            _logger.LogInformation("No new battle Find");
            return;
        }
        var runTable = await _storage.GetTableClientAsync<JobRun>();
        var newJobDto = new AddJobDto($"[{nameof(JobRun)}] for [{jobConfig.NinAuthContext.UserInfo.Nickname}]")
        {
            Tags = new List<string> { "stat.itok" }
        };
        var tJob = await _jobTracker.CreateNewJobAsync(newJobDto);
        var jobRun = new JobRun
        {
            TrackedId = tJob.JobId,
            JobConfigId = jobConfig.Id,
            PartitionKey = jobConfig.Id,
            RowKey = (long.MaxValue - tJob.JobId).ToString()
        };
        await runTable.UpsertEntityAsync(jobRun);
        await _jobTracker.UpdateJobStatesAsync(tJob.JobId,
            new UpdateJobStateDto(JobState.WaitingToRun, "JobRun Saved"));

        try
        {
            foreach (var battleTask in tasks)
            {
                battleTask.JobConfigId = jobConfig.Id;
                battleTask.JobRunTrackedId = battleTask.JobRunTrackedId;

                var addJobDto = new AddJobDto(nameof(BattleTaskPayload),
                    jobRun.TrackedId)
                {
                    Options = StatHelper.GetBattleIdForStatInk(battleTask.BattleIdRawStr)
                };
                var tJobRunTask =
                    await _jobTracker.CreateNewJobAsync(addJobDto);
                battleTask.TrackedId = tJobRunTask.JobId;
                try
                {
                    var queueClient = await _storage.GeJobRunTaskQueueClientAsync();
                    var jobRunTaskLite = battleTask.Adapt<JobRunTaskLite>();
                    jobRunTaskLite.Pk = battleTask.JobConfigId;
                    jobRunTaskLite.Rk = StatHelper.GetBattleIdForStatInk(battleTask.BattleIdRawStr);
                    var resp = await queueClient
                        .SendMessageAsync(
                            Helper.CompressStr(JsonConvert.SerializeObject(jobRunTaskLite)));
                    await _jobTracker.UpdateJobStatesAsync(battleTask.TrackedId,
                        new UpdateJobStateDto(JobState.WaitingToRun, $"queue message Id:{resp.Value.MessageId}"));
                    await SetJobRunTaskPayloadAsync(jobRunTaskLite.Pk, jobRunTaskLite.Rk, battleTask);
                }
                catch (Exception ex)
                {
                    await _jobTracker.UpdateJobStatesAsync(battleTask.TrackedId,
                        new UpdateJobStateDto(JobState.Faulted, ex.Message));
                }
            }
        }
        catch (Exception e)
        {
            await _jobTracker.UpdateJobStatesAsync(tJob.JobId, new UpdateJobStateDto(JobState.Faulted, e.Message));
        }

        await _jobTracker.UpdateJobStatesAsync(tJob.JobId,
            new UpdateJobStateDto(JobState.WaitingForChildrenToComplete));
    }

    private async Task<IList<BattleTaskPayload>> ExcludeExistBattlesAsync(JobConfig jobConfig, IList<BattleTaskPayload> tasks)
    {
        var res = new ConcurrentBag<BattleTaskPayload>();
        if (!tasks.Any()) return res.ToList();
        var payloadTable = await _storage.GetTableClientAsync<JobRunTaskPayload>();
        var checkTasks = tasks.Select(async task =>
        {
            var existBattleId =
               await payloadTable.GetEntityIfExistsAsync<JobRunTaskPayload>(jobConfig.Id,
                StatHelper.GetBattleIdForStatInk(task.BattleIdRawStr));
            if (existBattleId.HasValue)
            {
                _logger.LogInformation("{jobConfigId}: ignoring exist battle {battleId} ",
                    jobConfig.Id, StatHelper.GetBattleIdForStatInk(task.BattleIdRawStr));
                return;
            }
            res.Add(task);
        });
        await Task.WhenAll(checkTasks);
        return res.ToList();
    }

    [FunctionName("JobWorker")]
    public async Task ActJobWorkerAsync(
        [QueueTrigger(StatItokConstants.JobRunTaskQueueName, Connection = "WorkerQueueConnStr")]
        QueueMessage queueMsg)
    {
        var msgStr = Helper.DecompressStr(queueMsg.MessageText);
        var jobRunTaskLite = JsonConvert.DeserializeObject<JobRunTaskLite>(msgStr);
        var task = await GetJobRunTaskPayloadAsync<BattleTaskPayload>(jobRunTaskLite!.Pk, jobRunTaskLite.Rk);
        await _jobTracker.UpdateJobStatesAsync(jobRunTaskLite.TrackedId,
            new UpdateJobStateDto(JobState.Running, $"{queueMsg.MessageId};{queueMsg.PopReceipt};[{queueMsg.DequeueCount}]"));
        if (task == null)
        {
            var info = $"JobRunTaskPayload not found:{jobRunTaskLite.Pk}:{jobRunTaskLite.Rk}, go to next round";
            _logger.LogError(info);
            await _jobTracker.AppendToJobLogAsync(task.TrackedId, new AppendLogDto(info));
            throw new Exception(info);
        }

        try
        {
            var statInkWebBattleId = await RunBattleTaskAsync(task);
            await _jobTracker.UpdateJobOptionsAsync(task.TrackedId, new UpdateJobOptionsDto(statInkWebBattleId));
            await _jobTracker.UpdateJobStatesAsync(task.TrackedId, new UpdateJobStateDto(JobState.RanToCompletion));
        }
        catch (Exception e)
        {
            await _jobTracker.AppendToJobLogAsync(task.TrackedId, new AppendLogDto(e.Message));
            throw;
        }
    }

    [FunctionName("PoisonJobWorker")]
    public async Task ActPoisonJobWorkerAsync([QueueTrigger(StatItokConstants.JobRunTaskQueueName+"-poison",
        Connection = "WorkerQueueConnStr")]
        QueueMessage queueMsg)
    {
        try
        {
            var msgStr = Helper.DecompressStr(queueMsg.MessageText);
            var jobRunTaskLite = JsonConvert.DeserializeObject<JobRunTaskLite>(msgStr);
            await _jobTracker.UpdateJobStatesAsync(jobRunTaskLite.TrackedId, new UpdateJobStateDto(JobState.Faulted,
                $"PoisonJobWorker:{msgStr}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ERROR WHEN {nameof(ActPoisonJobWorkerAsync)}");
        }

    }
    private async Task<T> GetJobRunTaskPayloadAsync<T>(string pk, string rk)
    {
        var payloadTable = await _storage.GetTableClientAsync<JobRunTaskPayload>();
        var resp = await payloadTable.GetEntityIfExistsAsync<JobRunTaskPayload>(pk, rk);
        return resp.HasValue
            ? JsonConvert.DeserializeObject<T>(Helper.DecompressStr(resp.Value.CompressedPayload))
            : default;
    }

    private async Task<Dictionary<string, string>> GetGearsInfoAsync(bool uncached = false)
    {
        if (!uncached &&
            _memCache.TryGetValue<Dictionary<string, string>>(nameof(GetGearsInfoAsync), out var gearsInfo) &&
            gearsInfo != null)
        {
            return gearsInfo;
        }

        gearsInfo = await _mediator.Send(new ReqGetGearsInfo());
        _memCache.Set(nameof(GetGearsInfoAsync), gearsInfo);
        return gearsInfo;
    }

    private async Task<JobConfig> GetJobConfigAsync(string jobConfigId)
    {
        var jobConfigTable = await _storage.GetTableClientAsync<JobConfig>();
        var resp = await jobConfigTable.GetEntityIfExistsAsync<JobConfig>(nameof(JobConfig), jobConfigId);
        if (!resp.HasValue) throw new Exception("Cannot FindJobConfig");
        return resp.Value;
    }

    private async Task<string> RunBattleTaskAsync(BattleTaskPayload task)
    {
        var gearsInfo = await GetGearsInfoAsync();
        var jobConfig = await GetJobConfigAsync(task.JobConfigId);
        var detailRes = await _mediator.Send(new ReqDoGraphQL()
        {
            AuthContext = jobConfig.NinAuthContext,
            QueryHash = QueryHash.VsHistoryDetail,
            VarName = "vsResultId",
            VarValue = task.BattleIdRawStr
        });
        var battleBody = StatHelper.BuildStatInkBattleBody(
            detailRes,
            task.BattleGroupRawStr,
            jobConfig.NinAuthContext.UserInfo.Lang, gearsInfo);
        var resp = await _mediator.Send(new ReqPostBattle()
        {
            ApiKey = jobConfig.StatInkApiKey,
            Body = battleBody,
        });
        return resp.Id;
    }

    private async Task<Dictionary<string, string>> DoVsBattleUploadAsync(
        string queryHash,
        Dictionary<string, string> hisBattleIdDict,
        JobRun jobRunHis,
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
                    _logger.LogInformation("Skipping {battleId} due to previous upload", statInkBattleId);
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
}