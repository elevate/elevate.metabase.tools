FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS builder

WORKDIR /sln
COPY . .

RUN dotnet publish

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-alpine
WORKDIR /app
COPY --from=builder ./sln/metabase-exporter/bin/Debug/netcoreapp3.1/publish .
ENTRYPOINT ["dotnet", "metabase-exporter.dll"]

