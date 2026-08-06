#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
for product in Result Constraint Refinements Schema; do
  project="$ROOT_DIR/examples/Reified.$product.AotProbe/Reified.$product.AotProbe.fsproj"
  publish_dir="$ROOT_DIR/artifacts/publish/Reified.$product.AotProbe/linux-x64"

  dotnet publish "$project" -c Release -r linux-x64 -o "$publish_dir"
  "$publish_dir/Reified.$product.AotProbe"
done
