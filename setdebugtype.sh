#!/usr/bin/env bash
find . -type f -name "project.json" -exec sed -i 's/\"debugType\"[[:space:]]*:[[:space:]]*\"full\"/\"debugType\": \"portable\"/g' {} +