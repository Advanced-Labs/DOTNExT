# M0-B Wire-Lock Open Decisions

## 1. Purpose

Capture the remaining wire-level decisions that should be locked before finalizing encoder/decoder implementations.

---

## 2. Decision List

### D1: Enum Encoding Strategy

Question:

1. encode enums as text labels on wire (`"parent_mediated"`) or compact integer codes

Current default:

1. text labels in examples

Recommendation:

1. integer codes on wire, text labels in debug rendering

### D2: Timestamp Representation

Question:

1. wire timestamp as RFC3339 string vs unix epoch milliseconds

Current default:

1. RFC3339 strings in examples

Recommendation:

1. epoch milliseconds on wire, RFC3339 in debug tooling

### D3: Identifier Encoding

Question:

1. whether `msg_id`, `trace_id`, `relation_id` stay free-form strings or constrained typed ids

Current default:

1. free-form strings in docs

Recommendation:

1. typed string format with prefix classes (`msg-`, `tr-`, `rel-`) for human diagnostics, plus optional binary forms later

### D4: Proof Reference Encoding

Question:

1. represent `capability_refs` and other proof refs as text hashes or compact binary digests

Current default:

1. text references in examples

Recommendation:

1. binary digest payload on wire, canonical text in debug output

### D5: `expr_norm` Canonicalization Contract

Question:

1. authoritative canonicalization algorithm and normalization versioning

Current default:

1. `expr_norm` optional, resolver-authoritative when present

Recommendation:

1. lock a normalization version field (`expr_norm_v`) before wire freeze

### D6: Body Key Dictionary Stability

Question:

1. should the integer-key dictionary in wire examples be frozen for M0-B

Current default:

1. proposed-only

Recommendation:

1. freeze v1 dictionary at S1 completion; reserve growth ranges per message family

### D7: Deny Envelope Required Fields

Question:

1. make `policy_ref` conditionally required for policy-caused denies only, or always required

Current default:

1. conditional

Recommendation:

1. keep conditional with strict rule table per code

### D8: Relation Token Serialization Boundary

Question:

1. embed full relation token vs pass token reference + signed blob pointer

Current default:

1. token reference in examples

Recommendation:

1. reference + signed token blob hash in early versions; full embed optional for constrained environments

---

## 3. Priority for S1

Must lock before S1 implementation hardens:

1. D1 enum strategy
2. D2 timestamp representation
3. D4 proof reference encoding
4. D6 key dictionary freeze policy

Can defer to S2+:

1. D3 identifier strict typing
2. D5 normalization versioning details
3. D8 token boundary optimization

---

## 4. Proposed Lock Meeting Agenda

1. approve or adjust D1/D2/D4/D6 for S1
2. assign owners for dictionary freeze and serializer profile
3. record decisions in `M0-B-Wire-Examples.md` and `M0-B-Message-Field-Matrix.md`
