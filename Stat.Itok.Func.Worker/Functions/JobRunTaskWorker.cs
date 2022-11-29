using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Queues.Models;
using JobTrackerX.Client;
using JobTrackerX.SharedLibs;
using MediatR;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stat.Itok.Core;
using Stat.Itok.Core.Handlers;

namespace Stat.Itok.Func.Worker.Functions;

public class JobRunTaskWorker
{
    private readonly IMediator _mediator;
    private readonly ILogger<JobRunTaskWorker> _logger;
    private readonly IStorageAccessSvc _storage;
    private readonly IJobTrackerClient _jobTracker;
    private readonly IMemoryCache _memCache;

    public JobRunTaskWorker(
        IMediator mediator,
        ILogger<JobRunTaskWorker> logger,
        IStorageAccessSvc storage,
        IJobTrackerClient jobTracker,
        IMemoryCache memCache)
    {
        _mediator = mediator;
        _logger = logger;
        _storage = storage;
        _jobTracker = jobTracker;
        _memCache = memCache;
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
            new UpdateJobStateDto(JobState.Running,
                $"{queueMsg.MessageId};{queueMsg.PopReceipt};[{queueMsg.DequeueCount}]"));
        if (task == null)
        {
            var info = $"JobRunTaskPayload not found:{jobRunTaskLite.Pk}:{jobRunTaskLite.Rk}, go to next round";
            _logger.LogError(info);
            await _jobTracker.AppendToJobLogAsync(jobRunTaskLite.TrackedId, new AppendLogDto(info));
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

        var resp = await _mediator.Send(new ReqPostBattle
        {
            ApiKey = jobConfig.StatInkApiKey,
            Body = battleBody
        });

        try
        {
            await TrySaveCacheAsync(new RawBattleCacheEntity
            {
                JobConfigId = task.JobConfigId,
                StatInkBattleId = StatHelper.GetBattleIdForStatInk(task.BattleIdRawStr),
                BattleIdRawStr = task.BattleIdRawStr,
                StatInkWebBattleId = resp?.Id,
                BattleDetailRawStr = JsonConvert.SerializeObject(battleBody),
                BattleGroupRawStr = task.BattleGroupRawStr
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ERROR when {nameof(TrySaveCacheAsync)}:{task.JobConfigId}:{task.BattleIdRawStr}");
        }

        return resp.Id;
    }

    private async Task TrySaveCacheAsync(RawBattleCacheEntity entity)
    {
        var fileName = $"{entity.JobConfigId}__{entity.StatInkBattleId}.json";
        var container = await _storage.GetBlobClientAsync<RawBattleCacheEntity>();
        var blob = container.GetBlockBlobClient(fileName);
        using var ms = new MemoryStream(Helper.CompressBytes(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entity))));
        ms.Seek(0, SeekOrigin.Begin);
        await blob.UploadAsync(ms);
        var properties = await blob.GetPropertiesAsync();
        await blob.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            // Set the MIME ContentType every time the properties 
            // are updated or the field will be cleared
            ContentType = "application/json; charset=utf8",
            ContentEncoding = "br",

            // Populate remaining headers with 
            // the pre-existing properties
            CacheControl = properties.Value.CacheControl,
            ContentDisposition = properties.Value.ContentDisposition,
            ContentHash = properties.Value.ContentHash
        });
    }

    /// may used later
    private async Task<RawBattleCacheEntity> TryReadCachedAsync(string jobConfigId, string statInkBattleId)
    {
        var fileName = $"{jobConfigId}__{statInkBattleId}.json";
        var container = await _storage.GetBlobClientAsync<RawBattleCacheEntity>();
        var blob = container.GetBlockBlobClient(fileName);
        using var ms = new MemoryStream();
        await blob.DownloadToAsync(ms);
        ms.Seek(0, SeekOrigin.Begin);
        var content = Encoding.UTF8.GetString(Helper.DecompressBytes(ms.ToArray()));
        return JsonConvert.DeserializeObject<RawBattleCacheEntity>(content);
    }
}