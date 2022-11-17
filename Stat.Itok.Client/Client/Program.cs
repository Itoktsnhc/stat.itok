using Havit.Blazor.Components.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Text.Json;

namespace Stat.Itok.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");
            builder.Services.AddHxServices();
            builder.Services.AddSingleton(_ => new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
            });
            builder.Services.AddHxMessenger();
            builder.Services.AddHxMessageBoxHost();
            builder.Services.AddScoped(sp =>
            {
                var client = new HttpClient
                {
                    BaseAddress = new Uri(builder.Configuration["API_Prefix"] ?? builder.HostEnvironment.BaseAddress)
                };
                return client;
            });

            await builder.Build().RunAsync();
        }
    }
}