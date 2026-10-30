FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Services/Procurement/Procurement.API/Procurement.API.csproj", "Services/Procurement/Procurement.API/"]
COPY ["Services/Procurement/Procurement.Domain/Procurement.Domain.csproj", "Services/Procurement/Procurement.Domain/"]
COPY ["Services/Procurement/Procurement.Infrastructure/Procurement.Infrastructure.csproj", "Services/Procurement/Procurement.Infrastructure/"]
COPY ["BuildingBlocks/Shared.Common/Shared.Common.csproj", "BuildingBlocks/Shared.Common/"]
COPY ["BuildingBlocks/Common.Logging/Common.Logging.csproj", "BuildingBlocks/Common.Logging/"]
RUN dotnet restore "Services/Procurement/Procurement.API/Procurement.API.csproj"
COPY . .
WORKDIR "/src/Services/Procurement/Procurement.API"
RUN dotnet build "Procurement.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Procurement.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
RUN mkdir -p /app/data && chown app:app /app/data
USER app
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Procurement.API.dll"]