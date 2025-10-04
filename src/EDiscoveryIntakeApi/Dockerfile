FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/EDiscoveryIntakeApi/EDiscoveryIntakeApi.csproj", "src/EDiscoveryIntakeApi/"]
COPY ["src/EDiscovery.Shared/EDiscovery.Shared.csproj", "src/EDiscovery.Shared/"]
RUN dotnet restore "src/EDiscoveryIntakeApi/EDiscoveryIntakeApi.csproj"
COPY . .
WORKDIR "/src/src/EDiscoveryIntakeApi"
RUN dotnet build "EDiscoveryIntakeApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EDiscoveryIntakeApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EDiscoveryIntakeApi.dll"]