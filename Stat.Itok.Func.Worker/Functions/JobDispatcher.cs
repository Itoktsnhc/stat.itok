using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using JobTrackerX.Client;
using JobTrackerX.SharedLibs;
using Mapster;
using MediatR;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stat.Itok.Core;
using Stat.Itok.Core.Handlers;

namespace Stat.Itok.Func.Worker.Functions;

public class JobDispatcher
{
    private readonly IMediator _mediator;
    private readonly ILogger<JobDispatcher> _logger;
    private readonly IStorageAccessSvc _storage;
    private readonly IJobTrackerClient _jobTracker;

    public JobDispatcher(
        IMediator mediator,
        ILogger<JobDispatcher> logger,
        IStorageAccessSvc storage,
        IJobTrackerClient jobTracker)
    {
        _mediator = mediator;
        _logger = logger;
        _storage = storage;
        _jobTracker = jobTracker;
    }

    [FunctionName("JobDispatcher")]
    public async Task ActJobDispatcher([TimerTrigger("0 */5 * * * *"
#if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo timerInfo)
    {
        var jobConfigTable = await _storage.GetTableClientAsync<JobConfig>();
        var queryRes = jobConfigTable.QueryAsync<JobConfig>(x => x.PartitionKey == nameof(JobConfig) && x.Enabled);
        var jobs = await queryRes.ToListAsync();

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
        var ninMiscConfig = await _mediator.Send(new ReqGetNinMiscConfig());
        var queryHashDict = ninMiscConfig.GraphQL.APIs;
        await jobConfigTable.UpsertEntityAsync(jobConfig);

        var jobConfigLite = jobConfig.Adapt<JobConfigLite>();
        jobConfigLite.CorrectUserInfoLang();
        var jobRunTaskList = new List<BattleTaskPayload>();
        foreach (var queryName in jobConfigLite.EnabledQueries)
        {
            var queryNameFull = queryName.EndsWith("Query") ? queryName : queryName + "Query";
            if (queryHashDict.TryGetValue(queryNameFull, out var hash))
            {
                jobRunTaskList.AddRange(await GetJobRunTasksAsync(hash, jobConfigLite));
            }
            else
            {
                _logger.LogError("NotFoundQuery:{queryName}", queryName);
            }
        }

        await DispatchJobRunTasksAsync(jobConfigLite, jobRunTaskList);
    }

    private async Task<IList<BattleTaskPayload>> GetJobRunTasksAsync(
        string queryHash, JobConfigLite jobConfig)
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
                JobConfigId = jobConfig.JobConfigId
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

    private async Task DispatchJobRunTasksAsync(JobConfigLite jobConfigLite, IList<BattleTaskPayload> tasks)
    {
        tasks = await ExcludeExistBattlesAsync(jobConfigLite, tasks);
        if (!tasks.Any())
        {
            _logger.LogInformation("No new battle Find");
            return;
        }

        var runTable = await _storage.GetTableClientAsync<JobRun>();

        var newJobDto = new AddJobDto($"[{nameof(JobRun)}] for [{jobConfigLite.NinAuthContext.UserInfo.Nickname}]")
        {
            Tags = new List<string> { "stat.itok", runTable.AccountName }
        };
        var tJob = await _jobTracker.CreateNewJobAsync(newJobDto);
        var jobRun = new JobRun
        {
            TrackedId = tJob.JobId,
            JobConfigId = jobConfigLite.JobConfigId,
            PartitionKey = jobConfigLite.JobConfigId,
            RowKey = (long.MaxValue - tJob.JobId).ToString()
        };
        await runTable.UpsertEntityAsync(jobRun);
        await _jobTracker.UpdateJobStatesAsync(tJob.JobId,
            new UpdateJobStateDto(JobState.Running, "JobRun Saved"));

        try
        {
            foreach (var battleTask in tasks)
            {
                battleTask.JobConfigId = jobConfigLite.JobConfigId;
                battleTask.JobRunTrackedId = battleTask.JobRunTrackedId;

                var addJobDto = new AddJobDto($"[{nameof(BattleTaskPayload)}] for [{jobConfigLite.NinAuthContext.UserInfo.Nickname}]",
                    jobRun.TrackedId)
                {
                    Options = StatHelper.GetBattleIdForStatInk(battleTask.BattleIdRawStr),
                    Tags = new List<string> { "stat.itok", runTable.AccountName }
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
                            Helper.CompressStr(JsonConvert.SerializeObject(jobRunTaskLite)), TimeSpan.FromSeconds(30));
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

    private async Task<IList<BattleTaskPayload>> ExcludeExistBattlesAsync(JobConfigLite jobConfigLite,
        IList<BattleTaskPayload> tasks)
    {
        var res = new ConcurrentBag<BattleTaskPayload>();
        if (!tasks.Any()) return res.ToList();
        var payloadTable = await _storage.GetTableClientAsync<JobRunTaskPayload>();
        var checkTasks = tasks.Select(async task =>
        {
            var existBattleId =
                await payloadTable.GetEntityIfExistsAsync<JobRunTaskPayload>(jobConfigLite.JobConfigId,
                    StatHelper.GetBattleIdForStatInk(task.BattleIdRawStr));
            if (existBattleId.HasValue)
            {
                _logger.LogInformation("{jobConfigId}: ignoring exist battle {battleId} ",
                    jobConfigLite.JobConfigId, StatHelper.GetBattleIdForStatInk(task.BattleIdRawStr));
                return;
            }

            res.Add(task);
        });
        await Task.WhenAll(checkTasks);
        return res.ToList();
    }
}