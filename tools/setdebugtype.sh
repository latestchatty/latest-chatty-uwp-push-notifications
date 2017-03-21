#!/usr/bin/env bash
# Run from base project directory
find . -type f -name "SNPN.csproj" -exec sed -i 's/<DebugType>full<\/DebugType>/<DebugType>portable<\/DebugType>/g' {} +