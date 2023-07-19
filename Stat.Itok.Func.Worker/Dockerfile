FROM mcr.microsoft.com/dotnet/sdk:6.0 AS installer-env

# Build requires 3.1 SDK
COPY --from=mcr.microsoft.com/dotnet/core/sdk:3.1 /usr/share/dotnet /usr/share/dotnet

COPY ./Stat.Itok.Func.Worker /src/dotnet-function-app/Stat.Itok.Func.Worker
COPY ./Stat.Itok.Core /src/dotnet-function-app/Stat.Itok.Core
COPY ./Stat.Itok.Shared /src/dotnet-function-app/Stat.Itok.Shared

RUN cd /src/dotnet-function-app/Stat.Itok.Func.Worker && \
    mkdir -p /home/site/wwwroot && \
    dotnet publish --configuration Release ./Stat.Itok.Func.Worker.csproj --output /home/site/wwwroot

# To enable ssh & remote debugging on app service change the base image to the one below
# FROM mcr.microsoft.com/azure-functions/dotnet:4-appservice
FROM mcr.microsoft.com/azure-functions/dotnet:4
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY ./_misc /home/

ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}
ENV CORECLR_PROFILER_PATH=/home/Pyroscope.Profiler.Native.so
ENV LD_PRELOAD=/home/Pyroscope.Linux.ApiWrapper.x64.so


ENV PYROSCOPE_APPLICATION_NAME=stat.itok.worker
ENV PYROSCOPE_SERVER_ADDRESS=http://pyroscope:4040/
ENV PYROSCOPE_LOG_LEVEL=debug
ENV PYROSCOPE_PROFILING_ENABLED=1
ENV PYROSCOPE_PROFILING_ALLOCATION_ENABLED=true
ENV PYROSCOPE_PROFILING_CONTENTION_ENABLED=true
ENV PYROSCOPE_PROFILING_EXCEPTION_ENABLED=true

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]