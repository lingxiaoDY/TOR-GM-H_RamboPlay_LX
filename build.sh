#!/usr/bin/env bash
set -euo pipefail

mkdir -p output

export PythonBin=python

# The normal build
dotnet build -c Release \
    -p:OutputPath=../output/
