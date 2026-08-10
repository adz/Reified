#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
for product in Result Constraint Refinements Schema; do
  project="$ROOT_DIR/examples/Reified.$product.AotProbe/Reified.$product.AotProbe.fsproj"
  publish_dir="$ROOT_DIR/artifacts/publish/Reified.$product.AotProbe/linux-x64"

  # -m:1 is deliberate. Every probe pins its reference to the library's net8.0 build, and the
  # artifacts layout gives that build one output directory regardless of which probe reached it.
  # Built in parallel, two MSBuild nodes write the same Reified.<Pkg>.deps.json and one loses:
  #   error MSB4018: ... cannot access the file '.../Reified.Data.deps.json'
  # A single node serialises those writes. The probes are already sequential, so this costs little.
  dotnet publish "$project" -c Release -r linux-x64 -o "$publish_dir" -m:1
  "$publish_dir/Reified.$product.AotProbe"
done
