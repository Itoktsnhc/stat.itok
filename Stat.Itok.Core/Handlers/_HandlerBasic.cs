using Polly;

namespace Stat.Itok.Core.Handlers;

public class HandlerBase
{
    protected async Task<string> RunWithDefaultPolicy(Task<HttpResponseMessage> task, bool onlyRedirectUrl = false)
    {
        var policyResult = await Policy.Handle<Exception>()
            .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(3 + i))
            .ExecuteAndCaptureAsync(async () => await task);
        if (policyResult.FaultType != null)
        {
            throw policyResult.FinalException;
        }

        if (!policyResult.Result.IsSuccessStatusCode)
        {
            throw new Exception($"req Failed, rawResp[{policyResult.Result.StatusCode}] " +
                $"is: {await policyResult.Result.Content.ReadAsStringAsync()}");
        }

        if (onlyRedirectUrl)
        {
            return policyResult!.Result!.RequestMessage!.RequestUri!.ToString();
        }

        return await policyResult.Result.Content.ReadAsStringAsync();
    }
}