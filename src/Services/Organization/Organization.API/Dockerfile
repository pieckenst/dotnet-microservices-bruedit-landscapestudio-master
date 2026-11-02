FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Services/Organization/Organization.API/Organization.API.csproj", "Services/Organization/Organization.API/"]
COPY ["Services/Organization/Organization.Application/Organization.Application.csproj", "Services/Organization/Organization.Application/"]
COPY ["Services/Organization/Organization.Domain/Organization.Domain.csproj", "Services/Organization/Organization.Domain/"]
COPY ["Services/Organization/Organization.Infrastructure/Organization.Infrastructure.csproj", "Services/Organization/Organization.Infrastructure/"]
COPY ["BuildingBlocks/Shared.Common/Shared.Common.csproj", "BuildingBlocks/Shared.Common/"]
COPY ["BuildingBlocks/Shared.ServiceKit/Shared.ServiceKit.csproj", "BuildingBlocks/Shared.ServiceKit/"]
COPY ["BuildingBlocks/Common.Logging/Common.Logging.csproj", "BuildingBlocks/Common.Logging/"]
RUN dotnet restore "Services/Organization/Organization.API/Organization.API.csproj"
COPY . .
WORKDIR "/src/Services/Organization/Organization.API"
RUN dotnet build "Organization.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Organization.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
RUN mkdir -p /app/data && chown app:app /app/data
USER app
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Organization.API.dll"]
