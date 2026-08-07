# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/CentralPsi.Web/CentralPsi.Web.csproj src/CentralPsi.Web/
RUN dotnet restore src/CentralPsi.Web/CentralPsi.Web.csproj

COPY src/CentralPsi.Web/ src/CentralPsi.Web/
RUN dotnet publish src/CentralPsi.Web/CentralPsi.Web.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CentralPsi.Web.dll"]
