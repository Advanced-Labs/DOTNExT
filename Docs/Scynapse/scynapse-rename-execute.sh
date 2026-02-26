#!/usr/bin/env bash
# =============================================================================
# scynapse-rename-execute.sh
#
# Renames all Orleans/NewOrleans/Microsoft.Orleans references to
# Scynapse/Genesa.Scynapse throughout the project directory.
#
# Usage:
#   ./scynapse-rename-execute.sh --dry-run      # Preview only (RECOMMENDED FIRST)
#   ./scynapse-rename-execute.sh --execute       # Actually perform the rename
#   ./scynapse-rename-execute.sh --phase 3       # Run only phase 3 (content)
#   ./scynapse-rename-execute.sh --resume 3      # Resume from phase 3 onward
#
# IMPORTANT: Run the audit script BEFORE and AFTER to verify results.
# IMPORTANT: Commit your current state before running --execute.
# =============================================================================

set -euo pipefail

# ─── RENAME MAPPING ──────────────────────────────────────────────────────────
# These are applied IN ORDER. Longer/more-specific patterns MUST come first
# to prevent partial matches from corrupting them.
#
# Format: "OLD_PATTERN|NEW_PATTERN"
# These are used for both directory/file renames and content replacement.
# ─────────────────────────────────────────────────────────────────────────────

# Content replacement pairs (applied in this order via sed)
# CRITICAL: Order matters! More specific patterns before general ones.
CONTENT_REPLACEMENTS=(
    # --- Phase A: URLs (most specific — must come before generic patterns) ---
    "github\.com/dotnet/orleans|github.com/Scynapse/Core"   # GitHub repo URL
    "github\.com/dotnet/Orleans|github.com/Scynapse/Core"   # GitHub repo URL (PascalCase)

    # --- Phase B: Compound/prefixed patterns ---
    "Microsoft\.Orleans|Genesa.Scynapse"          # NuGet PackageIds: Microsoft.Orleans.Core -> Genesa.Scynapse.Core
    "microsoft\.orleans|genesa.scynapse"           # Lowercase variant if any
    "MICROSOFT\.ORLEANS|GENESA.SCYNAPSE"           # Uppercase variant if any

    # --- Phase C: Previous project name remnants ---
    "NewOrleans|Scynapse"                          # NewOrleans -> Scynapse
    "NEWORLEANS|SCYNAPSE"                          # NEWORLEANS -> SCYNAPSE
    "neworleans|scynapse"                          # neworleans -> scynapse
    "new-orleans|scynapse"                         # kebab-case
    "new_orleans|scynapse"                         # snake_case

    # --- Phase D: Main Orleans rename (PascalCase) ---
    "Orleans|Scynapse"                             # The big one: Orleans -> Scynapse

    # --- Phase E: Lowercase/uppercase variants ---
    "orleans|scynapse"                             # lowercase (URLs, paths, vars)
    "ORLEANS|SCYNAPSE"                             # UPPERCASE (diagnostic IDs, env vars)
)

# Directory/file name replacement pairs (simpler - no regex escaping needed)
NAME_REPLACEMENTS=(
    "Microsoft.Orleans|Genesa.Scynapse"
    "NewOrleans|Scynapse"
    "Orleans|Scynapse"
    "orleans|scynapse"
    "ORLEANS|SCYNAPSE"
)

# ─── CONFIGURABLE VARIABLES ──────────────────────────────────────────────────

# Root of the Scynapse project directory
PROJECT_ROOT="${PROJECT_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)/src/Scynapse}"

# File extensions for content replacement (comma-separated)
CONTENT_EXTENSIONS="cs,csproj,fsproj,sln,slnx,md,json,yaml,yml,xml,props,targets,cmd,ps1,sh,sql,config,txt,proto,resx,html,css,tsx,ts,fs,gitignore,gitattributes,editorconfig"

# Log file
LOG_FILE="${LOG_FILE:-/tmp/scynapse-rename-$(date +%Y%m%d-%H%M%S).log}"

# ─── END CONFIGURABLE VARIABLES ─────────────────────────────────────────────

# Parse arguments
DRY_RUN=true
RUN_PHASE=0      # 0 = all phases
RESUME_FROM=0    # 0 = start from beginning
while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)  DRY_RUN=true; shift ;;
        --execute)  DRY_RUN=false; shift ;;
        --phase)    RUN_PHASE=$2; shift 2 ;;
        --resume)   RESUME_FROM=$2; shift 2 ;;
        --help)
            echo "Usage: $0 [--dry-run|--execute] [--phase N] [--resume N]"
            echo ""
            echo "  --dry-run   Preview changes without executing (default)"
            echo "  --execute   Actually perform the rename"
            echo "  --phase N   Run only phase N (1=dirs, 2=files, 3=content)"
            echo "  --resume N  Resume from phase N onward"
            echo ""
            echo "Phases:"
            echo "  1 - Rename directories (deepest first)"
            echo "  2 - Rename files"
            echo "  3 - Replace content in files"
            echo ""
            echo "Environment variables:"
            echo "  PROJECT_ROOT  Path to Scynapse project (default: auto-detect)"
            echo "  LOG_FILE      Path to log file (default: /tmp/scynapse-rename-*.log)"
            exit 0 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Validate
if [[ ! -d "$PROJECT_ROOT" ]]; then
    echo "ERROR: Project directory not found: $PROJECT_ROOT"
    exit 1
fi

# Helper functions
log() {
    echo "$1" | tee -a "$LOG_FILE"
}

log_detail() {
    echo "$1" >> "$LOG_FILE"
}

should_run_phase() {
    local phase=$1
    if [[ $RUN_PHASE -gt 0 ]]; then
        [[ $RUN_PHASE -eq $phase ]]
    elif [[ $RESUME_FROM -gt 0 ]]; then
        [[ $phase -ge $RESUME_FROM ]]
    else
        true
    fi
}

# ─── START ───────────────────────────────────────────────────────────────────
echo "" > "$LOG_FILE"
log "============================================================"
log "  SCYNAPSE RENAME EXECUTION"
log "  Project:  $PROJECT_ROOT"
log "  Mode:     $(if $DRY_RUN; then echo 'DRY RUN (preview only)'; else echo 'EXECUTE (making changes!)'; fi)"
log "  Log:      $LOG_FILE"
log "  Date:     $(date '+%Y-%m-%d %H:%M:%S')"
log "============================================================"
log ""

if ! $DRY_RUN; then
    log "⚠  EXECUTING IN 5 SECONDS - Press Ctrl+C to abort!"
    sleep 5
fi

# Build include pattern for find/grep
build_include_args() {
    local args=""
    IFS=',' read -ra EXTS <<< "$CONTENT_EXTENSIONS"
    for ext in "${EXTS[@]}"; do
        args="$args --include=*.$ext"
    done
    echo "$args"
}
INCLUDE_ARGS=$(build_include_args)

# ═════════════════════════════════════════════════════════════════════════════
# PHASE 1: RENAME DIRECTORIES (deepest first)
# ═════════════════════════════════════════════════════════════════════════════
if should_run_phase 1; then
    log ""
    log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    log "  PHASE 1: RENAME DIRECTORIES"
    log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    log ""

    dir_rename_count=0
    dir_skip_count=0

    # Get all matching directories, sorted by depth (deepest first) to avoid
    # renaming a parent before its children
    while IFS= read -r dir; do
        [[ -z "$dir" ]] && continue

        dir_basename=$(basename "$dir")
        dir_parent=$(dirname "$dir")
        new_basename="$dir_basename"

        # Apply name replacements in order
        for pair in "${NAME_REPLACEMENTS[@]}"; do
            old="${pair%%|*}"
            new="${pair##*|}"
            new_basename="${new_basename//$old/$new}"
        done

        if [[ "$dir_basename" == "$new_basename" ]]; then
            ((dir_skip_count++)) || true
            continue
        fi

        new_path="$dir_parent/$new_basename"
        ((dir_rename_count++)) || true

        if $DRY_RUN; then
            log "  [DRY] mv: $(echo "$dir" | sed "s|$PROJECT_ROOT/||")"
            log "       ->  $(echo "$new_path" | sed "s|$PROJECT_ROOT/||")"
            log_detail ""
        else
            if [[ -d "$new_path" ]]; then
                log "  ⚠ SKIP (target exists): $new_basename"
            else
                git -C "$PROJECT_ROOT" mv "$dir" "$new_path" 2>/dev/null || mv "$dir" "$new_path"
                log "  ✓ Renamed: $(basename "$dir") -> $new_basename"
            fi
        fi
    done < <(find "$PROJECT_ROOT" -type d -iname "*orleans*" 2>/dev/null | awk '{print length, $0}' | sort -rn | cut -d' ' -f2-)

    log ""
    log "  Phase 1 complete: $dir_rename_count directories to rename ($dir_skip_count unchanged)"
fi

# ═════════════════════════════════════════════════════════════════════════════
# PHASE 2: RENAME FILES
# ═════════════════════════════════════════════════════════════════════════════
if should_run_phase 2; then
    log ""
    log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    log "  PHASE 2: RENAME FILES"
    log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    log ""

    file_rename_count=0
    file_skip_count=0

    while IFS= read -r file; do
        [[ -z "$file" ]] && continue

        file_basename=$(basename "$file")
        file_dir=$(dirname "$file")
        new_basename="$file_basename"

        # Apply name replacements in order
        for pair in "${NAME_REPLACEMENTS[@]}"; do
            old="${pair%%|*}"
            new="${pair##*|}"
            new_basename="${new_basename//$old/$new}"
        done

        if [[ "$file_basename" == "$new_basename" ]]; then
            ((file_skip_count++)) || true
            continue
        fi

        new_path="$file_dir/$new_basename"
        ((file_rename_count++)) || true

        if $DRY_RUN; then
            log "  [DRY] mv: $(echo "$file" | sed "s|$PROJECT_ROOT/||")"
            log "       ->  $(echo "$new_path" | sed "s|$PROJECT_ROOT/||")"
            log_detail ""
        else
            if [[ -f "$new_path" ]]; then
                log "  ⚠ SKIP (target exists): $new_basename"
            else
                git -C "$PROJECT_ROOT" mv "$file" "$new_path" 2>/dev/null || mv "$file" "$new_path"
                log "  ✓ Renamed: $file_basename -> $new_basename"
            fi
        fi
    done < <(find "$PROJECT_ROOT" -type f -iname "*orleans*" -o -type f -iname "*neworleans*" 2>/dev/null | sort)

    log ""
    log "  Phase 2 complete: $file_rename_count files to rename ($file_skip_count unchanged)"
fi

# ═════════════════════════════════════════════════════════════════════════════
# PHASE 3: REPLACE CONTENT IN FILES
# ═════════════════════════════════════════════════════════════════════════════
if should_run_phase 3; then
    log ""
    log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    log "  PHASE 3: REPLACE CONTENT IN FILES"
    log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    log ""

    # Build a single sed command with all replacements chained
    # This applies all patterns in one pass per file (efficient)
    SED_ARGS=""
    for pair in "${CONTENT_REPLACEMENTS[@]}"; do
        old="${pair%%|*}"
        new="${pair##*|}"
        SED_ARGS="$SED_ARGS -e 's|${old}|${new}|g'"
    done

    # Find all text files containing any case of 'orleans'
    content_files=$(grep -ril "orleans" "$PROJECT_ROOT" $INCLUDE_ARGS 2>/dev/null | sort || true)
    content_count=$(echo "$content_files" | grep -c . || true)

    log "  Files to process: $content_count"
    log ""

    if [[ $content_count -gt 0 ]]; then
        processed=0
        changed=0

        while IFS= read -r file; do
            [[ -z "$file" ]] && continue
            ((processed++)) || true

            # Show progress every 100 files
            if (( processed % 100 == 0 )); then
                log "  ... processed $processed / $content_count files ($changed changed so far)"
            fi

            if $DRY_RUN; then
                # In dry run, count how many lines would change
                match_count=$(eval "sed $SED_ARGS" < "$file" | diff --suppress-common-lines "$file" - 2>/dev/null | grep -c "^[<>]" || true)
                if [[ $match_count -gt 0 ]]; then
                    ((changed++)) || true
                    log_detail "  [DRY] Would change $match_count lines in: $(echo "$file" | sed "s|$PROJECT_ROOT/||")"
                fi
            else
                # Create temp file, apply sed, check if changed, replace if so
                tmp_file=$(mktemp)
                eval "sed $SED_ARGS" < "$file" > "$tmp_file"

                if ! cmp -s "$file" "$tmp_file"; then
                    cp "$tmp_file" "$file"
                    ((changed++)) || true
                    log_detail "  ✓ Updated: $(echo "$file" | sed "s|$PROJECT_ROOT/||")"
                fi
                rm -f "$tmp_file"
            fi
        done <<< "$content_files"

        log ""
        log "  Phase 3 complete: $processed files processed, $changed files $(if $DRY_RUN; then echo 'would be'; else echo 'were'; fi) changed"
    fi
fi

# ═════════════════════════════════════════════════════════════════════════════
# SUMMARY
# ═════════════════════════════════════════════════════════════════════════════
log ""
log "============================================================"
log "  EXECUTION COMPLETE"
log "============================================================"
log ""
if $DRY_RUN; then
    log "  This was a DRY RUN. No changes were made."
    log "  Review the log at: $LOG_FILE"
    log "  To execute for real:  $0 --execute"
else
    log "  Changes have been applied!"
    log "  Log saved to: $LOG_FILE"
    log ""
    log "  NEXT STEPS:"
    log "    1. Run the audit script to verify:  ./scynapse-rename-audit.sh --summary"
    log "    2. Handle binary files manually (logos, images)"
    log "    3. Review git diff for correctness"
    log "    4. Commit the changes"
fi
log ""
log "  ⚠ MANUAL ITEMS (cannot be scripted):"
log "    - Binary files (OrleansLogo.png, etc.) need manual replacement"
log "    - Verify .verified.cs snapshot files may need regeneration"
log "    - SQL migration scripts may need database-side updates"
log "============================================================"
