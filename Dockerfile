FROM microsoft/dotnet
#FROM microsoft/dotnet:nanoserver
WORKDIR /dotnetapp
COPY bin/Release/netcoreapp1.0/publish/ .
COPY appsettings.docker.json ./appsettings.json
ENTRYPOINT ["dotnet", "SNPN.dll"]
