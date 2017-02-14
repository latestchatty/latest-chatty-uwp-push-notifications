#!/usr/bin/env bash
# Run from base project directory
find . -type f -name "project.json" -exec sed -i 's/\"debugType\"[[:space:]]*:[[:space:]]*\"full\"/\"debugType\": \"portable\"/g' {} +