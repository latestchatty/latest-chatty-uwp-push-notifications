FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /build

ARG BUILD_VERSION="1.0.0.0"

COPY . .

RUN dotnet publish -c Release src/SNPN/SNPN.csproj -r alpine-x64 /p:BuildVersion=${BUILD_VERSION}

FROM mcr.microsoft.com/dotnet/runtime:5.0-alpine

WORKDIR /dotnetapp

COPY --from=build /build/src/SNPN/bin/Release/netcoreapp5.0/alpine-x64/publish/ .
COPY appsettings.json .

ENTRYPOINT ["./SNPN"]