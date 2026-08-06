#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
product="all"
skip_build=false

for arg in "$@"; do
  case "$arg" in
    schema|all) product="$arg" ;;
    --no-build) skip_build=true ;;
    *) echo "Usage: $0 [schema|all] [--no-build]" >&2; exit 2 ;;
  esac
done

schema_output="${DOCS_SCHEMA_EXAMPLES_OUTPUT:-$root_dir/docs/schema/examples.md}"

case "$product" in
  schema|all) emit_schema=true ;;
  *) echo "Usage: $0 [schema|all] [--no-build]" >&2; exit 2 ;;
esac

if ! $skip_build; then
  dotnet msbuild "$root_dir/scripts/docs-build.proj" \
    -t:Build -m -nologo -verbosity:minimal -p:DocsBuildScope=Examples
fi

mkdir -p "$(dirname "$schema_output")"

# Build the pages in temp files and move them into place only after every section succeeded,
# so a mid-run failure (or a killed run) can never leave truncated docs behind.
schema_staging="$(mktemp "${TMPDIR:-/tmp}/reified-schema-examples.XXXXXX")"
trap 'rm -f "$schema_staging"' EXIT

render_code_block() {
  local language="$1"
  local file_path="$2"

  printf '```%s\n' "$language"
  cat "$file_path"
  printf '\n```\n'
}

run_example() {
  local project_path="$1"
  local example_filter="${2:-}"

  if [[ -n "$example_filter" ]]; then
    REIFIED_EXAMPLE="$example_filter" dotnet run --project "$project_path" --no-build --no-restore --nologo 2>&1
  else
    dotnet run --project "$project_path" --no-build --no-restore --nologo 2>&1
  fi
}

render_example_section() {
  local title="$1"
  local description="$2"
  local project_path="$3"
  local source_file="$4"
  local source_link="$5"
  local run_command="$6"
  local example_filter="${7:-}"

  local example_output
  printf 'Building docs example: %s\n' "$title"

  if ! example_output="$(run_example "$project_path" "$example_filter")"; then
    printf 'Docs example failed: %s\n' "$title" >&2
    printf '%s\n' "$example_output" >&2
    return 1
  fi

  {
    printf '## %s\n\n' "$title"
    printf '%s\n\n' "$description"
    printf 'Run it:\n\n'
    printf '```bash\n%s\n```\n\n' "$run_command"
    printf 'Source:\n\n'
    printf -- '- [%s](%s)\n\n' "$(basename "$source_file")" "$source_link"
    printf 'Source code:\n\n'
    render_code_block fsharp "$source_file"
    printf '\n'
  } >> "$output_file"
}

write_page_header() {
  local file="$1"
  local description="$2"

  {
    printf -- '---\n'
    printf 'weight: 85\n'
    printf 'title: Runnable Examples\n'
    printf 'description: %s\n' "$description"
    printf -- '---\n\n'
    printf '# Runnable Examples\n\n'
    printf 'This page shows the examples that are executed during the docs build, so the public docs stay tied to real code and observed output.\n\n'
    printf 'The examples below are built from the repository projects, run with the current source, and then written back into this page.\n\n'
    printf 'The code blocks keep the important API calls on the same lines as the values they bind, with trailing comments where that makes the signature easier to read.\n'
    printf 'The examples prefer the normal direct-bind style inside computation expressions, so the docs reflect the recommended day-to-day usage.\n\n'
  } > "$file"
}

if $emit_schema; then
  write_page_header "$schema_staging" "Executable schema, refined, diagnostics, and policy examples mirrored back into the docs."
fi


if $emit_schema; then
output_file="$schema_staging"
render_example_section \
  "Refined Catalog Example" \
  "This example shows a request boundary that parses strings, builds refined numeric/text/collection values, chooses a domain union case, and rejects invalid input before the domain record is created." \
  "$root_dir/examples/Reified.Schema.Examples/Reified.Schema.Examples.fsproj" \
  "$root_dir/examples/Reified.Schema.Examples/RefinedCatalogExample.fs" \
  "https://github.com/adz/Reified/blob/main/examples/Reified.Schema.Examples/RefinedCatalogExample.fs" \
  "REIFIED_EXAMPLE=refined-catalog dotnet run --project examples/Reified.Schema.Examples/Reified.Schema.Examples.fsproj --nologo" \
  "refined-catalog"

render_example_section \
  "Refined Value Schema Example" \
  "This example shows total domain conversions built with Schema.convert, composed into a record schema, and lowered to executable checks." \
  "$root_dir/examples/Reified.Schema.Examples/Reified.Schema.Examples.fsproj" \
  "$root_dir/examples/Reified.Schema.Examples/RefinedValueSchemaExample.fs" \
  "https://github.com/adz/Reified/blob/main/examples/Reified.Schema.Examples/RefinedValueSchemaExample.fs" \
  "REIFIED_EXAMPLE=refined-value-schema dotnet run --project examples/Reified.Schema.Examples/Reified.Schema.Examples.fsproj --nologo" \
  "refined-value-schema"

fi


# mktemp creates the staging files with mode 600; the docs should stay world-readable.
if $emit_schema; then
  chmod 644 "$schema_staging"
  mv "$schema_staging" "$schema_output"
fi
