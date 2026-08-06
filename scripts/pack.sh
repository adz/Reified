#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root_dir"

output_dir="artifacts/package"

mkdir -p "$output_dir"
find "$output_dir" -maxdepth 1 -type f \( -name '*.nupkg' -o -name '*.snupkg' \) -delete

# Every package here ships on the one Reified version from Directory.Build.props; -v overrides it.
VERSION=""
while getopts "v:" opt; do
  case $opt in
    v) VERSION="$OPTARG" ;;
    *) echo "Usage: $0 [-v <reified-version>]"; exit 1 ;;
  esac
done

version_args=()
if [[ -n "$VERSION" ]]; then
  version_args+=("-p:ReifiedVersion=$VERSION")
fi

# The umbrella packs last so a failure in a focused package is reported against that package.
projects=(
  "src/Reified.Data/Reified.Data.fsproj"
  "src/Reified.Result/Reified.Result.fsproj"
  "src/Reified.Constraint/Reified.Constraint.fsproj"
  "src/Reified.Refinements/Reified.Refinements.fsproj"
  "src/Reified.Parse/Reified.Parse.fsproj"
  "src/Reified.Schema/Reified.Schema.fsproj"
  "src/Reified.Schema.Json/Reified.Schema.Json.fsproj"
  "src/Reified.Schema.Http/Reified.Schema.Http.fsproj"
  "src/Reified.Schema.Contracts.Build/Reified.Schema.Contracts.Build.fsproj"
  "src/Reified/Reified.fsproj"
)

echo "Packing projects to $output_dir..."

for project in "${projects[@]}"; do
  echo "--- Packing $(basename "$project") ---"
  dotnet pack "$project" --configuration Release --output "$output_dir" "${version_args[@]}"
done

echo "Done. Packages are in $output_dir"
ls -1 "$output_dir"/*.nupkg
