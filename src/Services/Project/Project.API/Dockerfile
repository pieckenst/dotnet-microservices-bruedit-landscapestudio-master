FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Services/Project/Project.API/Project.API.csproj", "Services/Project/Project.API/"]
COPY ["Services/Project/Project.Application/Project.Application.csproj", "Services/Project/Project.Application/"]
COPY ["Services/Project/Project.Domain/Project.Domain.csproj", "Services/Project/Project.Domain/"]
COPY ["Services/Project/Project.Infrastructure/Project.Infrastructure.csproj", "Services/Project/Project.Infrastructure/"]
COPY ["BuildingBlocks/Shared.Common/Shared.Common.csproj", "BuildingBlocks/Shared.Common/"]
COPY ["BuildingBlocks/Shared.ServiceKit/Shared.ServiceKit.csproj", "BuildingBlocks/Shared.ServiceKit/"]
COPY ["BuildingBlocks/Common.Logging/Common.Logging.csproj", "BuildingBlocks/Common.Logging/"]
RUN dotnet restore "Services/Project/Project.API/Project.API.csproj"
COPY . .
WORKDIR "/src/Services/Project/Project.API"
RUN dotnet build "Project.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Project.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
RUN mkdir -p /app/data && chown app:app /app/data
USER app
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Project.API.dll"]
