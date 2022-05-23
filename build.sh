#!/usr/bin/env bash
set -euo pipefail

mkdir -p output

alias py=/usr/bin/python3

# The normal build
dotnet build -c Release \
    -p:PythonBin:/usr/bin/python3
    -p:OutputPath=../output/
