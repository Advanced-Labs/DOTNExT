# S4 Fixture Pack

This folder contains observation/replay conformance fixtures for S4 vectors:

1. TV-007
2. TV-008
3. TV-009
4. TV-010

Schema basis:

1. `M0-B-Conformance-Harness-Checklist.md` section 4
2. `M0-B-Protocol-Test-Vectors.md` section 3

S4 fixture contract notes:

1. every S4 vector uses `slice_profile: "S4"`
2. `ObserveOpen` uses `scope_mode` and optional `follow_moves`
3. `ObserveGap` uses `cause`
4. `ObserveResume` uses `replay_available`
