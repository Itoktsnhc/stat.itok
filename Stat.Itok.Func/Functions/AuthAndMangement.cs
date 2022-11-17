using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Stat.Itok.Core.Handlers;
using Mapster;

namespace Stat.Itok.Func.Functions
{
    public class NinAuthFunctions
    {
        private readonly IMediator _mediator;
        private readonly ILogger<NinAuthFunctions> _logger;
        private readonly IOptions<GlobalConfig> _options;
        private readonly IStorageAccessSvc _storage;

        public NinAuthFunctions(
            IMediator mediator,
            ILogger<NinAuthFunctions> logger,
            IOptions<GlobalConfig> options,
            IStorageAccessSvc storage)
        {
            _mediator = mediator;
            _logger = logger;
            _options = options;
            _storage = storage;
        }

        [FunctionName("GetNintendoVerifyUrl")]
        public async Task<ApiResp<NinTokenCopyInfo>> GetNintendoVerifyUrlAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "nin/verify_url")]
            HttpRequest req)
        {
            var tokenCopyInfo = await _mediator.Send(new ReqGetTokenCopyInfo());
            return ApiResp.OkWith(tokenCopyInfo);
        }

        [FunctionName("AuthNintendoAccount")]
        public async Task<ApiResp<NinAuthContext>> AuthNintendoAccountAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "nin/auth_account")]
            HttpRequest req)
        {
            var bodyStr = await req.ReadAsStringAsync();
            req.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            try
            {
                var data = JsonConvert.DeserializeObject<NinTokenCopyInfo>(bodyStr);
                if (string.IsNullOrWhiteSpace(data?.RedirectUrl))
                    throw new ArgumentNullException(nameof(data.RedirectUrl));
                var authContext = await _mediator.Send(new ReqGenAuthContext() { TokenCopyInfo = data });
                req.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
                return ApiResp.OkWith(authContext);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error while {nameof(AuthNintendoAccountAsync)}");
                return ApiResp<NinAuthContext>.Error("NintendoAccountAuth exception:" + e);
            }
        }

        [FunctionName("UpsertJobConfig")]
        public async Task<ApiResp<JobConfigLite>> AddJobConfigAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "job/upsert")]
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
                if (upsertResp.IsError) throw new Exception($"upsert resp is ERROR:{upsertResp.Status},{upsertResp.ReasonPhrase}");
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
}