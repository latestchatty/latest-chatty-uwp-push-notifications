FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /build

ARG BUILD_VERSION="1.0.0.0"

COPY . .

RUN dotnet publish -c Release src/SNPN/SNPN.csproj -r alpine-x64 /p:BuildVersion=${BUILD_VERSION}

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-alpine

WORKDIR /dotnetapp

COPY --from=build /build/src/SNPN/bin/Release/netcoreapp3.1/alpine-x64/publish/ .
COPY appsettings.json .

ENTRYPOINT ["./SNPN"]