# S3 Fixture Pack

This folder contains encrypted endpoint disclosure grant conformance fixtures for S3 vectors:

1. TV-005
2. TV-006
3. TV-301
4. TV-302

Schema basis:

1. `M0-B-Conformance-Harness-Checklist.md` section 4
2. `M0-B-Protocol-Test-Vectors.md` section 3

S3 fixture contract notes:

1. every S3 vector uses `slice_profile: "S3"`
2. endpoint resolve requests (`operation_class=endpoint`) include:
   - `endpoint_directory_mode` (`plaintext|encrypted`)
   - `endpoint_grant_status` (`active|missing|expired|not_required`)
   - `endpoint_disclosure_allowed` (bool)
3. encrypted endpoint disclosure with active grant requires `GrantPresent` proof path before `ResolveResponse`.
