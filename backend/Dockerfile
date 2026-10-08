# Build: docker build -t liveboard-api ./backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY LiveBoard.Api/LiveBoard.Api.csproj LiveBoard.Api/
RUN dotnet restore LiveBoard.Api/LiveBoard.Api.csproj
COPY LiveBoard.Api/ LiveBoard.Api/
RUN dotnet publish LiveBoard.Api/LiveBoard.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    Database__MigrateOnStartup=true
# Render (and most PaaS hosts) inject PORT; default to 8080 elsewhere.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet LiveBoard.Api.dll"]
