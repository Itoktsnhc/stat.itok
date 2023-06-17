using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JobTrackerX.Client;
using JobTrackerX.SharedLibs;
using Mapster;
using MediatR;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stat.Itok.Core;
using Stat.Itok.Core.Handlers;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Net.Proxy;
using Stat.Itok.Core.Helpers;

namespace Stat.Itok.Func.Worker.Functions;

public class JobDispatcher
{
    private readonly IMediator _mediator;
    private readonly ILogger<JobDispatcher> _logger;
    private readonly IStorageAccessor _storage;
    private readonly IJobTrackerClient _jobTracker;
    private readonly ICosmosAccessor _cosmos;
    private readonly IOptions<GlobalConfig> _options;

    public JobDispatcher(
        IMediator mediator,
        ILogger<JobDispatcher> logger,
        IStorageAccessor storage,
        IJobTrackerClient jobTracker,
        ICosmosAccessor cosmos, IOptions<GlobalConfig> options)
    {
        _mediator = mediator;
        _logger = logger;
        _storage = storage;
        _jobTracker = jobTracker;
        _cosmos = cosmos;
        _options = options;
    }

    [FunctionName("JobDispatcher")]
    public async Task ActJobDispatcher([TimerTrigger("0 */5 * * * *"
#if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo timerInfo)
    {
        var pk = CosmosEntity.GetPartitionKey<JobConfig>(_options.Value.CosmosDbPkPrefix);
        using var feed = _cosmos.GetContainer<JobConfig>()
            .GetItemLinqQueryable<CosmosEntity<JobConfig>>()
            .Where(x => x.PartitionKey == pk)
            .ToFeedIterator();
        var jobConfigs = new List<JobConfig>();
        while (feed.HasMoreResults)
        {
            var resp = await feed.ReadNextAsync();
            foreach (var jobConfigEntity in resp)
            {
                jobConfigs.Add(jobConfigEntity.Data);
            }
        }

        _logger.LogInformation("JobExecutor GOT Total {N} records", jobConfigs.Count);
        jobConfigs = jobConfigs.Where(x => x.Enabled).ToList();
        _logger.LogInformation("JobExecutor GOT {N} active records", jobConfigs.Count);
        if (jobConfigs.Count <= 0) return;

        foreach (var job in jobConfigs)
        {
            try
            {
                await DispatchBattleJobRunAsync(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when DispatchJobRunAsync");
            }
        }
    }

    private async Task DispatchBattleJobRunAsync(JobConfig jobConfig)
    {
        var checkRes = await _mediator.Send(new ReqPreCheck {AuthContext = jobConfig.NinAuthContext});
        var statInkApiCheckRes = await _mediator.Send(new ReqTestStatApiKey()
        {
            ApiKey = jobConfig.StatInkApiKey
        });
        if (checkRes.Result == PreCheckResult.NeedBuildFromBegin || !statInkApiCheckRes.Result)
        {
            jobConfig.NeedBuildFromBeginCount++;
            await DoWarningIfNeedAsync(jobConfig);
            if (checkRes.Result == PreCheckResult.NeedBuildFromBegin)
            {
                _logger.LogError($"job config: {jobConfig.Id} failed renew");
            }

            if (!statInkApiCheckRes.Result)
            {
                _logger.LogError($"job config: {jobConfig.Id}'s stat.ink API KEY not OK");
            }

            return;
        }

        jobConfig.NeedBuildFromBeginCount = 0;

        jobConfig.NinAuthContext = checkRes.AuthContext;
        var ninMiscConfig = await _mediator.Send(new ReqGetNinMiscConfig());
        var queryHashDict = ninMiscConfig.GraphQL.APIs;
        jobConfig.CorrectUserInfoLang();
        await _cosmos.UpsertEntityInStoreAsync(jobConfig.Id, jobConfig);

        var jobRunTaskList = new List<BattleTaskPayload>();

        foreach (var queryName in jobConfig.EnabledQueries)
        {
            var queryNameFull = queryName.EndsWith("Query") ? queryName : queryName + "Query";
            if (queryHashDict.TryGetValue(queryNameFull, out var hash))
            {
                jobRunTaskList.AddRange(await GetJobRunTasksAsync(hash, jobConfig));
            }
            else
            {
                _logger.LogError("NotFoundQuery:{queryName}", queryName);
            }
        }

        await DispatchJobRunTasksAsync(jobConfig, jobRunTaskList);
    }

    private async Task DoWarningIfNeedAsync(JobConfig jobConfig)
    {
        await _cosmos.UpsertEntityInStoreAsync(jobConfig.Id, jobConfig);
        if (!jobConfig.Enabled
            ||
            jobConfig.NeedBuildFromBeginCount <
            Math.Min(_options.Value.MaxNeedBuildFromBeginCount, jobConfig.NeedBuildFromBeginLimit)) return;
        jobConfig.Enabled = false;
        try
        {
            await SendWarningEmailAsync(jobConfig);
            await _cosmos.UpsertEntityInStoreAsync(jobConfig.Id, jobConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error when {nameof(SendWarningEmailAsync)}");
        }
    }

    private async Task SendWarningEmailAsync(JobConfig config)
    {
        if (_options.Value.EmailConfig == null)
        {
            _logger.LogWarning($"{nameof(_options.Value.EmailConfig)} is null or empty");
            return;
        }

        var mailConfig = _options.Value.EmailConfig;
        using var client = new SmtpClient();
#if DEBUG
        //china main_land for gmail connection
        client.ProxyClient = new HttpProxyClient("127.0.0.1", 10245);
#endif
        await client.ConnectAsync(mailConfig.Server, mailConfig.Port, true);
        await client.AuthenticateAsync(mailConfig.Username, mailConfig.Password);
        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress(mailConfig.FromEmail, mailConfig.Username));
        if (string.IsNullOrWhiteSpace(config.NotificationEmail))
        {
            mail.To.Add(new MailboxAddress(mailConfig.AdminEmail, mailConfig.AdminEmail));
        }
        else
        {
            mail.To.Add(new MailboxAddress(config.NotificationEmail, config.NotificationEmail));
            mail.Bcc.Add(new MailboxAddress(mailConfig.AdminEmail, mailConfig.AdminEmail));
        }

        var content = config.ForcedUserLang.StartsWith("zh-", StringComparison.InvariantCultureIgnoreCase)
            ? @"你之前在stat.itok网站上配置的账号授权信息经检测已经失效(任天堂 或者 stat.ink)，如需继续使用对战历史监控功能，请重新设置。"
            : "The account information(nintendo or stat.ink) you previously configured on the stat.itok website has been tested to be invalid. " +
              "If you wish to continue using the match history monitoring feature, please config again.";
        var bodyBuilder = new BodyBuilder
        {
            TextBody = @$"
Hi {config.NinAuthContext?.UserInfo?.Nickname ?? config.NotificationEmail}:

    {content}

Thanks.
"
        };
        mail.Body = bodyBuilder.ToMessageBody();
        mail.Subject = "[Stat.Itok] Warning";
        await client.SendAsync(mail);
    }

    private async Task<IList<BattleTaskPayload>> GetJobRunTasksAsync(
        string queryHash, JobConfig jobConfig)
    {
        var groupRes = await _mediator.Send(new ReqDoGraphQL
        {
            AuthContext = jobConfig.NinAuthContext,
            QueryHash = queryHash,
        });
        var jobRunTaskList = BattleHelper.ExtractBattleIds(groupRes, queryHash)
            .SelectMany(x => x.BattleIds.Select(y => new BattleTaskPayload
            {
                BattleGroupRawStr = x.RawBattleGroup,
                BattleIdRawStr = y,
                JobConfigId = jobConfig.Id
            })).ToList();
        return jobRunTaskList;
    }

    private async Task DispatchJobRunTasksAsync(JobConfig jobConfig, IList<BattleTaskPayload> tasks)
    {
        tasks = await ExcludeExistBattlesAsync(jobConfig, tasks);
        if (!tasks.Any())
        {
            _logger.LogInformation("No new battle Find");
            return;
        }

        var newJobDto = new AddJobDto($"[{nameof(JobRun)}] for [{jobConfig.NinAuthContext.UserInfo.Nickname}]")
        {
            Options = $"{jobConfig.Id}",
            Tags = new List<string> {StatItokConstants.StatVersion},
            CreatedBy = "stat.itok"
        };
        var tJob = await _jobTracker.CreateNewJobAsync(newJobDto);
        var jobRun = new JobRun
        {
            TrackedId = tJob.JobId,
            JobConfigId = jobConfig.Id,
        };
        await _cosmos.UpsertEntityInStoreAsync($"{jobRun.JobConfigId}__{jobRun.TrackedId}", jobRun);
        await _jobTracker.UpdateJobStatesAsync(tJob.JobId,
            new UpdateJobStateDto(JobState.Running, "JobRun Saved"));

        try
        {
            foreach (var battleTask in tasks)
            {
                battleTask.JobConfigId = jobConfig.Id;
                battleTask.JobRunTrackedId = jobRun.TrackedId;

                var addJobDto = new AddJobDto(
                    $"[{nameof(BattleTaskPayload)}] for [{jobConfig.NinAuthContext.UserInfo.Nickname}]",
                    jobRun.TrackedId)
                {
                    Options = BattleHelper.GetBattleIdForStatInk(battleTask.BattleIdRawStr),
                    Tags = new List<string>
                    {
                        StatItokConstants.StatVersion, BattleHelper.GetPayloadTypeForStatInk(battleTask.BattleIdRawStr)
                    },
                    CreatedBy = "stat.itok"
                };
                var tJobRunTask =
                    await _jobTracker.CreateNewJobAsync(addJobDto);
                battleTask.TrackedId = tJobRunTask.JobId;
                try
                {
                    var queueClient = await _storage.GeJobRunTaskQueueClientAsync();
                    var jobRunTaskLite = battleTask.Adapt<JobRunTaskLite>();
                    jobRunTaskLite.PayloadId =
                        $"{jobConfig.Id}__{BattleHelper.GetBattleIdForStatInk(battleTask.BattleIdRawStr)}";

                    var resp = await queueClient
                        .SendMessageAsync(
                            CommonHelper.CompressStr(JsonConvert.SerializeObject(jobRunTaskLite)),
                            TimeSpan.FromSeconds(30));
                    await _jobTracker.UpdateJobStatesAsync(battleTask.TrackedId,
                        new UpdateJobStateDto(JobState.WaitingToRun, $"queue message Id:{resp.Value.MessageId}"));

                    await _cosmos.UpsertEntityInStoreAsync(jobRunTaskLite.PayloadId, battleTask);
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

    private async Task<IList<BattleTaskPayload>> ExcludeExistBattlesAsync(JobConfig jobConfig,
        IList<BattleTaskPayload> tasks)
    {
        var res = new ConcurrentBag<BattleTaskPayload>();
        if (!tasks.Any()) return res.ToList();
        var container = _cosmos.GetContainer<BattleTaskPayload>();
        var checkTasks = tasks.Select(async task =>
        {
            var targetBattleId =
                CosmosEntity.BuildCosmosRealId<BattleTaskPayload>(
                    $"{jobConfig.Id}__{BattleHelper.GetBattleIdForStatInk(task.BattleIdRawStr)}",
                    _options.Value.CosmosDbPkPrefix);

            var query = new QueryDefinition(
                query: "SELECT * FROM store AS s WHERE s.id = @targetBattleId"
            ).WithParameter("@targetBattleId", targetBattleId);
            using var filteredFeed = container.GetItemQueryIterator<PureIdDto>(
                queryDefinition: query
            );

            while (filteredFeed.HasMoreResults)
            {
                FeedResponse<PureIdDto> response = await filteredFeed.ReadNextAsync();

                // Iterate query results
                foreach (var _ in response)
                {
                    _logger.LogInformation("{jobConfigId}: ignoring exist battle {battleId} ",
                        jobConfig.Id, BattleHelper.GetBattleIdForStatInk(task.BattleIdRawStr));
                    return;
                }
            }

            res.Add(task);
        });
        await Task.WhenAll(checkTasks);
        return res.ToList();
    }

    private class PureIdDto
    {
        public string Id { get; set; }
    }
}