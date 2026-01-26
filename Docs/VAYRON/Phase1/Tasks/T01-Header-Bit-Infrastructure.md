# T01: Header Bit Infrastructure

> **Work Package:** WP1
> **Dependencies:** None
> **Estimated Complexity:** Low
> **Status:** Pending

---

## Objective

Repurpose `BIT_SBLK_UNUSED` (bit 31) as `BIT_SBLK_DDS_NONDEFAULT` for DDS routing.

---

## Files to Modify

| File | Changes |
|------|---------|
| `src/runtime/src/coreclr/vm/syncblk.h` | Rename constant, add accessor methods |
| `src/runtime/src/coreclr/vm/syncblk.cpp` | (minimal, if any implementation needed) |
| `src/runtime/src/coreclr/vm/object.h` | Add convenience method |

---

## Implementation Steps

### Step 1: Rename the Bit Constant

**File:** `syncblk.h`

```cpp
// BEFORE:
#define BIT_SBLK_UNUSED  0x80000000

// AFTER:
// DDS (Device Driver System) routing bit
// When set, this object uses non-default drivers for runtime operations
// When clear (default), standard CLR behavior applies
#define BIT_SBLK_DDS_NONDEFAULT  0x80000000

// Legacy alias for compatibility (remove after verification)
#define BIT_SBLK_UNUSED  BIT_SBLK_DDS_NONDEFAULT
```

### Step 2: Add ObjHeader Accessor Methods

**File:** `syncblk.h`, inside `class ObjHeader`

```cpp
public:
    // DDS routing support
    inline bool IsDDSNonDefault() const {
        return (GetBits() & BIT_SBLK_DDS_NONDEFAULT) != 0;
    }

    inline void SetDDSNonDefault() {
        SetBit(BIT_SBLK_DDS_NONDEFAULT);
    }

    inline void ClearDDSNonDefault() {
        ClrBit(BIT_SBLK_DDS_NONDEFAULT);
    }
```

### Step 3: Add Object Convenience Method

**File:** `object.h`, inside `class Object`

```cpp
public:
    // DDS routing
    inline bool IsDDSNonDefault() const {
        return GetHeader()->IsDDSNonDefault();
    }
```

### Step 4: Verification

Before marking complete:

1. **Search codebase** for other uses of `BIT_SBLK_UNUSED`
   ```bash
   grep -r "BIT_SBLK_UNUSED" src/runtime/
   ```

2. **Check DEBUG builds** for conditional usage
   ```bash
   grep -r "BIT_SBLK_UNUSED" src/runtime/ | grep -i debug
   ```

3. **Verify on platforms:** x64, ARM64

---

## Acceptance Criteria

- [ ] `BIT_SBLK_DDS_NONDEFAULT` defined at 0x80000000
- [ ] `ObjHeader::IsDDSNonDefault()` returns correct value
- [ ] `ObjHeader::SetDDSNonDefault()` sets bit correctly
- [ ] `ObjHeader::ClearDDSNonDefault()` clears bit correctly
- [ ] `Object::IsDDSNonDefault()` delegates to header
- [ ] Runtime compiles successfully on x64
- [ ] Runtime compiles successfully on ARM64
- [ ] No other code uses bit 31 (verified by search)
- [ ] Existing tests pass (no regressions)

---

## Testing

### Manual Verification

```cpp
// In a test or debugging context
Object* obj = AllocateObject(...);
assert(!obj->IsDDSNonDefault());  // Should be clear initially

obj->GetHeader()->SetDDSNonDefault();
assert(obj->IsDDSNonDefault());   // Should be set

obj->GetHeader()->ClearDDSNonDefault();
assert(!obj->IsDDSNonDefault());  // Should be clear again
```

### Thread Safety

The existing `SetBit()` and `ClrBit()` use interlocked operations:
```cpp
void ObjHeader::SetBit(DWORD dwBit) {
    // Uses InterlockedCompareExchange - thread-safe
}
```

No additional synchronization needed.

---

## Notes

- This is the foundation for all DDS routing
- The bit check is designed to be fast (~1ns)
- Default objects have bit clear (0), so fast path is unchanged
- Only routed objects have bit set (1), triggering driver lookup

---

## References

- Main Doc: Part I §1.1 (Object Header Structure)
- Main Doc: Part III §3.2 WP1
- CLR Integration Reference: §1 (Object Header Bit Layout)
