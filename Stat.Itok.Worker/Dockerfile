FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Stat.Itok.Worker/Stat.Itok.Worker.csproj", "Stat.Itok.Worker/"]
COPY ["Stat.Itok.Core/Stat.Itok.Core.csproj", "Stat.Itok.Core/"]
COPY ["Stat.Itok.Shared/Stat.Itok.Shared.csproj", "Stat.Itok.Shared/"]
RUN dotnet restore "Stat.Itok.Worker/Stat.Itok.Worker.csproj"
COPY . .
WORKDIR "/src/Stat.Itok.Worker"
RUN dotnet build "Stat.Itok.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Stat.Itok.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

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


ENTRYPOINT ["dotnet", "Stat.Itok.Worker.dll"]
