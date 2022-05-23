#!/usr/bin/env bash
set -euo pipefail

mkdir -p output

ln -s /usr/bin/python3 /usr/bin/py

# The normal build
dotnet build -c Release \
    -p:OutputPath=../output/
