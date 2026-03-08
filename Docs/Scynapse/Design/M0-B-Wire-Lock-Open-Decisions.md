# M0-B Wire-Lock Decisions

## 1. Purpose

Record wire-level decision status and locked outcomes for M0-B.

Last lock pass:

1. 2026-03-08 (S1 baseline lock)

---

## 2. Decision Register

| Decision | Status | Scope | Outcome |
|---|---|---|---|
| `D1` enum encoding strategy | `LOCKED` | S1 | unsigned integer enum codes on wire; canonical text in debug/tooling |
| `D2` timestamp representation | `LOCKED` | S1 | Unix epoch milliseconds (`int64`, UTC) on wire |
| `D3` identifier encoding | `DEFERRED` | S2+ | keep typed-string recommendation pending |
| `D4` proof reference encoding | `LOCKED` | S1 | compact binary digest references on wire |
| `D5` `expr_norm` versioning | `DEFERRED` | S2+ | keep normalization-version lock pending |
| `D6` body key dictionary stability | `LOCKED` | S1 | dictionary `v1` frozen for S1 field set; reserved growth ranges |
| `D7` deny envelope field policy | `DEFERRED` | S2+ | conditional `policy_ref` retained |
| `D8` relation token serialization boundary | `DEFERRED` | S2+ | keep reference + signed blob hash path pending |

---

## 3. Locked Decisions (S1)

### 3.1 D1 Enum Encoding Strategy (`LOCKED`)

1. Wire encoding uses compact unsigned integer codes.
2. Human-readable/debug rendering uses canonical text labels.
3. Unknown enum code on decode is a protocol/schema failure (deny response is not substituted silently).

S1 enum codebooks:

1. `intent`
   - `0=resolve`
   - `1=invoke`
   - `2=observe`
   - `3=policy`
2. `operation_class`
   - `0=meta`
   - `1=value`
   - `2=endpoint`
   - `3=invoke`
   - `4=observe`
3. `route_mode`
   - `0=parent_mediated`
   - `1=relay_mediated`
   - `2=anonymous_relay`
   - `3=direct_upgraded`
4. `disclosure_level`
   - `0=hidden`
   - `1=mediator_visible`
   - `2=mutual_visible`
5. `deny_code`
   - `1=PathNotFound`
   - `2=PolicyDenied`
   - `3=DisclosureDenied`
   - `4=TrustInsufficient`
   - `5=UpgradeRejected`
   - `6=MediatorUnavailable`
   - `7=GrantMissing`
   - `8=GrantExpired`
   - `9=ReplayWindowExpired`
   - `10=AmbiguousResolution`

### 3.2 D2 Timestamp Representation (`LOCKED`)

1. All wire temporal fields use Unix epoch milliseconds (`int64`, UTC).
2. Applies to fields such as:
   - `timestamp`
   - `issued_at`
   - `expires_at`
   - `referral_expiry`
   - `grant_expiry`
3. RFC3339 text remains debug/tooling-only rendering.

### 3.3 D4 Proof Reference Encoding (`LOCKED`)

1. Wire proof references use compact digest tuples: `[alg_code:uint, digest:bstr]`.
2. `capability_refs` and `attestation_refs` are arrays of digest tuples.
3. `bearer_proof` remains opaque proof payload (`bstr`) when present.
4. S1 algorithm codes:
   - `1=sha256`
   - `2=sha384`
   - `3=sha512`
5. Debug rendering uses canonical text (`sha256:<hex>`, etc).

### 3.4 D6 Body Key Dictionary Stability (`LOCKED`)

1. Dictionary `v1` is frozen now for S1 field coverage.
2. Reserved growth ranges are mandatory:
   - `1-31` envelope/common
   - `32-63` resolve family
   - `64-95` handshake family
   - `96-127` route/upgrade family
   - `128-159` observe family
   - `160-191` policy/grant family
3. Any new fields outside S1 scope must use unassigned slots in family range or require dictionary version bump.

Canonical per-field key assignments for `v1` live in:

1. `Docs/Scynapse/Design/M0-B-Message-Field-Matrix.md`

---

## 4. Deferred Decisions (S2+)

1. `D3`: identifier typed-string hardening
2. `D5`: normalization version field (`expr_norm_v`) lock
3. `D7`: conditional/always `policy_ref` final policy
4. `D8`: relation token embed vs reference boundary optimization

---

## 5. Follow-Through

Locked decisions must stay synchronized in:

1. `M0-B-Protocol-Skeleton.md`
2. `M0-B-Message-Field-Matrix.md`
3. `M0-B-Wire-Examples.md`

