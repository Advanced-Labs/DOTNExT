# M0-B Wire Examples (Draft)

## 1. Purpose

Provide minimal wire examples for M0-B in two views:

1. JSON debug view (human-readable)
2. CBOR diagnostic view (wire-oriented, canonical intent)

These examples are design aids, not final golden vectors.

---

## 2. Scope and Assumptions

1. Canonical wire format remains CBOR.
2. JSON debug rendering is non-authoritative.
3. Examples use a compact integer-key map profile to illustrate canonical wire friendliness.
4. Key dictionary in this document is `proposed` for M0-B, pending wire lock.

---

## 3. Proposed Compact Key Dictionary (v0 examples)

### 3.1 Envelope Keys

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

### 3.2 Nested Map Keys

`from`:

1. `1` -> `node_id`
2. `2` -> `name_anchor`

`proofs`:

1. `1` -> `capability_refs`
2. `2` -> `bearer_proof`
3. `3` -> `attestation_refs`

### 3.3 Body Keys by Message

`ResolveRequest.body`:

1. `1` -> `expr_raw`
2. `2` -> `expr_norm`
3. `3` -> `operation_class`
4. `4` -> `preferred_route_mode`
5. `5` -> `selector_hints`
6. `6` -> `cursor_or_revision`

`ResolveDeny.body`:

1. `1` -> `deny_code`
2. `2` -> `reason`
3. `3` -> `retryable`
4. `4` -> `remediation`
5. `5` -> `policy_ref`

`HandshakeAccept.body`:

1. `1` -> `relation_token_ref`
2. `2` -> `route_mode`
3. `3` -> `disclosure_level`
4. `4` -> `expires_at`
5. `5` -> `fallback_route_ref`

`RouteUpgradeProbe.body`:

1. `1` -> `upgrade_target_mode`
2. `2` -> `endpoint_disclosure_grant_ref`
3. `3` -> `consent_proof`
4. `4` -> `fallback_route_ref`

`ObserveOpen.body`:

1. `1` -> `scope`
2. `2` -> `subscription_mode`
3. `3` -> `profile`
4. `4` -> `follow_moves`
5. `5` -> `filters`
6. `6` -> `replay_cursor`

`ObserveEvent.body`:

1. `1` -> `subscription_id`
2. `2` -> `event_id`
3. `3` -> `revision`
4. `4` -> `event_type`
5. `5` -> `delivery_class`
6. `6` -> `payload_ref_or_inline`

`ObserveGap.body`:

1. `1` -> `subscription_id`
2. `2` -> `missing_revision_range`
3. `3` -> `cause`
4. `4` -> `recovery_hints`

---

## 4. Canonical CBOR Rules for Examples

1. Use definite-length maps and arrays.
2. Use shortest integer encoding for keys and integer values.
3. Keep key ordering canonical for the chosen CBOR deterministic profile.
4. Use UTF-8 text for identifiers and symbolic enums.
5. Treat these examples as semantic references, not byte-golden fixtures.

---

## 5. Message Examples

### 5.1 ResolveRequest (metadata resolve)

JSON debug:

```json
{
  "msg_type": "ResolveRequest",
  "msg_id": "msg-0001",
  "trace_id": "tr-1001",
  "timestamp": "2026-03-08T10:00:00Z",
  "from": {
    "node_id": "N1PUB",
    "name_anchor": ".Users.Alice"
  },
  "intent": "resolve",
  "target_scope": ".Adult.Games.RedX",
  "ttl_ms": 30000,
  "body": {
    "expr_raw": ".Adult.Games.RedX",
    "operation_class": "meta",
    "preferred_route_mode": "parent_mediated"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "ResolveRequest",
  2: "msg-0001",
  3: "tr-1001",
  4: "2026-03-08T10:00:00Z",
  5: {1: "N1PUB", 2: ".Users.Alice"},
  6: "resolve",
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    1: ".Adult.Games.RedX",
    3: "meta",
    4: "parent_mediated"
  }
}
```

### 5.2 ResolveDeny (ambiguous resolution)

JSON debug:

```json
{
  "msg_type": "ResolveDeny",
  "msg_id": "msg-0002",
  "trace_id": "tr-1001",
  "timestamp": "2026-03-08T10:00:01Z",
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
  4: "2026-03-08T10:00:01Z",
  5: {1: "PARENTPUB", 2: ".Adult"},
  6: "resolve",
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    1: "AmbiguousResolution",
    2: "Multiple candidate bindings; selector required",
    3: true,
    4: "Provide selector_hints"
  }
}
```

### 5.3 HandshakeAccept (parent-mediated relation)

JSON debug:

```json
{
  "msg_type": "HandshakeAccept",
  "msg_id": "msg-0010",
  "trace_id": "tr-2001",
  "timestamp": "2026-03-08T10:02:00Z",
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
    "relation_token_ref": "tok-rel-777-v1",
    "route_mode": "parent_mediated",
    "disclosure_level": "mediator_visible",
    "expires_at": "2026-03-08T10:17:00Z"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "HandshakeAccept",
  2: "msg-0010",
  3: "tr-2001",
  4: "2026-03-08T10:02:00Z",
  5: {1: "PARENTPUB", 2: ".Adult"},
  6: "invoke",
  7: ".Adult.Games.RedX",
  8: "rel-777",
  9: "parent_mediated",
  10: "mediator_visible",
  12: 30000,
  13: {
    1: "tok-rel-777-v1",
    2: "parent_mediated",
    3: "mediator_visible",
    4: "2026-03-08T10:17:00Z"
  }
}
```

### 5.4 RouteUpgradeProbe (encrypted endpoint grant provided)

JSON debug:

```json
{
  "msg_type": "RouteUpgradeProbe",
  "msg_id": "msg-0011",
  "trace_id": "tr-2001",
  "timestamp": "2026-03-08T10:03:00Z",
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
    "capability_refs": ["ccap-resolve-endpoint-1"],
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
  4: "2026-03-08T10:03:00Z",
  5: {1: "N1PUB", 2: ".Users.Alice"},
  6: "invoke",
  7: ".Adult.Games.RedX",
  8: "rel-777",
  9: "parent_mediated",
  10: "mediator_visible",
  11: {1: ["ccap-resolve-endpoint-1"], 2: "sig-bearer-1"},
  12: 30000,
  13: {
    1: "direct_upgraded",
    2: "grant-ep-123",
    3: "consent-n1-n2-v1",
    4: "route-parent-relay-7"
  }
}
```

### 5.5 ObserveOpen and ObserveEvent

JSON debug (`ObserveOpen`):

```json
{
  "msg_type": "ObserveOpen",
  "msg_id": "msg-0100",
  "trace_id": "tr-3001",
  "timestamp": "2026-03-08T10:05:00Z",
  "from": {
    "node_id": "N1PUB",
    "name_anchor": ".Users.Alice"
  },
  "intent": "observe",
  "target_scope": ".Adult.Games.RedX",
  "ttl_ms": 30000,
  "body": {
    "scope": ".Adult.Games.RedX.>",
    "subscription_mode": "OnChange",
    "profile": "Standard",
    "follow_moves": true
  }
}
```

CBOR diagnostic (`ObserveOpen`):

```cbor-diag
{
  1: "ObserveOpen",
  2: "msg-0100",
  3: "tr-3001",
  4: "2026-03-08T10:05:00Z",
  5: {1: "N1PUB", 2: ".Users.Alice"},
  6: "observe",
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    1: ".Adult.Games.RedX.>",
    2: "OnChange",
    3: "Standard",
    4: true
  }
}
```

JSON debug (`ObserveEvent`):

```json
{
  "msg_type": "ObserveEvent",
  "msg_id": "msg-0101",
  "trace_id": "tr-3001",
  "timestamp": "2026-03-08T10:05:03Z",
  "from": {
    "node_id": "PARENTPUB",
    "name_anchor": ".Adult"
  },
  "intent": "observe",
  "target_scope": ".Adult.Games.RedX.>",
  "relation_id": "rel-obs-22",
  "route_mode": "parent_mediated",
  "disclosure_level": "hidden",
  "ttl_ms": 30000,
  "body": {
    "subscription_id": "sub-22",
    "event_id": "evt-9001",
    "revision": 441,
    "event_type": "NameMoved",
    "delivery_class": "meta",
    "payload_ref_or_inline": "hash:blake2b:abcd1234"
  }
}
```

CBOR diagnostic (`ObserveEvent`):

```cbor-diag
{
  1: "ObserveEvent",
  2: "msg-0101",
  3: "tr-3001",
  4: "2026-03-08T10:05:03Z",
  5: {1: "PARENTPUB", 2: ".Adult"},
  6: "observe",
  7: ".Adult.Games.RedX.>",
  8: "rel-obs-22",
  9: "parent_mediated",
  10: "hidden",
  12: 30000,
  13: {
    1: "sub-22",
    2: "evt-9001",
    3: 441,
    4: "NameMoved",
    5: "meta",
    6: "hash:blake2b:abcd1234"
  }
}
```

### 5.6 ObserveGap (replay window expired)

JSON debug:

```json
{
  "msg_type": "ObserveGap",
  "msg_id": "msg-0102",
  "trace_id": "tr-3001",
  "timestamp": "2026-03-08T10:06:00Z",
  "from": {
    "node_id": "PARENTPUB",
    "name_anchor": ".Adult"
  },
  "intent": "observe",
  "target_scope": ".Adult.Games.RedX.>",
  "relation_id": "rel-obs-22",
  "route_mode": "parent_mediated",
  "disclosure_level": "hidden",
  "ttl_ms": 30000,
  "body": {
    "subscription_id": "sub-22",
    "missing_revision_range": "390-440",
    "cause": "ReplayWindowExpired",
    "recovery_hints": "Re-open from head cursor"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "ObserveGap",
  2: "msg-0102",
  3: "tr-3001",
  4: "2026-03-08T10:06:00Z",
  5: {1: "PARENTPUB", 2: ".Adult"},
  6: "observe",
  7: ".Adult.Games.RedX.>",
  8: "rel-obs-22",
  9: "parent_mediated",
  10: "hidden",
  12: 30000,
  13: {
    1: "sub-22",
    2: "390-440",
    3: "ReplayWindowExpired",
    4: "Re-open from head cursor"
  }
}
```

---

## 6. Usage Guidance

1. Use JSON examples for docs, tooling previews, and debugging.
2. Use CBOR diagnostic examples as semantic references for encoder/decoder tests.
3. Generate byte-level golden vectors only after key dictionary and field ordering are wire-locked.
4. Keep wire examples aligned with message field matrix and deterministic error/state specs.
