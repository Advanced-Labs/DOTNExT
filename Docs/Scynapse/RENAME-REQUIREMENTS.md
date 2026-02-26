# Scynapse Project Rename Requirements

## STATUS: Rename Complete (Text) / Binary Logos Pending

**Previous Rename:** NewOrleans -> Scynapse (completed 2026-02-26)
**Full Rename:** Orleans -> Scynapse / Microsoft.Orleans -> Genesa.Scynapse (completed 2026-02-26)
**Date Assessed:** 2026-02-26
**Scope:** `src/Scynapse/` directory (3,344 files, 772 directories)

### Current State

| Metric | Before Rename | After Rename |
|--------|--------------|--------------|
| Directories with "Orleans" in name | 139 | **0** |
| Files with "Orleans" in filename | 230 | **0** |
| Files with "Orleans" in content (text) | 2,891 | **0** |
| Binary files with "Orleans" metadata | 3 | **3** (logos, manual replacement) |
| Non-UTF-8 files with "Orleans" | 1 | **0** (fixed manually) |

---

## Script Inventory

Four scripts handle different aspects of the rename. All live in `Docs/Scynapse/` and should be run from that directory.

### 1. `scynapse-rename-audit.sh` — Discovery & Counting

**Purpose:** Find all remaining Orleans references. Run BEFORE and AFTER rename to verify completeness.

```bash
./scynapse-rename-audit.sh              # Full report
./scynapse-rename-audit.sh --summary    # Counts only
./scynapse-rename-audit.sh --output report  # Save to report files
```

**What it checks:** Directory names, file names, file content (by extension), binary files, GitHub URLs, NuGet package IDs, diagnostic IDs, environment variables, SQL schema references.

**Limitations:** Only searches text files by extension whitelist. Cannot detect encoding issues — non-UTF-8 files show up in content grep but `grep` may silently fail to match inside them.

---

### 2. `scynapse-rename-execute.sh` — Bulk Text Rename (3 Phases)

**Purpose:** Perform the actual find-and-replace across the entire codebase.

```bash
./scynapse-rename-execute.sh --dry-run      # Preview changes (ALWAYS run first)
./scynapse-rename-execute.sh --execute       # Apply changes
./scynapse-rename-execute.sh --phase 1       # Dirs only
./scynapse-rename-execute.sh --phase 2       # Files only
./scynapse-rename-execute.sh --phase 3       # Content only
```

**Phase 1 — Directory renames:** Finds directories with `Orleans` in the name, processes deepest-first (to avoid renaming parents before children), uses `git mv` where possible.

**Phase 2 — File renames:** Finds files with `Orleans` in the name, renames via `git mv`.

**Phase 3 — Content replacement:** Applies ordered `sed` substitutions across all text files matching the extension whitelist. All replacement patterns are applied in a single `sed` pass per file for efficiency.

**Replacement Mapping (applied in this order):**

| # | Old Pattern | New Pattern | Scope |
|---|-------------|-------------|-------|
| 1 | `Microsoft.Orleans` | `Genesa.Scynapse` | NuGet package IDs |
| 2 | `microsoft.orleans` | `genesa.scynapse` | Lowercase variant |
| 3 | `MICROSOFT.ORLEANS` | `GENESA.SCYNAPSE` | Uppercase variant |
| 4 | `NewOrleans` | `Scynapse` | Previous name remnants |
| 5 | `NEWORLEANS` | `SCYNAPSE` | Previous name uppercase |
| 6 | `neworleans` | `scynapse` | Previous name lowercase |
| 7 | `new-orleans` | `scynapse` | Kebab-case |
| 8 | `new_orleans` | `scynapse` | Snake_case |
| 9 | `Orleans` | `Scynapse` | Main rename (PascalCase) |
| 10 | `orleans` | `scynapse` | Lowercase (URLs, paths, vars) |
| 11 | `ORLEANS` | `SCYNAPSE` | Uppercase (diag IDs, env vars) |

**Critical: Order matters!** More-specific patterns (e.g., `Microsoft.Orleans`) must come before the general `Orleans` pattern, otherwise `Orleans` would be replaced first, creating `Microsoft.Scynapse` instead of the correct `Genesa.Scynapse`.

**What this script CANNOT handle (and why):**

| Gap | Why | Solution |
|-----|-----|----------|
| **Non-UTF-8 files** | `sed` reads byte streams. UTF-16 stores `Orleans` as `O\x00r\x00l\x00e\x00a\x00n\x00s\x00` — sed patterns won't match. | Use `scynapse-rename-encoding-fix.sh` after Phase 3. |
| **Binary files** | Images (.png, .jpg) can't be text-processed. | Manual replacement with new logo assets. |
| **SQL database objects** | Script renames the SQL *files*, but existing *databases* still have old table/procedure names. | Write SQL migration scripts for deployed databases. |
| **`.verified.cs` snapshots** | Content gets renamed by sed, but the generated output from re-running tests may differ from what sed produced (different formatting, ordering). | Re-run tests with `--update-snapshots` or Verify's auto-accept after rename. |

---

### 3. `scynapse-rename-encoding-fix.sh` — Non-UTF-8 File Handler

**Purpose:** Catch files that `sed` silently skipped due to non-UTF-8 encoding.

```bash
./scynapse-rename-encoding-fix.sh --scan-only   # List non-UTF-8 files
./scynapse-rename-encoding-fix.sh --dry-run      # Preview what would change
./scynapse-rename-encoding-fix.sh --execute       # Apply fixes
```

**How it works:**
1. Scans all text-like files with `file --mime-encoding` to detect non-UTF-8
2. For each non-UTF-8 file, converts to UTF-8 via `iconv`
3. Checks if the UTF-8 version contains `orleans` (case-insensitive)
4. Applies the same replacement mapping as the execute script
5. Converts back to the original encoding, preserving BOM if present

**Why this is a separate script:**
The main execute script uses `grep -ril "orleans"` to find candidates, then `sed` to replace. But `grep` on a UTF-16 file may find a match (since grep can be binary-aware) while `sed` fails to replace (since sed is strictly line-oriented ASCII/UTF-8). This creates a false sense of "processed" when the file was actually unchanged.

**When to run:** After `scynapse-rename-execute.sh --execute` Phase 3 completes. This is the "mop-up" pass.

**Real example:** `Scynapse.Core/GlobalSuppressions.cs` was UTF-16 encoded (Visual Studio creates these in UTF-16). The execute script's sed pass ran on it but couldn't match `Orleans.Runtime.SafeTimer` in the `[SuppressMessage]` target string. This was caught and fixed manually before this script existed — the script prevents this class of miss in the future.

---

### 4. `scynapse-rename-post-verify.sh` — Structural Integrity Check

**Purpose:** Verify that the rename didn't break structural references. This is NOT a grep for "Orleans" — it checks that the codebase still hangs together correctly after all the renaming.

```bash
./scynapse-rename-post-verify.sh              # Full verification
./scynapse-rename-post-verify.sh --quick       # Fast checks only
./scynapse-rename-post-verify.sh --fix         # Auto-fix trivial issues
```

**What it checks:**

| Check | What it validates |
|-------|-------------------|
| **ProjectReference integrity** | Every `<ProjectReference Include="...">` in every `.csproj` resolves to an actual file on disk. |
| **Solution file references** | Every project path in `.sln` and `.slnx` files resolves to an actual file. |
| **Non-UTF-8 Orleans check** | Re-scans non-UTF-8 files specifically (catches encoding-fix misses). |
| **PackageId consistency** | No `.csproj` has `<PackageId>` or `<AssemblyName>` containing "Orleans". |
| **Binary files** | Lists any image files whose binary content contains "orleans" metadata. |
| **Text content** | Quick grep for any remaining text references (defers to audit script for details). |
| **Directory/file names** | No directories or files still named with "Orleans". |

**When to run:** After ALL other rename scripts have completed, before committing.

---

## Complete Rename Workflow (for future AI agents)

If you need to re-run the rename (e.g., after merging upstream Orleans changes), follow this exact sequence:

```bash
cd Docs/Scynapse/

# ── STEP 1: Pre-rename audit ──
./scynapse-rename-audit.sh --summary
# Record the "before" numbers

# ── STEP 2: Commit current state ──
git add -A && git commit -m "Pre-rename checkpoint"

# ── STEP 3: Execute rename (3 phases) ──
./scynapse-rename-execute.sh --dry-run     # Review first!
./scynapse-rename-execute.sh --execute     # Phase 1 (dirs), 2 (files), 3 (content)

# ── STEP 4: Fix non-UTF-8 files ──
./scynapse-rename-encoding-fix.sh --dry-run
./scynapse-rename-encoding-fix.sh --execute

# ── STEP 5: Verify structural integrity ──
./scynapse-rename-post-verify.sh

# ── STEP 6: Post-rename audit ──
./scynapse-rename-audit.sh --summary
# Compare with "before" numbers — everything should be 0

# ── STEP 7: Handle manual items ──
# - Replace binary logo files with Scynapse versions
# - Re-run tests to regenerate .verified.cs snapshots
# - Write SQL migration scripts if databases are affected

# ── STEP 8: Commit ──
git add -A && git commit -m "Rename Orleans -> Scynapse / Microsoft.Orleans -> Genesa.Scynapse"
```

---

## What Remains (as of 2026-02-26)

### Complete — No Action Needed

| Category | Status |
|----------|--------|
| Directory names | 0 remaining |
| File names | 0 remaining |
| Namespaces (`namespace Orleans.*`) | All renamed to `Scynapse.*` |
| Type names (`OrleansException`, etc.) | All renamed to `Scynapse*` |
| NuGet PackageIds (`Microsoft.Orleans.*`) | All renamed to `Genesa.Scynapse.*` |
| Assembly names | All renamed |
| InternalsVisibleTo attributes | All updated |
| Diagnostic IDs (`ORLEANS0xxx`) | All renamed to `SCYNAPSE0xxx` |
| Experimental flags (`ORLEANSEXPxxx`) | All renamed to `SCYNAPSEEXPxxx` |
| Environment variables (`ORLEANS_*`) | All renamed to `SCYNAPSE_*` |
| GitHub URLs (`github.com/dotnet/orleans`) | All renamed to `github.com/dotnet/scynapse` |
| Comments and XML docs | All updated |
| SQL file content | All updated |
| Build configs (`.props`, `.targets`) | All updated |
| CI/CD configs (`.azure/`, `.github/`) | All updated |
| Non-UTF-8 encoded files | All fixed (1 was found and fixed) |

### Pending — Manual Action Required

| Item | Files | Action |
|------|-------|--------|
| **Logo/image files** | `assets/logo_128.png`, `Dashboard/.../ScynapseLogo.png`, `Scynapse.Identity/logo.png` | Replace with Scynapse-branded images. These are binary PNG files — the "orleans" reference is in embedded metadata (EXIF, PNG text chunks) that can't be text-edited. |
| **Deployed databases** | N/A (no deployed instances yet) | When SQL schemas are deployed, run migration scripts to rename stored procedures and tables. The SQL *files* are already renamed. |

### Future Considerations — When Publishing NuGet Packages

When Scynapse is published as NuGet packages:

1. **Package IDs are already set:** All `.csproj` files have `<PackageId>Genesa.Scynapse.*</PackageId>`.
2. **Package versions:** The `Directory.Packages.props` at the repo root controls package versions for internal consumption. When publishing externally, ensure version numbers don't conflict with the upstream `Microsoft.Orleans.*` packages.
3. **Package signing:** If publishing to nuget.org, packages will need a Genesa signing certificate. The upstream `Microsoft.Orleans.*` packages are Microsoft-signed — our packages must have a different identity chain.
4. **Metapackage:** `Genesa.Scynapse.Sdk` is the SDK metapackage (replaces `Microsoft.Orleans.Sdk`). Consumers will reference this.
5. **Code generators:** The `Genesa.Scynapse.CodeGenerator` package contains MSBuild `.props`/`.targets` files that were renamed from `Microsoft.Orleans.CodeGenerator.props` to `Genesa.Scynapse.CodeGenerator.props`. These are loaded by MSBuild via the NuGet package layout convention, so the filenames must match the package ID.

---

## Understanding the Rename Scripts' Design Decisions

### Why sed and not a smarter tool?

`sed` was chosen because:
- It's universally available on Linux/macOS/WSL
- It handles the vast majority of files (99.97% of text files are UTF-8/ASCII)
- It's fast — can process thousands of files in seconds
- The replacement patterns are simple string substitutions, not context-aware refactoring

The tradeoff is that `sed` can't handle non-UTF-8 encodings, which is why the encoding-fix script exists as a complement.

### Why is pattern order so critical?

Consider the string `Microsoft.Orleans.Core`:
- If `Orleans -> Scynapse` runs first: `Microsoft.Scynapse.Core` (WRONG — `Microsoft.` prefix kept)
- If `Microsoft.Orleans -> Genesa.Scynapse` runs first: `Genesa.Scynapse.Core` (CORRECT)

Similarly, `OrleansCodeGen.Orleans.Runtime`:
- The execute script handles this correctly because `Orleans -> Scynapse` is a global replacement, so both instances get replaced to `ScynapseCodeGen.Scynapse.Runtime`.

### Why are binary files excluded?

Binary files (PNG, JPG, DLL) store data in non-text formats. Even if they contain the ASCII string "orleans" (e.g., in PNG metadata chunks), running `sed` on a binary file would corrupt the file structure. Logo images specifically need to be re-created by a designer or re-exported from a graphics tool.

### Why is encoding detection a separate pass?

Detecting encoding is expensive (requires reading file headers via `file` command for every file) and rarely needed (only ~0.03% of files are non-UTF-8 in this codebase). Making it a separate script keeps the main rename fast while providing a thorough safety net.

---

## Handling Future Upstream Merges

When merging new Orleans upstream changes into Scynapse:

1. **New files from upstream will use `Orleans` naming.** After merging, re-run the full rename workflow (all 4 scripts).
2. **Conflict resolution:** If upstream renamed a file we also renamed, git will show a rename conflict. Resolve by keeping our `Scynapse` name.
3. **New namespaces:** If upstream adds new `Orleans.*` namespaces, the `Orleans -> Scynapse` sed pattern will catch them automatically.
4. **New NuGet packages:** If upstream adds new `Microsoft.Orleans.*` packages, the `Microsoft.Orleans -> Genesa.Scynapse` pattern will catch them.
5. **New diagnostic IDs:** If upstream adds `ORLEANS0014`, the `ORLEANS -> SCYNAPSE` pattern will rename it to `SCYNAPSE0014`.

The scripts are idempotent — running them on an already-renamed codebase produces no changes (the audit confirms 0 hits). This makes it safe to run them after every merge.

---

## Notes

- This document serves as the primary reference for the Scynapse rename process
- The full file-by-file inventory is in `RENAME-FILE-INVENTORY.md`
- All scripts are in `Docs/Scynapse/` and should be run from that directory
- The scripts use `PROJECT_ROOT` environment variable (auto-detected from script location)
- Log files are written to `/tmp/scynapse-*` by default
