FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/CronogramaTrabajo.Web/CronogramaTrabajo.Web.csproj src/CronogramaTrabajo.Web/
RUN dotnet restore src/CronogramaTrabajo.Web/CronogramaTrabajo.Web.csproj

COPY src/CronogramaTrabajo.Web/ src/CronogramaTrabajo.Web/
RUN dotnet publish src/CronogramaTrabajo.Web/CronogramaTrabajo.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "CronogramaTrabajo.Web.dll"]
