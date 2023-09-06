using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using FluentValidation;
using Mediator;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Stat.Itok.Core.Handlers;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using JobTrackerX.Client;
using JobTrackerX.SharedLibs;
using Mapster;
using System.Threading;
using System.Text.RegularExpressions;
using System.Linq;

namespace Stat.Itok.Func.Functions
{
    public class FetchJobHistory
    {
        private readonly ILogger<FetchJobHistory> _logger;
        private readonly ICosmosAccessor _cosmos;
        private readonly IServiceProvider _sp;
        private readonly IMediator _mediator;
        private readonly IMemoryCache _memCache;
        private readonly IOptions<GlobalConfig> _options;
        private readonly IJobTrackerClient _jobTracker;

        public FetchJobHistory(ILogger<FetchJobHistory> logger, ICosmosAccessor cosmos,
            IServiceProvider sp, IMediator mediator, IMemoryCache memCache, IOptions<GlobalConfig> options,
            IJobTrackerClient jobTracker)
        {
            _logger = logger;
            _cosmos = cosmos;
            _sp = sp;
            _mediator = mediator;
            _memCache = memCache;
            _options = options;
            _jobTracker = jobTracker;
        }

        [FunctionName("GetJobHistoryInStore")]
        public async Task<ApiResp<List<JobRunHistoryItem>>> GetJobHistoryInStoreAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "get_job_history_stored")] HttpRequest req)
        {
            var bodyStr = await req.ReadAsStringAsync();
            req.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            using var scope = _sp.CreateScope();
            var validator = scope.ServiceProvider.GetRequiredService<IValidator<NinAuthContext>>();
            try
            {
                var authContext = JsonConvert.DeserializeObject<NinAuthContext>(bodyStr);
                await validator.ValidateAndThrowAsync(authContext);
                string continuation = null;
                if (req.Headers.TryGetValue(StatItokConstants.QueryContinuationHeaderName, out var continuationVal))
                {
                    var cVal = continuationVal.ToString()?.ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(cVal) && cVal != "null")
                    {
                        continuation = continuationVal.ToString();
                    }
                }
                var container = _cosmos.GetContainer<BattleTaskPayload>();
                QueryDefinition query = new QueryDefinition(
                    "SELECT c.data.trackedId " +
                    "FROM c where c.partitionKey = " +
                    "@partitionKey and c.data.jobConfigId = @jobConfigId " +
                    "order by c._ts desc")
                    .WithParameter("@jobConfigId", $"nin_user_{authContext.UserInfo.Id}")
                    .WithParameter("@partitionKey", CosmosEntity.GetPartitionKey<BattleTaskPayload>(_options.Value.CosmosDbPkPrefix));
                using FeedIterator<JobRunHistoryItem> resultSetIterator = container.GetItemQueryIterator<JobRunHistoryItem>(query,
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxItemCount = 15
                    }, continuationToken: continuation);
                var results = new List<JobRunHistoryItem>();
                while (resultSetIterator.HasMoreResults)
                {
                    FeedResponse<JobRunHistoryItem> response = await resultSetIterator.ReadNextAsync();
                    results.AddRange(response);
                    if (response.Count > 0)
                    {
                        continuation = response.ContinuationToken;

                    }
                    else
                    {
                        continuation = null;
                    }
                    break;
                }
                var ct = CancellationToken.None;
                await Parallel.ForEachAsync(results, async (item, ct) =>
                {
                    try
                    {
                        var trackedJob = await _jobTracker.GetJobEntityLiteAsync(item.TrackedId);
                        item.TrackedJobEntity = trackedJob.Adapt<TrackedJobEntity>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "error when not fetch trackedJob");
                    }
                });
                req.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
                req.HttpContext.Response.Headers.Add(StatItokConstants.QueryContinuationHeaderName, continuation);
                results = results.Where(x => x.TrackedJobEntity?.CreateTime != null).ToList();
                return ApiResp.OkWith(results);
            }
            catch (Exception ex)
            {
                return ApiResp.Error<List<JobRunHistoryItem>>(ex.ToString());
            }
        }

    }

    public class JobRunHistoryItem
    {
        public long TrackedId { get; set; }
        public string StatInkLink
        {
            get
            {
                if (string.IsNullOrEmpty(TrackedJobEntity?.Options) || TrackedJobEntity.CurrentJobState != JobState.RanToCompletion)
                    return null;
                var matchRes = Regex.Match(TrackedJobEntity.Options, @"URL:\[(?<url>.+?)\]");
                if (matchRes.Groups.TryGetValue("url", out var matchGroup))
                {
                    return matchGroup.Value;
                }
                return null;
            }
        }
        public TrackedJobEntity TrackedJobEntity { get; set; }
    }

    public class TrackedJobEntity
    {
        public long JobId { get; set; }

        public string Options { get; set; }

        public JobState CurrentJobState { get; set; }

        public DateTimeOffset? CreateTime { get; set; }

        public DateTimeOffset? StartTime { get; set; }

        public DateTimeOffset? EndTime { get; set; }
    }
}
