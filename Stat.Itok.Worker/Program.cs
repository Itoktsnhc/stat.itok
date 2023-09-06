using System.Net;
using JobTrackerX.Client;
using Microsoft.Extensions.Options;
using Stat.Itok.Core;
using Stat.Itok.Core.ApiClients;
using Stat.Itok.Worker;
using Stat.Itok.Worker.Workers;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(b => BuildConfiguration(args, b))
    .ConfigureServices((builder, services) =>
    {
        services.AddConfigByType<GlobalConfig>(builder.Configuration);
        services.AddHttpClient()
            .AddMemoryCache()
            .AddMediator(cfg => cfg.ServiceLifetime = ServiceLifetime.Transient)
            .AddSingleton<IStorageAccessor, StorageAccessor>()
            .AddSingleton<IJobTrackerClient, JobTrackerClient>(x =>
                new JobTrackerClient(x.GetRequiredService<IOptions<GlobalConfig>>().Value.JobSysBase))
            .AddSingleton<RemoteConfigStore>()
            .AddSingleton<ICosmosAccessor, CosmosDbAccessor>()
            .AddSingleton(sp =>
            {
                var store = sp.GetRequiredService<RemoteConfigStore>();
                return store.GetNinMiscConfigAsync().GetAwaiter().GetResult();
            });
        services.AddHttpClient<INintendoApi, NintendoApi>()
            .ConfigurePrimaryHttpMessageHandler(_ =>
                new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                });
        services.AddHttpClient<IImInkApi, ImInkApi>()
            .ConfigurePrimaryHttpMessageHandler(_ =>
                new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                });
        services.AddHttpClient<IStatInkApi, StatInkApi>()
            .ConfigurePrimaryHttpMessageHandler(_ =>
                new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                });

        services.AddHostedService<Dispatcher>();
        services.AddHostedService<TaskWorker>();
    })
    .Build();

await host.RunAsync();
return 0;


static void BuildConfiguration(string[] args, IConfigurationBuilder builder = null)
{
    builder ??= new ConfigurationBuilder();
    var configFileName = Helper.GetConfigFileName();
    builder.SetBasePath(Directory.GetCurrentDirectory())
        .AddCommandLine(args)
        .AddJsonFile(Constant.DefaultConfigName)
        .AddJsonFile(configFileName)
        .AddEnvironmentVariables()
        .Build();
}