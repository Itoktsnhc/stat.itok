using System;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using JobTrackerX.Client;
using JobTrackerX.SharedLibs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stat.Itok.Core;

namespace Stat.Itok.Func.Worker.Functions;

public class JobRunTaskPoisonWorker
{
    private readonly ILogger<JobRunTaskPoisonWorker> _logger;
    private readonly IJobTrackerClient _jobTracker;

    public JobRunTaskPoisonWorker(
        ILogger<JobRunTaskPoisonWorker> logger,
        IJobTrackerClient jobTracker)
    {
        _logger = logger;
        _jobTracker = jobTracker;
    }

    
    [FunctionName("JobWorkerPoison")]
    public async Task ActPoisonJobWorkerAsync([QueueTrigger(StatItokConstants.JobRunTaskQueueName + "-poison",
            Connection = "WorkerQueueConnStr")]
        QueueMessage queueMsg)
    {
        try
        {
            var msgStr = Helper.DecompressStr(queueMsg.MessageText);
            var jobRunTaskLite = JsonConvert.DeserializeObject<JobRunTaskLite>(msgStr);
            await _jobTracker.UpdateJobStatesAsync(jobRunTaskLite!.TrackedId, new UpdateJobStateDto(JobState.Faulted,
                $"PoisonJobWorker:{msgStr}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ERROR WHEN {nameof(ActPoisonJobWorkerAsync)}");
        }
    }

}