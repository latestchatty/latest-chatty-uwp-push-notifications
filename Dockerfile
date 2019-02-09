FROM microsoft/dotnet:sdk AS build
WORKDIR /build

COPY src .
RUN dotnet publish -c Release SNPN/SNPN.csproj

FROM microsoft/dotnet:runtime

WORKDIR /dotnetapp

COPY --from=build /build/SNPN/bin/Release/netcoreapp2.1/publish/ .
COPY appsettings.json .

ENTRYPOINT ["dotnet", "SNPN.dll"]