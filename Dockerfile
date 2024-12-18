FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /build

ARG BUILD_VERSION="1.0.0.0"

COPY . .

RUN dotnet test --configuration Release
RUN dotnet publish -c Release src/SNPN/SNPN.csproj -o /build/output /p:BuildVersion=${BUILD_VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine

RUN apk add curl --no-cache
WORKDIR /dotnetapp

COPY --from=build /build/output/ .
COPY appsettings.json .

HEALTHCHECK --interval=30s --timeout=30s --start-period=5s --retries=3 CMD [ "curl", "-f", "http://localhost:4000/health" ]
ENTRYPOINT ["dotnet", "SNPN.dll"]