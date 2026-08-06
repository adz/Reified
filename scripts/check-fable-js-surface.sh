#!/usr/bin/env bash

# Compiles the Fable probe to JavaScript and runs it on Node. The probe asserts the same things it asserts on
# .NET (see examples/Reified.FableProbe/Checks.fs), so a divergence between the two runtimes fails here rather
# than in a browser months later. The differences that make this worth running: Fable erases Guid to a string
# and TimeSpan to a number, JavaScript and .NET disagree about which characters are whitespace, and text sizes
# are counted in code points over UTF-16 strings.

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root_dir/examples/Reified.FableProbe/Reified.FableProbe.fsproj"
out_dir="$root_dir/artifacts/fable-js-surface"

rm -rf "$out_dir"
mkdir -p "$out_dir"
printf '%s\n' '{ "type": "module" }' > "$out_dir/package.json"

dotnet fable "$project" --lang javascript --outDir "$out_dir"

if [ ! -f "$out_dir/src/Reified.Schema.Json/Json.js" ]; then
  echo "Reified.Schema.Json's Json.fs did not compile into the Fable JavaScript output." >&2
  exit 1
fi

program_output="$(node "$out_dir/Program.js")"

expect() {
  if ! grep -q "$1" <<<"$program_output"; then
    echo "$2" >&2
    echo "$program_output" >&2
    exit 1
  fi
}

expect "Schema record plan: ok" "The compiled record plan did not build in the Fable JavaScript output."
expect "Codec round-trip: ok" "Reified.Schema.Json encode/decode round-trip did not run in the Fable JavaScript output."
expect "Constraints: ok" "The type-directed constraint catalogue did not run correctly in the Fable JavaScript output."
expect "Operand agreement: ok" "Constraint operands were described differently under Fable than under .NET."
expect "Localization: ok" "Localized constraint rendering did not run correctly in the Fable JavaScript output."
expect "Data JSON boundaries: ok" "Portable Data JSON parsing and native JavaScript JSON conversion did not run."
expect "Reified Fable probe: ok" "The Fable probe did not reach the end of its checks."

# The .NET-only resource-manager constructors must be absent under Fable rather than compiling to a silent
# no-op that reports every message in the invariant culture.
if grep -Rq "ResourceManager" "$out_dir"; then
  echo "A .NET-only ResourceManager constructor leaked into the Fable JavaScript output." >&2
  exit 1
fi

echo "Fable JavaScript surface compiles, includes Reified.Schema.Json, and agrees with .NET."
