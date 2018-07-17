FROM microsoft/dotnet:2.1-sdk-alpine AS builder

WORKDIR /sln
COPY . .

RUN dotnet publish

FROM microsoft/dotnet:2.1-runtime-alpine
WORKDIR /app
COPY --from=builder ./sln/metabase-exporter/bin/Debug/netcoreapp2.0/publish .
ENTRYPOINT ["dotnet", "metabase-exporter.dll"]

