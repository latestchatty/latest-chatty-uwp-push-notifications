#!/usr/bin/env bash
# Run from base project directory
dotnet restore && dotnet build **/project.json && dotnet test test/SNPN.Test/