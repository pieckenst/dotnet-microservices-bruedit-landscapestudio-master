FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DesktopClients/by.pieckenst.debugtool.testapiconnectionstool/ ./tool/
RUN dotnet publish ./tool/by.pieckenst.debugtool.testapiconnectionstool.csproj -c Release -p:PublishAot=false -o /app/out
COPY ApiGateways/OcelotApiGateway/ocelot.json /app/out/ocelot.json

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "by.pieckenst.debugtool.testapiconnectionstool.dll"]
