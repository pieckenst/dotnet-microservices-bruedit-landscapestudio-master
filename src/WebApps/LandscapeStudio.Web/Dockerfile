FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY WebApps/LandscapeStudio.Web/LandscapeStudio.Web.csproj WebApps/LandscapeStudio.Web/
RUN dotnet restore WebApps/LandscapeStudio.Web/LandscapeStudio.Web.csproj
COPY . .
RUN dotnet publish WebApps/LandscapeStudio.Web/LandscapeStudio.Web.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LandscapeStudio.Web.dll"]
