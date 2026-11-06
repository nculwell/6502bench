#!/usr/bin/env bash
#
# Builds a distributable copy of SourceGen.
#
# This replaces the old MakeDist WPF utility, which could only run on Windows.  Everything
# it did -- publish the application and copy the data files, licence and notice next to the
# executable -- is expressed here as a dotnet publish plus a couple of copies.
#
# Usage:
#   ./MakeDist/publish.sh [runtime-identifier] [configuration] [--self-contained]
#
# Examples:
#   ./MakeDist/publish.sh                        # linux-x64, Release, framework-dependent
#   ./MakeDist/publish.sh win-x64
#   ./MakeDist/publish.sh linux-arm64 Release --self-contained
#
set -euo pipefail

RID="${1:-linux-x64}"
CONFIG="${2:-Release}"
SELF_CONTAINED="false"
if [[ "${3:-}" == "--self-contained" ]]; then
    SELF_CONTAINED="true"
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/DIST_Release/$RID"

echo "Publishing SourceGen"
echo "  runtime identifier: $RID"
echo "  configuration     : $CONFIG"
echo "  self-contained    : $SELF_CONTAINED"
echo "  output            : $OUT"

dotnet publish "$ROOT/SourceGen.App/SourceGen.App.csproj" \
    --configuration "$CONFIG" \
    --runtime "$RID" \
    --self-contained "$SELF_CONTAINED" \
    --output "$OUT"

# RuntimeData is copied by the project itself (see SourceGen.App.csproj), so the published
# tree is already runnable.  Add the paperwork.
for extra in LICENSE NOTICE.txt; do
    if [[ -f "$ROOT/$extra" ]]; then
        cp "$ROOT/$extra" "$OUT/"
    fi
done

# The help content is served from docs/sgmanual.
if [[ -d "$ROOT/docs/sgmanual" ]]; then
    mkdir -p "$OUT/Help"
    cp "$ROOT"/docs/sgmanual/*.html "$OUT/Help/" 2>/dev/null || true
fi

echo
echo "Done.  Run with: $OUT/SourceGen"
