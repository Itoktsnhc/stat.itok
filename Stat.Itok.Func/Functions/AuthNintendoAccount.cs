using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stat.Itok.Core.Handlers;

namespace Stat.Itok.Func.Functions;

public class AuthNintendoAccount
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthNintendoAccount> _logger;

    public AuthNintendoAccount(IMediator mediator, ILogger<AuthNintendoAccount> logger)
    {
        _mediator = mediator;
        _logger = logger;
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
            var authContext = await _mediator.Send(new ReqGenAuthContext() {TokenCopyInfo = data});
            req.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            return ApiResp.OkWith(authContext);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error while {nameof(AuthNintendoAccountAsync)}");
            return ApiResp<NinAuthContext>.Error("NintendoAccountAuth exception:" + e);
        }
    }
}