# Code Style and Conventions

## General

- **Indentation**: 4 spaces (no tabs)
- **Line endings**: CRLF for Windows files (.cmd, .bat), LF for shell scripts (.sh)
- **Encoding**: UTF-8 for project files
- **Final newline**: Required at end of files
- **Trailing whitespace**: Trim

## C# Style

### Naming Conventions
| Element | Style | Example |
|---------|-------|---------|
| Classes, Structs | PascalCase | `MyClass` |
| Interfaces | IPascalCase | `IDisposable` |
| Methods | PascalCase | `GetValue()` |
| Properties | PascalCase | `Value` |
| Public fields | PascalCase | `PublicField` |
| Private fields | _camelCase | `_privateField` |
| Static fields | s_camelCase | `s_staticField` |
| Constants | PascalCase | `MaxValue` |
| Parameters | camelCase | `paramName` |
| Local variables | camelCase | `localVar` |

### Braces
- **Allman style**: Opening brace on new line
```csharp
if (condition)
{
    DoSomething();
}
```

### Type Usage
- Use language keywords over BCL types: `int` not `Int32`, `string` not `String`
- Avoid `var` unless type is obvious from context
- Avoid `this.` qualification

### Modifiers Order
```csharp
public private protected internal file static extern new virtual abstract sealed override readonly unsafe required volatile async
```

### Using Directives
- Place outside namespace
- Sort System.* first
- Prefer braces for all control statements

### File Header (Required)
```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
```

## C++ Style

- **Braces**: Allman style (same as C#)
- **Indentation**: 4 spaces

## XML/Project Files

- **Indentation**: 2 spaces
- Files: .csproj, .vbproj, .vcxproj, .props, .targets, .xml, etc.

## Commit Messages

```
Summarize change in 50 characters or less

Provide more detail after the first line. Leave one blank line below the
summary and wrap all lines at 72 characters or less.

Fix #42
```

## Do's and Don'ts

### DO
- Follow existing style in the file you're changing
- Include tests for new features
- Keep discussions focused
- State clearly when taking an issue

### DON'T
- Make PRs for style-only changes
- Submit large PRs without prior discussion
- Add APIs without filing an issue first
- Commit code you didn't write without discussion
