using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Stat.Itok.Func.Functions
{
    public static class EmptyActive
    {
        [FunctionName("EmptyActive")]
        public static async Task<IActionResult> RunEmptyActiveAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "active_func")] HttpRequest req,
            ILogger log)
        {
            await Task.CompletedTask;
            return new OkObjectResult("OK");
        }
    }
}
