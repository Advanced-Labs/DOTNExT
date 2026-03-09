# M0-B Protocol Test Vectors (Draft)

## 1. Purpose

Provide executable-style test vectors for M0-B protocol behavior.

Each vector includes:

1. scenario id
2. preconditions
3. message sequence
4. expected state trace
5. expected outcome (success or deterministic deny code)

---

## 2. Conventions

1. Message names follow `M0-B-Protocol-Skeleton.md`.
2. States follow `M0-B-State-Transition-Matrix.md`.
3. Deny codes follow `M0-B-Error-Mapping.md`.
4. `N1`, `N2`, `P` denote requester node, target node, and parent mediator.

---

## 3. Test Vectors

### TV-001 Cold Resolve Miss, Full Walk, Success

Preconditions:

1. requester cache miss
2. no direct authority hint
3. path exists under reachable namespace

Sequence:

1. `ResolveRequest` (`expr_raw`, `operation_class=meta`)
2. one or more `ResolveReferral`
3. `ResolveResponse` with canonical scope and candidate bindings

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> PolicyEvaluate -> DisclosurePlan -> MediatedHandshake -> RelayedSession -> Completed`

Expected outcome:

1. success
2. no deny code

### TV-002 Cold Resolve Miss, Path Not Found

Preconditions:

1. requester cache miss
2. namespace walk completes with no authority for target

Sequence:

1. `ResolveRequest`
2. terminal `ResolveDeny`

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> Deny`

Expected outcome:

1. `PathNotFound`
2. `retryable=false` by default

### TV-003 Parent-Mediated Handshake, No Direct Upgrade Allowed

Preconditions:

1. parent policy enforces mediated route
2. direct disclosure disallowed

Sequence:

1. `HandshakeInit`
2. `HandshakeChallenge`
3. `HandshakeProof`
4. `HandshakeAccept` (`route_mode=parent_mediated`, `disclosure_level=hidden|mediator_visible`)

Expected state trace:

1. `MediatedHandshake -> RelayedSession -> Completed`

Expected outcome:

1. success in relayed mode
2. any `RouteUpgradeProbe` attempt must yield `UpgradeRejected` or `DisclosureDenied`

### TV-004 Relayed to Direct Upgrade Success

Preconditions:

1. relation already active in `RelayedSession`
2. policy allows direct upgrade
3. both peers consent and trust checks pass

Sequence:

1. `RouteUpgradeProbe`
2. `RouteUpgradeAccept`
3. operation message over direct route

Expected state trace:

1. `RelayedSession -> DirectUpgradeProbe -> DirectSession -> Completed`

Expected outcome:

1. success
2. fallback relayed route remains valid

### TV-005 Encrypted Endpoint Resolve with Valid Grant

Preconditions:

1. endpoint directory mode is encrypted
2. requester has valid `resolve.endpoint` grant

Sequence:

1. `ResolveRequest` (`operation_class=endpoint`)
2. `GrantPresent` proof path
3. `ResolveResponse` with permitted endpoint disclosure payload

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> PolicyEvaluate -> DisclosurePlan -> MediatedHandshake -> RelayedSession -> Completed`

Expected outcome:

1. success
2. disclosure level does not exceed grant scope

### TV-006 Encrypted Endpoint Resolve Denied (Missing Grant)

Preconditions:

1. endpoint directory mode is encrypted
2. requester lacks `resolve.endpoint` grant

Sequence:

1. `ResolveRequest` (`operation_class=endpoint`)
2. deny response

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> PolicyEvaluate -> DisclosurePlan -> Deny`

Expected outcome:

1. `GrantMissing`
2. remediation must include required action + scope

### TV-007 Observe Subtree with `follow_moves=true`

Preconditions:

1. subscription scope is subtree
2. default `follow_moves=true`

Sequence:

1. `ObserveOpen`
2. `ObserveAck`
3. rename/move occurs under subtree
4. `ObserveEvent` (`event_type=NameMoved`)
5. subsequent descendant events continue on same subscription

Expected state trace:

1. `ObserveIdle -> ObservePendingAck -> ObserveActive -> ObserveActive`

Expected outcome:

1. subscription follows moved descendants
2. no resubscribe required

### TV-008 Observe Exact Path with `follow_moves=false`

Preconditions:

1. subscription scope is exact path
2. default `follow_moves=false`

Sequence:

1. `ObserveOpen`
2. `ObserveAck`
3. exact target renamed
4. structural `ObserveEvent` is emitted
5. no further value/meta events for new path on same subscription

Expected state trace:

1. `ObserveIdle -> ObservePendingAck -> ObserveActive`

Expected outcome:

1. old exact path binding ends
2. caller must re-open for new path

### TV-009 Replay Resume Within Retention Window

Preconditions:

1. active subscription with cursor
2. temporary transport break
3. replay window retained

Sequence:

1. `ObserveResume` (cursor)
2. `ObserveAck` with resumed cursor
3. replayed `ObserveEvent` sequence

Expected state trace:

1. `ObserveActive -> ObserveResuming -> ObserveActive`

Expected outcome:

1. success
2. monotonic revisions preserved

### TV-010 Replay Resume Outside Retention Window

Preconditions:

1. subscription cursor older than retention window

Sequence:

1. `ObserveResume`
2. `ObserveGap` with cause

Expected state trace:

1. `ObserveActive -> ObserveGap`

Expected outcome:

1. `ReplayWindowExpired`
2. remediation points to full resubscribe from head

### TV-011 Parent Hard Policy Blocks Child Weakening

Preconditions:

1. ancestor policy hard-locks constraint
2. child attempts weaker route/disclosure policy
3. no delegated override grant

Sequence:

1. policy-affecting request (`PolicyDelta` or handshake/resolve under weakened claim)
2. deny response

Expected state trace:

1. `PolicyEvaluate -> Deny`

Expected outcome:

1. `PolicyDenied`
2. include policy revision in deny payload

### TV-012 Ambiguous Resolution (Fail-Closed)

Preconditions:

1. multiple candidate bindings match expression
2. caller provides no selector hints

Sequence:

1. `ResolveRequest`
2. `ResolveDeny`

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> Deny`

Expected outcome:

1. `AmbiguousResolution`
2. remediation includes selector strategy hints

### TV-013 Ambiguous Resolution Resolved with Selector

Preconditions:

1. same ambiguity as TV-012
2. caller supplies selector hints

Sequence:

1. `ResolveRequest` with selector hints
2. `ResolveResponse`

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> PolicyEvaluate -> DisclosurePlan -> MediatedHandshake -> RelayedSession -> Completed`

Expected outcome:

1. success

### TV-014 Direct Upgrade Rejected, Fallback Preserved

Preconditions:

1. relayed session active
2. upgrade gate fails (policy or trust or disclosure)

Sequence:

1. `RouteUpgradeProbe`
2. `RouteUpgradeReject`
3. operation continues over relayed path

Expected state trace:

1. `RelayedSession -> DirectUpgradeProbe -> RelayedSession -> Completed`

Expected outcome:

1. `UpgradeRejected` or specific gate code
2. relayed fallback continuity confirmed

### TV-601 D5 Normalization Version Pass

Preconditions:

1. resolve request includes `expr_norm`
2. `expr_norm_v=1`

Sequence:

1. `ResolveRequest` with `expr_raw`, `expr_norm`, `expr_norm_v`
2. terminal resolve decision

Expected outcome:

1. conformance pass
2. no normalization-version failure

### TV-602 D5 Normalization Version Missing

Preconditions:

1. resolve request includes `expr_norm`
2. `expr_norm_v` omitted

Sequence:

1. `ResolveRequest` with missing `expr_norm_v`

Expected outcome:

1. expected fail by exact error ID (`E2061_EXPR_NORM_VERSION_REQUIRED`)

### TV-603 D5 Normalization Version Unsupported

Preconditions:

1. resolve request includes `expr_norm`
2. `expr_norm_v` outside supported set

Sequence:

1. `ResolveRequest` with unsupported `expr_norm_v`

Expected outcome:

1. expected fail by exact error ID (`E2063_EXPR_NORM_VERSION_UNSUPPORTED`)

### TV-604 D7 Policy-Causal Deny Missing Policy Reference

Preconditions:

1. deny code is policy-causal (`PolicyDenied`)
2. `policy_ref` omitted

Sequence:

1. handshake deny path emits policy-causal code without `policy_ref`

Expected outcome:

1. expected fail by exact error ID (`E2065_POLICY_REF_REQUIRED_FOR_DENY`)

### TV-605 D7 Policy-Causal Deny with Policy Reference

Preconditions:

1. deny code is policy-causal (`PolicyDenied`)
2. `policy_ref` present

Sequence:

1. handshake deny path with typed `policy_ref`

Expected outcome:

1. conformance pass

### TV-606 D8 Relation Token Reference Transport Pass

Preconditions:

1. handshake accept path active
2. token transport mode is `reference`

Sequence:

1. `HandshakeAccept` with `token_transport`, `relation_token_ref`, `relation_token_cid`

Expected outcome:

1. conformance pass

### TV-607 D8 Relation Token Inline Transport Pass

Preconditions:

1. handshake accept path active
2. token transport mode is `inline`

Sequence:

1. `HandshakeAccept` with boundary fields + `relation_token_blob`

Expected outcome:

1. conformance pass

### TV-608 D8 Reference Transport with Inline Blob (Invalid)

Preconditions:

1. token transport mode is `reference`
2. inline token blob is present

Sequence:

1. `HandshakeAccept` includes forbidden blob field

Expected outcome:

1. expected fail by exact error ID (`E2073_TOKEN_BLOB_FORBIDDEN_REFERENCE`)

### TV-609 D3 Typed Identifier Fail

Preconditions:

1. policy-causal deny carries `policy_ref`
2. `policy_ref` is not typed identifier format

Sequence:

1. handshake deny with invalid `policy_ref` value

Expected outcome:

1. expected fail by exact error ID (`E2080_TYPED_IDENTIFIER_INVALID`)

### TV-610 D8 Missing Token CID

Preconditions:

1. handshake accept path active
2. `relation_token_cid` missing

Sequence:

1. `HandshakeAccept` missing required token CID

Expected outcome:

1. expected fail by exact error ID (`E2070_TOKEN_CID_REQUIRED`)

### TV-701 M1-S2 Mediated Runtime Data Path Success

Preconditions:

1. relation established in mediated mode
2. no direct upgrade accepted

Sequence:

1. handshake accept into `RelayedSession`
2. `RouteData` requester to target over mediated transport
3. `RouteData` target to requester over mediated transport

Expected outcome:

1. success
2. bridge transit trace includes mediated hops through mediator

### TV-702 M1-S2 Direct Runtime Data Path Success After Upgrade

Preconditions:

1. direct-upgrade gates pass
2. direct session accepted

Sequence:

1. relayed session established
2. `RouteUpgradeProbe` + `RouteUpgradeAccept`
3. `RouteData` over direct transport

Expected outcome:

1. success
2. bridge transit trace includes direct requester-target hop

### TV-703 M1-S2 Upgrade Reject Fallback Continuity

Preconditions:

1. direct-upgrade gate fails
2. fallback relayed session remains active

Sequence:

1. `RouteUpgradeProbe` + `RouteUpgradeReject`
2. `RouteData` over mediated transport

Expected outcome:

1. success
2. fallback relayed data path remains usable

### TV-704 M1-S2 Direct Data Attempt While Mediated

Preconditions:

1. session remains relayed
2. data transfer claims direct path

Sequence:

1. relayed handshake
2. `RouteData` with `transport_path=direct`

Expected outcome:

1. expected fail by exact error ID (`E3064_M1S2_DIRECT_PATH_WHILE_MEDIATED`)

### TV-705 M1-S2 Mediated Data Attempt After Direct Upgrade

Preconditions:

1. direct session established
2. data transfer claims mediated path

Sequence:

1. direct upgrade accepted
2. `RouteData` with `transport_path=mediated`

Expected outcome:

1. expected fail by exact error ID (`E3065_M1S2_MEDIATED_PATH_AFTER_DIRECT`)

### TV-706 M1-S2 RouteData Before Session

Preconditions:

1. no relayed or direct session established

Sequence:

1. `RouteData` emitted before handshake accept

Expected outcome:

1. expected fail by exact error ID (`E3063_M1S2_ROUTE_DATA_OUTSIDE_SESSION`)

### TV-801 M1-S3 Strict Proof Verification Success

Preconditions:

1. security adapter strict mode is active
2. proof chain and nonce are valid

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. strict verification is executed
3. `HandshakeAccept` establishes relayed session

Expected outcome:

1. success
2. no deny code

### TV-802 M1-S3 Strict Replay Detection

Preconditions:

1. strict mode replay probe is requested

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. strict verification executes replay path

Expected outcome:

1. expected fail by exact error ID (`E3072_M1S3_NONCE_REPLAY_DETECTED`)

### TV-803 M1-S3 Strict Invalid Signature Detection

Preconditions:

1. strict mode proof payload is intentionally corrupted

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. strict verification executes invalid-signature path

Expected outcome:

1. expected fail by exact error ID (`E3071_M1S3_PROOF_INVALID_SIGNATURE`)

### TV-804 M1-S3 Mock Verification Success

Preconditions:

1. mock mode is selected
2. mock signature/replay flags are positive

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. mock verification passes
3. `HandshakeAccept` establishes relayed session

Expected outcome:

1. success
2. no deny code

### TV-805 M1-S3 Mock Replay Detection

Preconditions:

1. mock mode replay flag is asserted

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. mock verification forces replay deny path

Expected outcome:

1. expected fail by exact error ID (`E3072_M1S3_NONCE_REPLAY_DETECTED`)

### TV-901 M1-S4 Strict Verification Success

Preconditions:

1. security adapter strict mode is active
2. strict failure mode is absent or `none`
3. proof chain and nonce are valid

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. strict verification executes with no injected strict failure mode
3. `HandshakeAccept` establishes relayed session

Expected outcome:

1. success
2. no deny code

### TV-902 M1-S4 Strict Expired Assertion

Preconditions:

1. strict mode is active
2. strict failure mode is `expired`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. strict verification executes expiry-failure path

Expected outcome:

1. expected fail by exact error ID (`E3081_M1S4_PROOF_EXPIRED`)

### TV-903 M1-S4 Strict Revoked Assertion

Preconditions:

1. strict mode is active
2. strict failure mode is `revoked`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. strict verification executes revocation-failure path

Expected outcome:

1. expected fail by exact error ID (`E3082_M1S4_PROOF_REVOKED`)

### TV-904 M1-S4 Strict Unresolvable Proof Chain

Preconditions:

1. strict mode is active
2. strict failure mode is `unresolvable_proof`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. strict verification executes proof-chain unresolvable path

Expected outcome:

1. expected fail by exact error ID (`E3083_M1S4_PROOF_CHAIN_UNRESOLVABLE`)

### TV-905 M1-S4 Strict Not-Yet-Valid Assertion

Preconditions:

1. strict mode is active
2. strict failure mode is `not_yet_valid`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. strict verification executes not-yet-valid path

Expected outcome:

1. expected fail by exact error ID (`E3084_M1S4_PROOF_NOT_YET_VALID`)

### TV-906 M1-S4 Invalid strict_failure_mode Schema

Preconditions:

1. strict mode is active
2. `strict_failure_mode` contains a value outside the locked domain

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. schema validation evaluates `strict_failure_mode`

Expected outcome:

1. expected fail by exact error ID (`E3080_M1S4_STRICT_FAILURE_MODE_INVALID`)

### TV-1001 M1-S5 Inline Relation Token CID Match

Preconditions:

1. strict security mode is active
2. inline token transport is selected
3. `relation_token_cid` matches `sha256(relation_token_blob)`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` uses inline token transport with matching CID/blob
3. relayed session is established

Expected outcome:

1. success
2. no deny code

### TV-1002 M1-S5 Inline Relation Token CID Mismatch

Preconditions:

1. strict security mode is active
2. inline token transport is selected
3. `relation_token_cid` is intentionally mismatched against blob hash

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides mismatched inline token CID/blob

Expected outcome:

1. expected fail by exact error ID (`E3091_M1S5_TOKEN_CID_MISMATCH`)

### TV-1003 M1-S5 Reference Relation Token Boundary Pass

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. inline token blob is absent

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides reference token fields (`token_transport`, `relation_token_ref`, `relation_token_cid`)

Expected outcome:

1. success
2. no deny code

### TV-1004 M1-S5 Reference Relation Token Blob Forbidden

Preconditions:

1. strict security mode is active
2. reference token transport incorrectly includes `relation_token_blob`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` includes forbidden inline blob while `token_transport=reference`

Expected outcome:

1. expected fail by exact error ID (`E2073_TOKEN_BLOB_FORBIDDEN_REFERENCE`)

### TV-1101 M1-S6 Reference Token Lookup Resolved

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. reference lookup resolves with CID equal to `relation_token_cid`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides reference token fields and resolved lookup metadata

Expected outcome:

1. success
2. no deny code

### TV-1102 M1-S6 Reference Token Lookup Missing

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. reference lookup status is `missing`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides `reference_lookup_status=missing`

Expected outcome:

1. expected fail by exact error ID (`E3101_M1S6_REFERENCE_TOKEN_UNRESOLVED`)

### TV-1103 M1-S6 Reference Token Lookup CID Mismatch

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. reference lookup resolves to CID different from `relation_token_cid`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides mismatched `reference_lookup_cid`

Expected outcome:

1. expected fail by exact error ID (`E3102_M1S6_REFERENCE_TOKEN_CID_MISMATCH`)

### TV-1104 M1-S6 Reference Token Rebinding Detected

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. lookup path signals rebinding detection

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides `reference_lookup_status=rebinding_detected`

Expected outcome:

1. expected fail by exact error ID (`E3103_M1S6_REFERENCE_TOKEN_REBIND_DETECTED`)

### TV-1105 M1-S6 Reference Lookup Schema Missing CID

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. lookup status is `resolved` but `reference_lookup_cid` is omitted

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. schema validation evaluates resolved lookup fields on `HandshakeAccept`

Expected outcome:

1. expected fail by exact error ID (`E3104_M1S6_REFERENCE_LOOKUP_CID_REQUIRED`)

### TV-1201 M1-S7 Reference Grant Active and Lookup Resolved

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. reference grant status is active with typed grant reference
4. reference lookup resolves with CID equal to `relation_token_cid`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides active grant metadata plus resolved lookup metadata

Expected outcome:

1. success
2. no deny code

### TV-1202 M1-S7 Reference Grant Schema Missing Status

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. grant status metadata is omitted

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. schema validation evaluates grant metadata on `HandshakeAccept`

Expected outcome:

1. expected fail by exact error ID (`E3110_M1S7_REFERENCE_GRANT_STATUS_REQUIRED`)

### TV-1203 M1-S7 Reference Grant Missing

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. reference grant status is `missing`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides `reference_grant_status=missing`

Expected outcome:

1. expected fail by exact error ID (`E3111_M1S7_REFERENCE_GRANT_MISSING`)

### TV-1204 M1-S7 Reference Grant Expired

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. reference grant status is `expired`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides `reference_grant_status=expired`

Expected outcome:

1. expected fail by exact error ID (`E3112_M1S7_REFERENCE_GRANT_EXPIRED`)

### TV-1205 M1-S7 Reference Grant Revoked

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. reference grant status is `revoked`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides `reference_grant_status=revoked`

Expected outcome:

1. expected fail by exact error ID (`E3113_M1S7_REFERENCE_GRANT_REVOKED`)

### TV-1206 M1-S7 Active Grant Missing Reference

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. reference grant status is `active`
4. `reference_grant_ref` is omitted

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. schema validation evaluates active grant metadata on `HandshakeAccept`

Expected outcome:

1. expected fail by exact error ID (`E3115_M1S7_REFERENCE_GRANT_REF_REQUIRED`)

### TV-1207 M1-S7 Active Grant Invalid Reference

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. reference grant status is `active`
4. `reference_grant_ref` is not a valid typed identifier

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. schema validation evaluates active grant metadata on `HandshakeAccept`

Expected outcome:

1. expected fail by exact error ID (`E3116_M1S7_REFERENCE_GRANT_REF_INVALID`)

### TV-1301 M1-S8 Active Grant Strict Proof Pass

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. reference grant is active with strict grant-proof verification mode
4. strict grant-proof failure mode is `none`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides active grant metadata and strict proof-binding fields

Expected outcome:

1. success
2. no deny code

### TV-1302 M1-S8 Active Grant Mock Proof Pass

Preconditions:

1. strict security mode is active
2. reference token transport is selected
3. reference grant is active with mock grant-proof verification mode
4. mock grant-proof validity is `true`

Sequence:

1. mediated handshake flow reaches `HandshakeProof`
2. `HandshakeAccept` provides active grant metadata and mock proof-binding fields

Expected outcome:

1. success
2. no deny code

### TV-1303 M1-S8 Active Grant Missing Verification Mode

Preconditions:

1. reference token transport is selected
2. reference grant is active
3. `reference_grant_verification_mode` is omitted

Expected outcome:

1. expected fail by exact error ID (`E3120_M1S8_REFERENCE_GRANT_VERIFICATION_MODE_REQUIRED`)

### TV-1304 M1-S8 Strict Mode Missing Proof Ref

Preconditions:

1. reference token transport is selected
2. reference grant is active
3. `reference_grant_verification_mode=strict`
4. `reference_grant_proof_ref` is omitted

Expected outcome:

1. expected fail by exact error ID (`E3122_M1S8_REFERENCE_GRANT_PROOF_REF_REQUIRED`)

### TV-1305 M1-S8 Strict Mode Invalid Proof Ref

Preconditions:

1. reference token transport is selected
2. reference grant is active
3. `reference_grant_verification_mode=strict`
4. `reference_grant_proof_ref` is not a typed identifier

Expected outcome:

1. expected fail by exact error ID (`E3123_M1S8_REFERENCE_GRANT_PROOF_REF_INVALID`)

### TV-1306 M1-S8 Mock Mode Missing Proof Valid Flag

Preconditions:

1. reference token transport is selected
2. reference grant is active
3. `reference_grant_verification_mode=mock`
4. `reference_grant_mock_valid` is omitted

Expected outcome:

1. expected fail by exact error ID (`E3124_M1S8_REFERENCE_GRANT_MOCK_VALID_REQUIRED`)

### TV-1307 M1-S8 Proof Fields Present for Non-Active Grant

Preconditions:

1. reference token transport is selected
2. reference grant status is not active
3. grant-proof fields are present

Expected outcome:

1. expected fail by exact error ID (`E3125_M1S8_REFERENCE_GRANT_PROOF_FIELDS_FORBIDDEN`)

### TV-1308 M1-S8 Strict Grant Proof Expired

Preconditions:

1. reference token transport is selected
2. reference grant is active
3. strict grant proof failure mode is `expired`

Expected outcome:

1. expected fail by exact error ID (`E3132_M1S8_REFERENCE_GRANT_PROOF_EXPIRED`)

### TV-1309 M1-S8 Strict Grant Proof Revoked

Preconditions:

1. reference token transport is selected
2. reference grant is active
3. strict grant proof failure mode is `revoked`

Expected outcome:

1. expected fail by exact error ID (`E3133_M1S8_REFERENCE_GRANT_PROOF_REVOKED`)

### TV-1310 M1-S8 Strict Grant Proof Invalid Signature

Preconditions:

1. reference token transport is selected
2. reference grant is active
3. strict grant proof failure mode is `invalid_signature`

Expected outcome:

1. expected fail by exact error ID (`E3130_M1S8_REFERENCE_GRANT_PROOF_INVALID_SIGNATURE`)

### TV-1311 M1-S8 Strict Grant Proof Unresolvable

Preconditions:

1. reference token transport is selected
2. reference grant is active
3. strict grant proof failure mode is `unresolvable_proof`

Expected outcome:

1. expected fail by exact error ID (`E3131_M1S8_REFERENCE_GRANT_PROOF_CHAIN_UNRESOLVABLE`)

### TV-1312 M1-S8 Strict Grant Proof Not Yet Valid

Preconditions:

1. reference token transport is selected
2. reference grant is active
3. strict grant proof failure mode is `not_yet_valid`

Expected outcome:

1. expected fail by exact error ID (`E3134_M1S8_REFERENCE_GRANT_PROOF_NOT_YET_VALID`)

### TV-1313 M1-S8 Mock Grant Proof Invalid

Preconditions:

1. reference token transport is selected
2. reference grant is active
3. `reference_grant_verification_mode=mock`
4. `reference_grant_mock_valid=false`

Expected outcome:

1. expected fail by exact error ID (`E3135_M1S8_REFERENCE_GRANT_PROOF_INVALID_MOCK`)

---

## 4. Minimal Coverage Map

1. Resolve: TV-001, TV-002, TV-012, TV-013
2. Handshake/Route: TV-003, TV-004, TV-014
3. Endpoint grant/disclosure: TV-005, TV-006
4. Observation/replay: TV-007, TV-008, TV-009, TV-010
5. Policy inheritance: TV-011
6. M1 wire closure: TV-601, TV-602, TV-603, TV-604, TV-605, TV-606, TV-607, TV-608, TV-609, TV-610
7. M1 runtime bridge: TV-701, TV-702, TV-703, TV-704, TV-705, TV-706
8. M1 security adapter bridge: TV-801, TV-802, TV-803, TV-804, TV-805
9. M1 strict failure mapping: TV-901, TV-902, TV-903, TV-904, TV-905, TV-906
10. M1 relation token integrity: TV-1001, TV-1002, TV-1003, TV-1004
11. M1 reference token guard: TV-1101, TV-1102, TV-1103, TV-1104, TV-1105
12. M1 reference grant guard: TV-1201, TV-1202, TV-1203, TV-1204, TV-1205, TV-1206, TV-1207
13. M1 reference grant proof binding: TV-1301, TV-1302, TV-1303, TV-1304, TV-1305, TV-1306, TV-1307, TV-1308, TV-1309, TV-1310, TV-1311, TV-1312, TV-1313

### 4.1 S1 Transition-Edge Negative Extension Set

1. TV-101: `ResolveResponse` before `ResolveRequest` context
2. TV-102: required field omission (`ResolveRequest.expr_raw`)
3. TV-103: invalid deny-code mapping
4. TV-104: handshake terminal message (`HandshakeAccept`) before proof
5. TV-105: route-upgrade response before probe
6. TV-106: message after terminal state
7. TV-107: ambiguous resolve path without selector hints but success response
8. TV-108: ambiguous resolve path with empty selector hints but success response
9. TV-109: mediated handshake rejection must remain terminal

### 4.2 Slice Ownership Map

1. S1 owned vectors:
   - TV-001, TV-002, TV-003, TV-012, TV-013
   - TV-101, TV-102, TV-103, TV-104, TV-105, TV-106, TV-107, TV-108, TV-109
2. S2 owned vectors:
   - TV-004, TV-014
   - TV-201 (policy gate deny path)
   - TV-202 (disclosure gate deny path)
   - TV-203 (grant missing gate deny path)
   - TV-204 (grant expired gate deny path)
   - TV-205 (trust insufficient gate deny path)
   - TV-206 (invalid accept with failed gates; expected fail by exact error ID)
3. S3 owned vectors:
   - TV-005 (encrypted endpoint resolve with valid grant)
   - TV-006 (encrypted endpoint resolve deny with missing grant)
   - TV-301 (grant proof message before resolve context; expected fail by exact error ID)
   - TV-302 (endpoint response without required grant proof path; expected fail by exact error ID)
4. S4 owned vectors:
   - TV-007 (observe subtree follow-moves semantics)
   - TV-008 (observe exact-path no-follow semantics)
   - TV-009 (replay resume success in retention window)
   - TV-010 (replay resume expiry behavior with deterministic deny semantics)
5. S5 owned vectors:
   - TV-011 (parent hard policy blocks child weakening)
   - TV-501 (policy deny before policy context; expected fail by exact error ID)
   - TV-502 (policy deny code mismatch; expected fail by exact error ID)
6. M1-S1 owned vectors:
   - TV-601 (expr_norm with supported expr_norm_v pass)
   - TV-602 (expr_norm missing expr_norm_v; expected fail by exact error ID)
   - TV-603 (expr_norm unsupported expr_norm_v; expected fail by exact error ID)
   - TV-604 (policy-causal deny without policy_ref; expected fail by exact error ID)
   - TV-605 (policy-causal deny with typed policy_ref pass)
   - TV-606 (relation token boundary reference mode pass)
   - TV-607 (relation token boundary inline mode pass)
   - TV-608 (reference mode with token blob; expected fail by exact error ID)
   - TV-609 (typed identifier invalid for policy_ref; expected fail by exact error ID)
   - TV-610 (missing relation token CID; expected fail by exact error ID)
7. M1-S2 owned vectors:
   - TV-701 (mediated runtime data path success)
   - TV-702 (direct runtime data path success after upgrade)
   - TV-703 (upgrade reject fallback continuity with mediated data path)
   - TV-704 (direct data attempt while mediated; expected fail by exact error ID)
   - TV-705 (mediated data attempt after direct upgrade; expected fail by exact error ID)
   - TV-706 (route data before session; expected fail by exact error ID)
8. M1-S3 owned vectors:
   - TV-801 (strict proof verification success)
   - TV-802 (strict nonce replay detection; expected fail by exact error ID)
   - TV-803 (strict invalid signature detection; expected fail by exact error ID)
   - TV-804 (mock proof verification success)
   - TV-805 (mock replay detection; expected fail by exact error ID)
9. M1-S4 owned vectors:
   - TV-901 (strict verification success baseline)
   - TV-902 (strict expired assertion; expected fail by exact error ID)
   - TV-903 (strict revoked assertion; expected fail by exact error ID)
   - TV-904 (strict unresolvable proof chain; expected fail by exact error ID)
   - TV-905 (strict not-yet-valid assertion; expected fail by exact error ID)
   - TV-906 (invalid strict_failure_mode schema; expected fail by exact error ID)
10. M1-S5 owned vectors:
   - TV-1001 (inline token CID match pass)
   - TV-1002 (inline token CID mismatch; expected fail by exact error ID)
   - TV-1003 (reference token boundary pass)
   - TV-1004 (reference token blob forbidden; expected fail by exact error ID)
11. M1-S6 owned vectors:
   - TV-1101 (reference lookup resolved with CID match pass)
   - TV-1102 (reference lookup unresolved; expected fail by exact error ID)
   - TV-1103 (reference lookup CID mismatch; expected fail by exact error ID)
   - TV-1104 (reference lookup rebinding detected; expected fail by exact error ID)
   - TV-1105 (resolved lookup missing CID; expected fail by exact error ID)
12. M1-S7 owned vectors:
    - TV-1201 (active grant + resolved lookup pass)
    - TV-1202 (missing grant status schema; expected fail by exact error ID)
    - TV-1203 (grant missing deny; expected fail by exact error ID)
    - TV-1204 (grant expired deny; expected fail by exact error ID)
    - TV-1205 (grant revoked deny; expected fail by exact error ID)
    - TV-1206 (active grant missing ref schema; expected fail by exact error ID)
    - TV-1207 (active grant invalid ref schema; expected fail by exact error ID)
13. M1-S8 owned vectors:
    - TV-1301 (active grant strict proof pass)
    - TV-1302 (active grant mock proof pass)
    - TV-1303 (active grant missing verification mode schema; expected fail by exact error ID)
    - TV-1304 (strict mode missing grant proof ref schema; expected fail by exact error ID)
    - TV-1305 (strict mode invalid grant proof ref schema; expected fail by exact error ID)
    - TV-1306 (mock mode missing grant proof valid flag schema; expected fail by exact error ID)
    - TV-1307 (grant proof fields forbidden when grant status non-active; expected fail by exact error ID)
    - TV-1308 (strict grant proof expired; expected fail by exact error ID)
    - TV-1309 (strict grant proof revoked; expected fail by exact error ID)
    - TV-1310 (strict grant proof invalid signature; expected fail by exact error ID)
    - TV-1311 (strict grant proof unresolvable; expected fail by exact error ID)
    - TV-1312 (strict grant proof not-yet-valid; expected fail by exact error ID)
    - TV-1313 (mock grant proof invalid; expected fail by exact error ID)

---

## 5. Next Step

Completed:

1. runnable harness checklist and fixture contract (`M0-B-Conformance-Harness-Checklist.md`)
2. S1 fixture pack execution complete with deterministic baseline (`Docs/Scynapse/Design/Fixtures/S1`)
3. S2 fixture pack execution complete with deterministic baseline (`Docs/Scynapse/Design/Fixtures/S2`)
4. S3 fixture pack execution complete with deterministic baseline (`Docs/Scynapse/Design/Fixtures/S3`)
5. S4 fixture pack execution complete with deterministic baseline (`Docs/Scynapse/Design/Fixtures/S4`)
6. S5 fixture pack execution complete with deterministic baseline (`Docs/Scynapse/Design/Fixtures/S5`)

Current next step:

1. M1-S1 deferred wire closure vectors executed and stabilized (`TV-601..TV-610`)
2. M1-S2 runtime-bridge vectors executed and stabilized (`TV-701..TV-706`)
3. M1-S3 security-adapter vectors executed and stabilized (`TV-801..TV-805`)
4. M1-S4 strict failure-mapping vectors executed and stabilized (`TV-901..TV-906`)
5. M1-S5 relation-token integrity vectors executed and stabilized (`TV-1001..TV-1004`)
6. M1-S6 reference-token guard vectors executed and stabilized (`TV-1101..TV-1105`)
7. M1-S7 reference-grant guard vectors executed and stabilized (`TV-1201..TV-1207`)
8. M1-S8 reference-grant proof-binding vectors executed and stabilized (`TV-1301..TV-1313`)
9. preserve S1/S2/S3/S4/S5 + M1-S1 + M1-S2 + M1-S3 + M1-S4 + M1-S5 + M1-S6 + M1-S7 + M1-S8 baseline behavior and machine-checkable error ID stability
10. open next bounded M1 slice from current closure baseline
