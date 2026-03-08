# S5 Fixture Pack

This folder contains policy inheritance hard-lock conformance fixtures for S5 vectors:

1. TV-011
2. TV-501
3. TV-502

Schema basis:

1. `M0-B-Conformance-Harness-Checklist.md` section 4
2. `M0-B-Protocol-Test-Vectors.md` section 3

S5 fixture contract notes:

1. every S5 vector uses `slice_profile: "S5"`
2. policy requests use `PolicyDelta` with:
   - `parent_hard_lock` (bool)
   - `child_weaken_attempt` (bool)
   - `override_granted` (bool)
3. hard-lock violations require `PolicyDeny` with deterministic `deny_code`.
