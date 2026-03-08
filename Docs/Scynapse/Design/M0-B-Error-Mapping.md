# M0-B Deterministic Error Mapping (Draft)

## 1. Purpose

Define which deny/error codes are valid for each message family, plus retryability and remediation requirements.

This removes ambiguity in failure behavior and aligns with M0-B deterministic goals.

---

## 2. Canonical Deny/Error Codes

1. `PathNotFound`
2. `PolicyDenied`
3. `DisclosureDenied`
4. `TrustInsufficient`
5. `UpgradeRejected`
6. `MediatorUnavailable`
7. `GrantMissing`
8. `GrantExpired`
9. `ReplayWindowExpired`
10. `AmbiguousResolution`

---

## 3. Response Envelope for Deny/Error

All deny/error responses should include:

1. `code` (machine-stable)
2. `reason` (stable human-readable)
3. `retryable` (bool)
4. `retry_after_ms` (optional, required for throttling/transient guidance)
5. `remediation` (optional but recommended)
6. `policy_ref` (optional, required when policy is causal)
7. `trace_id` and `msg_id`

---

## 4. Message-to-Code Mapping

### 4.1 Resolve Family

| Message | Allowed Codes |
|---|---|
| `ResolveRequest` | `PathNotFound`, `PolicyDenied`, `DisclosureDenied`, `AmbiguousResolution`, `MediatorUnavailable`, `TrustInsufficient` |
| `ResolveReferral` follow-up | `PathNotFound`, `MediatorUnavailable`, `PolicyDenied` |

Defaults:

1. `AmbiguousResolution` is retryable only with explicit selector/disambiguation hints.
2. `PathNotFound` is non-retryable unless referral/cache TTL indicates possible convergence lag.

### 4.2 Handshake Family

| Message | Allowed Codes |
|---|---|
| `HandshakeInit/Proof` | `PolicyDenied`, `TrustInsufficient`, `GrantMissing`, `GrantExpired`, `DisclosureDenied`, `MediatorUnavailable` |
| `HandshakeChallenge` response failures | `TrustInsufficient`, `GrantMissing`, `GrantExpired` |

Defaults:

1. `TrustInsufficient` is non-retryable until trust material changes.
2. `GrantExpired` is retryable after grant renewal.

### 4.3 Route/Upgrade Family

| Message | Allowed Codes |
|---|---|
| `RouteEstablish` | `MediatorUnavailable`, `PolicyDenied`, `TrustInsufficient` |
| `RouteUpgradeProbe` | `UpgradeRejected`, `DisclosureDenied`, `GrantMissing`, `GrantExpired`, `TrustInsufficient`, `PolicyDenied` |
| `RouteKeepAlive` | `MediatorUnavailable`, `TrustInsufficient` |

Defaults:

1. `UpgradeRejected` keeps relayed path alive unless close is explicit.
2. `DisclosureDenied` is non-retryable without policy/consent change.

### 4.4 Observation Family

| Message | Allowed Codes |
|---|---|
| `ObserveOpen` | `PolicyDenied`, `GrantMissing`, `GrantExpired`, `DisclosureDenied`, `MediatorUnavailable` |
| `ObserveResume` | `ReplayWindowExpired`, `PolicyDenied`, `GrantExpired` |
| `ObserveEvent` delivery path failures | `MediatorUnavailable`, `TrustInsufficient` |

Defaults:

1. `ReplayWindowExpired` is retryable via full resubscribe from current head.
2. `PolicyDenied` on resume is non-retryable until policy changes.

### 4.5 Policy/Grant Family

| Message | Allowed Codes |
|---|---|
| `GrantPresent` request path | `PolicyDenied`, `TrustInsufficient`, `MediatorUnavailable` |
| `GrantRefused` | `PolicyDenied`, `TrustInsufficient`, `GrantExpired` (for renewal misuse cases) |
| `PolicySnapshot/Delta` access | `PolicyDenied`, `DisclosureDenied`, `MediatorUnavailable` |

---

## 5. Retryability Matrix (Default)

| Code | Retryable | Typical Condition |
|---|---|---|
| `PathNotFound` | No | Unless convergence/referral lag hinted |
| `PolicyDenied` | No | Until policy revision changes |
| `DisclosureDenied` | No | Until consent/disclosure policy changes |
| `TrustInsufficient` | No | Until trust/attestation state changes |
| `UpgradeRejected` | Yes | Continue relayed mode; may retry later |
| `MediatorUnavailable` | Yes | Backoff + alternate mediator/referral |
| `GrantMissing` | Yes | After obtaining required grant |
| `GrantExpired` | Yes | After grant renewal |
| `ReplayWindowExpired` | Yes | Re-open subscription from current head |
| `AmbiguousResolution` | Yes | With selector/disambiguation strategy |

---

## 6. Required Remediation Hints by Code

| Code | Required Remediation Hint |
|---|---|
| `PathNotFound` | nearest known authoritative scope/referral hint |
| `PolicyDenied` | policy ref and required action class |
| `DisclosureDenied` | required disclosure level and missing gate |
| `TrustInsufficient` | missing trust proof class |
| `UpgradeRejected` | active fallback route ref |
| `MediatorUnavailable` | retry-after and alternate mediator/referral if known |
| `GrantMissing` | required grant action + scope |
| `GrantExpired` | grant renewal endpoint/scope |
| `ReplayWindowExpired` | resubscribe strategy and earliest available cursor |
| `AmbiguousResolution` | candidate summary and selector strategy |

---

## 7. Implementation Rule

If an error code is not in the allowed set for that message type, emit:

1. `PolicyDenied` only if policy is truly causal, otherwise
2. `TrustInsufficient` for trust chain failures, otherwise
3. hard protocol violation (implementation bug) to diagnostics pipeline.
