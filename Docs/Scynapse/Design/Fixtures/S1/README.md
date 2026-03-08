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
9. TV-104
10. TV-105
11. TV-106
12. TV-107
13. TV-108
14. TV-109

Schema basis:

1. `M0-B-Conformance-Harness-Checklist.md` section 4
2. `M0-B-Protocol-Test-Vectors.md` section 3

These fixtures are draft inputs for harness implementation and may be refined when message schemas are wire-locked.

Conformance expectation mode:

1. `expect_conformance: "pass"` (default) means harness must accept the vector.
2. `expect_conformance: "fail"` means harness must reject the vector; rejection is counted as pass for that vector.
3. `expected_error_ids` (optional, preferred) lists exact machine-checkable error IDs that must appear for `expect_conformance: "fail"` vectors.
4. `expected_error_contains` (optional, compatibility fallback) lists substrings expected in human-readable error text.
