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
using Mapster;
using MediatR;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stat.Itok.Core;
using Stat.Itok.Core.Handlers;
using Stat.Itok.Core.Helpers;

namespace Stat.Itok.Func.Worker.Functions;

public class JobRunTaskWorker
{
    private readonly IMediator _mediator;
    private readonly ILogger<JobRunTaskWorker> _logger;
    private readonly IStorageAccessor _storage;
    private readonly IJobTrackerClient _jobTracker;
    private readonly IMemoryCache _memCache;
    private readonly ICosmosAccessor _cosmos;

    public JobRunTaskWorker(
        IMediator mediator,
        ILogger<JobRunTaskWorker> logger,
        IStorageAccessor storage,
        IJobTrackerClient jobTracker,
        IMemoryCache memCache, ICosmosAccessor cosmos)
    {
        _mediator = mediator;
        _logger = logger;
        _storage = storage;
        _jobTracker = jobTracker;
        _memCache = memCache;
        _cosmos = cosmos;
    }

    [FunctionName("JobWorker")]
    public async Task ActJobWorkerAsync(
        [QueueTrigger(StatItokConstants.JobRunTaskQueueName, Connection = "WorkerQueueConnStr")]
        QueueMessage queueMsg)
    {
        var msgStr = CommonHelper.DecompressStr(queueMsg.MessageText);
        var jobRunTaskLite = JsonConvert.DeserializeObject<JobRunTaskLite>(msgStr);
        var task = await _cosmos.GetEntityIfExistAsync<BattleTaskPayload>(jobRunTaskLite!.PayloadId);
        await _jobTracker.UpdateJobStatesAsync(jobRunTaskLite.TrackedId,
            new UpdateJobStateDto(JobState.Running,
                $"{queueMsg.MessageId};{queueMsg.PopReceipt};[{queueMsg.DequeueCount}]"));
        if (task == null)
        {
            var info = $"JobRunTaskPayload not found:{jobRunTaskLite.PayloadId}, go to next round";
            _logger.LogError(info);
            await _jobTracker.AppendToJobLogAsync(jobRunTaskLite.TrackedId, new AppendLogDto(info));
            throw new Exception(info);
        }

        try
        {
            var (status, msg) = await RunBattleTaskAsync(task);

            if (status == RunBattleTaskStatus.Ok)
            {
                await _jobTracker.UpdateJobStatesAsync(task.TrackedId,
                    new UpdateJobStateDto(JobState.RanToCompletion, $"{status}, {msg}"));
                await _jobTracker.UpdateJobOptionsAsync(task.TrackedId, new UpdateJobOptionsDto($"{status}, {msg}"));
            }
            else
            {
                await _jobTracker.UpdateJobStatesAsync(task.TrackedId,
                    new UpdateJobStateDto(JobState.Faulted, $"{status}, {msg}"));
            }
        }
        catch (Exception e)
        {
            await _jobTracker.AppendToJobLogAsync(task.TrackedId, new AppendLogDto(e.Message));
            throw;
        }
    }

    private async Task<Dictionary<string, string>> GetGearsInfoAsync(bool unCached = false)
    {
        if (!unCached &&
            _memCache.TryGetValue<Dictionary<string, string>>(nameof(GetGearsInfoAsync), out var gearsInfo) &&
            gearsInfo != null)
        {
            return gearsInfo;
        }

        gearsInfo = await _mediator.Send(new ReqGetGearsInfo());
        _memCache.Set(nameof(GetGearsInfoAsync), gearsInfo);
        return gearsInfo;
    }


    private async Task<Dictionary<string, string>> GetSalmonWeaponsInfoAsync(bool unCached = false)
    {

        if (!unCached &&
            _memCache.TryGetValue<Dictionary<string, string>>(nameof(GetSalmonWeaponsInfoAsync), out var weaponInfo) &&
            weaponInfo != null)
        {
            return weaponInfo;
        }

        weaponInfo = await _mediator.Send(new ReqGetSalmonWeaponsInfo());
        _memCache.Set(nameof(GetSalmonWeaponsInfoAsync), weaponInfo);
        return weaponInfo;
    }

    private async Task<(RunBattleTaskStatus, string)> RunBattleTaskAsync(BattleTaskPayload task)
    {
        var debugContext = new BattleTaskDebugContext();
        try
        {
            #region Fill Basic DebugInfo

            debugContext.JobConfigId = task.JobConfigId;
            debugContext.BattleIdRawStr = task.BattleIdRawStr;
            debugContext.BattleGroupRawStr = task.BattleGroupRawStr;
            debugContext.StatInkBattleId = BattleHelper.GetBattleIdForStatInk(task.BattleIdRawStr);

            #endregion

            var ninMiscConfig = await _mediator.Send(new ReqGetNinMiscConfig());
            var queryHashDict = ninMiscConfig.GraphQL.APIs;

            var jobConfig = await _cosmos.GetEntityIfExistAsync<JobConfig>(task.JobConfigId);
            jobConfig.CorrectUserInfoLang();

            var (detailRes, payloadType) =
                await GetDetailResAndTypeAsync(jobConfig.NinAuthContext, task.BattleIdRawStr, queryHashDict);

            #region Fill Basic DebugInfo

            debugContext.BattleDetailRawStr = detailRes;
            debugContext.PayloadType = payloadType;

            #endregion

            var (battleBody, salmonBody, resp) = await BuildBodyAndSendAsync(payloadType, detailRes,
                task.BattleGroupRawStr,
                jobConfig, debugContext);


            #region Fill Basic DebugInfo

            debugContext.StatInkBattleBody = battleBody;
            debugContext.StatInkSalmonBody = salmonBody;

            #endregion

            if (battleBody == null && salmonBody == null)
            {
                return (RunBattleTaskStatus.BattleBodyIsNull, $"DebugContext:[{debugContext.FilePath}]");
            }

            #region Fill Basic DebugInfo

            debugContext.StatInkPostBodySuccess = resp;

            #endregion

            return (RunBattleTaskStatus.Ok, $"URL:[{resp.Url}]  ID:[{resp.Id}]");
        }
        finally
        {
            try
            {
                await SaveDebugContextAsync(debugContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    $"ERROR when {nameof(SaveDebugContextAsync)}:{task.JobConfigId}:{task.BattleIdRawStr}");
            }
        }
    }

    private async Task<(StatInkBattleBody, StatInkSalmonBody, StatInkPostBodySuccess)>
        BuildBodyAndSendAsync(string payloadType, string detailRes, string groupRawStr, JobConfig jobConfig, BattleTaskDebugContext debugContext)
    {
        switch (payloadType)
        {
            case nameof(QueryHash.VsHistoryDetail):
                {
                    var gearsInfo = await GetGearsInfoAsync();
                    var battleBody = BattleHelper.BuildStatInkBattleBody(
                        detailRes, groupRawStr, jobConfig.NinAuthContext.UserInfo.Lang, gearsInfo);
                    debugContext.StatInkBattleBody = battleBody;
                    if (battleBody != null)
                    {
                        var resp = await _mediator.Send(new ReqPostBattle
                        {
                            ApiKey = jobConfig.StatInkApiKey,
                            Body = battleBody
                        });
                        return (battleBody, null, resp);
                    }

                    break;
                }
            case nameof(QueryHash.CoopHistoryDetail):
                {
                    var weaponsInfo = await GetSalmonWeaponsInfoAsync();
                    var salmonBody = BattleHelper.BuildStatInkSalmonBody(
                        detailRes, groupRawStr, jobConfig.NinAuthContext.UserInfo.Lang, weaponsInfo);
                    debugContext.StatInkSalmonBody = salmonBody;
                    if (salmonBody != null)
                    {
                        var resp = await _mediator.Send(new ReqPostSalmon
                        {
                            ApiKey = jobConfig.StatInkApiKey,
                            Body = salmonBody
                        });
                        return (null, salmonBody, resp);
                    }

                    break;
                }
        }


        return (null, null, null);
    }


    private async Task<(string, string)> GetDetailResAndTypeAsync(NinAuthContext authContext, string payloadIdRawStr,
        Dictionary<string, string> queryHashDict)
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(payloadIdRawStr));
        var payloadType = decoded.StartsWith(nameof(QueryHash.CoopHistoryDetail))
            ? nameof(QueryHash.CoopHistoryDetail)
            : nameof(QueryHash.VsHistoryDetail);
        var detailRes = await _mediator.Send(new ReqDoGraphQL()
        {
            AuthContext = authContext,
            QueryHash = queryHashDict[$"{payloadType}Query"],
            VarName = payloadType is nameof(QueryHash.CoopHistoryDetail) ? "coopHistoryDetailId" : "vsResultId",
            VarValue = payloadIdRawStr
        });
        return (detailRes, payloadType);
    }

    private async Task SaveDebugContextAsync(BattleTaskDebugContext entity)
    {
        var container = await _storage.GetBlobContainerClientAsync<BattleTaskDebugContext>();
        var blob = container.GetBlockBlobClient(entity.FilePath);
        using var ms =
            new MemoryStream(CommonHelper.CompressBytes(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entity))));
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
    private async Task<BattleTaskDebugContext> TryReadCachedAsync(string jobConfigId, string statInkBattleId)
    {
        var fileName = $"{jobConfigId}/{statInkBattleId}.json";
        var container = await _storage.GetBlobContainerClientAsync<BattleTaskDebugContext>();
        var blob = container.GetBlockBlobClient(fileName);
        using var ms = new MemoryStream();
        await blob.DownloadToAsync(ms);
        ms.Seek(0, SeekOrigin.Begin);
        var content = Encoding.UTF8.GetString(CommonHelper.DecompressBytes(ms.ToArray()));
        return JsonConvert.DeserializeObject<BattleTaskDebugContext>(content);
    }
}