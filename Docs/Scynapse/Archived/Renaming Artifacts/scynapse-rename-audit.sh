#!/usr/bin/env bash
# =============================================================================
# scynapse-rename-audit.sh
#
# Exhaustively searches for ALL remaining Orleans/NewOrleans/Microsoft.Orleans
# references in the Scynapse project directory. Run BEFORE and AFTER the
# rename script to verify completeness.
#
# Usage:
#   ./scynapse-rename-audit.sh                  # Full audit
#   ./scynapse-rename-audit.sh --summary        # Counts only
#   ./scynapse-rename-audit.sh --output report  # Save to report files
# =============================================================================

set -euo pipefail

# ─── CONFIGURABLE VARIABLES ──────────────────────────────────────────────────
# Root of the Scynapse project directory
PROJECT_ROOT="${PROJECT_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)/src/Scynapse}"

# Patterns to search for (case-sensitive variants)
PATTERNS=(
    "Microsoft\.Orleans"    # NuGet package IDs (must check before bare Orleans)
    "NewOrleans"            # Previous project name remnants
    "NEWORLEANS"            # Previous project name (uppercase)
    "OrleansCodeGen"        # Generated code namespace prefix
    "OrleansAWSUtils"       # AWS utility namespace
    "Orleans"               # PascalCase (main catch-all)
    "orleans"               # lowercase (URLs, paths, variables)
    "ORLEANS"               # UPPERCASE (diagnostic IDs, env vars)
)

# File extensions to search in content (empty = all files)
# Add more if needed
CONTENT_EXTENSIONS="cs,csproj,fsproj,sln,slnx,md,json,yaml,yml,xml,props,targets,cmd,ps1,sh,sql,config,txt,proto,resx,html,css,tsx,ts,fs,gitignore,gitattributes,editorconfig"

# ─── END CONFIGURABLE VARIABLES ─────────────────────────────────────────────

# Parse arguments
SUMMARY_ONLY=false
OUTPUT_PREFIX=""
while [[ $# -gt 0 ]]; do
    case $1 in
        --summary) SUMMARY_ONLY=true; shift ;;
        --output)  OUTPUT_PREFIX="$2"; shift 2 ;;
        --help)
            echo "Usage: $0 [--summary] [--output PREFIX] [--help]"
            echo "  --summary   Show counts only (no file listings)"
            echo "  --output X  Save detailed results to X-dirs.txt, X-files.txt, X-content.txt"
            exit 0 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Validate project root
if [[ ! -d "$PROJECT_ROOT" ]]; then
    echo "ERROR: Project directory not found: $PROJECT_ROOT"
    echo "Set PROJECT_ROOT environment variable or run from repo root."
    exit 1
fi

echo "============================================================"
echo "  SCYNAPSE RENAME AUDIT"
echo "  Project: $PROJECT_ROOT"
echo "  Date:    $(date '+%Y-%m-%d %H:%M:%S')"
echo "============================================================"
echo ""

# ─── SECTION 1: DIRECTORY NAMES ─────────────────────────────────────────────
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  SECTION 1: DIRECTORIES WITH ORLEANS IN NAME"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

DIR_RESULTS=$(find "$PROJECT_ROOT" -type d -iname "*orleans*" 2>/dev/null | sort)
DIR_COUNT=$(echo "$DIR_RESULTS" | grep -c . || true)

echo "  Found: $DIR_COUNT directories"
echo ""

if [[ "$SUMMARY_ONLY" == false && -n "$DIR_RESULTS" ]]; then
    echo "$DIR_RESULTS" | sed "s|$PROJECT_ROOT/||"
    echo ""
fi

if [[ -n "$OUTPUT_PREFIX" ]]; then
    echo "$DIR_RESULTS" | sed "s|$PROJECT_ROOT/||" > "${OUTPUT_PREFIX}-dirs.txt"
    echo "  Saved to: ${OUTPUT_PREFIX}-dirs.txt"
fi

# Also check for NewOrleans and Microsoft in dir names
NEWORLEANS_DIRS=$(find "$PROJECT_ROOT" -type d -iname "*neworleans*" 2>/dev/null | sort)
NEWORLEANS_DIR_COUNT=$(echo "$NEWORLEANS_DIRS" | grep -c . || true)
if [[ $NEWORLEANS_DIR_COUNT -gt 0 ]]; then
    echo "  ⚠ NewOrleans in directory names: $NEWORLEANS_DIR_COUNT"
    if [[ "$SUMMARY_ONLY" == false ]]; then
        echo "$NEWORLEANS_DIRS" | sed "s|$PROJECT_ROOT/||"
    fi
fi
echo ""

# ─── SECTION 2: FILE NAMES ──────────────────────────────────────────────────
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  SECTION 2: FILES WITH ORLEANS IN NAME"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

FILE_RESULTS=$(find "$PROJECT_ROOT" -type f -iname "*orleans*" 2>/dev/null | sort)
FILE_COUNT=$(echo "$FILE_RESULTS" | grep -c . || true)

echo "  Found: $FILE_COUNT files"
echo ""

if [[ "$SUMMARY_ONLY" == false && -n "$FILE_RESULTS" ]]; then
    echo "  By extension:"
    echo "$FILE_RESULTS" | sed 's/.*\.//' | sort | uniq -c | sort -rn | sed 's/^/    /'
    echo ""
    echo "  Full list:"
    echo "$FILE_RESULTS" | sed "s|$PROJECT_ROOT/||"
    echo ""
fi

if [[ -n "$OUTPUT_PREFIX" ]]; then
    echo "$FILE_RESULTS" | sed "s|$PROJECT_ROOT/||" > "${OUTPUT_PREFIX}-files.txt"
    echo "  Saved to: ${OUTPUT_PREFIX}-files.txt"
fi

# Also check for NewOrleans in filenames
NEWORLEANS_FILES=$(find "$PROJECT_ROOT" -type f -iname "*neworleans*" 2>/dev/null | sort)
NEWORLEANS_FILE_COUNT=$(echo "$NEWORLEANS_FILES" | grep -c . || true)
if [[ $NEWORLEANS_FILE_COUNT -gt 0 ]]; then
    echo "  ⚠ NewOrleans in filenames: $NEWORLEANS_FILE_COUNT"
    if [[ "$SUMMARY_ONLY" == false ]]; then
        echo "$NEWORLEANS_FILES" | sed "s|$PROJECT_ROOT/||"
    fi
fi
echo ""

# ─── SECTION 3: FILE CONTENTS ───────────────────────────────────────────────
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  SECTION 3: FILES CONTAINING ORLEANS IN CONTENT"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Build include pattern for grep
INCLUDE_ARGS=""
IFS=',' read -ra EXTS <<< "$CONTENT_EXTENSIONS"
for ext in "${EXTS[@]}"; do
    INCLUDE_ARGS="$INCLUDE_ARGS --include=*.$ext"
done

CONTENT_RESULTS=$(grep -ril "orleans" "$PROJECT_ROOT" $INCLUDE_ARGS 2>/dev/null | sort || true)
CONTENT_COUNT=$(echo "$CONTENT_RESULTS" | grep -c . || true)

echo "  Found: $CONTENT_COUNT files with 'orleans' (case-insensitive) in content"
echo ""

if [[ "$SUMMARY_ONLY" == false && -n "$CONTENT_RESULTS" ]]; then
    echo "  By extension:"
    echo "$CONTENT_RESULTS" | sed 's/.*\.//' | sort | uniq -c | sort -rn | sed 's/^/    /'
    echo ""
    echo "  By top-level subdirectory:"
    echo "$CONTENT_RESULTS" | sed "s|$PROJECT_ROOT/||" | cut -d/ -f1 | sort | uniq -c | sort -rn | sed 's/^/    /'
    echo ""
fi

if [[ -n "$OUTPUT_PREFIX" ]]; then
    echo "$CONTENT_RESULTS" | sed "s|$PROJECT_ROOT/||" > "${OUTPUT_PREFIX}-content.txt"
    echo "  Saved to: ${OUTPUT_PREFIX}-content.txt"
fi

# ─── SECTION 4: PATTERN-SPECIFIC COUNTS ─────────────────────────────────────
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  SECTION 4: OCCURRENCE COUNTS BY PATTERN"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
printf "  %-30s %8s %8s\n" "PATTERN" "LINES" "FILES"
printf "  %-30s %8s %8s\n" "──────────────────────────────" "────────" "────────"

for pattern in "${PATTERNS[@]}"; do
    line_count=$(grep -r "$pattern" "$PROJECT_ROOT" $INCLUDE_ARGS 2>/dev/null | wc -l || true)
    file_count=$(grep -rl "$pattern" "$PROJECT_ROOT" $INCLUDE_ARGS 2>/dev/null | wc -l || true)
    printf "  %-30s %8d %8d\n" "$pattern" "$line_count" "$file_count"
done

echo ""

# ─── SECTION 5: BINARY FILES ────────────────────────────────────────────────
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  SECTION 5: BINARY FILES WITH ORLEANS"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

BINARY_RESULTS=$(grep -ril "orleans" "$PROJECT_ROOT" --include="*.png" --include="*.jpg" --include="*.gif" --include="*.ico" --include="*.svg" --include="*.dll" --include="*.exe" --include="*.woff" --include="*.woff2" 2>/dev/null | sort || true)
BINARY_COUNT=$(echo "$BINARY_RESULTS" | grep -c . || true)

echo "  Found: $BINARY_COUNT binary files (require manual handling)"
if [[ -n "$BINARY_RESULTS" ]]; then
    echo "$BINARY_RESULTS" | sed "s|$PROJECT_ROOT/||" | sed 's/^/    /'
fi
echo ""

# ─── SECTION 6: SPECIAL PATTERNS ────────────────────────────────────────────
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  SECTION 6: NOTABLE SPECIAL PATTERNS"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

echo ""
echo "  GitHub URLs (github.com/dotnet/orleans):"
gh_url_count=$(grep -r "github.com/dotnet/orleans" "$PROJECT_ROOT" $INCLUDE_ARGS 2>/dev/null | wc -l || true)
echo "    $gh_url_count occurrences"

echo ""
echo "  NuGet package IDs (Microsoft.Orleans.*):"
nuget_count=$(grep -r "Microsoft\.Orleans" "$PROJECT_ROOT" $INCLUDE_ARGS 2>/dev/null | wc -l || true)
echo "    $nuget_count occurrences"

echo ""
echo "  Diagnostic IDs (ORLEANS0xxx):"
diag_count=$(grep -r "ORLEANS0[0-9]" "$PROJECT_ROOT" $INCLUDE_ARGS 2>/dev/null | wc -l || true)
echo "    $diag_count occurrences"

echo ""
echo "  Experimental flags (ORLEANSEXPxxx):"
exp_count=$(grep -r "ORLEANSEXP[0-9]" "$PROJECT_ROOT" $INCLUDE_ARGS 2>/dev/null | wc -l || true)
echo "    $exp_count occurrences"

echo ""
echo "  Environment variables (ORLEANS_*):"
env_count=$(grep -r "ORLEANS_" "$PROJECT_ROOT" $INCLUDE_ARGS 2>/dev/null | wc -l || true)
echo "    $env_count occurrences"

echo ""
echo "  SQL schema references:"
sql_count=$(grep -ri "orleans" "$PROJECT_ROOT" --include="*.sql" 2>/dev/null | wc -l || true)
echo "    $sql_count occurrences across SQL files"

echo ""

# ─── SUMMARY ─────────────────────────────────────────────────────────────────
echo "============================================================"
echo "  AUDIT SUMMARY"
echo "============================================================"
echo ""
TOTAL_FILES_IN_PROJECT=$(find "$PROJECT_ROOT" -type f 2>/dev/null | wc -l)
TOTAL_DIRS_IN_PROJECT=$(find "$PROJECT_ROOT" -type d 2>/dev/null | wc -l)
echo "  Project scope:       $TOTAL_FILES_IN_PROJECT files / $TOTAL_DIRS_IN_PROJECT directories"
echo "  Dirs to rename:      $DIR_COUNT directories"
echo "  Files to rename:     $FILE_COUNT files"
echo "  Content to update:   $CONTENT_COUNT files"
echo "  Binary (manual):     $BINARY_COUNT files"
CONTENT_PCT=$((CONTENT_COUNT * 100 / TOTAL_FILES_IN_PROJECT))
echo "  Content coverage:    ${CONTENT_PCT}% of all files contain 'orleans'"
echo ""

if [[ $DIR_COUNT -eq 0 && $FILE_COUNT -eq 0 && $CONTENT_COUNT -eq 0 ]]; then
    echo "  ✅ NO ORLEANS REFERENCES FOUND - RENAME IS COMPLETE!"
else
    echo "  ⚠ Orleans references still present - rename needed"
fi
echo ""
echo "============================================================"
