# syntax=docker/dockerfile:1

# Restore is a separate stage keyed only on project files, so editing source does not invalidate
# the NuGet layer — the slowest part of the build by a wide margin.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

COPY backend/Directory.Build.props backend/Directory.Packages.props backend/OptimizeAll.sln ./
COPY backend/src/OptimizeAll.SharedKernel/*.csproj src/OptimizeAll.SharedKernel/
COPY backend/src/OptimizeAll.Domain/*.csproj src/OptimizeAll.Domain/
COPY backend/src/OptimizeAll.Application/*.csproj src/OptimizeAll.Application/
COPY backend/src/OptimizeAll.Infrastructure/*.csproj src/OptimizeAll.Infrastructure/
COPY backend/src/OptimizeAll.Api/*.csproj src/OptimizeAll.Api/
COPY backend/src/OptimizeAll.Worker/*.csproj src/OptimizeAll.Worker/
COPY backend/tests/OptimizeAll.Domain.Tests/*.csproj tests/OptimizeAll.Domain.Tests/
COPY backend/tests/OptimizeAll.Application.Tests/*.csproj tests/OptimizeAll.Application.Tests/
COPY backend/tests/OptimizeAll.Architecture.Tests/*.csproj tests/OptimizeAll.Architecture.Tests/

RUN dotnet restore src/OptimizeAll.Api/OptimizeAll.Api.csproj

FROM restore AS publish
COPY backend/ ./
RUN dotnet publish src/OptimizeAll.Api/OptimizeAll.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Runs unprivileged. A container process that does not need root should not have it, and this is a
# process that executes model-directed work.
RUN groupadd --system --gid 64198 optimizeall \
    && useradd --system --uid 64198 --gid 64198 --no-create-home optimizeall

COPY --from=publish --chown=optimizeall:optimizeall /app ./
USER optimizeall

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD ["dotnet", "OptimizeAll.Api.dll", "--health-check"]

ENTRYPOINT ["dotnet", "OptimizeAll.Api.dll"]
