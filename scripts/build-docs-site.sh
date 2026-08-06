#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HUGO_BASEURL="${HUGO_BASEURL:-"/"}"

for project in \
  "src/Reified.Result/Reified.Result.fsproj" \
  "src/Reified.Constraint/Reified.Constraint.fsproj" \
  "src/Reified.Refinements/Reified.Refinements.fsproj" \
  "src/Reified.Parse/Reified.Parse.fsproj" \
  "src/Reified.Data/Reified.Data.fsproj" \
  "src/Reified.Schema/Reified.Schema.fsproj" \
  "src/Reified.Schema.Json/Reified.Schema.Json.fsproj" \
  "src/Reified.Schema.Http/Reified.Schema.Http.fsproj"
do
  dotnet build "$root_dir/$project" --nologo -v minimal
done

"$root_dir/scripts/generate-example-docs.sh"
bash "$root_dir/scripts/generate-api-docs.sh"
bash "$root_dir/scripts/populate-hugo-content.sh"

# Hugo build
hugo --source "$root_dir/site" --destination "$root_dir/output" --baseURL "$HUGO_BASEURL" --cleanDestinationDir
