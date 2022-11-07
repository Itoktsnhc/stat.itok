using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stat.Itok.Core;
using Stat.Itok.Core.ApiClients;
using System.Net;
using MediatR;
using Stat.Itok.Core.Handlers;

Console.WriteLine("Hello, World!");

var svc = new ServiceCollection()
    .AddSingleton(_ => Options.Create(new GlobalConfig()))
    .AddHttpClient()
    .AddMemoryCache()
    .AddMediatR(typeof(NintendoPublicHandlers))
    .AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipeline<,>))
    .AddLogging();
svc.AddHttpClient<INintendoApi, NintendoApi>()
    .ConfigurePrimaryHttpMessageHandler(x =>
        new HttpClientHandler()
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });
svc.AddHttpClient<IImInkApi, ImInkApi>()
    .ConfigurePrimaryHttpMessageHandler(x =>
        new HttpClientHandler()
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });
svc.AddHttpClient<IStatInkApi, StatInkApi>()
    .ConfigurePrimaryHttpMessageHandler(x =>
        new HttpClientHandler()
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });
var sp = svc.BuildServiceProvider();

var mediator = sp.GetRequiredService<IMediator>();
var link = await mediator.Send(new ReqGetTokenCopyInfo());
Console.WriteLine(link.TokenCopyUrl);

var redirect = Console.ReadLine();
link.RedirectUrl = redirect;
var authCtx = await mediator.Send(new ReqGenAuthContext()
{
    TokenCopyInfo = link
});

var queryRes = await mediator.Send(new ReqDoGraphQL()
{
    AuthContext = authCtx,
    QueryHash = QueryHash.RegularBattleHistories
});


Console.ReadLine();