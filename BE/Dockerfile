FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NuGet.Config ./
COPY TetGift.DAL/TetGift.DAL.csproj TetGift.DAL/
COPY TetGift.BLL/TetGift.BLL.csproj TetGift.BLL/
COPY TetGift/TetGift.csproj TetGift/
RUN dotnet restore TetGift/TetGift.csproj --configfile NuGet.Config

COPY . .
WORKDIR /src/TetGift
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/playwright/dotnet:v1.59.0-noble AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

EXPOSE 10000

USER pwuser
ENTRYPOINT ["dotnet", "TetGift.dll"]
