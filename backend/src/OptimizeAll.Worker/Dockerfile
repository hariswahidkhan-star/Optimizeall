# syntax=docker/dockerfile:1

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

RUN dotnet restore src/OptimizeAll.Worker/OptimizeAll.Worker.csproj

FROM restore AS publish
COPY backend/ ./
RUN dotnet publish src/OptimizeAll.Worker/OptimizeAll.Worker.csproj \
        --configuration Release \
        --no-restore \
        --output /app

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app

RUN groupadd --system --gid 64198 optimizeall \
    && useradd --system --uid 64198 --gid 64198 --no-create-home optimizeall

COPY --from=publish --chown=optimizeall:optimizeall /app ./
USER optimizeall

ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "OptimizeAll.Worker.dll"]
