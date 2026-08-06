#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root_dir/benchmarks/Reified.Benchmarks.Fable/Reified.Benchmarks.Fable.fsproj"
out_dir="$root_dir/artifacts/fable-js-surface"

rm -rf "$out_dir"
mkdir -p "$out_dir"
printf '%s\n' '{ "type": "module" }' > "$out_dir/package.json"

dotnet fable "$project" --lang javascript --define BENCHMARK_NODE --outDir "$out_dir"

if [ ! -f "$out_dir/src/Reified.Schema.Json/Json.js" ]; then
  echo "Reified.Schema.Json's Json.fs did not compile into the Fable JavaScript output." >&2
  exit 1
fi

program_output="$(node "$out_dir/Program.js")"

if ! grep -q "Codec round-trip: ok" <<<"$program_output"; then
  echo "Reified.Schema.Json encode/decode round-trip did not run in the Fable JavaScript output." >&2
  echo "$program_output" >&2
  exit 1
fi

if ! grep -q "Data JSON boundaries: ok" <<<"$program_output"; then
  echo "Portable Data JSON parsing and native JavaScript JSON conversion did not run." >&2
  echo "$program_output" >&2
  exit 1
fi

if ! grep -q "Constraints: ok" <<<"$program_output"; then
  echo "The type-directed constraint catalogue did not run correctly in the Fable JavaScript output." >&2
  echo "$program_output" >&2
  exit 1
fi

if ! grep -q "Localization: ok" <<<"$program_output"; then
  echo "Localized constraint rendering did not run correctly in the Fable JavaScript output." >&2
  echo "$program_output" >&2
  exit 1
fi

if ! grep -q "Operand agreement: ok" <<<"$program_output"; then
  echo "Constraint operands were described differently under Fable than under .NET." >&2
  echo "$program_output" >&2
  exit 1
fi

if ! grep -q "Otel spans: ok" <<<"$program_output"; then
  echo "Axial.Flow.Telemetry.JavaScript spans did not record correctly in the Fable JavaScript output." >&2
  echo "$program_output" >&2
  exit 1
fi

if ! grep -q "App hosting: ok" <<<"$program_output"; then
  echo "App plus Node/browser hosting did not run correctly in the Fable JavaScript output." >&2
  echo "$program_output" >&2
  exit 1
fi

if grep -R "ColdTask" "$out_dir" >/dev/null; then
  echo "ColdTask leaked into the Fable JavaScript output." >&2
  exit 1
fi

echo "Fable JavaScript surface compiles, includes Reified.Schema.Json, and excludes .NET-only ColdTask."
