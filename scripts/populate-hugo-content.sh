#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

upsert_frontmatter() {
  local file="$1"
  local key="$2"
  local value="$3"
  local tmp

  if [ ! -f "$file" ]; then
    return 0
  fi

  tmp="$(mktemp)"
  awk -v key="$key" -v value="$value" '
    NR == 1 && $0 == "---" {
      in_frontmatter = 1
      print
      next
    }

    in_frontmatter && $0 == "---" {
      if (!seen) {
        print key ": " value
      }
      in_frontmatter = 0
      print
      next
    }

    in_frontmatter && $0 ~ "^" key ":" {
      print key ": " value
      seen = 1
      next
    }

    { print }
  ' "$file" > "$tmp"
  mv "$tmp" "$file"
}

# Reified has four product documentation areas: /result/, /values/, /data/, and
# /schema/. Generated API reference is distributed under the product that owns
# each package. Values is a navigation grouping over Constraint, Refinements, and
# Parse — there is no Reified.Values package behind it.
products=(result values data schema)

result_dir="$root_dir/site/content/result"
values_dir="$root_dir/site/content/values"
data_dir="$root_dir/site/content/data"
schema_dir="$root_dir/site/content/schema"

# Notes is not a product area: it has no package, no llms.txt, and no generated
# reference. It holds the meta pages — inventory, platforms, benchmarks, and the
# compiler-directed design — that would otherwise sit inside a learning path.
notes_dir="$root_dir/site/content/notes"

# error-handling, validation, and flow are retired area names; remove any leftovers
# so a stale tree cannot keep serving pages after the split.
rm -rf "$root_dir/site/content/error-handling" "$root_dir/site/content/validation" \
  "$root_dir/site/content/flow" "$result_dir" "$values_dir" "$data_dir" "$schema_dir" \
  "$root_dir/site/content/docs" "$root_dir/site/content/reference" "$root_dir/site/content/parse" \
  "$notes_dir" "$root_dir/site/content/getting-started.md"

for product in "${products[@]}"; do
  mkdir -p "$root_dir/site/content/$product"
  cp -r "$root_dir/docs/$product/." "$root_dir/site/content/$product/"
  rm -f "$root_dir/site/content/$product/llms.txt"
done

mkdir -p "$notes_dir"
cp -r "$root_dir/docs/notes/." "$notes_dir/"

# Product-local generated API reference is copied with the guides. Apply the
# navigation weights needed by the rendered site.
result_ref="$result_dir/reference"
values_ref="$values_dir/reference"
data_ref="$data_dir/reference"
schema_ref="$schema_dir/reference"
upsert_frontmatter "$result_ref/result/_index.md" "weight" "10"
upsert_frontmatter "$values_ref/constraint/_index.md" "weight" "10"
upsert_frontmatter "$values_ref/refined/_index.md" "weight" "20"
upsert_frontmatter "$values_ref/parse/_index.md" "weight" "30"
upsert_frontmatter "$schema_ref/schema/_index.md" "weight" "10"
upsert_frontmatter "$schema_ref/codec/_index.md" "weight" "20"

# Hugo's docs layout supplies the page title. Keep generated content uniform
# with pages whose source already omits a body-level H1.
find "$result_dir" "$values_dir" "$data_dir" "$schema_dir" "$notes_dir" -type f -name "*.md" -print0 |
  node -e '
    const fs = require("node:fs");
    for (const path of fs.readFileSync(0, "utf8").split("\0")) {
      if (!path) continue;
      const content = fs.readFileSync(path, "utf8")
        .split(/(?<=\n)/)
        .filter(line => !line.startsWith("# "))
        .join("");
      const frontmatterEnd = content.indexOf("\n---", 4);
      if (frontmatterEnd < 0) throw new Error(`missing frontmatter: ${path}`);
      let frontmatter = content.slice(0, frontmatterEnd);
      if (/^type:/m.test(frontmatter)) {
        frontmatter = frontmatter.replace(/^type:.*$/m, "type: docs");
      } else {
        frontmatter += "\ntype: docs";
      }
      fs.writeFileSync(path, frontmatter + content.slice(frontmatterEnd));
    }
  '

# Copy root assets
cp "$root_dir/llms.txt" "$root_dir/site/static/" 2>/dev/null || true
rm -rf "$root_dir/site/static/error-handling" "$root_dir/site/static/flow"
for product in "${products[@]}"; do
  mkdir -p "$root_dir/site/static/$product"
  cp "$root_dir/docs/$product/llms.txt" "$root_dir/site/static/$product/llms.txt"
done
mkdir -p "$root_dir/site/static/content"
cp -r "$root_dir/docs/content/"* "$root_dir/site/static/content/" 2>/dev/null || true

# Copy root homepage
cp "$root_dir/docs/index.md" "$root_dir/site/content/_index.md"

# The repository-wide getting started sits above the product areas: it is the single
# primary route off the landing page, so it is a top-level page rather than a product one.
cp "$root_dir/docs/getting-started.md" "$root_dir/site/content/getting-started.md"
