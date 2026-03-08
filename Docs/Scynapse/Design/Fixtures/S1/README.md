# S1 Fixture Pack

This folder contains first concrete harness fixtures for S1 vectors:

1. TV-001
2. TV-002
3. TV-003
4. TV-012
5. TV-013
6. TV-101
7. TV-102
8. TV-103

Schema basis:

1. `M0-B-Conformance-Harness-Checklist.md` section 4
2. `M0-B-Protocol-Test-Vectors.md` section 3

These fixtures are draft inputs for harness implementation and may be refined when message schemas are wire-locked.

Conformance expectation mode:

1. `expect_conformance: "pass"` (default) means harness must accept the vector.
2. `expect_conformance: "fail"` means harness must reject the vector; rejection is counted as pass for that vector.
3. `expected_error_contains` (optional) lists substrings that must appear in harness errors for `expect_conformance: "fail"` vectors.
