FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS builder

WORKDIR /sln
COPY . .

RUN dotnet publish

FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine
WORKDIR /app
COPY --from=builder ./sln/metabase-exporter/bin/Debug/net6.0/publish .
ENTRYPOINT ["dotnet", "metabase-exporter.dll"]

