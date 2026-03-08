# M0-B Wire Examples (S1 Lock Profile)

## 1. Purpose

Provide minimal wire examples for M0-B using the S1 locked wire profile:

1. JSON debug view (human-readable labels)
2. CBOR diagnostic view (wire-oriented, canonical intent)

These are semantic examples, not golden byte fixtures.

---

## 2. Locked Assumptions

1. Canonical wire format is deterministic CBOR.
2. Enums are compact unsigned integer codes on wire (`D1`).
3. Timestamps are Unix epoch milliseconds (`int64`, UTC) on wire (`D2`).
4. Proof refs are digest tuples (`[alg_code, digest_bstr]`) on wire (`D4`).
5. Key dictionary `v1` is frozen for S1 field set (`D6`).

Authority references:

1. `M0-B-Wire-Lock-Open-Decisions.md`
2. `M0-B-Message-Field-Matrix.md`

---

## 3. Locked Key Dictionary `v1` (S1)

Envelope/common keys:

| Key | Field |
|---|---|
| `1` | `msg_type` |
| `2` | `msg_id` |
| `3` | `trace_id` |
| `4` | `timestamp` |
| `5` | `from` |
| `6` | `intent` |
| `7` | `target_scope` |
| `8` | `relation_id` |
| `9` | `route_mode` |
| `10` | `disclosure_level` |
| `11` | `proofs` |
| `12` | `ttl_ms` |
| `13` | `body` |

Nested map keys:

1. `from`: `1=node_id`, `2=name_anchor`
2. `proofs`: `1=capability_refs`, `2=bearer_proof`, `3=attestation_refs`

Example S1 body keys used below:

1. resolve: `33=expr_raw`, `35=operation_class`, `49=deny_code`, `50=reason`, `51=retryable`, `52=remediation`
2. handshake: `75=relation_token`, `76=route_mode`, `77=disclosure_level`, `71=expires_at`
3. route/upgrade: `101=upgrade_target_mode`, `102=endpoint_disclosure_grant_ref`, `103=consent_proof`, `104=fallback_route_ref`

---

## 4. Enum and Digest Codebooks (S1)

Enum codes:

1. `intent`: `0=resolve`, `1=invoke`, `2=observe`, `3=policy`
2. `operation_class`: `0=meta`, `1=value`, `2=endpoint`, `3=invoke`, `4=observe`
3. `route_mode`: `0=parent_mediated`, `1=relay_mediated`, `2=anonymous_relay`, `3=direct_upgraded`
4. `disclosure_level`: `0=hidden`, `1=mediator_visible`, `2=mutual_visible`
5. `deny_code`: `1=PathNotFound`, `2=PolicyDenied`, `3=DisclosureDenied`, `4=TrustInsufficient`, `5=UpgradeRejected`, `6=MediatorUnavailable`, `7=GrantMissing`, `8=GrantExpired`, `9=ReplayWindowExpired`, `10=AmbiguousResolution`

Digest algorithm codes:

1. `1=sha256`
2. `2=sha384`
3. `3=sha512`

---

## 5. Canonical CBOR Rules Used Here

1. Definite-length maps/arrays.
2. Shortest integer encoding for keys and integers.
3. Canonical deterministic key ordering.
4. JSON view is for readability only and may show text aliases for enum meanings.

---

## 6. Example Messages

### 6.1 ResolveRequest (metadata resolve)

JSON debug:

```json
{
  "msg_type": "ResolveRequest",
  "msg_id": "msg-0001",
  "trace_id": "tr-1001",
  "timestamp": 1772964000000,
  "from": {
    "node_id": "N1PUB",
    "name_anchor": ".Users.Alice"
  },
  "intent": "resolve",
  "target_scope": ".Adult.Games.RedX",
  "ttl_ms": 30000,
  "body": {
    "expr_raw": ".Adult.Games.RedX",
    "operation_class": "meta"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "ResolveRequest",
  2: "msg-0001",
  3: "tr-1001",
  4: 1772964000000,
  5: {1: "N1PUB", 2: ".Users.Alice"},
  6: 0,
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    33: ".Adult.Games.RedX",
    35: 0
  }
}
```

### 6.2 ResolveDeny (ambiguous resolution)

JSON debug:

```json
{
  "msg_type": "ResolveDeny",
  "msg_id": "msg-0002",
  "trace_id": "tr-1001",
  "timestamp": 1772964001000,
  "from": {
    "node_id": "PARENTPUB",
    "name_anchor": ".Adult"
  },
  "intent": "resolve",
  "target_scope": ".Adult.Games.RedX",
  "ttl_ms": 30000,
  "body": {
    "deny_code": "AmbiguousResolution",
    "reason": "Multiple candidate bindings; selector required",
    "retryable": true,
    "remediation": "Provide selector_hints"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "ResolveDeny",
  2: "msg-0002",
  3: "tr-1001",
  4: 1772964001000,
  5: {1: "PARENTPUB", 2: ".Adult"},
  6: 0,
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    49: 10,
    50: "Multiple candidate bindings; selector required",
    51: true,
    52: "Provide selector_hints"
  }
}
```

### 6.3 HandshakeAccept (parent-mediated relation)

JSON debug:

```json
{
  "msg_type": "HandshakeAccept",
  "msg_id": "msg-0010",
  "trace_id": "tr-2001",
  "timestamp": 1772964120000,
  "from": {
    "node_id": "PARENTPUB",
    "name_anchor": ".Adult"
  },
  "intent": "invoke",
  "target_scope": ".Adult.Games.RedX",
  "relation_id": "rel-777",
  "route_mode": "parent_mediated",
  "disclosure_level": "mediator_visible",
  "ttl_ms": 30000,
  "body": {
    "relation_token": "tok-rel-777-v1",
    "route_mode": "parent_mediated",
    "disclosure_level": "mediator_visible",
    "expires_at": 1772965020000
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "HandshakeAccept",
  2: "msg-0010",
  3: "tr-2001",
  4: 1772964120000,
  5: {1: "PARENTPUB", 2: ".Adult"},
  6: 1,
  7: ".Adult.Games.RedX",
  8: "rel-777",
  9: 0,
  10: 1,
  12: 30000,
  13: {
    75: "tok-rel-777-v1",
    76: 0,
    77: 1,
    71: 1772965020000
  }
}
```

### 6.4 RouteUpgradeProbe (grant proof path)

JSON debug:

```json
{
  "msg_type": "RouteUpgradeProbe",
  "msg_id": "msg-0011",
  "trace_id": "tr-2001",
  "timestamp": 1772964180000,
  "from": {
    "node_id": "N1PUB",
    "name_anchor": ".Users.Alice"
  },
  "intent": "invoke",
  "target_scope": ".Adult.Games.RedX",
  "relation_id": "rel-777",
  "route_mode": "parent_mediated",
  "disclosure_level": "mediator_visible",
  "ttl_ms": 30000,
  "proofs": {
    "capability_refs": ["sha256:5d7c..."],
    "bearer_proof": "sig-bearer-1"
  },
  "body": {
    "upgrade_target_mode": "direct_upgraded",
    "endpoint_disclosure_grant_ref": "grant-ep-123",
    "consent_proof": "consent-n1-n2-v1",
    "fallback_route_ref": "route-parent-relay-7"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "RouteUpgradeProbe",
  2: "msg-0011",
  3: "tr-2001",
  4: 1772964180000,
  5: {1: "N1PUB", 2: ".Users.Alice"},
  6: 1,
  7: ".Adult.Games.RedX",
  8: "rel-777",
  9: 0,
  10: 1,
  11: {1: [[1, h'5D7C01']], 2: h'7369672D6265617265722D31'},
  12: 30000,
  13: {
    101: 3,
    102: "grant-ep-123",
    103: "consent-n1-n2-v1",
    104: "route-parent-relay-7"
  }
}
```

Observe-family wire examples are intentionally deferred to S2 when observe key assignments are locked.
