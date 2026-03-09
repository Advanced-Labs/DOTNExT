# M0-B Wire Examples (S1 + M1-S1/M1-S5/M1-S6/M1-S7/M1-S8/M1-S9/M1-S10 Lock Profiles)

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
6. M1-S1 wire closure locks:
   - typed identifiers for id/ref fields (`D3`)
   - `expr_norm` requires `expr_norm_v` (`D5`)
   - policy-causal denies require `policy_ref` (`D7`)
   - relation token transport boundary fields in `HandshakeAccept` (`D8`)
7. M1-S3 security-adapter lock profile:
   - `HandshakeProof.verification_mode` (`mock|strict`)
   - strict mode fields: `proof_ref`, optional `replay_probe`, optional `force_bad_signature`
   - mock mode fields: optional `mock_signature_valid`, optional `mock_replay_detected`
8. M1-S4 strict-failure mapping lock profile:
   - optional strict-mode field: `strict_failure_mode`
   - allowed values: `none|expired|revoked|unresolvable_proof|not_yet_valid`
9. M1-S5 relation-token integrity lock profile:
   - `HandshakeAccept` uses M1-S1 token-boundary fields
   - inline transport requires `relation_token_cid == sha256(relation_token_blob)`
10. M1-S6 reference-token guard lock profile:
   - `HandshakeAccept` reference transport requires `reference_lookup_status`
   - resolved lookup requires `reference_lookup_cid` and equality with `relation_token_cid`
11. M1-S7 reference-grant guard lock profile:
   - `HandshakeAccept` reference transport requires `reference_grant_status`
   - active grant requires typed `reference_grant_ref`
12. M1-S8 reference-grant proof-binding lock profile:
   - active reference grant requires `reference_grant_verification_mode` (`mock|strict`)
   - strict mode requires typed `reference_grant_proof_ref`
   - mock mode requires boolean `reference_grant_mock_valid`
   - strict mode optional `reference_grant_strict_failure_mode`
13. M1-S9 reference-grant freshness/replay lock profile:
   - active reference grant requires `reference_grant_proof_freshness_status` (`fresh|stale`)
   - active reference grant requires `reference_grant_proof_replay_status` (`clear|replayed`)
14. M1-S10 reference-grant claim-binding lock profile:
   - `HandshakeInit` requires claim-binding source fields: `requester_subject_ref`, `requested_scope`, `requested_ops`
   - active reference-grant `HandshakeAccept` requires claim fields: `reference_grant_claim_subject_ref`, `reference_grant_claim_scope`, `reference_grant_claim_action`

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

1. resolve: `33=expr_raw`, `34=expr_norm`, `53=expr_norm_v`, `35=operation_class`, `49=deny_code`, `50=reason`, `51=retryable`, `52=remediation`
2. handshake: `75=relation_token`, `76=route_mode`, `77=disclosure_level`, `71=expires_at`, `80=policy_ref`, `82=token_transport`, `83=relation_token_ref`, `84=relation_token_cid`, `85=relation_token_blob`, `93=reference_lookup_status`, `94=reference_lookup_cid`, `95=reference_grant_status`, `96=reference_grant_ref`
    - M1-S3/M1-S4 handshake proof extension: `86=verification_mode`, `87=proof_ref`, `88=replay_probe`, `89=force_bad_signature`, `90=mock_signature_valid`, `91=mock_replay_detected`, `92=strict_failure_mode`
    - M1-S8/M1-S9/M1-S10 handshake accept extension fields (dictionary keys reserved for next lock pass): `reference_grant_verification_mode`, `reference_grant_proof_ref`, `reference_grant_mock_valid`, `reference_grant_strict_failure_mode`, `reference_grant_proof_freshness_status`, `reference_grant_proof_replay_status`, `reference_grant_claim_subject_ref`, `reference_grant_claim_scope`, `reference_grant_claim_action`
    - M1-S10 handshake init extension fields (dictionary keys reserved for next lock pass): `requester_subject_ref`, `requested_scope`
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
  "msg_id": "mid:msg-0001",
  "trace_id": "trc:tr-1001",
  "timestamp": 1772964000000,
  "from": {
    "node_id": "nid:N1PUB",
    "name_anchor": ".Users.Alice"
  },
  "intent": "resolve",
  "target_scope": ".Adult.Games.RedX",
  "ttl_ms": 30000,
  "body": {
    "expr_raw": ".Adult.Games.RedX",
    "expr_norm": ".adult.games.redx",
    "expr_norm_v": 1,
    "operation_class": "meta"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "ResolveRequest",
  2: "mid:msg-0001",
  3: "trc:tr-1001",
  4: 1772964000000,
  5: {1: "nid:N1PUB", 2: ".Users.Alice"},
  6: 0,
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    33: ".Adult.Games.RedX",
    34: ".adult.games.redx",
    53: 1,
    35: 0
  }
}
```

### 6.2 ResolveDeny (ambiguous resolution)

JSON debug:

```json
{
  "msg_type": "ResolveDeny",
  "msg_id": "mid:msg-0002",
  "trace_id": "trc:tr-1001",
  "timestamp": 1772964001000,
  "from": {
    "node_id": "nid:PARENTPUB",
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
  2: "mid:msg-0002",
  3: "trc:tr-1001",
  4: 1772964001000,
  5: {1: "nid:PARENTPUB", 2: ".Adult"},
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
  "msg_id": "mid:msg-0010",
  "trace_id": "trc:tr-2001",
  "timestamp": 1772964120000,
  "from": {
    "node_id": "nid:PARENTPUB",
    "name_anchor": ".Adult"
  },
  "intent": "invoke",
  "target_scope": ".Adult.Games.RedX",
  "relation_id": "rid:rel-777",
  "route_mode": "parent_mediated",
  "disclosure_level": "mediator_visible",
  "ttl_ms": 30000,
  "body": {
    "route_mode": "parent_mediated",
    "disclosure_level": "mediator_visible",
    "token_transport": "reference",
    "relation_token_ref": "tid:rel-777.token",
    "relation_token_cid": "sha256:5d7c01ab99fe33cc",
    "expires_at": 1772965020000
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "HandshakeAccept",
  2: "mid:msg-0010",
  3: "trc:tr-2001",
  4: 1772964120000,
  5: {1: "nid:PARENTPUB", 2: ".Adult"},
  6: 1,
  7: ".Adult.Games.RedX",
  8: "rid:rel-777",
  9: 0,
  10: 1,
  12: 30000,
  13: {
    76: 0,
    77: 1,
    82: "reference",
    83: "tid:rel-777.token",
    84: "sha256:5d7c01ab99fe33cc",
    71: 1772965020000
  }
}
```

### 6.4 RouteUpgradeProbe (grant proof path)

JSON debug:

```json
{
  "msg_type": "RouteUpgradeProbe",
  "msg_id": "mid:msg-0011",
  "trace_id": "trc:tr-2001",
  "timestamp": 1772964180000,
  "from": {
    "node_id": "nid:N1PUB",
    "name_anchor": ".Users.Alice"
  },
  "intent": "invoke",
  "target_scope": ".Adult.Games.RedX",
  "relation_id": "rid:rel-777",
  "route_mode": "parent_mediated",
  "disclosure_level": "mediator_visible",
  "ttl_ms": 30000,
  "proofs": {
    "capability_refs": ["sha256:5d7c..."],
    "bearer_proof": "sig-bearer-1"
  },
  "body": {
    "upgrade_target_mode": "direct_upgraded",
    "endpoint_disclosure_grant_ref": "gid:grant-ep-123",
    "consent_proof": "consent-n1-n2-v1",
    "fallback_route_ref": "rte:parent-relay-7"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "RouteUpgradeProbe",
  2: "mid:msg-0011",
  3: "trc:tr-2001",
  4: 1772964180000,
  5: {1: "nid:N1PUB", 2: ".Users.Alice"},
  6: 1,
  7: ".Adult.Games.RedX",
  8: "rid:rel-777",
  9: 0,
  10: 1,
  11: {1: [[1, h'5D7C01']], 2: h'7369672D6265617265722D31'},
  12: 30000,
  13: {
    101: 3,
    102: "gid:grant-ep-123",
    103: "consent-n1-n2-v1",
    104: "rte:parent-relay-7"
  }
}
```

### 6.5 HandshakeDeny (policy-causal deny requires policy_ref)

JSON debug:

```json
{
  "msg_type": "HandshakeDeny",
  "msg_id": "mid:msg-0012",
  "trace_id": "trc:tr-2001",
  "timestamp": 1772964200000,
  "from": {
    "node_id": "nid:PARENTPUB",
    "name_anchor": ".Adult"
  },
  "intent": "invoke",
  "target_scope": ".Adult.Games.RedX",
  "ttl_ms": 30000,
  "body": {
    "deny_code": "PolicyDenied",
    "policy_ref": "pid:adult.policy.v5",
    "retryable": false
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "HandshakeDeny",
  2: "mid:msg-0012",
  3: "trc:tr-2001",
  4: 1772964200000,
  5: {1: "nid:PARENTPUB", 2: ".Adult"},
  6: 1,
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    79: 2,
    80: "pid:adult.policy.v5",
    81: false
  }
}
```

### 6.6 RouteData (runtime bridge transport marker)

JSON debug:

```json
{
  "msg_type": "RouteData",
  "msg_id": "mid:msg-0013",
  "trace_id": "trc:tr-2001",
  "timestamp": 1772964210000,
  "from": {
    "node_id": "nid:N1PUB",
    "name_anchor": ".Users.Alice"
  },
  "intent": "invoke",
  "target_scope": ".Adult.Games.RedX",
  "relation_id": "rid:rel-777",
  "route_mode": "parent_mediated",
  "ttl_ms": 30000,
  "body": {
    "route_mode": "parent_mediated",
    "transport_path": "mediated",
    "payload_class": "invoke"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "RouteData",
  2: "mid:msg-0013",
  3: "trc:tr-2001",
  4: 1772964210000,
  5: {1: "nid:N1PUB", 2: ".Users.Alice"},
  6: 1,
  7: ".Adult.Games.RedX",
  8: "rid:rel-777",
  9: 0,
  12: 30000,
  13: {
    76: 0,
    109: "mediated",
    110: "invoke"
  }
}
```

### 6.7 HandshakeProof (M1-S3 strict security-adapter mode)

JSON debug:

```json
{
  "msg_type": "HandshakeProof",
  "msg_id": "mid:msg-0014",
  "trace_id": "trc:tr-2001",
  "timestamp": 1772964220000,
  "from": {
    "node_id": "nid:N1PUB",
    "name_anchor": ".Users.Alice"
  },
  "intent": "invoke",
  "target_scope": ".Adult.Games.RedX",
  "ttl_ms": 30000,
  "body": {
    "verification_mode": "strict",
    "proof_ref": "nid:m1s3.strict.pass",
    "replay_probe": false,
    "force_bad_signature": false
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "HandshakeProof",
  2: "mid:msg-0014",
  3: "trc:tr-2001",
  4: 1772964220000,
  5: {1: "nid:N1PUB", 2: ".Users.Alice"},
  6: 1,
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    86: "strict",
    87: "nid:m1s3.strict.pass",
    88: false,
    89: false
  }
}
```

### 6.8 HandshakeProof (M1-S4 strict failure mapping: expired)

JSON debug:

```json
{
  "msg_type": "HandshakeProof",
  "msg_id": "mid:msg-0015",
  "trace_id": "trc:tr-2002",
  "timestamp": 1772964230000,
  "from": {
    "node_id": "nid:N1PUB",
    "name_anchor": ".Users.Alice"
  },
  "intent": "invoke",
  "target_scope": ".Adult.Games.RedX",
  "ttl_ms": 30000,
  "body": {
    "verification_mode": "strict",
    "proof_ref": "nid:m1s4.strict.expired",
    "strict_failure_mode": "expired"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "HandshakeProof",
  2: "mid:msg-0015",
  3: "trc:tr-2002",
  4: 1772964230000,
  5: {1: "nid:N1PUB", 2: ".Users.Alice"},
  6: 1,
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    86: "strict",
    87: "nid:m1s4.strict.expired",
    92: "expired"
  }
}
```

### 6.9 HandshakeAccept (M1-S5 inline token integrity pass)

JSON debug:

```json
{
  "msg_type": "HandshakeAccept",
  "msg_id": "mid:msg-0016",
  "trace_id": "trc:tr-2002",
  "timestamp": 1772964240000,
  "from": {
    "node_id": "nid:PARENTPUB",
    "name_anchor": ".Adult"
  },
  "intent": "invoke",
  "target_scope": ".Adult.Games.RedX",
  "ttl_ms": 30000,
  "body": {
    "route_mode": "parent_mediated",
    "disclosure_level": "mediator_visible",
    "token_transport": "inline",
    "relation_token_ref": "tid:reltok-1001",
    "relation_token_cid": "sha256:37b5f0dac1403b5e80b1b4946dce8347f76fcc0f1cd88062ac97994cb789a9eb",
    "relation_token_blob": "m1s5-inline-token-alpha"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "HandshakeAccept",
  2: "mid:msg-0016",
  3: "trc:tr-2002",
  4: 1772964240000,
  5: {1: "nid:PARENTPUB", 2: ".Adult"},
  6: 1,
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    76: 0,
    77: 1,
    82: "inline",
    83: "tid:reltok-1001",
    84: "sha256:37b5f0dac1403b5e80b1b4946dce8347f76fcc0f1cd88062ac97994cb789a9eb",
    85: "m1s5-inline-token-alpha"
  }
}
```

### 6.10 HandshakeAccept (M1-S6 reference lookup resolved pass)

JSON debug:

```json
{
  "msg_type": "HandshakeAccept",
  "msg_id": "mid:msg-0017",
  "trace_id": "trc:tr-2002",
  "timestamp": 1772964250000,
  "from": {
    "node_id": "nid:PARENTPUB",
    "name_anchor": ".Adult"
  },
  "intent": "invoke",
  "target_scope": ".Adult.Games.RedX",
  "ttl_ms": 30000,
  "body": {
    "route_mode": "parent_mediated",
    "disclosure_level": "mediator_visible",
    "token_transport": "reference",
    "relation_token_ref": "tid:reltok-1002",
    "relation_token_cid": "sha256:8f8c5967903fd0d4f90f27f00f3db498f1c57b18a2668ec998ca0f8f40d31bd9",
    "reference_lookup_status": "resolved",
    "reference_lookup_cid": "sha256:8f8c5967903fd0d4f90f27f00f3db498f1c57b18a2668ec998ca0f8f40d31bd9"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "HandshakeAccept",
  2: "mid:msg-0017",
  3: "trc:tr-2002",
  4: 1772964250000,
  5: {1: "nid:PARENTPUB", 2: ".Adult"},
  6: 1,
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    76: 0,
    77: 1,
    82: "reference",
    83: "tid:reltok-1002",
    84: "sha256:8f8c5967903fd0d4f90f27f00f3db498f1c57b18a2668ec998ca0f8f40d31bd9",
    93: "resolved",
    94: "sha256:8f8c5967903fd0d4f90f27f00f3db498f1c57b18a2668ec998ca0f8f40d31bd9"
  }
}
```

### 6.11 HandshakeAccept (M1-S7 reference grant active pass)

JSON debug:

```json
{
  "msg_type": "HandshakeAccept",
  "msg_id": "mid:msg-0018",
  "trace_id": "trc:tr-2003",
  "timestamp": 1772964260000,
  "from": {
    "node_id": "nid:PARENTPUB",
    "name_anchor": ".Adult"
  },
  "intent": "invoke",
  "target_scope": ".Adult.Games.RedX",
  "ttl_ms": 30000,
  "body": {
    "route_mode": "parent_mediated",
    "disclosure_level": "mediator_visible",
    "token_transport": "reference",
    "relation_token_ref": "tid:reltok-1003",
    "relation_token_cid": "sha256:60cc6ef532cb8f1836f545319a6ad0b4dd5228c16f30916495b8f92aa5e2f113",
    "reference_lookup_status": "resolved",
    "reference_lookup_cid": "sha256:60cc6ef532cb8f1836f545319a6ad0b4dd5228c16f30916495b8f92aa5e2f113",
    "reference_grant_status": "active",
    "reference_grant_ref": "gid:grant-2003"
  }
}
```

CBOR diagnostic:

```cbor-diag
{
  1: "HandshakeAccept",
  2: "mid:msg-0018",
  3: "trc:tr-2003",
  4: 1772964260000,
  5: {1: "nid:PARENTPUB", 2: ".Adult"},
  6: 1,
  7: ".Adult.Games.RedX",
  12: 30000,
  13: {
    76: 0,
    77: 1,
    82: "reference",
    83: "tid:reltok-1003",
    84: "sha256:60cc6ef532cb8f1836f545319a6ad0b4dd5228c16f30916495b8f92aa5e2f113",
    93: "resolved",
    94: "sha256:60cc6ef532cb8f1836f545319a6ad0b4dd5228c16f30916495b8f92aa5e2f113",
    95: "active",
    96: "gid:grant-2003"
  }
}
```

Observe-family wire examples are intentionally deferred to S2 when observe key assignments are locked.


