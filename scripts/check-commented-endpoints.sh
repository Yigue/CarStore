#!/bin/bash
# CI Hygiene Check Script for Commented-Out Endpoint Files (REQ-FIN-DEAD-001)
#
# This script scans C# endpoint files in src/Web.Api/Endpoints/ and flags files
# where line comments (//) make up 80% or more of the non-empty lines.
#
# CI Wiring instructions:
# Add a step in CI/CD pipeline definition (.github/workflows/ci.yml or Dockerfile/Jenkinsfile):
#   run: chmod +x scripts/check-commented-endpoints.sh && ./scripts/check-commented-endpoints.sh

set -euo pipefail

FAILED=0

# Scan all CS files in Endpoints
while IFS= read -r -d '' file; do
    # Skip if file is empty
    if [ ! -s "$file" ]; then
        continue
    fi

    # Total lines (excluding blank lines)
    total_lines=$(grep -cve '^\s*$' "$file" || true)
    
    if [ "$total_lines" -eq 0 ]; then
        continue
    fi

    # Lines that are line comments (ignoring leading whitespace)
    comment_lines=$(grep -ce '^\s*//' "$file" || true)

    # Calculate percentage using bash arithmetic
    percentage=$(( comment_lines * 100 / total_lines ))

    if [ "$percentage" -ge 80 ]; then
        echo "FAIL: $file has too many line comments ($percentage% of lines are comments). Fully commented-out endpoints are dead code and must be deleted." >&2
        FAILED=1
    fi
done < <(find src/Web.Api/Endpoints -type f -name "*.cs" -print0)

if [ "$FAILED" -eq 1 ]; then
    exit 1
fi

echo "SUCCESS: No commented-out endpoint files found."
exit 0
