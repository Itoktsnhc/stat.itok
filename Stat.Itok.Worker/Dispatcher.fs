namespace Stat.Itok.Worker

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type Dispatcher(logger: ILogger<Dispatcher>) =
    inherit BackgroundService()
    let DoConfigDispatchAsync() = async {
            logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now)
        }
    
    override _.ExecuteAsync(ct: CancellationToken) =
        task {
            while not ct.IsCancellationRequested do
                try
                    do! DoConfigDispatchAsync()
                with
                    | ex  -> logger.LogError(ex, $"Error when do {nameof(Dispatcher)}")
                    
                do! Task.Delay(TimeSpan.FromMinutes(5))
                
        }
    