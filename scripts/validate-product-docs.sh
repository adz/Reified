#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
product="${1:-}"

case "$product" in
  data|result|values|schema) ;;
  *) echo "Usage: $0 <data|result|values|schema>" >&2; exit 2 ;;
esac

HUGO_BASEURL="${HUGO_BASEURL:-http://localhost:3000/}"
validate_dir="${REIFIED_DOCS_VALIDATE_DIR:-$root_dir/.fsdocs/validate-$product}"

if [[ "$product" == "schema" ]]; then
  "$root_dir/scripts/generate-example-docs.sh" "$product"
fi
bash "$root_dir/scripts/generate-api-docs.sh" "$product"
bash "$root_dir/scripts/populate-hugo-content.sh"

hugo --source "$root_dir/site" --destination "$validate_dir" --baseURL "$HUGO_BASEURL" --cleanDestinationDir

case "$product" in
  data)
    test -f "$validate_dir/data/tutorial/index.html"
    test -f "$validate_dir/data/reference/data/t-data/index.html"
    ;;
  result)
    test -f "$validate_dir/result/quickstart/index.html"
    test -f "$validate_dir/result/collecting-errors/index.html"
    test -f "$validate_dir/result/fstoolkit-comparison/index.html"
    test -f "$validate_dir/result/reference/result/result/m-result-result-traverse/index.html"
    duplicate_sidebar_ids="$(grep -o 'id="[^"]*"' "$validate_dir/result/reference/result/index.html" | sort | uniq -d)"
    test -z "$duplicate_sidebar_ids"
    ;;
  values)
    test -f "$validate_dir/values/quickstart/index.html"
    test -f "$validate_dir/values/constraint/localization/index.html"
    test -f "$validate_dir/values/constraint/adding-a-language/index.html"
    test -f "$validate_dir/values/constraint/fable/index.html"
    test -f "$validate_dir/values/refined/domain-values/index.html"
    test -f "$validate_dir/values/parse/index.html"
    test -f "$validate_dir/values/reference/constraint/t-renderer/index.html"
    # Values is navigation only: no page may advertise a Reified.Values package.
    ! grep -rqF 'dotnet add package Reified.Values' "$validate_dir/values"
    ;;
  schema)
    test -f "$validate_dir/schema/quickstart/index.html"
    test -f "$validate_dir/schema/reference/schema/t-schema/index.html"
    grep -q 'id="package-schemaoverview-reference-check" checked' "$validate_dir/schema/reference/schema/index.html"
    grep -q 'id="package-schemajson-codec-reference-check" checked' "$validate_dir/schema/reference/codec/index.html"
    grep -q 'id="package-schemahttp-servers-reference-check" checked' "$validate_dir/schema/reference/schema/http/index.html"
    ;;
esac

echo "$product docs validation build written to $validate_dir"
