using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper.Mappers;
using Azure.Data.Tables;
using JobTrackerX.Client;
using JobTrackerX.SharedLibs;
using Mapster;
using MediatR;
using Microsoft.Azure.WebJobs;
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

    public JobDispatcher(
        IMediator mediator,
        ILogger<JobDispatcher> logger,
        IStorageAccessSvc storage,
        IOptions<GlobalConfig> options,
        IJobTrackerClient jobTracker)
    {
        _mediator = mediator;
        _logger = logger;
        _storage = storage;
        _options = options;
        _jobTracker = jobTracker;
    }

    [FunctionName("JobDispatcher")]
    public async Task ActJobDispatcher([TimerTrigger("0 */5 * * * *", RunOnStartup = true)] TimerInfo timerInfo)
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

    [FunctionName("JobWorker")]
    public static async Task ActJobWorkerAsync(
        [QueueTrigger(StatItokConstants.JobRunTaskQueueName, Connection ="WorkerQueueConnStr")]
        string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        var str = Helper.DecompressStr(content);
        //var jobRunTask
        //May need base64 decode;
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

        var jobRunTaskList = new List<JobRunTask<BattleTaskPayload>>();
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
        await TryDispatchJobRunTasksAsync(jobConfig, jobRunTaskList);
    }

    private async Task<IList<JobRunTask<BattleTaskPayload>>> GetJobRunTasksAsync(
        string queryHash, JobConfig jobConfig)
    {
        var groupRes = await _mediator.Send(new ReqDoGraphQL
        {
            AuthContext = jobConfig.NinAuthContext,
            QueryHash = queryHash,
        });
        var jobRunTaskList = StatHelper.ExtractBattleIds(groupRes, queryHash)
            .SelectMany(x => x.BattleIds.Select(y => new JobRunTask<BattleTaskPayload>
            {
                Payload = new BattleTaskPayload()
                {
                    BattleGroupRawStr = x.RawBattleGroup,
                    BattleIdRawStr = y
                },
                JobConfigId = jobConfig.Id
            })).ToList();
        return jobRunTaskList;
    }


    private async Task TryDispatchJobRunTasksAsync(JobConfig jobConfig, IList<JobRunTask<BattleTaskPayload>> tasks)
    {
        if (!tasks.Any()) return;
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
        var battleIdTable = await _storage.GetTableClientAsync<JobBattleIdHis>();
        try
        {
            foreach (var jobRunTask in tasks)
            {
                jobRunTask.JobConfigId = jobConfig.Id;
                jobRunTask.JobRunTrackedId = jobRunTask.JobRunTrackedId;
                var existBattleId =
                    await battleIdTable.GetEntityIfExistsAsync<JobBattleIdHis>(jobConfig.Id,
                        StatHelper.GetBattleIdForStatInk(jobRunTask.Payload.BattleIdRawStr));
                if (existBattleId.HasValue)
                    continue;
                var addJobDto = new AddJobDto($"[{nameof(JobRunTask<BattleTaskPayload>).ToUpperInvariant()}]",
                    jobRun.TrackedId)
                {
                    Options = StatHelper.GetBattleIdForStatInk(jobRunTask.Payload.BattleIdRawStr)

                };
                var tJobRunTask =
                    await _jobTracker.CreateNewJobAsync(addJobDto);
                jobRunTask.TrackedId = tJobRunTask.JobId;
                try
                {
                    var queueClient = await _storage.GeJobRunTaskQueueClientAsync();
                    var resp = await queueClient
                        .SendMessageAsync(Helper.CompressStr(JsonConvert.SerializeObject(jobRunTask.Adapt<JobRunTaskLite>())));
                    await _jobTracker.UpdateJobStatesAsync(jobRunTask.TrackedId,
                        new UpdateJobStateDto(JobState.WaitingToRun, $"queue message Id:{resp.Value.MessageId}"));
                    await battleIdTable.UpsertEntityAsync(new JobBattleIdHis()
                    {
                        CompressedPayload = Helper.CompressStr(JsonConvert.SerializeObject(jobRunTask)),
                        PartitionKey = jobConfig.Id,
                        RowKey = StatHelper.GetBattleIdForStatInk(jobRunTask.Payload.BattleIdRawStr)
                    });
                }
                catch (Exception ex)
                {
                    await _jobTracker.UpdateJobStatesAsync(jobRunTask.TrackedId,
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