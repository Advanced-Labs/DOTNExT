# S2 Fixture Pack

This folder contains direct-upgrade conformance fixtures for S2 vectors:

1. TV-004
2. TV-014
3. TV-201
4. TV-202
5. TV-203
6. TV-204
7. TV-205
8. TV-206

Schema basis:

1. `M0-B-Conformance-Harness-Checklist.md` section 4
2. `M0-B-Protocol-Test-Vectors.md` section 3

S2 fixture contract notes:

1. every S2 vector uses `slice_profile: "S2"`
2. `RouteUpgradeProbe` body must include:
   - `policy_allowed`
   - `disclosure_allowed`
   - `grant_status` (`active|missing|expired|not_required`)
   - `trust_sufficient`
3. `expected_error_ids` remains the preferred fail-vector oracle.
