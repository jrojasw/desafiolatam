# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/CentralPsi.Web/CentralPsi.Web.csproj src/CentralPsi.Web/
RUN dotnet restore src/CentralPsi.Web/CentralPsi.Web.csproj -r linux-x64

COPY src/CentralPsi.Web/ src/CentralPsi.Web/
# Self-contained: bundles the .NET 8 runtime into the app itself, so it doesn't matter that the runtime stage
# below is built around .NET 10 (needed for the Playwright base image) - no shared-runtime version conflict.
RUN dotnet publish src/CentralPsi.Web/CentralPsi.Web.csproj -c Release -o /app/publish --no-restore \
    --self-contained true -r linux-x64

# Runtime stage - based on Microsoft's official Playwright image (Chromium/Firefox/WebKit + all their OS
# dependencies already baked in for Ubuntu Noble) so the certificate-validation feature can render the
# Superintendencia de Salud's JavaScript-based lookup page like a real browser. The tag's Playwright version
# (v1.61.0) must stay in sync with the Microsoft.Playwright NuGet package version in the .csproj - a mismatch
# here means "browser not found" at runtime.
FROM mcr.microsoft.com/playwright/dotnet:v1.61.0-noble AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .

ENTRYPOINT ["./CentralPsi.Web"]
