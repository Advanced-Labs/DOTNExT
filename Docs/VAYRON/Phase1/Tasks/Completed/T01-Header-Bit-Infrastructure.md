# T01: Header Bit Infrastructure

> **Work Package:** WP1
> **Dependencies:** None
> **Estimated Complexity:** Low
> **Status:** Completed

---

## Objective

Repurpose `BIT_SBLK_UNUSED` (bit 31) as `BIT_SBLK_TDS_NONDEFAULT` for TypeDriver System routing.

---

## Naming Convention

| Context | Convention | Example |
|---------|------------|---------|
| C++ bit constant | `BIT_SBLK_TDS_NONDEFAULT` | `0x80000000` |
| C++ accessor methods | `IsTDSNonDefault()`, `SetTDSNonDefault()`, `ClearTDSNonDefault()` | On ObjHeader class |

**TDS** = TypeDriver System (used in C++ for brevity)

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
// TDS (TypeDriver System) routing bit
// When set, this object uses non-default drivers for runtime operations
// When clear (default), standard CLR behavior applies
#define BIT_SBLK_TDS_NONDEFAULT  0x80000000

// Legacy alias for compatibility (remove after verification)
#define BIT_SBLK_UNUSED  BIT_SBLK_TDS_NONDEFAULT
```

### Step 2: Add ObjHeader Accessor Methods

**File:** `syncblk.h`, inside `class ObjHeader`

```cpp
public:
    // TDS (TypeDriver System) routing support
    inline bool IsTDSNonDefault() const {
        return (GetBits() & BIT_SBLK_TDS_NONDEFAULT) != 0;
    }

    inline void SetTDSNonDefault() {
        SetBit(BIT_SBLK_TDS_NONDEFAULT);
    }

    inline void ClearTDSNonDefault() {
        ClrBit(BIT_SBLK_TDS_NONDEFAULT);
    }
```

### Step 3: Add Object Convenience Method

**File:** `object.h`, inside `class Object`

```cpp
public:
    // TDS routing
    inline bool IsTDSNonDefault() const {
        return GetHeader()->IsTDSNonDefault();
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

- [x] `BIT_SBLK_TDS_NONDEFAULT` defined at 0x80000000
- [x] `ObjHeader::IsTDSNonDefault()` returns correct value
- [x] `ObjHeader::SetTDSNonDefault()` sets bit correctly
- [x] `ObjHeader::ClearTDSNonDefault()` clears bit correctly
- [x] `Object::IsTDSNonDefault()` delegates to header
- [ ] Runtime compiles successfully on x64 (pending TAI verification)
- [ ] Runtime compiles successfully on ARM64 (pending TAI verification)
- [x] No other code uses bit 31 (verified by search)
- [ ] Existing tests pass (pending TAI verification)

---

## Testing

### Manual Verification

```cpp
// In a test or debugging context
Object* obj = AllocateObject(...);
assert(!obj->IsTDSNonDefault());  // Should be clear initially

obj->GetHeader()->SetTDSNonDefault();
assert(obj->IsTDSNonDefault());   // Should be set

obj->GetHeader()->ClearTDSNonDefault();
assert(!obj->IsTDSNonDefault());  // Should be clear again
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

- This is the foundation for all TypeDriver routing
- The bit check is designed to be fast (~1ns)
- Default objects have bit clear (0), so fast path is unchanged
- Only routed objects have bit set (1), triggering driver lookup

---

## References

- Main Doc: Part I SS1.1 (Object Header Structure)
- Main Doc: Part III SS3.2 WP1
- CLR Integration Reference: SS1 (Object Header Bit Layout)

---

## Implementation Notes

**Completed:** 2026-01-26
**Commits:** `4e2cf252a`

### What Was Done
- Added `BIT_SBLK_TDS_NONDEFAULT` constant (0x80000000) to syncblk.h
- Preserved `BIT_SBLK_UNUSED` as legacy alias for compatibility
- Added ObjHeader methods with `FORCEINLINE` and `LIMITED_METHOD_DAC_CONTRACT`
- Added Object::IsTDSNonDefault() convenience method
- Created TDS directory structure: `src/runtime/src/coreclr/vm/tds/`
- Created native test header: `tds/tds_tests.h`
- Created managed test template: `src/tests/tds/Phase1/T01_HeaderBitTests.cs`

### Deviations from Plan
- Used `FORCEINLINE` instead of `inline` for better performance guarantee
- Used `LIMITED_METHOD_DAC_CONTRACT` for debugger compatibility
- Used `LoadWithoutBarrier()` in IsTDSNonDefault() instead of GetBits() for cleaner read

### Issues Encountered
- None

### Follow-up Items
- Build verification pending (TAI at Checkpoint 1 after T03)
- Acceptance criteria items marked with [ ] need TAI verification
