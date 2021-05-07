FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS builder

WORKDIR /sln
COPY . .

RUN dotnet publish

FROM mcr.microsoft.com/dotnet/runtime:5.0-alpine
WORKDIR /app
COPY --from=builder ./sln/metabase-exporter/bin/Debug/net5.0/publish .
ENTRYPOINT ["dotnet", "metabase-exporter.dll"]

