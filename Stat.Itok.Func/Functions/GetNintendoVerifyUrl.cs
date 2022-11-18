using System.Threading.Tasks;
using MediatR;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Stat.Itok.Core.Handlers;

namespace Stat.Itok.Func.Functions;

public class GetNintendoVerifyUrl
{
    private readonly IMediator _mediator;

    public GetNintendoVerifyUrl(IMediator mediator)
    {
        _mediator = mediator;
    }

    [FunctionName("GetNintendoVerifyUrl")]
    public async Task<ApiResp<NinTokenCopyInfo>> GetNintendoVerifyUrlAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "nin/verify_url")]
        HttpRequest req)
    {
        var tokenCopyInfo = await _mediator.Send(new ReqGetTokenCopyInfo());
        return ApiResp.OkWith(tokenCopyInfo);
    }
}