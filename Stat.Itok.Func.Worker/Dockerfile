FROM mcr.microsoft.com/dotnet/sdk:6.0 AS installer-env

# Build requires 3.1 SDK
COPY --from=mcr.microsoft.com/dotnet/core/sdk:3.1 /usr/share/dotnet /usr/share/dotnet

COPY ./Stat.Itok.Func.Worker /src/dotnet-function-app/Stat.Itok.Func.Worker
COPY ./Stat.Itok.Core /src/dotnet-function-app/Stat.Itok.Core
COPY ./Stat.Itok.Shared /src/dotnet-function-app/Stat.Itok.Shared

RUN cd /src/dotnet-function-app/Stat.Itok.Func.Worker && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish ./Stat.Itok.Func.Worker.csproj --output /home/site/wwwroot

# To enable ssh & remote debugging on app service change the base image to the one below
# FROM mcr.microsoft.com/azure-functions/dotnet:4-appservice
FROM mcr.microsoft.com/azure-functions/dotnet:4
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]