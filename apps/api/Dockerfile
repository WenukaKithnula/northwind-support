# The API image. Build it from the repository root, because the shared build files live there:
#   docker build -f apps/api/Dockerfile -t northwind-api .
# docker compose runs that for you.

# --- Stage 1: build and publish with the SDK -----------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore needs only the project files and the shared build files, so those are copied alone and
# restored first. Docker caches the result, and it is only thrown away when one of these files
# changes: editing a .cs file leaves it alone.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# Only now the source, so a code change rebuilds from here and not from the restore above.
COPY apps/api/src apps/api/src
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    --configuration Release --no-restore --output /app /p:UseAppHost=false

# --- Stage 2: run on the much smaller ASP.NET runtime image -----------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Plain HTTP on 8080. TLS is ended in front of the container, so there are no certificates here.
# Nothing is configured in the image: the connection string, the signing key and the environment
# name arrive as environment variables when the container starts (see docker-compose.yml).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# The runtime image comes with an unprivileged user; run as it instead of root.
USER $APP_UID

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
