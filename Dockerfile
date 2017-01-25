FROM microsoft/dotnet
# WORKDIR /dotnetapp
# COPY project.json .
# RUN dotnet restore
# COPY . .
# RUN dotnet publish -c Release -o out
# ENTRYPOINT ["dotnet", "out/dotnetapp.dll"]
#FROM microsoft/dotnet:nanoserver
WORKDIR /dotnetapp
COPY bin/Release/netcoreapp1.0/publish/ .
COPY appsettings.json .
ENTRYPOINT ["dotnet", "dotnetapp.dll"]