FROM microsoft/dotnet:sdk AS build
WORKDIR /build

ARG BUILD_VERSION

COPY src .

# RUN dotnet publish -c Release SNPN/SNPN.csproj -r alpine-x64 /p:BuildVersion=$(BUILD_VERSION)

RUN dotnet publish -c Release SNPN/SNPN.csproj /p:BuildVersion=${BUILD_VERSION}

FROM microsoft/dotnet:runtime

WORKDIR /dotnetapp

COPY --from=build /build/SNPN/bin/Release/netcoreapp2.1/publish/ .
COPY appsettings.json .

ENTRYPOINT ["dotnet", "SNPN.dll"]

# FROM alpine

# RUN apk add --no-cache libstdc++ libgcc libintl icu-libs libssl1.0

# WORKDIR /dotnetapp

# COPY --from=build /build/SNPN/bin/Release/netcoreapp2.1/alpine-x64/publish/ .
# COPY appsettings.json .
# #RUN ls

# ENTRYPOINT ["./SNPN"]