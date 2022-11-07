using System.Reflection;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Stat.Itok.Core.Handlers;

public class LoggingPipeline<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingPipeline<TRequest, TResponse>> _logger;

    public LoggingPipeline(ILogger<LoggingPipeline<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {Req}", typeof(TRequest).Name);
        var reqType = request.GetType();
        IList<PropertyInfo> props = new List<PropertyInfo>(reqType.GetProperties());
        foreach (var prop in props)
        {
            var propValue = prop.GetValue(request, null);
            _logger.LogTrace("{Property} : {@Value}", prop.Name, propValue);
        }

        try
        {
            var response = await next();
            _logger.LogTrace("Handled {TResp}", typeof(TResponse).Name);
            return response;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception in {Req}", typeof(TRequest).Name);
            throw;
        }
    }
}