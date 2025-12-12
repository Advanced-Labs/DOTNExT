# REBOOT DOCUMENT - READ THIS FIRST AFTER CONTEXT RESET

## Mission
Reconstruct the current vision for a "Virtual Operating System" (VOS) platform built on a forked dotnet 9 runtime. The runtime evolves from "runtime" to "kernel" of this VOS.

## Terminology Quick Reference
- **DOTNExT**: Codename for the forked dotnet 9 runtime (not final name)
- **NewOrleans**: Fork of Microsoft Orleans for this platform
- **VCOM**: Component-Object Model to be built into/over platform kernel/runtime and VOS services
- **VARIA**: Types/objects built over VCOM, served by VOS (like ActiveX depended on COM/DCOM/COM+)
- **VAYRON**: ABANDONED - Early prototyping without runtime changes (can't achieve goals without runtime integration)
- **Async+**: UNCLEAR STATUS - May be replaced by runtime-integrated approach inspired by "Unwinder" work on "Async2"

## Current Status
STATUS: BLOCKED - MISSING KEY DOCUMENTS

### Work Completed
- [x] Initial document inventory
- [x] Read all available NewOrleans docs in repo
- [ ] Document timeline reconstruction
- [x] Vision extraction from AVAILABLE documents
- [ ] Deprecated content identification
- [ ] Current vision consolidation

### Current Phase
PHASE: BLOCKED - WAITING FOR USER TO PROVIDE MISSING FILES

### CRITICAL ISSUE
The following documents mentioned by the user are **NOT IN THE REPOSITORY**:
- `Docs/Async+/Async+.md`
- `Docs/New Orleans/New Orleans Features/OrleansAsync+.md`
- `Contexts/001 - 2025-12-04/Analysis/` (entire folder)

These documents likely contain the VOS/VCOM/VARIA vision that is NOT in the NewOrleans technical docs.

## Document Inventory
### Available in Repository (from git history):
1. `/src/NewOrleans/NewOrleans.md` (39KB) - Main NewOrleans documentation
2. `/docs/NewOrleans/References/PluginGrainArchitecture.md` - Added 2025-11-28
3. `/docs/NewOrleans/References/DynamicGrainAccess.md` - Added 2025-11-28
4. `/docs/NewOrleans/Researches/ClarificationsAboutDirectoriesAndArchitecture.md`
5. `/docs/NewOrleans/Old Orleans Orginal Internals/` - Reference docs for original Orleans

### User-Mentioned Paths (may need user to provide):
- `Docs/Async+/Async+.md` - NOT FOUND IN REPO
- `Docs/New Orleans/New Orleans.md` - May be same as src/NewOrleans/NewOrleans.md
- `Docs/New Orleans/New Orleans Features/OrleansAsync+.md` - NOT FOUND IN REPO
- `Contexts/001 - 2025-12-04/Analysis/` - NOT FOUND IN REPO

## Recovery Instructions
1. Read this file FIRST
2. Check `01-PROGRESS.md` for detailed progress
3. Check `02-FINDINGS.md` for extracted vision elements
4. Check `03-TIMELINE.md` for document evolution
5. Continue from where left off

## Key Questions Being Answered
1. What is the current vision for the VOS?
2. What components are still valid vs deprecated?
3. How do VCOM, VARIA, NewOrleans, and the runtime kernel fit together?
4. What execution model is planned (Async+ or Async2-inspired)?
