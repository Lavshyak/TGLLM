FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER root
RUN apt-get update && apt-get install -y --no-install-recommends libgomp1
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TGLLM.App/TGLLM.App.fsproj", "TGLLM.App/"]
RUN dotnet restore "TGLLM.App/TGLLM.App.fsproj"
COPY . .
WORKDIR "/src/TGLLM.App"
RUN dotnet build "TGLLM.App.fsproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "TGLLM.App.fsproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER $APP_UID
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TGLLM.App.dll"]
