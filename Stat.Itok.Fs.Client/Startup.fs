namespace Stat.Itok.Fs.Client

open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open Microsoft.Extensions.DependencyInjection
open System
open System.Net.Http

module Program =

    [<EntryPoint>]
    let Main args =
        let builder = WebAssemblyHostBuilder.CreateDefault(args)
        builder.RootComponents.Add<Main.StatItokApp>("#main")
        builder.Services.AddScoped<HttpClient>(fun _ ->
            let baseAddr =  
                if builder.Configuration["API_Prefix"] = null 
                then builder.HostEnvironment.BaseAddress 
                else builder.Configuration["API_Prefix"]
            new HttpClient(BaseAddress =new Uri(baseAddr) )
           ) |> ignore
        builder.Build().RunAsync() |> ignore
        0
