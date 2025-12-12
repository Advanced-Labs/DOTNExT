# VS2026 Migration Testing Plan

> **Purpose:** Document the feasibility and process for migrating DOTNExT build/deploy workflow from VS2022 to VS2026
> **Created:** 2025-12-09
> **Status:** NEEDS TESTING

---

## Executive Summary

**Goal:** Verify that we can reproduce the current DOTNExT workflow (build VMR + Roslyn, deploy VSIX to experimental instance, launch custom VS) with VS2026 instead of VS2022.

**Why This Matters:**
- .NET 10 **requires** VS2026 for targeting `net10.0`
- VS2022 can only do "downlevel" targeting (net9.0 and earlier) with .NET 10 SDK
- If we want runtime-async (a .NET 10 feature), we need the VS2026 workflow working

**Initial Assessment:** Based on research, the workflow **should work** with minimal changes. VS2026 maintains compatibility with VS2022 patterns.

---

## Current VS2022 Workflow (Working)

### Build Steps (`Update-DOTNExT.ps1`)

1. **Build Runtime:**
   ```powershell
   # Uses VsDevCmd.bat for build environment
   & cmd.exe /c "...\VsDevCmd.bat && cd runtime && build.cmd -subset clr+libs -c Release"
   ```

2. **Generate Core_Root:**
   ```powershell
   & cmd.exe /c "...\VsDevCmd.bat && src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=Release"
   ```

3. **Build Roslyn with VSIX:**
   ```powershell
   & cmd.exe /c "...\VsDevCmd.bat && Build.cmd -restore -build -c Release -deployExtensions"
   ```

4. **Deploy VSIX to Experimental Instance:**
   ```powershell
   & $VSIXInstaller /quiet /rootSuffix:RoslynDev $compilerVsix
   & $VSIXInstaller /quiet /rootSuffix:RoslynDev $setupVsix
   ```

5. **Launch VS with Custom Environment:**
   ```cmd
   SET DOTNET_ROOT=...
   SET CORE_ROOT=...
   devenv.exe /rootSuffix RoslynDev
   ```

### Key Paths (VS2022)

| Component | Path |
|-----------|------|
| VS Install | `C:\Program Files\Microsoft Visual Studio\2022\{Edition}` |
| VsDevCmd.bat | `...\Common7\Tools\VsDevCmd.bat` |
| VSIXInstaller | `...\Common7\IDE\VSIXInstaller.exe` |
| devenv.exe | `...\Common7\IDE\devenv.exe` |
| Experimental Hive | `/rootSuffix RoslynDev` |

---

## VS2026 Compatibility Research

### Sources Consulted

1. **[Visual Studio 2026 Release Notes](https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes)**
   - Released November 24, 2025 (GA)
   - Full .NET 10 and C# 14 support
   - Over 4,000 VS2022 extensions compatible

2. **[VS2026 Port, Migrate, and Upgrade Projects](https://learn.microsoft.com/en-us/visualstudio/releases/2026/port-migrate-and-upgrade-visual-studio-projects)**
   - Extensions with MinimumVersion 17.0+ work unchanged
   - No SDK or manifest changes required for forward compatibility

3. **[The Experimental Instance Documentation](https://learn.microsoft.com/en-us/visualstudio/extensibility/the-experimental-instance?view=vs-2022)**
   - Still uses `/rootSuffix` pattern
   - New menu: Extensions → Extension Development → Start/Reset Experimental Instance
   - Old pattern (`devenv /rootSuffix Exp`) still works

4. **[Roslyn Building Guide](https://github.com/dotnet/roslyn/blob/main/docs/contributing/Building,%20Debugging,%20and%20Testing%20on%20Windows.md)**
   - RoslynDev hive is "an entirely separate instance"
   - `devenv /rootSuffix RoslynDev` is the launch pattern
   - Currently only documents VS2022, but patterns are compatible

5. **[Roslyn VSIX Manifest](https://github.com/dotnet/roslyn/blob/main/src/Deployment/source.extension.vsixmanifest)**
   - Version range: `[17.0,19.0)` - includes VS2026 (18.x)

### Compatibility Findings

| Component | VS2022 | VS2026 | Compatible? |
|-----------|--------|--------|-------------|
| Version Number | 17.x | 18.x | N/A |
| VsDevCmd.bat location | Same relative path | Same relative path | ✅ Yes |
| VSIXInstaller.exe | Same relative path | Same relative path | ✅ Yes |
| `/rootSuffix` pattern | Supported | Supported | ✅ Yes |
| `RoslynDev` hive | Works | Should work | ⚠️ NEEDS TEST |
| VSIX manifest range | `[17.0,19.0)` | Includes 18.x | ✅ Yes |
| Extension compatibility | N/A | 4000+ VS2022 extensions | ✅ Yes |

---

## Required Script Changes

### `Update-DOTNExT.ps1` Modifications

**Change 1: VS Version Detection**

Current code (VS2022 only):
```powershell
$editions = @("Enterprise", "Professional", "Community")
foreach ($edition in $editions) {
    $path = "${env:ProgramFiles}\Microsoft Visual Studio\2022\$edition"
    if (Test-Path $path) {
        $script:VSInstallDir = $path
        break
    }
}
```

Proposed change (VS2026 preferred, VS2022 fallback):
```powershell
$editions = @("Enterprise", "Professional", "Community")
$versions = @("2026", "2022")  # Prefer 2026

foreach ($ver in $versions) {
    foreach ($edition in $editions) {
        $path = "${env:ProgramFiles}\Microsoft Visual Studio\$ver\$edition"
        if (Test-Path $path) {
            $script:VSInstallDir = $path
            $script:VSVersion = $ver
            Write-Info "Found VS $ver $edition"
            break
        }
    }
    if ($script:VSInstallDir) { break }
}
```

**Change 2: Add Version Parameter**

```powershell
param(
    # ... existing params ...
    [ValidateSet("2022", "2026", "Auto")]
    [string]$VSVersion = "Auto"
)
```

### `vsdotnext.cmd` Modifications

Current:
```cmd
start "" "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe" /rootSuffix RoslynDev %*
```

Should be generated dynamically based on detected VS version.

---

## Testing Plan

### Phase 1: VS2026 Installation Verification

- [ ] Install VS2026 (any edition)
- [ ] Verify paths exist:
  - [ ] `C:\Program Files\Microsoft Visual Studio\2026\{Edition}\Common7\Tools\VsDevCmd.bat`
  - [ ] `C:\Program Files\Microsoft Visual Studio\2026\{Edition}\Common7\IDE\VSIXInstaller.exe`
  - [ ] `C:\Program Files\Microsoft Visual Studio\2026\{Edition}\Common7\IDE\devenv.exe`

### Phase 2: Manual Build Test (No Script Changes)

1. [ ] Open VS2026 Developer Command Prompt
2. [ ] Navigate to `D:\Dev\DOTNExT\src\runtime`
3. [ ] Run: `build.cmd -subset clr+libs -c Release`
4. [ ] Note any errors or warnings

### Phase 3: Manual Roslyn Build Test

1. [ ] Open VS2026 Developer Command Prompt
2. [ ] Navigate to `D:\Dev\DOTNExT\src\roslyn`
3. [ ] Run: `Build.cmd -restore -build -c Release -deployExtensions`
4. [ ] Verify VSIX files created in `artifacts\VSSetup\Release\`

### Phase 4: VSIX Deployment Test

1. [ ] Run manually:
   ```cmd
   "C:\Program Files\Microsoft Visual Studio\2026\{Edition}\Common7\IDE\VSIXInstaller.exe" /quiet /rootSuffix:RoslynDev "path\to\Roslyn.Compilers.Extension.vsix"
   ```
2. [ ] Check for errors
3. [ ] Repeat for `Roslyn.VisualStudio.Setup.vsix`

### Phase 5: Experimental Instance Launch Test

1. [ ] Set environment variables:
   ```cmd
   SET DOTNET_ROOT=D:\Dev\DOTNExT\src\sdk\artifacts\bin\redist\Release\dotnet
   SET CORE_ROOT=D:\Dev\DOTNExT\src\runtime\artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root
   ```
2. [ ] Launch:
   ```cmd
   "C:\Program Files\Microsoft Visual Studio\2026\{Edition}\Common7\IDE\devenv.exe" /rootSuffix RoslynDev
   ```
3. [ ] Verify VS2026 launches with RoslynDev hive
4. [ ] Open a C# project and verify custom Roslyn is active

### Phase 6: Full Automated Test

1. [ ] Create branch: `dotnext/vs2026-workflow-test`
2. [ ] Modify `Update-DOTNExT.ps1` with VS2026 detection
3. [ ] Run: `.\Update-DOTNExT.ps1 -VSVersion 2026`
4. [ ] Run: `.\vsdotnext.cmd`
5. [ ] Verify everything works end-to-end

---

## Known Risks and Mitigations

### Risk 1: RoslynDev Hive Name Change
**Possibility:** VS2026 might use a different experimental hive naming scheme.
**Mitigation:** Test manually first. Check VS2026 extension development docs.
**Fallback:** Use standard `Exp` suffix if RoslynDev doesn't work.

### Risk 2: VSIX Version Incompatibility
**Possibility:** VS2026 might reject VS2022-era VSIX.
**Mitigation:** The manifest range `[17.0,19.0)` should cover VS2026 (18.x).
**Fallback:** Update Roslyn's `source.extension.vsixmanifest` to include VS2026.

### Risk 3: VsDevCmd.bat Differences
**Possibility:** Build environment setup might differ.
**Mitigation:** Test build manually before automating.
**Fallback:** Use explicit environment variable setup instead of VsDevCmd.bat.

### Risk 4: Path Changes
**Possibility:** VS2026 might have different internal paths.
**Mitigation:** All known paths follow same pattern as VS2022.
**Verification:** Manual path check in Phase 1.

---

## Success Criteria

The migration is successful when:

1. ✅ Runtime builds with VS2026 build tools
2. ✅ Roslyn builds and produces VSIX with VS2026 build tools
3. ✅ VSIX deploys to VS2026 RoslynDev experimental instance
4. ✅ VS2026 launches with custom runtime and compiler
5. ✅ C# IntelliSense and compilation use custom Roslyn
6. ✅ `Update-DOTNExT.ps1` works with `-VSVersion 2026`
7. ✅ `vsdotnext.cmd` launches VS2026 correctly

---

## Next Steps After Successful Testing

1. **Update Scripts:**
   - Modify `Update-DOTNExT.ps1` to prefer VS2026
   - Add `-VSVersion` parameter for explicit control
   - Update `vsdotnext.cmd` generation

2. **Document Changes:**
   - Update CLAUDE.md with VS2026 workflow
   - Note any behavior differences

3. **Plan .NET 10 Upgrade:**
   - Once VS2026 workflow is proven, consider upgrading DOTNExT from .NET 9 to .NET 10
   - This unlocks runtime-async feature

---

## References

- [Visual Studio 2026 Release Notes](https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes)
- [VS2026 is here (DevBlog)](https://devblogs.microsoft.com/visualstudio/visual-studio-2026-is-here-faster-smarter-and-a-hit-with-early-adopters/)
- [Port, Migrate, and Upgrade Projects](https://learn.microsoft.com/en-us/visualstudio/releases/2026/port-migrate-and-upgrade-visual-studio-projects)
- [The Experimental Instance](https://learn.microsoft.com/en-us/visualstudio/extensibility/the-experimental-instance)
- [Roslyn Building Guide](https://github.com/dotnet/roslyn/blob/main/docs/contributing/Building,%20Debugging,%20and%20Testing%20on%20Windows.md)
- [Modernizing VS Extension Compatibility](https://devblogs.microsoft.com/visualstudio/modernizing-visual-studio-extension-compatibility-effortless-migration-for-extension-developers-and-users/)

---

*Document created by Claude during runtime-async research session. Testing required to validate assumptions.*
