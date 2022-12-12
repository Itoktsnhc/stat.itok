using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using Newtonsoft.Json;
using Stat.Itok.Core.Handlers;

namespace Stat.Itok.Func.Functions
{
    public class FetchInfo
    {
        private readonly ILogger<FetchInfo> _logger;
        private readonly ICosmosAccessor _cosmos;
        private readonly IServiceProvider _sp;
        private readonly IMediator _mediator;

        public FetchInfo(ILogger<FetchInfo> logger, ICosmosAccessor cosmos,
        IServiceProvider sp, IMediator mediator)
        {
            _logger = logger;
            _cosmos = cosmos;
            _sp = sp;
            _mediator = mediator;
        }
        [FunctionName("GetJobConfig")]
        public async Task<ApiResp<JobConfig>> GetJobConfigAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "nin/jobConfig")] HttpRequest req)
        {
            var bodyStr = await req.ReadAsStringAsync();
            req.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            using var scope = _sp.CreateScope();
            var validator = scope.ServiceProvider.GetRequiredService<IValidator<NinAuthContext>>();
            try
            {
                var authContext = JsonConvert.DeserializeObject<NinAuthContext>(bodyStr);
                await validator.ValidateAndThrowAsync(authContext);
                var configInDb = await _cosmos.GetEntityIfExistAsync<JobConfig>(authContext.UserInfo.Id);
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
                return ApiResp.OkWith(configInDb);

            }
            catch (Exception ex)
            {
                return ApiResp.Error<JobConfig>(ex.ToString());
            }
        }

    }

    public class NinAuthContextValidator : AbstractValidator<NinAuthContext>
    {
        public NinAuthContextValidator()
        {
            RuleFor(x => x).NotNull();
            RuleFor(x => x.UserInfo).NotNull();
            RuleFor(x => x.UserInfo.Id).NotNull();
            RuleFor(x => x.SessionToken).NotEmpty(); ;
        }
    }
}
