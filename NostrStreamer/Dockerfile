FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["NostrStreamer/NostrStreamer.csproj", "NostrStreamer/"]
RUN dotnet restore "NostrStreamer/NostrStreamer.csproj"
COPY . .
WORKDIR "/src/NostrStreamer"
RUN dotnet build "NostrStreamer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NostrStreamer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apt update \
    && apt install -y --no-install-recommends ffmpeg  \
    && apt clean \
    && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NostrStreamer.dll"]
