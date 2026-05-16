FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ProductTrackerBot/ProductTrackerBot.csproj ProductTrackerBot/
COPY ProductTrackerBot.Web/ProductTrackerBot.Web.csproj ProductTrackerBot.Web/
RUN dotnet restore ProductTrackerBot.Web/ProductTrackerBot.Web.csproj
COPY ProductTrackerBot/ ProductTrackerBot/
COPY ProductTrackerBot.Web/ ProductTrackerBot.Web/
RUN dotnet publish ProductTrackerBot.Web/ProductTrackerBot.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "ProductTrackerBot.Web.dll"]
