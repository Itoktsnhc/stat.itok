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
ENTRYPOINT ["dotnet", "Stat.Itok.Worker.dll"]
