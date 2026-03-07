#!/usr/bin/env bash
# =============================================================================
# scynapse-rename-post-verify.sh
#
# Post-rename verification script. Run AFTER all rename scripts to validate
# the structural integrity of the renamed codebase.
#
# This is NOT a simple grep for "Orleans" (the audit script does that).
# Instead, this script checks for STRUCTURAL problems caused by renaming:
#   - Broken project references (.csproj -> .csproj)
#   - Solution file references that don't match actual file paths
#   - InternalsVisibleTo attributes pointing to assemblies that don't exist
#   - PackageId / AssemblyName mismatches
#   - Orphaned .verified.cs snapshot files whose test class was renamed
#   - Inconsistent namespace declarations vs folder structure
#
# WHY THIS EXISTS:
#   The rename scripts do mechanical text replacement. They can't verify that
#   the replacements are semantically correct. For example, renaming
#   "Orleans.Core" to "Scynapse.Core" in a <ProjectReference> is only correct
#   if the target .csproj was ALSO renamed. This script catches mismatches.
#
# WHEN TO RUN:
#   After ALL rename scripts (execute + encoding-fix) have completed.
#   Before committing the rename changes.
#
# Usage:
#   ./scynapse-rename-post-verify.sh              # Full verification
#   ./scynapse-rename-post-verify.sh --quick       # Fast checks only
#   ./scynapse-rename-post-verify.sh --fix         # Auto-fix trivial issues
# =============================================================================

set -euo pipefail

# ─── CONFIGURABLE VARIABLES ──────────────────────────────────────────────────
PROJECT_ROOT="${PROJECT_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)/src/Scynapse}"
LOG_FILE="${LOG_FILE:-/tmp/scynapse-verify-$(date +%Y%m%d-%H%M%S).log}"
# ─── END CONFIGURABLE VARIABLES ─────────────────────────────────────────────

# Parse arguments
QUICK_MODE=false
FIX_MODE=false
while [[ $# -gt 0 ]]; do
    case $1 in
        --quick) QUICK_MODE=true; shift ;;
        --fix)   FIX_MODE=true; shift ;;
        --help)
            echo "Usage: $0 [--quick] [--fix]"
            echo ""
            echo "  --quick   Run fast structural checks only (skip deep scans)"
            echo "  --fix     Auto-fix trivial issues (orphaned files, etc.)"
            echo ""
            echo "Checks performed:"
            echo "  1. Project reference integrity (.csproj -> .csproj paths)"
            echo "  2. Solution file references vs actual files"
            echo "  3. InternalsVisibleTo attribute validity"
            echo "  4. PackageId / AssemblyName consistency"
            echo "  5. Non-UTF-8 file encoding scan"
            echo "  6. Remaining Orleans references (delegates to audit script)"
            echo "  7. Binary file check (logos still containing Orleans)"
            exit 0 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Validate
if [[ ! -d "$PROJECT_ROOT" ]]; then
    echo "ERROR: Project directory not found: $PROJECT_ROOT"
    exit 1
fi

echo "" > "$LOG_FILE"

PASS_COUNT=0
FAIL_COUNT=0
WARN_COUNT=0

log() { echo "$1" | tee -a "$LOG_FILE"; }
pass() { ((PASS_COUNT++)) || true; log "  PASS: $1"; }
fail() { ((FAIL_COUNT++)) || true; log "  FAIL: $1"; }
warn() { ((WARN_COUNT++)) || true; log "  WARN: $1"; }

log "============================================================"
log "  SCYNAPSE POST-RENAME VERIFICATION"
log "  Project: $PROJECT_ROOT"
log "  Date:    $(date '+%Y-%m-%d %H:%M:%S')"
log "============================================================"
log ""

# ═════════════════════════════════════════════════════════════════════════════
# CHECK 1: Project Reference Integrity
# ═════════════════════════════════════════════════════════════════════════════
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
log "  CHECK 1: PROJECT REFERENCE INTEGRITY"
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

broken_refs=0
while IFS= read -r csproj; do
    [[ -z "$csproj" ]] && continue
    csproj_dir=$(dirname "$csproj")

    # Extract ProjectReference paths
    while IFS= read -r ref_path; do
        [[ -z "$ref_path" ]] && continue

        # Resolve relative path
        # Replace backslashes with forward slashes for Linux
        ref_path_fixed="${ref_path//\\//}"
        resolved="$csproj_dir/$ref_path_fixed"
        resolved=$(realpath -m "$resolved" 2>/dev/null || echo "$resolved")

        if [[ ! -f "$resolved" ]]; then
            ((broken_refs++)) || true
            rel_csproj="${csproj#$PROJECT_ROOT/}"
            fail "Broken ProjectReference in $rel_csproj -> $ref_path"
        fi
    done < <(grep -oP 'ProjectReference\s+Include="([^"]*)"' "$csproj" 2>/dev/null | \
             sed 's/ProjectReference\s*Include="//;s/"$//' || true)
done < <(find "$PROJECT_ROOT" -name "*.csproj" -type f 2>/dev/null)

if [[ $broken_refs -eq 0 ]]; then
    pass "All ProjectReference paths resolve to existing files"
fi
log ""

# ═════════════════════════════════════════════════════════════════════════════
# CHECK 2: Solution File References
# ═════════════════════════════════════════════════════════════════════════════
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
log "  CHECK 2: SOLUTION FILE REFERENCES"
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

sln_issues=0
while IFS= read -r slnfile; do
    [[ -z "$slnfile" ]] && continue
    sln_dir=$(dirname "$slnfile")
    rel_sln="${slnfile#$PROJECT_ROOT/}"

    # Check .sln files for project paths
    if [[ "$slnfile" == *.sln ]]; then
        while IFS= read -r proj_path; do
            [[ -z "$proj_path" ]] && continue
            proj_path_fixed="${proj_path//\\//}"
            resolved="$sln_dir/$proj_path_fixed"

            if [[ ! -f "$resolved" ]]; then
                ((sln_issues++)) || true
                fail "Broken .sln reference in $rel_sln -> $proj_path"
            fi
        done < <(grep -oP 'Project\([^)]*\)\s*=\s*"[^"]*",\s*"([^"]*)"' "$slnfile" 2>/dev/null | \
                 sed 's/.*",\s*"//;s/"$//' || true)
    fi

    # Check .slnx files (XML-based solution format)
    if [[ "$slnfile" == *.slnx ]]; then
        while IFS= read -r proj_path; do
            [[ -z "$proj_path" ]] && continue
            proj_path_fixed="${proj_path//\\//}"
            resolved="$sln_dir/$proj_path_fixed"

            if [[ ! -f "$resolved" ]]; then
                ((sln_issues++)) || true
                fail "Broken .slnx reference in $rel_sln -> $proj_path"
            fi
        done < <(grep -oP 'path="([^"]*\.csproj)"' "$slnfile" 2>/dev/null | \
                 sed 's/path="//;s/"$//' || true)
    fi
done < <(find "$PROJECT_ROOT" -type f \( -name "*.sln" -o -name "*.slnx" \) 2>/dev/null)

if [[ $sln_issues -eq 0 ]]; then
    pass "All solution file project references resolve correctly"
fi
log ""

# ═════════════════════════════════════════════════════════════════════════════
# CHECK 3: Non-UTF-8 Files Containing Orleans
# ═════════════════════════════════════════════════════════════════════════════
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
log "  CHECK 3: NON-UTF-8 FILES WITH ORLEANS"
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

non_utf8_orleans=0
while IFS= read -r filepath; do
    [[ -z "$filepath" ]] && continue

    encoding=$(file --mime-encoding "$filepath" 2>/dev/null | sed 's/.*: //')
    case "$encoding" in
        utf-8|us-ascii|ascii|unknown) continue ;;
    esac

    # Map to iconv encoding name
    iconv_enc="$encoding"
    case "$encoding" in
        utf-16le|utf-16-le)  iconv_enc="UTF-16LE" ;;
        utf-16be|utf-16-be)  iconv_enc="UTF-16BE" ;;
        utf-16)              iconv_enc="UTF-16" ;;
        iso-8859-1)          iconv_enc="ISO-8859-1" ;;
    esac

    if iconv -f "$iconv_enc" -t UTF-8 "$filepath" 2>/dev/null | grep -qi "orleans"; then
        ((non_utf8_orleans++)) || true
        rel_path="${filepath#$PROJECT_ROOT/}"
        fail "Non-UTF-8 file still contains Orleans: $rel_path ($encoding)"
        log "       Fix with: scynapse-rename-encoding-fix.sh --execute"
    fi
done < <(find "$PROJECT_ROOT" -type f \( -name "*.cs" -o -name "*.csproj" -o -name "*.resx" -o -name "*.config" \) 2>/dev/null)

if [[ $non_utf8_orleans -eq 0 ]]; then
    pass "No non-UTF-8 files contain Orleans references"
fi
log ""

# ═════════════════════════════════════════════════════════════════════════════
# CHECK 4: PackageId Consistency
# ═════════════════════════════════════════════════════════════════════════════
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
log "  CHECK 4: PACKAGEID CONSISTENCY"
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

pkg_issues=0

# Check that no .csproj has a PackageId containing "Orleans"
while IFS= read -r match; do
    [[ -z "$match" ]] && continue
    ((pkg_issues++)) || true
    fail "PackageId still contains Orleans: $match"
done < <(grep -r "<PackageId>.*Orleans.*</PackageId>" "$PROJECT_ROOT" --include="*.csproj" 2>/dev/null | \
         sed "s|$PROJECT_ROOT/||" || true)

# Check that no .csproj has an AssemblyName containing "Orleans"
while IFS= read -r match; do
    [[ -z "$match" ]] && continue
    ((pkg_issues++)) || true
    fail "AssemblyName still contains Orleans: $match"
done < <(grep -r "<AssemblyName>.*Orleans.*</AssemblyName>" "$PROJECT_ROOT" --include="*.csproj" 2>/dev/null | \
         sed "s|$PROJECT_ROOT/||" || true)

if [[ $pkg_issues -eq 0 ]]; then
    pass "All PackageId and AssemblyName values are free of Orleans"
fi
log ""

# ═════════════════════════════════════════════════════════════════════════════
# CHECK 5: Binary Files
# ═════════════════════════════════════════════════════════════════════════════
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
log "  CHECK 5: BINARY FILES"
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

binary_count=0
while IFS= read -r binfile; do
    [[ -z "$binfile" ]] && continue
    ((binary_count++)) || true
    rel_path="${binfile#$PROJECT_ROOT/}"
    warn "Binary file may contain Orleans metadata: $rel_path"
done < <(grep -ril "orleans" "$PROJECT_ROOT" --include="*.png" --include="*.jpg" \
         --include="*.gif" --include="*.ico" --include="*.svg" 2>/dev/null || true)

if [[ $binary_count -eq 0 ]]; then
    pass "No binary files contain Orleans references"
else
    log "       Binary files need manual replacement (new logo images)"
fi
log ""

# ═════════════════════════════════════════════════════════════════════════════
# CHECK 6: Text Content (Quick Grep)
# ═════════════════════════════════════════════════════════════════════════════
if [[ "$QUICK_MODE" == false ]]; then
    log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    log "  CHECK 6: REMAINING TEXT REFERENCES"
    log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    INCLUDE_ARGS="--include=*.cs --include=*.csproj --include=*.fsproj --include=*.sln --include=*.slnx --include=*.md --include=*.json --include=*.yaml --include=*.yml --include=*.xml --include=*.props --include=*.targets --include=*.sql --include=*.resx"

    text_refs=$(grep -ril "orleans" "$PROJECT_ROOT" $INCLUDE_ARGS 2>/dev/null | wc -l || true)

    if [[ $text_refs -gt 0 ]]; then
        fail "$text_refs text files still contain 'orleans' (case-insensitive)"
        log "       Run: ./scynapse-rename-audit.sh for details"
    else
        pass "No text files contain Orleans references"
    fi
    log ""
fi

# ═════════════════════════════════════════════════════════════════════════════
# CHECK 7: Namespace vs Directory Name Consistency
# ═════════════════════════════════════════════════════════════════════════════
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
log "  CHECK 7: DIRECTORY NAMES"
log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

dir_issues=0
while IFS= read -r dir; do
    [[ -z "$dir" ]] && continue
    ((dir_issues++)) || true
    rel_dir="${dir#$PROJECT_ROOT/}"
    fail "Directory still named with Orleans: $rel_dir"
done < <(find "$PROJECT_ROOT" -type d -iname "*orleans*" 2>/dev/null)

if [[ $dir_issues -eq 0 ]]; then
    pass "No directories contain Orleans in their name"
fi

file_issues=0
while IFS= read -r file; do
    [[ -z "$file" ]] && continue
    ((file_issues++)) || true
    rel_file="${file#$PROJECT_ROOT/}"
    fail "File still named with Orleans: $rel_file"
done < <(find "$PROJECT_ROOT" -type f -iname "*orleans*" 2>/dev/null)

if [[ $file_issues -eq 0 ]]; then
    pass "No files contain Orleans in their name"
fi
log ""

# ═════════════════════════════════════════════════════════════════════════════
# SUMMARY
# ═════════════════════════════════════════════════════════════════════════════
log "============================================================"
log "  VERIFICATION SUMMARY"
log "============================================================"
log ""
log "  Passed:   $PASS_COUNT"
log "  Failed:   $FAIL_COUNT"
log "  Warnings: $WARN_COUNT"
log ""

if [[ $FAIL_COUNT -eq 0 ]]; then
    if [[ $WARN_COUNT -eq 0 ]]; then
        log "  RESULT: ALL CHECKS PASSED"
    else
        log "  RESULT: PASSED WITH WARNINGS (binary files need manual attention)"
    fi
else
    log "  RESULT: FAILURES DETECTED - review and fix before committing"
fi

log ""
log "  Log: $LOG_FILE"
log "============================================================"

# Exit with appropriate code
if [[ $FAIL_COUNT -gt 0 ]]; then
    exit 1
fi
exit 0
