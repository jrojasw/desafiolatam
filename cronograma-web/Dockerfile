FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/CronogramaTrabajo.Web/CronogramaTrabajo.Web.csproj src/CronogramaTrabajo.Web/
RUN dotnet restore src/CronogramaTrabajo.Web/CronogramaTrabajo.Web.csproj

COPY src/CronogramaTrabajo.Web/ src/CronogramaTrabajo.Web/
RUN dotnet publish src/CronogramaTrabajo.Web/CronogramaTrabajo.Web.csproj -c Release -o /app/publish

FROM litestream/litestream:latest AS litestream

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=litestream /usr/local/bin/litestream /usr/local/bin/litestream
COPY litestream.yml /etc/litestream.yml
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["/app/entrypoint.sh"]
