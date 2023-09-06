namespace Stat.Itok.Worker;


public abstract class YetBgWorker : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _appStoppingTokenSource = new();

    private readonly IHostApplicationLifetime _appLifetime;

    protected YetBgWorker(IHostApplicationLifetime appLifetime)
    {
        this._appLifetime = appLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"IHostedService.StartAsync for {GetType().Name}");
        _appLifetime.ApplicationStarted.Register(
            // ReSharper disable once AsyncVoidLambda
            async () =>
                await ExecuteAsync(_appStoppingTokenSource.Token).ConfigureAwait(false)
        );
        return InitializingAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"IHostedService.StopAsync for {GetType().Name}");
        _appStoppingTokenSource.Cancel();
        await StoppingAsync(cancellationToken).ConfigureAwait(false);
        Dispose();
    }

    protected virtual Task InitializingAsync(CancellationToken cancelInitToken)
        => Task.CompletedTask;

    protected abstract Task ExecuteAsync(CancellationToken ctx);

    protected virtual Task StoppingAsync(CancellationToken cancelStopToken)
        => Task.CompletedTask;

    public virtual void Dispose()
    {
    }
}