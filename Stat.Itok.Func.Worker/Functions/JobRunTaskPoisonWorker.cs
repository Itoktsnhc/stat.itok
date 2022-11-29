using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
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
    private readonly IStorageAccessSvc _storage;


    public JobRunTaskPoisonWorker(
        ILogger<JobRunTaskPoisonWorker> logger,
        IStorageAccessSvc storage,
        IJobTrackerClient jobTracker)
    {
        _logger = logger;
        _jobTracker = jobTracker;
        _storage = storage;
    }


    [FunctionName("JobWorkerPoison")]
    public async Task ActPoisonJobWorkerAsync([QueueTrigger(StatItokConstants.JobRunTaskQueueName + "-poison",
            Connection = "WorkerQueueConnStr")]
        QueueMessage queueMsg)
    {
        try
        {
            await TrySavePoisonAsync(queueMsg);
            var msgStr = Helper.DecompressStr(queueMsg.MessageText);
            var jobRunTaskLite = JsonConvert.DeserializeObject<JobRunTaskLite>(msgStr);
            _logger.LogWarning("Parsed Poison FOR TrackedId {jobId}", jobRunTaskLite.TrackedId);

            await _jobTracker.UpdateJobStatesAsync(jobRunTaskLite!.TrackedId, new UpdateJobStateDto(JobState.Faulted,
                $"PoisonJobWorker:{msgStr}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"ERROR WHEN {nameof(ActPoisonJobWorkerAsync)}");
        }
    }

    private async Task TrySavePoisonAsync(QueueMessage msg)
    {
        var fileName = $"{msg.MessageId}.payload";
        var container = await _storage.GetBlobClientAsync<PoisonQueueMsg>();
        var blob = container.GetBlockBlobClient(fileName);
        using var ms = new MemoryStream(Helper.CompressBytes(Encoding.UTF8.GetBytes(msg.MessageText)));
        ms.Seek(0, SeekOrigin.Begin);
        await blob.UploadAsync(ms);
        var properties = await blob.GetPropertiesAsync();
        await blob.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            // Set the MIME ContentType every time the properties 
            // are updated or the field will be cleared
            ContentType = "application/json; charset=utf8",
            ContentEncoding = "br",

            // Populate remaining headers with 
            // the pre-existing properties
            CacheControl = properties.Value.CacheControl,
            ContentDisposition = properties.Value.ContentDisposition,
            ContentHash = properties.Value.ContentHash
        });
    }
    public class PoisonQueueMsg { }

}