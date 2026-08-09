#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HUGO_BASEURL="${HUGO_BASEURL:-http://localhost:3000/}"
validate_dir="${REIFIED_DOCS_VALIDATE_DIR:-"$root_dir/.fsdocs/validate"}"

"$root_dir/scripts/generate-example-docs.sh" all
bash "$root_dir/scripts/generate-api-docs.sh" all
bash "$root_dir/scripts/populate-hugo-content.sh"

hugo --source "$root_dir/site" --destination "$validate_dir" --baseURL "$HUGO_BASEURL" --cleanDestinationDir

assert_edit_link() {
  local rendered_page="$1"
  local source_path="$2"
  local expected="https://github.com/adz/Reified/edit/main/$source_path"

  if ! grep -Fq "$expected" "$validate_dir/$rendered_page"; then
    echo "Missing expected Edit link in $rendered_page: $expected" >&2
    exit 1
  fi
}

assert_edit_link "getting-started/index.html" "docs/getting-started.md"
assert_edit_link "schema/quickstart/index.html" "docs/schema/quickstart.md"
assert_edit_link "schema/reference/schema/t-schema/index.html" "docs/schema/reference/schema/t-schema.md"

echo "Docs validation build written to $validate_dir"
