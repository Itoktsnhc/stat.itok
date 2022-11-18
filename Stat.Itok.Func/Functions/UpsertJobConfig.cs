using System;
using System.Threading.Tasks;
using Mapster;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Stat.Itok.Func.Functions;

public class UpsertJobConfig
{
    private readonly ILogger<UpsertJobConfig> _logger;
    private readonly IStorageAccessSvc _storage;

    public UpsertJobConfig(ILogger<UpsertJobConfig> logger,IStorageAccessSvc storage)
    {
        _logger = logger;
        _storage = storage;
    }

    [FunctionName("UpsertJobConfig")]
    public async Task<ApiResp<JobConfigLite>> AddJobConfigAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "job_config/upsert")]
        HttpRequest req)
    {
        var bodyStr = await req.ReadAsStringAsync();
        req.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        try
        {
            var jobConfigLite = JsonConvert.DeserializeObject<JobConfigLite>(bodyStr);
            if (string.IsNullOrWhiteSpace(jobConfigLite?.NinAuthContext?.UserInfo?.Id))
                throw new ArgumentException("did not find Id for JobConfig");
            jobConfigLite!.Id = $"nin_user_{jobConfigLite.NinAuthContext.UserInfo.Id}";

            var jobConfig = jobConfigLite.Adapt<JobConfig>();
            jobConfig.PartitionKey = nameof(JobConfig);
            jobConfig.RowKey = jobConfig.Id;
            var tableClient = await _storage.GetTableClientAsync<JobConfig>();
            var upsertResp = await tableClient.UpsertEntityAsync(jobConfig);
            if (upsertResp.IsError)
                throw new Exception($"upsert resp is ERROR:{upsertResp.Status},{upsertResp.ReasonPhrase}");
            _logger.LogInformation("upsert doc:{obj} with resp:{resp}", jobConfig, upsertResp);
            req.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            return ApiResp.OkWith(jobConfigLite);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error while {nameof(AddJobConfigAsync)}");
            return ApiResp<JobConfigLite>.Error("AddJobConfigAsync exception:" + e.Message);
        }
    }
}