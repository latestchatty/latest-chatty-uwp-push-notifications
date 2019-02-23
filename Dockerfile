FROM microsoft/dotnet:sdk AS build
WORKDIR /build

ARG BUILD_VERSION="1.0.0.0"

COPY . .

RUN dotnet publish -c Release src/SNPN/SNPN.csproj -r alpine-x64 /p:BuildVersion=${BUILD_VERSION}

FROM microsoft/dotnet:2.1-runtime-deps-alpine

WORKDIR /dotnetapp

COPY --from=build /build/src/SNPN/bin/Release/netcoreapp2.1/alpine-x64/publish/ .
COPY appsettings.json .

ENTRYPOINT ["./SNPN"]