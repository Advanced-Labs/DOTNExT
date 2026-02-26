#!/usr/bin/env bash
# =============================================================================
# scynapse-rename-encoding-fix.sh
#
# Handles Orleans -> Scynapse renaming in files that are NOT UTF-8 encoded.
# The main scynapse-rename-execute.sh uses sed, which only works on UTF-8/ASCII.
# Files encoded as UTF-16, UTF-16LE, UTF-16BE, or other encodings are silently
# skipped by sed -- this script catches and fixes those.
#
# WHY THIS EXISTS:
#   Visual Studio and some .NET tooling occasionally create files in UTF-16
#   encoding (e.g., GlobalSuppressions.cs, some .resx files, some designer
#   files). The main rename script's sed pass reads these files but fails to
#   match any patterns because the bytes don't match: in UTF-16, "Orleans"
#   is stored as "O\x00r\x00l\x00e\x00a\x00n\x00s\x00" which doesn't match
#   the ASCII/UTF-8 byte sequence "Orleans" that sed looks for.
#
# HOW IT WORKS:
#   1. Scans all text-like files for non-UTF-8 encoding using `file` command
#   2. For each non-UTF-8 file, checks if it contains "orleans" (case-insensitive)
#   3. Converts to UTF-8, applies the same replacement patterns, converts back
#   4. Preserves BOM (Byte Order Mark) if the original had one
#
# WHEN TO RUN:
#   After scynapse-rename-execute.sh (Phase 3). This is a "mop-up" pass.
#   Can also be run standalone for targeted fixes.
#
# Usage:
#   ./scynapse-rename-encoding-fix.sh --dry-run    # Preview (default)
#   ./scynapse-rename-encoding-fix.sh --execute     # Apply changes
#   ./scynapse-rename-encoding-fix.sh --scan-only   # Just list non-UTF-8 files
# =============================================================================

set -euo pipefail

# ─── CONFIGURABLE VARIABLES ──────────────────────────────────────────────────
PROJECT_ROOT="${PROJECT_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)/src/Scynapse}"

# Same replacement mapping as scynapse-rename-execute.sh (order matters!)
REPLACEMENTS=(
    "Microsoft.Orleans|Genesa.Scynapse"
    "microsoft.orleans|genesa.scynapse"
    "MICROSOFT.ORLEANS|GENESA.SCYNAPSE"
    "NewOrleans|Scynapse"
    "NEWORLEANS|SCYNAPSE"
    "neworleans|scynapse"
    "new-orleans|scynapse"
    "new_orleans|scynapse"
    "Orleans|Scynapse"
    "orleans|scynapse"
    "ORLEANS|SCYNAPSE"
)

# Extensions to check (text-like files that might be non-UTF-8)
CHECK_EXTENSIONS=(
    cs csproj fsproj sln slnx md json yaml yml xml props targets
    cmd ps1 sh sql config txt proto resx html css tsx ts fs
    gitignore gitattributes editorconfig designer
)

LOG_FILE="${LOG_FILE:-/tmp/scynapse-encoding-fix-$(date +%Y%m%d-%H%M%S).log}"
# ─── END CONFIGURABLE VARIABLES ─────────────────────────────────────────────

# Parse arguments
MODE="dry-run"
while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)    MODE="dry-run"; shift ;;
        --execute)    MODE="execute"; shift ;;
        --scan-only)  MODE="scan-only"; shift ;;
        --help)
            echo "Usage: $0 [--dry-run|--execute|--scan-only]"
            echo ""
            echo "  --dry-run     Preview which files would be changed (default)"
            echo "  --execute     Apply replacements to non-UTF-8 files"
            echo "  --scan-only   Just list non-UTF-8 files (no replacement check)"
            echo ""
            echo "Environment variables:"
            echo "  PROJECT_ROOT  Path to Scynapse project (default: auto-detect)"
            echo "  LOG_FILE      Path to log file (default: /tmp/scynapse-encoding-fix-*.log)"
            exit 0 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Validate
if [[ ! -d "$PROJECT_ROOT" ]]; then
    echo "ERROR: Project directory not found: $PROJECT_ROOT"
    exit 1
fi

# Check for required tools
for tool in file iconv python3; do
    if command -v "$tool" &>/dev/null; then
        continue
    fi
    # python3 is optional (fallback), iconv is required
    if [[ "$tool" == "iconv" ]]; then
        echo "ERROR: 'iconv' is required but not found. Install it with: apt-get install -y libc-bin"
        exit 1
    fi
done

log() { echo "$1" | tee -a "$LOG_FILE"; }

echo "" > "$LOG_FILE"
log "============================================================"
log "  SCYNAPSE ENCODING FIX"
log "  Project: $PROJECT_ROOT"
log "  Mode:    $MODE"
log "  Date:    $(date '+%Y-%m-%d %H:%M:%S')"
log "============================================================"
log ""

# ─── STEP 1: Find all non-UTF-8 encoded text files ──────────────────────────
log "Scanning for non-UTF-8 encoded files..."
log ""

# Build find expression for extensions
FIND_EXPR=""
for i in "${!CHECK_EXTENSIONS[@]}"; do
    if [[ $i -gt 0 ]]; then
        FIND_EXPR="$FIND_EXPR -o"
    fi
    FIND_EXPR="$FIND_EXPR -name '*.${CHECK_EXTENSIONS[$i]}'"
done

NON_UTF8_FILES=()
NON_UTF8_ENCODINGS=()

while IFS= read -r filepath; do
    [[ -z "$filepath" ]] && continue

    # Use `file` to detect encoding
    encoding_info=$(file --mime-encoding "$filepath" 2>/dev/null || echo "unknown")
    encoding=$(echo "$encoding_info" | sed 's/.*: //')

    # Skip UTF-8 and ASCII (sed handles these fine)
    case "$encoding" in
        utf-8|us-ascii|ascii|unknown) continue ;;
    esac

    NON_UTF8_FILES+=("$filepath")
    NON_UTF8_ENCODINGS+=("$encoding")

done < <(eval "find '$PROJECT_ROOT' -type f \( $FIND_EXPR \)" 2>/dev/null | sort)

log "Found ${#NON_UTF8_FILES[@]} non-UTF-8 files"
log ""

if [[ ${#NON_UTF8_FILES[@]} -eq 0 ]]; then
    log "No non-UTF-8 files found. Nothing to do."
    exit 0
fi

# List all non-UTF-8 files
for i in "${!NON_UTF8_FILES[@]}"; do
    rel_path="${NON_UTF8_FILES[$i]#$PROJECT_ROOT/}"
    log "  [$i] ${NON_UTF8_ENCODINGS[$i]}: $rel_path"
done
log ""

if [[ "$MODE" == "scan-only" ]]; then
    log "Scan complete. Use --dry-run or --execute to process files."
    exit 0
fi

# ─── STEP 2: Check which files contain Orleans references ───────────────────
log "Checking which non-UTF-8 files contain Orleans references..."
log ""

FILES_WITH_ORLEANS=()
FILES_ENCODINGS=()

for i in "${!NON_UTF8_FILES[@]}"; do
    filepath="${NON_UTF8_FILES[$i]}"
    encoding="${NON_UTF8_ENCODINGS[$i]}"

    # Convert to UTF-8 and check for Orleans (case-insensitive)
    # Map common encoding names to iconv-compatible names
    iconv_enc="$encoding"
    case "$encoding" in
        utf-16le|utf-16-le)  iconv_enc="UTF-16LE" ;;
        utf-16be|utf-16-be)  iconv_enc="UTF-16BE" ;;
        utf-16)              iconv_enc="UTF-16" ;;
        iso-8859-1|latin1)   iconv_enc="ISO-8859-1" ;;
        iso-8859-15)         iconv_enc="ISO-8859-15" ;;
        windows-1252|cp1252) iconv_enc="CP1252" ;;
    esac

    # Try to convert and grep
    if iconv -f "$iconv_enc" -t UTF-8 "$filepath" 2>/dev/null | grep -qi "orleans"; then
        FILES_WITH_ORLEANS+=("$filepath")
        FILES_ENCODINGS+=("$iconv_enc")
        rel_path="${filepath#$PROJECT_ROOT/}"
        log "  HIT: $rel_path ($iconv_enc)"
    fi
done

log ""
log "Files with Orleans references: ${#FILES_WITH_ORLEANS[@]}"
log ""

if [[ ${#FILES_WITH_ORLEANS[@]} -eq 0 ]]; then
    log "No non-UTF-8 files contain Orleans references. All clean!"
    exit 0
fi

# ─── STEP 3: Apply replacements ─────────────────────────────────────────────
log "Processing files..."
log ""

changed_count=0

for i in "${!FILES_WITH_ORLEANS[@]}"; do
    filepath="${FILES_WITH_ORLEANS[$i]}"
    encoding="${FILES_ENCODINGS[$i]}"
    rel_path="${filepath#$PROJECT_ROOT/}"

    # Convert to UTF-8
    tmp_utf8=$(mktemp)
    if ! iconv -f "$encoding" -t UTF-8 "$filepath" > "$tmp_utf8" 2>/dev/null; then
        log "  SKIP (iconv failed): $rel_path"
        rm -f "$tmp_utf8"
        continue
    fi

    # Check if file starts with UTF-8 BOM (from UTF-16 BOM conversion)
    has_bom=false
    if head -c 3 "$tmp_utf8" | od -An -tx1 | head -1 | grep -q "ef bb bf"; then
        has_bom=true
    fi

    # Apply all replacements via sed (same order as main script)
    tmp_replaced=$(mktemp)
    SED_ARGS=""
    for pair in "${REPLACEMENTS[@]}"; do
        old="${pair%%|*}"
        new="${pair##*|}"
        # Escape dots for sed regex
        old_escaped="${old//./\\.}"
        SED_ARGS="$SED_ARGS -e s|${old_escaped}|${new}|g"
    done

    eval "sed $SED_ARGS" < "$tmp_utf8" > "$tmp_replaced"

    # Check if anything changed
    if cmp -s "$tmp_utf8" "$tmp_replaced"; then
        log "  UNCHANGED: $rel_path (no Orleans references matched after conversion)"
        rm -f "$tmp_utf8" "$tmp_replaced"
        continue
    fi

    ((changed_count++)) || true

    if [[ "$MODE" == "dry-run" ]]; then
        # Show what would change
        diff_lines=$(diff "$tmp_utf8" "$tmp_replaced" | grep "^[<>]" | wc -l || true)
        log "  [DRY] Would change $diff_lines lines in: $rel_path ($encoding)"
    else
        # Convert back to original encoding and write
        tmp_final=$(mktemp)
        if iconv -f UTF-8 -t "$encoding" "$tmp_replaced" > "$tmp_final" 2>/dev/null; then
            cp "$tmp_final" "$filepath"
            log "  FIXED: $rel_path ($encoding, converted round-trip)"
        else
            # Fallback: try writing as UTF-8 (may change file encoding)
            cp "$tmp_replaced" "$filepath"
            log "  FIXED: $rel_path (WARNING: could not convert back to $encoding, saved as UTF-8)"
        fi
        rm -f "$tmp_final"
    fi

    rm -f "$tmp_utf8" "$tmp_replaced"
done

log ""
log "============================================================"
log "  ENCODING FIX COMPLETE"
log "============================================================"
log ""
log "  Non-UTF-8 files found:    ${#NON_UTF8_FILES[@]}"
log "  Files with Orleans refs:  ${#FILES_WITH_ORLEANS[@]}"
log "  Files changed/to change:  $changed_count"
log ""

if [[ "$MODE" == "dry-run" ]]; then
    log "  This was a DRY RUN. No changes were made."
    log "  To execute: $0 --execute"
elif [[ $changed_count -gt 0 ]]; then
    log "  Changes applied! Review with: git diff"
fi

log ""
log "  Log: $LOG_FILE"
log "============================================================"
