#!/usr/bin/env bash

set -euo pipefail

read -r -p "Stop other processes and run the .NET benchmark suite? [y/N] " answer

case "$answer" in
  y|Y|yes|YES)
    dotnet run \
      --configuration Release \
      --project benchmarks/Reified.Schema.Benchmarks/ReifiedBenchmarks.fsproj \
      -- \
      "$@"
    ;;
  *)
    echo "Benchmark run cancelled."
    exit 1
    ;;
esac
