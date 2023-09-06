using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using FluentValidation;
using JobTrackerX.Client;
using Mediator;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stat.Itok.Core.ApiClients;
using Stat.Itok.Core.Handlers;
using Stat.Itok.Func.Functions;

[assembly: FunctionsStartup(typeof(Stat.Itok.Func.Startup))]

namespace Stat.Itok.Func
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.RegisterConfiguration<GlobalConfig>(nameof(GlobalConfig), ServiceLifetime.Scoped);
            builder.Services.AddHttpClient()
                .AddMemoryCache()
                .AddSingleton<IStorageAccessor, StorageAccessor>()
                .AddMediator(cfg => cfg.ServiceLifetime = ServiceLifetime.Transient)
                .AddSingleton<RemoteConfigStore>()
                .AddSingleton<ICosmosAccessor, CosmosDbAccessor>()
                .AddSingleton(sp =>
                {
                    var store = sp.GetRequiredService<RemoteConfigStore>();
                    return store.GetNinMiscConfigAsync().GetAwaiter().GetResult();
                })
                .AddLogging();

            builder.Services.AddHttpClient<INintendoApi, NintendoApi>()
                .ConfigurePrimaryHttpMessageHandler(_ =>
                    new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    });
            builder.Services.AddHttpClient<IImInkApi, ImInkApi>()
                .ConfigurePrimaryHttpMessageHandler(_ =>
                    new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    });
            builder.Services.AddHttpClient<IStatInkApi, StatInkApi>()
                .ConfigurePrimaryHttpMessageHandler(_ =>
                    new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    });

            builder.Services.AddScoped<IValidator<JobConfig>, JobConfigValidator>();
            builder.Services.AddScoped<IValidator<NinAuthContext>, NinAuthContextValidator>();
            builder.Services.AddSingleton<IJobTrackerClient, JobTrackerClient>(x =>
                    new JobTrackerClient(x.GetRequiredService<IOptions<GlobalConfig>>().Value.JobSysBase));

            //Replace ILogger<T> with the one that works fine in all scenarios

            var logger = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(ILogger<>));
            if (logger != null)
                builder.Services.Remove(logger);

            builder.Services.Add(new ServiceDescriptor(typeof(ILogger<>), typeof(FunctionsLogger<>), ServiceLifetime.Transient));
        }

        class FunctionsLogger<T> : ILogger<T>
        {
            readonly ILogger _logger;
            public FunctionsLogger(ILoggerFactory factory)
                // See https://github.com/Azure/azure-functions-host/issues/4689#issuecomment-533195224
                => _logger = factory.CreateLogger(LogCategories.CreateFunctionUserCategory(typeof(T).FullName));
            public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);
            public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                => _logger.Log(logLevel, eventId, state, exception, formatter);
        }

    }

    public static class ServiceExtensions
    {
        public static void RegisterConfiguration<TCustomConfiguration>(this IServiceCollection services, string sectionName, ServiceLifetime serviceLifetime) where TCustomConfiguration : class, new()
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (string.IsNullOrWhiteSpace(sectionName))
            {
                throw new ArgumentNullException(nameof(sectionName));
            }

            services.AddOptions<TCustomConfiguration>().Configure<IConfiguration>((customSetting, configuration) =>
            {
                configuration.GetSection(sectionName).Bind(customSetting);
            });

            services.Add(new ServiceDescriptor(typeof(TCustomConfiguration), provider =>
            {
                var options = provider.GetRequiredService<IOptions<TCustomConfiguration>>();
                return options.Value;
            }, serviceLifetime));
        }

        public static void RegisterConfiguration<TCustomConfiguration>(this IServiceCollection services, ServiceLifetime serviceLifetime) where TCustomConfiguration : class, new()
        {
            services.RegisterConfiguration<TCustomConfiguration>(typeof(TCustomConfiguration).Name, serviceLifetime);
        }
    }
}
