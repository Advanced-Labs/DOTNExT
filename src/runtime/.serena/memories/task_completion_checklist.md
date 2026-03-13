# Task Completion Checklist

When completing a task in the .NET Runtime repository, ensure the following:

## Before Submitting Changes

### Code Quality
- [ ] Code follows the project's coding style (see `code_style_conventions.md`)
- [ ] File header is present: `// Licensed to the .NET Foundation...`
- [ ] No trailing whitespace
- [ ] Final newline at end of file

### Building
- [ ] Build completes without errors:
  ```cmd
  build.cmd -subset <affected_subset> -c Release
  ```
- [ ] For CLR changes: `build.cmd -subset clr -c Release`
- [ ] For library changes: `build.cmd -subset libs -c Release`

### Testing
- [ ] Existing tests pass:
  ```cmd
  build.cmd -test -subset <affected_subset>
  ```
- [ ] New tests added for new functionality
- [ ] Bug fixes include regression tests

### Documentation
- [ ] Public APIs have XML documentation comments
- [ ] Complex code has inline comments explaining why (not what)

## Git Commit Guidelines

- [ ] Commit message follows format:
  - Summary line ≤50 characters
  - Blank line after summary
  - Body wrapped at 72 characters
  - Reference issue if applicable: `Fix #123`

- [ ] Commits are logically organized (not too large, not too small)

## For API Changes

- [ ] API proposal issue filed and approved
- [ ] Breaking changes discussed and approved
- [ ] API follows .NET design guidelines

## Common Issues to Check

1. **NullReferenceException risks**: Use null checks or null-conditional operators
2. **Resource leaks**: Ensure IDisposable resources are properly disposed
3. **Thread safety**: Consider concurrent access if applicable
4. **Performance**: Avoid unnecessary allocations in hot paths
5. **Security**: No hardcoded secrets, validate untrusted input

## Subset-Specific Notes

### CoreCLR Changes
- May require rebuilding test host: `src\tests\build.cmd generatelayoutonly`
- Test with corerun for quick validation

### Library Changes
- Consider impact on all supported platforms
- Check for nullable reference type warnings

### Mono Changes
- Test on relevant platforms (WASM, mobile if applicable)
