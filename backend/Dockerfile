# syntax=docker/dockerfile:1
# Production image of PhotoStudio for Railway. Build context: the backend folder.
# One image, two services: the API (default entrypoint) and the worker (start command: dotnet worker/PhotoStudio.Worker.dll).

# ---- Build stage: restore with only the project files first so the restore layer is cached between builds.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/PhotoStudio.Domain/PhotoStudio.Domain.csproj src/PhotoStudio.Domain/
COPY src/PhotoStudio.Application/PhotoStudio.Application.csproj src/PhotoStudio.Application/
COPY src/PhotoStudio.Infrastructure/PhotoStudio.Infrastructure.csproj src/PhotoStudio.Infrastructure/
COPY src/PhotoStudio.Api/PhotoStudio.Api.csproj src/PhotoStudio.Api/
COPY src/PhotoStudio.Worker/PhotoStudio.Worker.csproj src/PhotoStudio.Worker/
RUN dotnet restore src/PhotoStudio.Api/PhotoStudio.Api.csproj \
    && dotnet restore src/PhotoStudio.Worker/PhotoStudio.Worker.csproj

COPY src/ src/
RUN dotnet publish src/PhotoStudio.Api/PhotoStudio.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false
RUN dotnet publish src/PhotoStudio.Worker/PhotoStudio.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish-worker \
    /p:UseAppHost=false

# ---- Runtime stage: ASP.NET runtime only, running as the non-root user provided by the image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
COPY --from=build /app/publish-worker ./worker

USER $APP_UID
ENTRYPOINT ["dotnet", "PhotoStudio.Api.dll"]
