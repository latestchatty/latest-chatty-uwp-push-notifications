#!/usr/bin/env bash
# Run from base project directory
dotnet restore
dotnet build
dotnet test test/SNPN.Test/SNPN.csproj