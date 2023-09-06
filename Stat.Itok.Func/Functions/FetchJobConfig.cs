using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using Newtonsoft.Json;
using Stat.Itok.Core.Handlers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Stat.Itok.Func.Functions
{
    public class FetchJobConfig
    {
        private readonly ILogger<FetchJobHistory> _logger;
        private readonly ICosmosAccessor _cosmos;
        private readonly IServiceProvider _sp;
        private readonly IMediator _mediator;
        private readonly IMemoryCache _memCache;
        private readonly IOptions<GlobalConfig> _options;

        public FetchJobConfig(ILogger<FetchJobHistory> logger, ICosmosAccessor cosmos,
        IServiceProvider sp, IMediator mediator, IMemoryCache memCache, IOptions<GlobalConfig> options)
        {
            _logger = logger;
            _cosmos = cosmos;
            _sp = sp;
            _mediator = mediator;
            _memCache = memCache;
            _options = options;
        }
        [FunctionName("GetJobConfigInStore")]
        public async Task<ApiResp<JobConfig>> GetJobConfigInStoreAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "get_job_config_stored")] HttpRequest req)
        {
            var bodyStr = await req.ReadAsStringAsync();
            req.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            using var scope = _sp.CreateScope();
            var validator = scope.ServiceProvider.GetRequiredService<IValidator<NinAuthContext>>();
            try
            {
                var authContext = JsonConvert.DeserializeObject<NinAuthContext>(bodyStr);
                await validator.ValidateAndThrowAsync(authContext);
                var configInDb = await _cosmos.GetEntityIfExistAsync<JobConfig>($"nin_user_{authContext.UserInfo.Id}");
                if (configInDb == null)
                {
                    req.HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                    return ApiResp.Error<JobConfig>("NotFound");
                }
                if (configInDb.NinAuthContext?.SessionToken == authContext.SessionToken)
                {
                    req.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
                    return ApiResp.OkWith(configInDb);
                }
                var preCheckRes = await _mediator.Send(new ReqPreCheck()
                {
                    AuthContext = authContext
                });
                if (preCheckRes.Result == PreCheckResult.NeedBuildFromBegin)
                {
                    req.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return ApiResp.Error<JobConfig>("NeedBuildFromBegin");
                }
                configInDb.NinAuthContext = preCheckRes.AuthContext;
                await _cosmos.UpsertEntityInStoreAsync(configInDb.Id, configInDb);
                req.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
                return ApiResp.OkWith(configInDb);
            }
            catch (Exception ex)
            {
                return ApiResp.Error<JobConfig>(ex.ToString());
            }
        }

    }
}
