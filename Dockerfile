FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build

ARG BUILD_VERSION="1.0.0.0"

COPY . .

RUN dotnet test --configuration Release
RUN dotnet publish -c Release src/SNPN/SNPN.csproj -r linux-x64 /p:BuildVersion=${BUILD_VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /dotnetapp

COPY --from=build /build/src/SNPN/bin/Release/net8.0/linux-x64/publish/ .
COPY appsettings.json .

ENTRYPOINT ["dotnet", "SNPN.dll"]