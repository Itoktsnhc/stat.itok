﻿using System;
using System.Net;
using System.Threading.Tasks;
using Mapster;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Mediator;
using Stat.Itok.Core.Handlers;

namespace Stat.Itok.Func.Functions;

public class UpsertJobConfig
{
    private readonly ILogger<UpsertJobConfig> _logger;
    private readonly ICosmosAccessor _cosmos;
    private readonly IServiceProvider _sp;
    private readonly IMediator _mediator;

    public UpsertJobConfig(ILogger<UpsertJobConfig> logger, ICosmosAccessor cosmos,
        IServiceProvider sp, IMediator mediator)
    {
        _logger = logger;
        _cosmos = cosmos;
        _sp = sp;
        _mediator = mediator;
    }

    [FunctionName("UpsertJobConfig")]
    public async Task<ApiResp<JobConfig>> AddJobConfigAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "job_config/upsert")]
        HttpRequest req)
    {
        var bodyStr = await req.ReadAsStringAsync();
        req.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        using var scope = _sp.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<JobConfig>>();
        try
        {
            var jobConfig = JsonConvert.DeserializeObject<JobConfig>(bodyStr);

            jobConfig.NotificationEmail = jobConfig.NotificationEmail?.Trim();
            jobConfig.StatInkApiKey = jobConfig.StatInkApiKey?.Trim();
            jobConfig.NeedBuildFromBeginLimit = Math.Max(12, jobConfig.NeedBuildFromBeginLimit);
            await validator.ValidateAndThrowAsync(jobConfig);
            jobConfig!.Id = $"nin_user_{jobConfig.NinAuthContext.UserInfo.Id}";
            var preCheckRes = await _mediator.Send(new ReqPreCheck()
            {
                AuthContext = jobConfig.NinAuthContext
            });
            var statInkApiCheckRes = await _mediator.Send(new ReqTestStatApiKey()
            {
                ApiKey = jobConfig.StatInkApiKey
            });

            if (preCheckRes.Result == PreCheckResult.NeedBuildFromBegin)
            {
                throw new Exception("Cannot Login. Try re-login your account in browser");
            }

            if (!statInkApiCheckRes.Result)
            {
                throw new Exception("Cannot Login. stat.ink's api key not working");
            }

            jobConfig.NinAuthContext = preCheckRes.AuthContext;
            jobConfig.LastUpdateTime = DateTimeOffset.Now;
            var upsertResp = await _cosmos.UpsertEntityInStoreAsync(jobConfig.Id, jobConfig);
            _logger.LogInformation("upsert doc:{obj} with resp:{resp}", jobConfig, upsertResp);
            req.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            return ApiResp.OkWith(jobConfig);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error while {nameof(AddJobConfigAsync)}");
            return ApiResp<JobConfig>.Error("AddJobConfigAsync exception:" + e.Message);
        }
    }
}

public class JobConfigValidator : AbstractValidator<JobConfig>
{
    public JobConfigValidator()
    {
        RuleFor(config => config).NotNull();
        RuleFor(config => config.NinAuthContext).NotNull();

        RuleFor(config => config.NinAuthContext.SessionToken).NotEmpty();

        RuleFor(config => config.EnabledQueries).NotNull();

        RuleFor(config => config.StatInkApiKey).NotEmpty();
        When(config => !string.IsNullOrWhiteSpace(config.NotificationEmail),
            () => { RuleFor(config => config.NotificationEmail).EmailAddress(); });
        RuleFor(config => config.NeedBuildFromBeginLimit).GreaterThanOrEqualTo(12);
    }
}